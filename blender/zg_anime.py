"""
Organic / anime-style modelling helpers used by the character generators.

  * skin_body()     - smooth, continuous limbs and torsos from a joint skeleton
                      (Skin modifier + Subdivision Surface, applied)
  * sculpt_head()   - a soft anime head with a tapered jaw and small chin
  * Decal / project - flat 2D shapes (anime eyes, lashes, mouth, numbers)
                      projected onto a surface so they hug it perfectly
  * hair_lock()     - tapered, pointed hair locks for spiky anime hair
  * auto_weights()  - smooth distance-based skin weights to the armature
"""

import math

import bpy  # noqa: F401  (import before bmesh/mathutils when using pip bpy)
import bmesh
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

import zg_common as zg


# --------------------------------------------------------------------------- #
# Skin-modifier bodies
# --------------------------------------------------------------------------- #
def skin_body(name, joints, edges, mat, root=0, subdiv=2, smooth=True):
    """joints: list of (co, (rx, ry)). edges: list of (i, j) index pairs."""
    mesh = bpy.data.meshes.new(name + "_skel")
    mesh.from_pydata([Vector(j[0]) for j in joints], edges, [])
    obj = bpy.data.objects.new(name, mesh)
    zg.link(obj)
    zg.set_active(obj)
    sk = obj.modifiers.new("skin", "SKIN")
    sk.use_smooth_shade = smooth
    sk.branch_smoothing = 0.6
    layer = obj.data.skin_vertices[0].data
    for i, (_, r) in enumerate(joints):
        layer[i].radius = r
        layer[i].use_root = (i == root)
    sub = obj.modifiers.new("sub", "SUBSURF")
    sub.levels = subdiv
    sub.render_levels = subdiv
    bpy.ops.object.modifier_apply(modifier=sk.name)
    bpy.ops.object.modifier_apply(modifier=sub.name)
    obj.data.materials.clear()
    obj.data.materials.append(mat)
    for p in obj.data.polygons:
        p.use_smooth = smooth
    return obj


def subdivide(obj, levels=1):
    zg.set_active(obj)
    m = obj.modifiers.new("sub", "SUBSURF")
    m.levels = levels
    bpy.ops.object.modifier_apply(modifier=m.name)
    for p in obj.data.polygons:
        p.use_smooth = True
    return obj


def deform(obj, fn):
    """Apply fn(co) -> new co to every vertex (object space == world, transforms applied)."""
    for v in obj.data.vertices:
        v.co = fn(v.co.copy())
    obj.data.update()
    return obj


# --------------------------------------------------------------------------- #
# Heads
# --------------------------------------------------------------------------- #
def sculpt_head(name, center, rx, ry, rz, mat, jaw=0.32, chin=0.28, segments=64, rings=48):
    """Anime head: round cranium, jaw narrowing toward a small soft chin."""
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, radius=1.0)
    obj = bpy.context.active_object
    obj.name = name
    c = Vector(center)

    def f(v):
        x, y, z = v.x, v.y, v.z
        if z < 0:
            t = -z
            x *= 1.0 - jaw * t ** 1.4             # narrower jaw
            z *= 1.0 + chin * t ** 1.2            # longer chin
            if y < 0:
                y *= 1.0 - 0.18 * t ** 2          # chin a touch flatter at front
        if y > 0:
            y *= 1.06                             # fuller back of the skull
        # very slight flattening of the sides
        x *= 1.0 - 0.06 * max(0.0, abs(x) - 0.6)
        return c + Vector((x * rx, y * ry, z * rz))

    deform(obj, f)
    obj.data.materials.clear()
    obj.data.materials.append(mat)
    for p in obj.data.polygons:
        p.use_smooth = True
    return obj


