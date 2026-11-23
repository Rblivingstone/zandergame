"""
Shared helpers for the Zander's Hoop Shrines Blender generator scripts.

Every generator script builds its model from scratch out of simple primitives,
rigs it (characters only), keys a set of named animation actions and exports an
FBX straight into the Unity project's Resources/Models folder.

Conventions (Blender side):
  * 1 Blender unit = 1 metre.
  * Z is up, characters FACE -Y (Blender's "front" view looks at their face).
  * Exported with axis_forward='-Z', axis_up='Y' so that in Unity the models
    stand on +Y and face +Z (Unity's forward).

Pose-bone rotation cheat sheet (verified with bpy, rotation_mode 'XYZ'):
  * Bones that point DOWN (arms, legs): +X swings the tail BACKWARD (+Y),
    so a forward swing is a NEGATIVE X rotation. +Z moves the tail toward -X.
  * Bones that point UP (spine, neck, head): +X leans FORWARD (-Y).
"""

import math
import os
import sys

import bpy  # noqa: F401  (must come before bmesh when using the pip bpy module)
import bmesh
import bpy
from mathutils import Euler, Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
REPO_ROOT = os.path.dirname(HERE)
DEFAULT_OUT = os.path.join(
    REPO_ROOT, "unity", "ZanderHoopShrines", "Assets", "ZanderGame", "Resources", "Models"
)
FPS = 30


# --------------------------------------------------------------------------- #
# CLI / scene helpers
# --------------------------------------------------------------------------- #
def parse_out_dir():
    """Return the output directory from `-- --out <dir>` (Blender style args)."""
    argv = sys.argv
    if "--" in argv:
        argv = argv[argv.index("--") + 1:]
    out = DEFAULT_OUT
    for i, a in enumerate(argv):
        if a == "--out" and i + 1 < len(argv):
            out = os.path.abspath(argv[i + 1])
    os.makedirs(out, exist_ok=True)
    return out


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene.render.fps = FPS
    _MATS.clear()
    return scene


def link(obj):
    bpy.context.scene.collection.objects.link(obj)
    return obj


def deselect_all():
    for o in bpy.context.scene.objects:
        o.select_set(False)


def set_active(obj):
    deselect_all()
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


# --------------------------------------------------------------------------- #
# Materials
# --------------------------------------------------------------------------- #
_MATS = {}


def hex_color(h):
    h = h.lstrip("#")
    r, g, b = (int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4))

    # sRGB -> linear so the colour looks the same in Blender and Unity.
    def lin(c):
        return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4

    return (lin(r), lin(g), lin(b), 1.0)


def material(name, hex_rgb, roughness=0.6, metallic=0.0, emission=None, emission_strength=1.0):
    """Create (or reuse) a simple Principled BSDF material named ZG_<name>."""
    key = "ZG_" + name
    if key in _MATS:
        return _MATS[key]
    mat = bpy.data.materials.new(key)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    col = hex_color(hex_rgb)
    bsdf.inputs["Base Color"].default_value = col
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = metallic
    if emission:
        e = hex_color(emission)
        for nm in ("Emission Color", "Emission"):
            if nm in bsdf.inputs:
                bsdf.inputs[nm].default_value = e
                break
        if "Emission Strength" in bsdf.inputs:
            bsdf.inputs["Emission Strength"].default_value = emission_strength
    mat.diffuse_color = col
    _MATS[key] = mat
    return mat


# --------------------------------------------------------------------------- #
# Primitive part builders. Every builder returns a mesh object with its
# transform APPLIED (so later joins are clean) and a single material.
# `bone` tags the part with a vertex group of that name for rigid skinning.
# --------------------------------------------------------------------------- #
def _finish(obj, name, mat, bone=None, smooth=True):
    obj.name = name
    if mat is not None:
        obj.data.materials.clear()
        obj.data.materials.append(mat)
    set_active(obj)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    if smooth:
        for p in obj.data.polygons:
            p.use_smooth = True
    if bone:
        vg = obj.vertex_groups.new(name=bone)
        vg.add([v.index for v in obj.data.vertices], 1.0, "REPLACE")
    obj["zg_bone"] = bone or ""
    return obj


def _xf(obj, loc, rot, scale):
    obj.location = Vector(loc)
    if rot is not None:
        obj.rotation_euler = Euler([math.radians(a) for a in rot], "XYZ")
    if scale is not None:
        obj.scale = Vector(scale)


def sphere(name, loc, radius=1.0, scale=None, rot=None, mat=None, bone=None, segments=24, rings=16):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, radius=radius)
    obj = bpy.context.active_object
    _xf(obj, loc, rot, scale)
    return _finish(obj, name, mat, bone)


def cylinder(name, loc, radius, depth, scale=None, rot=None, mat=None, bone=None, verts=24, smooth=True):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=radius, depth=depth)
    obj = bpy.context.active_object
    _xf(obj, loc, rot, scale)
    return _finish(obj, name, mat, bone, smooth)


