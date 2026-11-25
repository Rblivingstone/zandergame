# Usage: python blender/scene_mockup.py, then render with render_preview.py + ZG_CAM.
# Builds a Shrine 1 mock-up (Blender coords: x right, y = Unity z, z up) for a preview render
import bpy, os, math
R = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
T = R + "/unity/ZanderHoopShrines/Assets/ZanderGame/Resources/Textures/"
bpy.ops.wm.read_factory_settings(use_empty=True)

def tex_mat(name, img, sx=1, sy=1):
    m = bpy.data.materials.new(name); m.use_nodes = True
    nt = m.node_tree; b = nt.nodes["Principled BSDF"]
    t = nt.nodes.new("ShaderNodeTexImage"); t.image = bpy.data.images.load(img)
    mp = nt.nodes.new("ShaderNodeMapping"); mp.inputs["Scale"].default_value = (sx, sy, 1)
    tc = nt.nodes.new("ShaderNodeTexCoord")
    nt.links.new(tc.outputs["UV"], mp.inputs[0]); nt.links.new(mp.outputs[0], t.inputs[0])
    nt.links.new(t.outputs["Color"], b.inputs["Base Color"]); return m

def col_mat(name, rgb):
    m = bpy.data.materials.new(name); m.use_nodes = True
    m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = rgb + (1,); m.diffuse_color = rgb + (1,); return m

def box(name, c, s, mat, uv_scale=None):
    bpy.ops.mesh.primitive_cube_add(size=1, location=c); o = bpy.context.active_object
    o.scale = s; o.name = name
    bpy.ops.object.transform_apply(scale=True)
    if uv_scale:   # world-ish box UVs: each face 0..1 scaled
        bpy.ops.object.mode_set(mode="EDIT"); bpy.ops.uv.cube_project(cube_size=2.0); bpy.ops.object.mode_set(mode="OBJECT")
    o.data.materials.append(mat); return o

# court 18 x 24, Unity z 18..42 -> here y 18..42
bpy.ops.mesh.primitive_plane_add(size=1, location=(0, 30, 0)); f = bpy.context.active_object
f.scale = (18, 24, 1); bpy.ops.object.transform_apply(scale=True)
f.data.materials.append(tex_mat("Court", T + "Court_Shrine1.png"))
stone = tex_mat("Stone", T + "StoneWall.png")
base = col_mat("WallBase", (0.04, 0.05, 0.08)); cap = col_mat("WallCap", (0.2, 0.24, 0.33)); dark = col_mat("CapTrim", (0.025, 0.03, 0.05))
def wall(name, c, s, has_base=True):
    box(name, c, s, stone, True)
    bottom, top = c[2] - s[2] / 2, c[2] + s[2] / 2
    if has_base: box(name + "b", (c[0], c[1], bottom + 0.22), (s[0] + 0.12, s[1] + 0.12, 0.44), base)
    box(name + "c", (c[0], c[1], top + 0.09), (s[0] + 0.22, s[1] + 0.22, 0.18), cap)
    box(name + "t", (c[0], c[1], top - 0.04), (s[0] + 0.08, s[1] + 0.08, 0.08), dark)
wall("W", (-9.5, 30, 4), (1, 26, 8)); wall("E", (9.5, 30, 4), (1, 26, 8))
wall("NL", (-6.25, 42.5, 4), (5.5, 1, 8)); wall("NR", (6.25, 42.5, 4), (5.5, 1, 8))
wall("NT", (0, 42.5, 6.15), (3.0, 1, 3.7), False)
glow = col_mat("Glow", (1.0, 0.55, 0.1))
for x in (-8.98, 8.98): box("trim", (x, 30, 2.6), (0.06, 24, 0.12), glow)

def append(blend, loc, rot_z=0, scale=1.0):
    with bpy.data.libraries.load(blend) as (src, dst):
        dst.objects = [n for n in src.objects]
    roots = []
    for o in dst.objects:
        if o is None: continue
        bpy.context.scene.collection.objects.link(o)
        if o.parent is None: roots.append(o)
    for o in roots:
        o.location = loc; o.rotation_euler = (0, 0, math.radians(rot_z)); o.scale = (scale,) * 3
    return roots
O = R + "/blender/output/"
append(O + "Hoop.blend", (0, 38.4, 0), 180)     # faces -Y (south) toward the player... rotate so rim faces camera
append(O + "Gate.blend", (0, 42.5, 0), 0)
append(O + "PressurePlate.blend", (5.5, 33, 0))
append(O + "PressurePlate.blend", (-5, 25, 0))
append(O + "BallDispenser.blend", (5, 25, 0))
append(O + "ShrinePillar.blend", (-7.8, 40.8, 0)); append(O + "ShrinePillar.blend", (7.8, 40.8, 0))
z = append(O + "Zander.blend", (1.5, 27.5, 0), 200)
for o in bpy.data.objects:
    if o.type == "ARMATURE" and o.animation_data:
        for t in o.animation_data.nla_tracks: t.mute = True
        o.animation_data.action = bpy.data.actions.get("Idle")
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(R, "blender", "output", "Shrine1_Mockup.blend"))