# --------------------------------------------------------------------------- #
# Projected decals (anime eyes, lashes, mouths, jersey numbers)
# --------------------------------------------------------------------------- #
class Projector:
    """Projects 2D (x, z) shapes onto a target mesh along +Y (from the front)
    or -Y (from the back)."""

    def __init__(self, target, from_back=False, remap=None):
        """remap: optional (kx, kz, z_from, z_to) to rescale authored 2D layouts."""
        self.remap = remap
        deps = bpy.context.evaluated_depsgraph_get()
        self.bvh = BVHTree.FromObject(target, deps)
        self.dir = Vector((0, -1, 0)) if from_back else Vector((0, 1, 0))
        self.start_y = 5.0 if from_back else -5.0

    def point(self, x, z, offset):
        if self.remap:
            kx, kz, z0, z1 = self.remap
            x, z = x * kx, z1 + (z - z0) * kz
        hit, normal, _, _ = self.bvh.ray_cast(Vector((x, self.start_y, z)), self.dir)
        if hit is None:
            return None
        return hit + normal.normalized() * offset

    def shape(self, name, outline, mat, offset, bone=None, fill=True):
        """outline: list of (x, z) points (closed polygon). Fan-triangulated."""
        pts = [self.point(x, z, offset) for x, z in outline]
        pts = [p for p in pts if p is not None]
        if len(pts) < 3:
            return None
        cx = sum(p.x for p in pts) / len(pts)
        cz = sum(p.z for p in pts) / len(pts)
        cen = self.point(cx, cz, offset) or sum(pts, Vector()) / len(pts)
        verts = [cen] + pts
        n = len(pts)
        faces = [(0, i + 1, (i + 1) % n + 1) for i in range(n)]
        return _mesh(name, verts, faces, mat, bone)

    def strip(self, name, path, widths, mat, offset, bone=None):
        """A ribbon along a 2D path [(x,z)...] with per-point width (tapered lines)."""
        if not isinstance(widths, (list, tuple)):
            widths = [widths] * len(path)
        verts, faces = [], []
        for i, (x, z) in enumerate(path):
            a = Vector(path[max(0, i - 1)])
            b = Vector(path[min(len(path) - 1, i + 1)])
            t = (b - a)
            t = Vector((t.x, t.y)).normalized() if t.length > 0 else Vector((1, 0))
            nrm = Vector((-t.y, t.x)) * widths[i] * 0.5 * (self.remap[0] if self.remap else 1.0)
            for s in (1, -1):
                p = self.point(x + nrm.x * s, z + nrm.y * s, offset)
                verts.append(p if p is not None else Vector((x, 0, z)))
        for i in range(len(path) - 1):
            a, b, c, d = 2 * i, 2 * i + 1, 2 * i + 3, 2 * i + 2
            faces.append((a, b, c, d))
        return _mesh(name, verts, faces, mat, bone)


def _mesh(name, verts, faces, mat, bone):
    me = bpy.data.meshes.new(name)
    me.from_pydata(verts, [], faces)
    me.update()
    obj = bpy.data.objects.new(name, me)
    zg.link(obj)
    obj.data.materials.append(mat)
    for p in obj.data.polygons:
        p.use_smooth = True
    if bone:
        vg = obj.vertex_groups.new(name=bone)
        vg.add([v.index for v in obj.data.vertices], 1.0, "REPLACE")
    # make sure decals face outward (toward -Y for the front)
    return obj


def ellipse(cx, cz, rx, rz, n=24, rot=0.0, squash_top=0.0):
    pts = []
    for i in range(n):
        a = i / n * 2 * math.pi
        x, z = math.cos(a) * rx, math.sin(a) * rz
        if squash_top and z > 0:
            z *= 1.0 - squash_top
        xr = x * math.cos(rot) - z * math.sin(rot)
        zr = x * math.sin(rot) + z * math.cos(rot)
        pts.append((cx + xr, cz + zr))
    return pts


def arc(cx, cz, rx, rz, a0, a1, n=12):
    return [(cx + math.cos(a0 + (a1 - a0) * i / (n - 1)) * rx,
             cz + math.sin(a0 + (a1 - a0) * i / (n - 1)) * rz) for i in range(n)]


# --------------------------------------------------------------------------- #
# Hair
# --------------------------------------------------------------------------- #
def hair_lock(name, p0, p1, p2, width, thickness, mat, bone="head", segs=8, ring=8, flat_axis=None,
              tip_power=0.9):
    """A tapered, pointed lock following a quadratic bezier p0 -> p1 -> p2."""
    p0, p1, p2 = Vector(p0), Vector(p1), Vector(p2)

    def bez(t):
        return (1 - t) ** 2 * p0 + 2 * (1 - t) * t * p1 + t * t * p2

    def tan(t):
        return (2 * (1 - t) * (p1 - p0) + 2 * t * (p2 - p1)).normalized()

    up = Vector(flat_axis) if flat_axis else Vector((1, 0, 0))
    verts, faces = [], []
    for i in range(segs):
        t = i / segs
        c = bez(t)
        tg = tan(t)
        side = up - tg * up.dot(tg)
        if side.length < 1e-4:
            side = Vector((0, 1, 0)).cross(tg)
        side.normalize()
        other = tg.cross(side).normalized()
        k = (1 - t) ** tip_power
        for j in range(ring):
            a = j / ring * 2 * math.pi
            verts.append(c + side * math.cos(a) * width * k + other * math.sin(a) * thickness * k)
    verts.append(bez(1.0))
    tip = len(verts) - 1
    for i in range(segs - 1):
        for j in range(ring):
            a = i * ring + j
            b = i * ring + (j + 1) % ring
            faces.append((a, b, b + ring, a + ring))
    last = (segs - 1) * ring
    for j in range(ring):
        faces.append((last + j, last + (j + 1) % ring, tip))
    # cap the base
    faces.append(tuple(reversed(range(ring))))
    obj = _mesh(name, verts, faces, mat, bone)
    lock_uvs(obj, segs, ring)
    return obj


