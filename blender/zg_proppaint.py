"""
Gives the props (hoops, gates, plates, pads, boards, lifts...) the same
hand-painted anime look as the characters.

Every paintable polygon is tagged with its material's colour plus a "kind"
(stone, metal, plain, leather), the object is UV-unwrapped, and a texture is
painted for it in 3D (zg_bake): soft top-lit gradients, painted stone blocks
with mortar lines, anime metal with a sharp highlight streak, darker bases and
light top edges. Emissive and runtime-tinted materials (glows, rims, plate and
pad tops, team-colour pads) are left alone so the game can still recolour them.
"""

import numpy as np

import bpy
import zg_bake as zb

# materials the game re-tints or that glow: never baked
KEEP = ("Rim", "HoopPad", "PadTop", "PlateTop", "Glow", "Gem", "Led", "SBRed", "Rune", "LiftGlow", "BridgeGlow")
KIND_PLAIN, KIND_STONE, KIND_METAL, KIND_LEATHER = 1.0, 0.8, 0.6, 0.2


def kind_of(name):
    n = name.lower()
    if any(k in n for k in ("stone", "pillar", "gatestone", "dark", "plinth")):
        return KIND_STONE
    if any(k in n for k in ("metal", "pole", "post", "frame", "rail", "hanger")):
        return KIND_METAL
    if "ballorange" in n or "trophyball" in n:
        return KIND_LEATHER
    return KIND_PLAIN


def _hash(a):
    return np.modf(np.sin(a * 12.9898 + 78.233) * 43758.5453)[0] % 1.0


def _ss(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1)
    return t * t * (3 - 2 * t)


def make_painter(zmin, zmax):
    def paint(P, N, W):
        cw = W["__col"]
        col = cw[:, :3].copy()
        kind = cw[:, 3]
        x, y, z = P[:, 0], P[:, 1], P[:, 2]
        zn = np.clip((z - zmin) / max(zmax - zmin, 1e-4), 0, 1)
        up = N[:, 2]

        # soft anime top-light gradient + darker contact base + light top faces
        shade = 0.9 + 0.16 * zn
        shade *= 1.0 - 0.18 * (1 - _ss(0.0, 0.08, zn))
        shade += 0.07 * _ss(0.75, 0.95, up)
        shade -= 0.08 * _ss(0.3, 0.9, -up)

        stone = np.abs(kind - KIND_STONE) < 0.05
        metal = np.abs(kind - KIND_METAL) < 0.05
        leather = np.abs(kind - KIND_LEATHER) < 0.05

        # ---- painted stone blocks ----
        if stone.any():
            side_u = np.where(np.abs(N[:, 0]) > np.abs(N[:, 1]), y, x)
            top = np.abs(up) > 0.7
            u = np.where(top, x, side_u)
            v = np.where(top, y, z)
            bw, bh = 0.5, 0.28
            row = np.floor(v / bh)
            uu = u / bw + 0.5 * (row % 2)
            colid = np.floor(uu)
            fu, fv = uu - colid, v / bh - row
            mortar = np.minimum(np.minimum(fu, 1 - fu) * bw, np.minimum(fv, 1 - fv) * bh)
            line = 1 - _ss(0.008, 0.02, mortar)
            tint = (_hash(colid * 7.1 + row * 13.7) - 0.5) * 0.14
            chip = _ss(0.75, 1.0, _hash(colid * 3.3 + row * 5.1)) * _ss(0.02, 0.0, np.abs(fu - 0.3) * bw) * 0.0
            s_col = col * (1 + tint)[:, None]
            s_col = s_col * (1 - 0.45 * line)[:, None] + chip[:, None]
            # a soft painted highlight along the top of every block
            s_col += (_ss(0.7, 0.95, fv) * 0.05 * (1 - line))[:, None]
            col = np.where(stone[:, None], s_col, col)

        # ---- anime metal: banded shading + sharp white streak ----
        if metal.any():
            L = np.array([-0.4, -0.7, 0.6])
            L /= np.linalg.norm(L)
            s = N @ L
            m_col = col * (0.78 + 0.3 * _ss(-0.2, 0.6, s))[:, None]
            streak = _ss(0.8, 0.86, s) * 0.65
            m_col = m_col * (1 - streak)[:, None] + streak[:, None]
            col = np.where(metal[:, None], m_col, col)

        # ---- basketball leather pebbles ----
        if leather.any():
            q = np.floor(P * 320)
            dots = _ss(0.6, 0.9, _hash(q[:, 0] * 1.3 + q[:, 1] * 7.7 + q[:, 2] * 3.1)) * 0.08
            col = np.where(leather[:, None], col * (1 - dots)[:, None], col)

        col = col * shade[:, None]
        return np.clip(col, 0, 1)
    return paint


def paint_object(obj, tex_name, size=1024):
    """Bake a painted texture for every paintable polygon of a mesh object."""
    me = obj.data
    if not me.polygons:
        return
    paintable = []
    for i, slot in enumerate(obj.material_slots):
        m = slot.material
        if m is not None and not any(k in m.name for k in KEEP):
            paintable.append(i)
    if not paintable:
        return
    # per-corner colour + kind attribute
    attr = me.color_attributes.get("zg_col") or me.color_attributes.new("zg_col", "FLOAT_COLOR", "CORNER")
    lin = {}
    for i, slot in enumerate(obj.material_slots):
        m = slot.material
        if m is None:
            continue
        c = list(m.diffuse_color)[:3]
        lin[i] = [pow(max(v, 0), 1 / 2.2) for v in c] + [kind_of(m.name)]   # linear -> sRGB for the PNG
    for poly in me.polygons:
        val = lin.get(poly.material_index, [1, 1, 1, 1])
        for li in poly.loop_indices:
            attr.data[li].color = val
    zb.unwrap(obj, margin=0.006)
    zs = [(obj.matrix_world @ v.co).z for v in me.vertices]
    tex = zb.bake(obj, size, make_painter(min(zs), max(zs)), tex_name, attr_name="zg_col")
    mat = bpy.data.materials.new("ZG_" + tex_name.replace(".png", "") + "_Painted")
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = nt.nodes.get("Principled BSDF")
    bsdf.inputs["Roughness"].default_value = 0.55
    t = nt.nodes.new("ShaderNodeTexImage")
    t.image = bpy.data.images.load(tex, check_existing=True)
    nt.links.new(t.outputs["Color"], bsdf.inputs["Base Color"])
    me.materials.append(mat)
    new_idx = len(me.materials) - 1
    for poly in me.polygons:
        if poly.material_index in paintable:
            poly.material_index = new_idx
    # drop now-unused material slots (keeps names the game looks for)
    used = {p.material_index for p in me.polygons}
    for i in reversed(range(len(me.materials))):
        if i not in used:
            me.materials.pop(index=i)      # Blender re-numbers the polygons itself


# objects the game finds by name and moves: never merged with the rest
NODES = ("PlateTop", "PadTop", "GatePanel", "BoardMesh", "BallMesh")


def paint_prop(root):
    import zg_common as zg
    loose = [o for o in root.children if o.type == "MESH" and o.name not in NODES]
    if len(loose) > 1:
        body = zg.join(loose, root.name + "_Body")
        body.parent = root
    meshes = [o for o in [root] + list(root.children_recursive) if o.type == "MESH"]
    for k, o in enumerate(meshes):
        size = 1024 if len(o.data.polygons) > 400 else 512
        paint_object(o, "prop_%s_%d.png" % (root.name.lower(), k), size)
