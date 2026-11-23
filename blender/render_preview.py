"""
Renders quick preview images of a generated .blend (or .fbx) for review.

    python blender/render_preview.py <file.blend|file.fbx> <out_prefix> [action] [frame]

Uses the Workbench engine so it runs headless without a GPU.
"""
import math
import os
import sys

import bpy
from mathutils import Vector

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
src, prefix = args[0], args[1]
action = args[2] if len(args) > 2 else None
frame = int(args[3]) if len(args) > 3 else 1

if src.endswith(".blend"):
    bpy.ops.wm.open_mainfile(filepath=src)
else:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=src)

scene = bpy.context.scene
if scene.world is None:
    scene.world = bpy.data.worlds.new("w")
for o in scene.objects:
    if o.type == "ARMATURE" and action:
        if o.animation_data is None:
            o.animation_data_create()
        for t in o.animation_data.nla_tracks:
            t.mute = True
        o.animation_data.action = bpy.data.actions[action]
scene.frame_set(frame)

meshes = [o for o in scene.objects if o.type == "MESH"]
pts = [o.matrix_world @ Vector(c) for o in meshes for c in o.bound_box]
lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
if os.environ.get("ZG_FOCUS_HEAD"):
    H = hi.z - lo.z
    lo, hi = Vector((lo.x, lo.y, hi.z - H * 0.5)), Vector((hi.x, hi.y, hi.z - H * 0.14))
centre = (lo + hi) / 2
size = max((hi - lo).x, (hi - lo).y, (hi - lo).z)
if os.environ.get("ZG_FOCUS_HEAD"):
    size = (hi.z - lo.z)

def toonify():
    """Cel-shade every material + inverted-hull ink outlines (matches the Unity toon shader)."""
    ink = bpy.data.materials.new("ZG_PreviewInk")
    ink.use_nodes = True
    nt = ink.node_tree
    nt.nodes.clear()
    em = nt.nodes.new("ShaderNodeEmission")
    em.inputs["Color"].default_value = (0.03, 0.02, 0.02, 1)
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    nt.links.new(em.outputs[0], out.inputs[0])
    ink.use_backface_culling = True
    skip = ("Blush", "Iris", "Pupil", "Eye", "Lash", "Brow", "Mouth", "Tongue", "Trim", "Seam", "Tooth", "Gem", "Glow", "Shine")
    for mat in list(bpy.data.materials):
        if mat == ink or not mat.use_nodes:
            continue
        nt = mat.node_tree
        bsdf = nt.nodes.get("Principled BSDF")
        if bsdf is None:
            continue
        col_in = bsdf.inputs["Base Color"]
        src = col_in.links[0].from_socket if col_in.links else None
        base = tuple(col_in.default_value)
        emis_strength = bsdf.inputs["Emission Strength"].default_value if "Emission Strength" in bsdf.inputs else 0
        diff = nt.nodes.new("ShaderNodeBsdfDiffuse")
        s2r = nt.nodes.new("ShaderNodeShaderToRGB")
        ramp = nt.nodes.new("ShaderNodeValToRGB")
        ramp.color_ramp.interpolation = "CONSTANT"
        ramp.color_ramp.elements[0].color = (0.72, 0.68, 0.86, 1)
        ramp.color_ramp.elements[1].position = 0.28
        ramp.color_ramp.elements[1].color = (1, 1, 1, 1)
        mul = nt.nodes.new("ShaderNodeMixRGB")
        mul.blend_type = "MULTIPLY"
        mul.inputs[0].default_value = 1.0
        if src is not None:
            nt.links.new(src, mul.inputs[1])
        else:
            mul.inputs[1].default_value = base
        nt.links.new(diff.outputs[0], s2r.inputs[0])
        nt.links.new(s2r.outputs[0], ramp.inputs[0])
        nt.links.new(ramp.outputs[0], mul.inputs[2])
        em = nt.nodes.new("ShaderNodeEmission")
        em.inputs["Strength"].default_value = 1.0 if emis_strength < 1.5 else 1.6
        nt.links.new(mul.outputs[0], em.inputs[0])
        outn = [n for n in nt.nodes if n.type == "OUTPUT_MATERIAL"][0]
        nt.links.new(em.outputs[0], outn.inputs[0])
    for o in scene.objects:
        if o.type != "MESH":
            continue
        n_slots = len(o.data.materials)
        o.data.materials.append(ink)
        sol = o.modifiers.new("ink", "SOLIDIFY")
        sol.thickness = float(os.environ.get("ZG_INK", "0.006"))
        sol.offset = 1.0
        sol.use_flip_normals = True
        sol.use_rim = False
        sol.material_offset = n_slots
        # no outline around painted-on details
        for i, mt in enumerate(o.data.materials[:n_slots]):
            if mt and any(k in mt.name for k in skip):
                pass


if os.environ.get("ZG_TOON"):
    toonify()
    for eng in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE"):
        try:
            scene.render.engine = eng
            break
        except TypeError:
            continue
    scene.view_settings.view_transform = "Standard"
    scene.world.use_nodes = True
    bg = scene.world.node_tree.nodes.get("Background")
    bg.inputs[0].default_value = (1, 1, 1, 1)
    sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN"))
    sun.rotation_euler = (math.radians(50), math.radians(10), math.radians(-35))
    sun.data.energy = 3.0
    scene.collection.objects.link(sun)
else:
    scene.render.engine = "BLENDER_WORKBENCH"
if scene.render.engine == "BLENDER_WORKBENCH":
    scene.display.shading.light = "STUDIO"
scene.display.shading.color_type = "TEXTURE"
scene.display.shading.show_shadows = scene.render.engine == "BLENDER_WORKBENCH"
scene.render.resolution_x = 640
scene.render.resolution_y = 640
scene.render.film_transparent = False

cam_data = bpy.data.cameras.new("cam")
cam_data.type = "ORTHO"
cam_data.ortho_scale = size * 1.15
cam = bpy.data.objects.new("cam", cam_data)
scene.collection.objects.link(cam)
scene.camera = cam

views = {"front": (0, -1, 0.15), "side": (1, 0, 0.1), "back": (0, 1, 0.1), "three_quarter": (-0.7, -1, 0.35)}
only = os.environ.get("ZG_VIEWS")
if only:
    views = {k: v for k, v in views.items() if k in only.split(",")}
for name, d in views.items():
    d = Vector(d).normalized()
    cam.location = centre + d * size * 3
    cam.rotation_euler = (centre - cam.location).to_track_quat("-Z", "Y").to_euler()
    scene.render.filepath = f"{prefix}_{name}.png"
    bpy.ops.render.render(write_still=True)
print("actions:", [a.name for a in bpy.data.actions])