# --------------------------------------------------------------------------- #
# Skinning
# --------------------------------------------------------------------------- #
def _seg_dist(p, a, b):
    ab = b - a
    t = max(0.0, min(1.0, (p - a).dot(ab) / max(1e-9, ab.dot(ab))))
    return (p - (a + ab * t)).length


def auto_weights(obj, rig, only=None, exclude=(), falloff=4.0, max_bones=3, side_lock=True):
    """Smooth distance-based weights. `only` limits candidate bones.
    side_lock keeps .L vertices off .R bones (and vice versa)."""
    bones = []
    for b in rig.data.bones:
        if only and b.name not in only:
            continue
        if b.name in exclude or b.name == "root":
            continue
        bones.append((b.name, rig.matrix_world @ b.head_local, rig.matrix_world @ b.tail_local))
    groups = {n: (obj.vertex_groups.get(n) or obj.vertex_groups.new(name=n)) for n, _, _ in bones}
    for v in obj.data.vertices:
        p = obj.matrix_world @ v.co
        ds = []
        for n, a, b in bones:
            if side_lock:
                if n.endswith(".L") and p.x < -0.02:
                    continue
                if n.endswith(".R") and p.x > 0.02:
                    continue
            ds.append((_seg_dist(p, a, b), n))
        if not ds:
            continue
        ds.sort()
        ds = ds[:max_bones]
        d0 = ds[0][0]
        ws = [(n, 1.0 / ((d / max(d0, 1e-4)) ** falloff)) for d, n in ds if d < d0 * 1.8 + 0.01]
        tot = sum(w for _, w in ws)
        for n, w in ws:
            groups[n].add([v.index], w / tot, "REPLACE")


def rigid(obj, bone):
    vg = obj.vertex_groups.get(bone) or obj.vertex_groups.new(name=bone)
    vg.add([v.index for v in obj.data.vertices], 1.0, "REPLACE")
    return obj


def mirror_x(points):
    return [(-x, z) for x, z in points]


def fix_normals(obj, projector):
    """Make a projected decal face outward (toward the projector), so it isn't
    hidden by back-face culling in the game."""
    if obj is None:
        return None
    me = obj.data
    me.update()
    total = sum(p.normal.dot(projector.dir) for p in me.polygons)
    if total > 0:
        bm = bmesh.new()
        bm.from_mesh(me)
        bmesh.ops.reverse_faces(bm, faces=bm.faces[:])
        bm.to_mesh(me)
        bm.free()
        me.update()
    return obj


# --------------------------------------------------------------------------- #
# Painted textures + UVs
# --------------------------------------------------------------------------- #
def tex_material(name, image_path, roughness=0.6):
    """A material whose base colour is a painted texture (exported into the FBX)."""
    key = "ZG_" + name
    mat = bpy.data.materials.get(key) or bpy.data.materials.new(key)
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = nt.nodes.get("Principled BSDF")
    bsdf.inputs["Roughness"].default_value = roughness
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = bpy.data.images.load(image_path, check_existing=True)
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    mat.diffuse_color = (1, 1, 1, 1)
    return mat


def set_uvs(obj, fn, name="UVMap", wrap_u=False):
    """fn(vertex_co) -> (u, v). With wrap_u, faces crossing the u seam are fixed."""
    me = obj.data
    uv = me.uv_layers.get(name) or me.uv_layers.new(name=name)
    for poly in me.polygons:
        uvs = [fn(me.vertices[me.loops[li].vertex_index].co) for li in poly.loop_indices]
        if wrap_u:
            us = [u for u, _ in uvs]
            if max(us) - min(us) > 0.5:
                uvs = [(u + 1.0 if u < 0.5 else u, v) for u, v in uvs]
        for li, (u, v) in zip(poly.loop_indices, uvs):
            uv.data[li].uv = (u, v)


def lock_uvs(obj, segs, ring):
    """UVs for a hair_lock: u around (mirrored, so no seam), v root -> tip."""
    def f(idx):
        if idx >= segs * ring:
            return (0.5, 1.0)
        i, j = divmod(idx, ring)
        return (1.0 - abs(2.0 * j / ring - 1.0), i / segs)
    me = obj.data
    uv = me.uv_layers.get("UVMap") or me.uv_layers.new(name="UVMap")
    for poly in me.polygons:
        for li in poly.loop_indices:
            uv.data[li].uv = f(me.loops[li].vertex_index)
