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
    lo = Vector((lo.x, lo.y, hi.z - (hi.z - lo.z) * 0.24))
centre = (lo + hi) / 2
size = max((hi - lo).x, (hi - lo).y, (hi - lo).z)
if os.environ.get("ZG_FOCUS_HEAD"):
    size = (hi.z - lo.z)

scene.render.engine = "BLENDER_WORKBENCH"
scene.display.shading.light = "STUDIO"
scene.display.shading.color_type = "MATERIAL"
scene.display.shading.show_shadows = True
scene.render.resolution_x = 640
scene.render.resolution_y = 640
scene.render.film_transparent = False
if scene.world is None:
    scene.world = bpy.data.worlds.new("w")

cam_data = bpy.data.cameras.new("cam")
cam_data.type = "ORTHO"
cam_data.ortho_scale = size * 1.15
cam = bpy.data.objects.new("cam", cam_data)
scene.collection.objects.link(cam)
scene.camera = cam

views = {"front": (0, -1, 0.15), "side": (1, 0, 0.1), "back": (0, 1, 0.1), "three_quarter": (-0.7, -1, 0.35)}
for name, d in views.items():
    d = Vector(d).normalized()
    cam.location = centre + d * size * 3
    cam.rotation_euler = (centre - cam.location).to_track_quat("-Z", "Y").to_euler()
    scene.render.filepath = f"{prefix}_{name}.png"
    bpy.ops.render.render(write_still=True)
print("actions:", [a.name for a in bpy.data.actions])
