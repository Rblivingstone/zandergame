# Usage: python blender/arena_mockup.py, then render with render_preview.py + ZG_CAM.
# Builds a Championship Court mock-up (Blender coords: x right, y = Unity z, z up).
import math
import os

import bpy

R = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
T = R + "/unity/ZanderHoopShrines/Assets/ZanderGame/Resources/Textures/"
O = R + "/blender/output/"
bpy.ops.wm.read_factory_settings(use_empty=True)


def tex_mat(name, img, sx=1, sy=1, alpha=False):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    b = nt.nodes["Principled BSDF"]
    t = nt.nodes.new("ShaderNodeTexImage")
    t.image = bpy.data.images.load(img)
    mp = nt.nodes.new("ShaderNodeMapping")
    mp.inputs["Scale"].default_value = (sx, sy, 1)
    tc = nt.nodes.new("ShaderNodeTexCoord")
    nt.links.new(tc.outputs["UV"], mp.inputs[0])
    nt.links.new(mp.outputs[0], t.inputs[0])
    nt.links.new(t.outputs["Color"], b.inputs["Base Color"])
    if alpha:
        nt.links.new(t.outputs["Alpha"], b.inputs["Alpha"])
        try:
            m.surface_render_method = "DITHERED"
        except Exception:
            m.blend_method = "CLIP"
    return m


def col(name, rgb, emit=0.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = rgb + (1,)
    if emit:
        b.inputs["Emission Color"].default_value = rgb + (1,)
        b.inputs["Emission Strength"].default_value = emit
    m.diffuse_color = rgb + (1,)
    return m


def box(name, c, s, mat, cube_uv=False):
    bpy.ops.mesh.primitive_cube_add(size=1, location=c)
    o = bpy.context.active_object
    o.scale = s
    o.name = name
    bpy.ops.object.transform_apply(scale=True)
    if cube_uv:
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.uv.cube_project(cube_size=2.0)
        bpy.ops.object.mode_set(mode="OBJECT")
    o.data.materials.append(mat)
    return o


def plane(name, c, sx, sy, mat, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_plane_add(size=1, location=c, rotation=rot)
    o = bpy.context.active_object
    o.scale = (sx, sy, 1)
    o.name = name
    bpy.ops.object.transform_apply(scale=True)
    o.data.materials.append(mat)
    return o


def append(blend, loc, rot_z=0):
    with bpy.data.libraries.load(blend) as (src, dst):
        dst.objects = list(src.objects)
    for o in dst.objects:
        if o is None:
            continue
        bpy.context.scene.collection.objects.link(o)
        if o.parent is None:
            o.location = loc
            o.rotation_euler = (0, 0, math.radians(rot_z))


plane("Court", (0, 150, 0), 30, 40, tex_mat("Court", T + "Court_Arena.png"))
stone = tex_mat("Stone", T + "StoneWall.png")
base = col("Base", (0.04, 0.05, 0.08))
cap = col("Cap", (0.2, 0.24, 0.33))


def wall(name, c, s):
    box(name, c, s, stone, True)
    box(name + "b", (c[0], c[1], 0.22), (s[0] + 0.12, s[1] + 0.12, 0.44), base)
    box(name + "c", (c[0], c[1], 14.09), (s[0] + 0.22, s[1] + 0.22, 0.18), cap)


wall("W", (-15.5, 150, 7), (1, 42, 14))
wall("E", (15.5, 150, 7), (1, 42, 14))
wall("N", (0, 170.5, 7), (30, 1, 14))
gold = col("Gold", (1.0, 0.6, 0.05), 1.5)
for x in (-14.97, 14.97):
    box("band", (x, 150, 6.6), (0.07, 40, 0.18), gold)
# ceiling (does not block the sun's light, like in the game)
ceil = plane("Ceiling", (0, 150, 14), 32, 42, tex_mat("Ceil", T + "Ceiling.png", 8, 10.5), rot=(math.pi, 0, 0))
ceil.visible_shadow = False
wood = col("Wood", (0.2, 0.09, 0.04))
for y in range(132, 170, 4):
    b = box("beam", (0, y, 13.8), (32, 0.45, 0.4), wood)
    b.visible_shadow = False
lamp = col("Lamp", (1, 0.97, 0.9), 6)
for y in (140, 160):
    for x in (-9, 9):
        box("rig", (x, y, 13.2), (3.2, 1.0, 0.35), col("Rig", (0.03, 0.03, 0.04))).visible_shadow = False
        for k in (-1, 0, 1):
            box("lamp", (x + k, y, 13.0), (0.7, 0.7, 0.05), lamp).visible_shadow = False
        L = bpy.data.lights.new("pt", "POINT")
        L.energy = 2500
        L.color = (1, 0.95, 0.85)
        lo = bpy.data.objects.new("pt", L)
        lo.location = (x, y, 11)
        bpy.context.scene.collection.objects.link(lo)
# banners
for y, name in ((136, "Banner_King"), (143, "Banner_King"), (157, "Banner_Zander"), (164, "Banner_Zander")):
    for x, rz in ((-14.94, -math.pi / 2), (14.94, math.pi / 2)):
        plane("banner", (x, y, 9.5), -2.2, 4.4, tex_mat(name + str(x), T + name + ".png", alpha=True),
              rot=(math.pi / 2, 0, rz)).visible_shadow = False
for x in (-7, 7):
    plane("bannerN", (x, 169.94, 9.5), -2.4, 4.8, tex_mat("bz" + str(x), T + "Banner_Zander.png", alpha=True),
          rot=(math.pi / 2, 0, math.pi)).visible_shadow = False

append(O + "Hoop.blend", (0, 166.4, 0), 0)
append(O + "Hoop.blend", (0, 133.6, 0), 180)
append(O + "Bleachers.blend", (-11.5, 150, 0), -90)
append(O + "Bleachers.blend", (11.5, 150, 0), 90)
append(O + "Scoreboard.blend", (0, 150, 8.5), 0)
append(O + "KingSwat.blend", (0, 157, 0), 0)
append(O + "Zander.blend", (2.5, 146, 0), 180)
append(O + "BallDispenser.blend", (-9, 151, 0))
append(O + "BallDispenser.blend", (9, 151, 0))
for o in bpy.data.objects:
    if o.type == "ARMATURE" and o.animation_data:
        for t in o.animation_data.nla_tracks:
            t.mute = True
        o.animation_data.action = bpy.data.actions.get("Taunt") if "King" in o.name else bpy.data.actions.get("Idle")
bpy.context.scene.frame_set(12)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(R, "blender", "output", "Arena_Mockup.blend"))