def cone(name, loc, r1, r2, depth, scale=None, rot=None, mat=None, bone=None, verts=16):
    bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=r1, radius2=r2, depth=depth)
    obj = bpy.context.active_object
    _xf(obj, loc, rot, scale)
    return _finish(obj, name, mat, bone)


def box(name, loc, size, rot=None, mat=None, bone=None, bevel=0.0):
    bpy.ops.mesh.primitive_cube_add(size=1.0)
    obj = bpy.context.active_object
    _xf(obj, loc, rot, size)
    obj = _finish(obj, name, mat, bone, smooth=False)
    if bevel > 0:
        mod = obj.modifiers.new("bevel", "BEVEL")
        mod.width = bevel
        mod.segments = 3
        set_active(obj)
        bpy.ops.object.modifier_apply(modifier=mod.name)
        for p in obj.data.polygons:
            p.use_smooth = True
    return obj


def torus(name, loc, major, minor, rot=None, scale=None, mat=None, bone=None, major_seg=32, minor_seg=10):
    bpy.ops.mesh.primitive_torus_add(
        major_radius=major, minor_radius=minor, major_segments=major_seg, minor_segments=minor_seg
    )
    obj = bpy.context.active_object
    _xf(obj, loc, rot, scale)
    return _finish(obj, name, mat, bone)


