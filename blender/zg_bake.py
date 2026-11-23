"""
3D texture painting ("procedural bake").

Instead of wrapping a flat picture around a mesh, every texel of the texture is
painted with the colour it should have at its 3D position on the model. Garments
are described as 3D shapes (e.g. "a tank top: torso between hem and shoulders,
excluding arm holes and the neckline"), so they wrap correctly under the arms,
around the sides and over the shoulders, whatever the UV layout looks like.

    unwrap(obj)                      smart-UV-unwrap the mesh (non-overlapping)
    bake(obj, size, paint_fn, name)  paint_fn(P, N, W) -> RGB, all numpy arrays:
                                     P = positions (n,3), N = normals (n,3),
                                     W = dict of per-texel vertex-group weights
"""

import os

import bpy  # noqa: F401
import numpy as np

import zg_common as zg

TEX_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "textures")


def unwrap(obj, margin=0.004):
    zg.set_active(obj)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    try:
        bpy.ops.uv.smart_project(angle_limit=1.15, island_margin=margin)
    except TypeError:
        bpy.ops.uv.smart_project(angle_limit=66.0, island_margin=margin)
    bpy.ops.object.mode_set(mode="OBJECT")


def bake(obj, size, paint_fn, name, groups=(), supersample=2, dilate=6, attr_name=None):
    """Rasterise every UV triangle, interpolate 3D position/normal/weights per
    texel and let paint_fn colour them. Supersampled, then edge-dilated."""
    me = obj.data
    me.calc_loop_triangles()
    uv_layer = me.uv_layers.active.data
    n_v = len(me.vertices)
    co = np.empty(n_v * 3, dtype=np.float64)
    me.vertices.foreach_get("co", co)
    co = co.reshape(-1, 3)
    nrm = np.empty(n_v * 3, dtype=np.float64)
    me.vertices.foreach_get("normal", nrm)
    nrm = nrm.reshape(-1, 3)
    gw = {}
    gidx = {g.name: g.index for g in obj.vertex_groups}
    for gname in groups:
        w = np.zeros(n_v)
        if gname in gidx:
            gi = gidx[gname]
            for v in me.vertices:
                for g in v.groups:
                    if g.group == gi:
                        w[v.index] = g.weight
        gw[gname] = w
    loop_col = None
    if attr_name and attr_name in me.color_attributes:
        a = me.color_attributes[attr_name]
        loop_col = np.empty(len(me.loops) * 4, dtype=np.float64)
        a.data.foreach_get("color", loop_col)
        loop_col = loop_col.reshape(-1, 4)

    S = size * supersample
    pix_idx, pos, nor = [], [], []
    wts = {k: [] for k in gw}
    if loop_col is not None:
        wts["__col"] = []
    for tri in me.loop_triangles:
        uv = np.array([uv_layer[li].uv[:] for li in tri.loops]) * S - 0.5
        vi = list(tri.vertices)
        x0, y0 = np.floor(uv.min(0)).astype(int)
        x1, y1 = np.ceil(uv.max(0)).astype(int)
        x0, y0 = max(x0, 0), max(y0, 0)
        x1, y1 = min(x1, S - 1), min(y1, S - 1)
        if x1 < x0 or y1 < y0:
            continue
        xs, ys = np.meshgrid(np.arange(x0, x1 + 1), np.arange(y0, y1 + 1))
        xs, ys = xs.ravel(), ys.ravel()
        (ax, ay), (bx, by), (cx, cy) = uv
        den = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy)
        if abs(den) < 1e-12:
            continue
        l1 = ((by - cy) * (xs - cx) + (cx - bx) * (ys - cy)) / den
        l2 = ((cy - ay) * (xs - cx) + (ax - cx) * (ys - cy)) / den
        l3 = 1 - l1 - l2
        eps = -1e-4
        inside = (l1 >= eps) & (l2 >= eps) & (l3 >= eps)
        if not inside.any():
            continue
        L = np.stack([l1[inside], l2[inside], l3[inside]], 1)
        pix_idx.append(ys[inside] * S + xs[inside])
        pos.append(L @ co[vi])
        nor.append(L @ nrm[vi])
        for k, w in gw.items():
            wts[k].append(L @ w[vi])
        if loop_col is not None:
            wts["__col"].append(L @ loop_col[list(tri.loops)])

    img = np.zeros((S * S, 3), dtype=np.float32)
    filled = np.zeros(S * S, dtype=bool)
    if pix_idx:
        idx = np.concatenate(pix_idx)
        P = np.concatenate(pos)
        N = np.concatenate(nor)
        N /= np.maximum(np.linalg.norm(N, axis=1, keepdims=True), 1e-9)
        W = {k: np.concatenate(v) for k, v in wts.items()}
        img[idx] = paint_fn(P, N, W)
        filled[idx] = True
    img = img.reshape(S, S, 3)
    filled = filled.reshape(S, S)
    # dilate colours into the gutters between UV islands (prevents seams)
    for _ in range(dilate * supersample):
        if filled.all():
            break
        acc = np.zeros_like(img)
        cnt = np.zeros((S, S), dtype=np.float32)
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            sh_f = np.roll(filled, (dy, dx), (0, 1))
            sh_i = np.roll(img, (dy, dx), (0, 1))
            acc += sh_i * sh_f[..., None]
            cnt += sh_f
        grow = (~filled) & (cnt > 0)
        img[grow] = acc[grow] / cnt[grow][:, None]
        filled = filled | grow
    # downsample (anti-aliasing)
    img = img.reshape(size, supersample, size, supersample, 3).mean((1, 3))
    os.makedirs(TEX_DIR, exist_ok=True)
    path = os.path.join(TEX_DIR, name)
    rgba = np.concatenate([np.clip(img, 0, 1), np.ones((size, size, 1), np.float32)], 2)
    im = bpy.data.images.new(name, size, size, alpha=False)
    im.pixels.foreach_set(rgba.ravel())   # Blender image rows start at the bottom == UV v=0
    im.filepath_raw = path
    im.file_format = "PNG"
    im.save()
    bpy.data.images.remove(im)
    print("[ZG] baked", path)
    return path


# --------------------------------------------------------------------------- #
# small numpy helpers for garment painting
# --------------------------------------------------------------------------- #
def hexrgb(h):
    h = h.lstrip("#")
    return np.array([int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4)], dtype=np.float32)


def soft(d, e=0.0015):
    """Coverage from a signed distance (positive = inside), anti-aliased."""
    return np.clip(d / e + 0.5, 0.0, 1.0)


def lay(col, mask, color):
    m = mask[:, None]
    return col * (1 - m) + color[None, :] * m


def sample_decal(rgba, x, z, x0, x1, z0, z1):
    """Sample a (H, W, 4) float decal covering [x0,x1] x [z0,z1] (z up)."""
    h, w = rgba.shape[:2]
    u = (x - x0) / (x1 - x0) * (w - 1)
    v = (z1 - z) / (z1 - z0) * (h - 1)
    ok = (u >= 0) & (u <= w - 1) & (v >= 0) & (v <= h - 1)
    ui = np.clip(u, 0, w - 1).astype(int)
    vi = np.clip(v, 0, h - 1).astype(int)
    out = rgba[vi, ui]
    out[~ok] = 0
    return out