def capsule_between(name, a, b, radius, mat=None, bone=None, verts=16):
    """A rounded cylinder from point a to point b (sphere caps)."""
    a, b = Vector(a), Vector(b)
    d = b - a
    length = d.length
    mid = (a + b) / 2
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=radius, depth=length)
    cyl = bpy.context.active_object
    cyl.location = mid
    cyl.rotation_euler = d.to_track_quat("Z", "Y").to_euler()
    parts = [_finish(cyl, name + "_c", mat, None)]
    for i, p in enumerate((a, b)):
        bpy.ops.mesh.primitive_uv_sphere_add(segments=verts, ring_count=max(8, verts // 2), radius=radius)
        s = bpy.context.active_object
        s.location = p
        parts.append(_finish(s, name + "_s%d" % i, mat, None))
    obj = join(parts, name)
    if bone:
        vg = obj.vertex_groups.new(name=bone)
        vg.add([v.index for v in obj.data.vertices], 1.0, "REPLACE")
    return obj


def half_torus(name, loc, major, minor, rot=None, scale=None, mat=None, bone=None, keep="below"):
    """Torus with half of it removed (used for smiles)."""
    bpy.ops.mesh.primitive_torus_add(major_radius=major, minor_radius=minor, major_segments=32, minor_segments=8)
    obj = bpy.context.active_object
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    kill = [v for v in bm.verts if (v.co.y > 0.0 if keep == "below" else v.co.y < 0.0)]
    bmesh.ops.delete(bm, geom=kill, context="VERTS")
    bm.to_mesh(obj.data)
    bm.free()
    _xf(obj, loc, rot, scale)
    return _finish(obj, name, mat, bone)


def trim_mesh(obj, keep_fn):
    """Delete every vertex whose world-space co does NOT satisfy keep_fn(co)."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    kill = [v for v in bm.verts if not keep_fn(v.co)]
    bmesh.ops.delete(bm, geom=kill, context="VERTS")
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()
    return obj


def join(objs, name):
    objs = [o for o in objs if o is not None]
    deselect_all()
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    if len(objs) > 1:
        bpy.ops.object.join()
    obj = bpy.context.active_object
    obj.name = name
    obj.data.name = name + "_mesh"
    return obj


def empty(name, loc=(0, 0, 0), parent=None):
    e = bpy.data.objects.new(name, None)
    e.location = Vector(loc)
    link(e)
    if parent:
        e.parent = parent
    return e


def parent_keep(child, parent):
    child.parent = parent
    child.matrix_parent_inverse = parent.matrix_world.inverted()


# --------------------------------------------------------------------------- #
# Rigging
# --------------------------------------------------------------------------- #
def build_armature(name, bones):
    """bones: list of (name, head, tail, parent_name_or_None)."""
    arm = bpy.data.armatures.new(name + "_data")
    rig = bpy.data.objects.new(name, arm)
    link(rig)
    set_active(rig)
    bpy.ops.object.mode_set(mode="EDIT")
    for bname, head, tail, parent in bones:
        eb = arm.edit_bones.new(bname)
        eb.head = Vector(head)
        eb.tail = Vector(tail)
        eb.roll = 0.0
        if parent:
            eb.parent = arm.edit_bones[parent]
            eb.use_connect = False
    bpy.ops.object.mode_set(mode="OBJECT")
    for pb in rig.pose.bones:
        pb.rotation_mode = "XYZ"
    return rig


def skin_to(mesh_obj, rig):
    mesh_obj.parent = rig
    mod = mesh_obj.modifiers.new("Armature", "ARMATURE")
    mod.object = rig
    return mod


# --------------------------------------------------------------------------- #
# Animation
# --------------------------------------------------------------------------- #
class ActionBuilder:
    """Keys an action on the rig. Rotations are in degrees, per pose bone."""

    def __init__(self, rig, name, frames, loop=True):
        self.rig = rig
        self.name = name
        self.frames = frames
        self.loop = loop
        if rig.animation_data is None:
            rig.animation_data_create()
        self.action = bpy.data.actions.new(name)
        self.action.use_fake_user = True
        rig.animation_data.action = self.action
        self._rest_all()

    def _rest_all(self):
        for pb in self.rig.pose.bones:
            pb.rotation_euler = (0, 0, 0)
            pb.location = (0, 0, 0)
            pb.scale = (1, 1, 1)

    def pose(self, frame, rot=None, loc=None, scale=None):
        """rot: {bone: (x,y,z) deg}; loc: {bone: (x,y,z)}; scale: {bone: s}.
        Bones not mentioned are keyed at rest so every key is a full pose."""
        rot = rot or {}
        loc = loc or {}
        scale = scale or {}
        for pb in self.rig.pose.bones:
            r = rot.get(pb.name, (0, 0, 0))
            pb.rotation_euler = Euler([math.radians(a) for a in r], "XYZ")
            pb.location = Vector(loc.get(pb.name, (0, 0, 0)))
            s = scale.get(pb.name, 1.0)
            pb.scale = (s, s, s) if isinstance(s, (int, float)) else Vector(s)
            pb.keyframe_insert("rotation_euler", frame=frame)
            pb.keyframe_insert("location", frame=frame)
            pb.keyframe_insert("scale", frame=frame)

    def finish(self):
        self.action.frame_range = (1, self.frames)
        self.action["zg_loop"] = 1 if self.loop else 0
        self._rest_all()
        return self.action


def push_actions_to_nla(rig, actions):
    """Store every action in its own (muted) NLA track so nothing is lost and
    the FBX exporter can find them all."""
    ad = rig.animation_data
    for act in actions:
        track = ad.nla_tracks.new()
        track.name = act.name
        strip = track.strips.new(act.name, 1, act)
        strip.name = act.name
        track.mute = True
    ad.action = None


# --------------------------------------------------------------------------- #
# Export
# --------------------------------------------------------------------------- #
def export_fbx(path, objects=None, animated=False):
    deselect_all()
    objs = objects or [o for o in bpy.context.scene.objects]
    for o in objs:
        o.select_set(True)
    kwargs = dict(
        filepath=path,
        use_selection=True,
        object_types={"ARMATURE", "MESH", "EMPTY"},
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z",
        axis_up="Y",
        use_mesh_modifiers=True,
        mesh_smooth_type="FACE",
        add_leaf_bones=False,
        primary_bone_axis="Y",
        secondary_bone_axis="X",
        bake_anim=animated,
        path_mode="STRIP",          # textures ship as separate PNGs (see write_texture_manifest)
    )
    if animated:
        kwargs.update(
            bake_anim_use_all_bones=True,
            bake_anim_use_nla_strips=False,
            bake_anim_use_all_actions=True,
            bake_anim_force_startend_keying=True,
            bake_anim_simplify_factor=0.0,
        )
    bpy.ops.export_scene.fbx(**kwargs)
    print("[ZG] exported", path)
    write_texture_manifest(path, objs)


def write_texture_manifest(fbx_path, objs):
    """Unity doesn't reliably hook up textures embedded in an FBX, so every
    painted texture is ALSO copied to Resources/Textures/Models and listed in
    Resources/Models/<Model>_textures.txt as "MaterialName=texture". The game's
    ToonStyler uses this list to put the right texture on each material."""
    import shutil
    models_dir = os.path.dirname(fbx_path)
    tex_dir = os.path.join(os.path.dirname(models_dir), "Textures", "Models")
    os.makedirs(tex_dir, exist_ok=True)
    lines = []
    seen = set()
    for o in objs:
        if o.type != "MESH":
            continue
        for slot in o.material_slots:
            m = slot.material
            if m is None or m.name in seen or not m.use_nodes:
                continue
            seen.add(m.name)
            for n in m.node_tree.nodes:
                if n.type == "TEX_IMAGE" and n.image is not None:
                    src = bpy.path.abspath(n.image.filepath)
                    if not os.path.exists(src):
                        continue
                    base = os.path.splitext(os.path.basename(src))[0]
                    shutil.copyfile(src, os.path.join(tex_dir, base + ".png"))
                    lines.append("%s=%s" % (m.name, base))
                    break
    name = os.path.splitext(os.path.basename(fbx_path))[0]
    with open(os.path.join(models_dir, name + "_textures.txt"), "w") as f:
        f.write("\n".join(lines) + "\n")
    print("[ZG] texture manifest:", len(lines), "textures")


def save_blend(path):
    bpy.ops.wm.save_as_mainfile(filepath=path)
    print("[ZG] saved", path)
