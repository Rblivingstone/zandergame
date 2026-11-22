"""
Generates every prop / environment piece used by Zander's Hoop Shrines.

Each prop is built in a fresh scene under an empty root named after the prop and
exported to its own FBX. Moving parts are separate, NAMED child objects so the
Unity scripts can find and animate them:

  Prop            Moving child      Notes (Blender coords: Z up, front = -Y)
  --------------  ----------------  ----------------------------------------------
  Basketball      -                 radius 0.14, centred on origin
  Hoop            -                 origin on floor under the rim; rim centre at
                                    (0, 0, 3.0), radius 0.32; backboard behind the
                                    rim (+Y); material ZG_Rim is re-tinted in game
  PressurePlate   PlateTop          1.6 x 1.6 base, top surface at z = 0.18
  Gate            GatePanel         opening 3.0 wide x 3.5 high, panel sinks
  BallDispenser   -                 ball rests at z ~= 1.06
  BouncePad       PadTop            top surface at z = 0.28
  ReboundBoard    BoardPivot        rotates around Z; board centre at z = 2.0
                                    faces -Y (Unity +Z)
  Bleachers       -                 10 m wide, 3 tiers rising toward +Y
  Scoreboard      -                 4 x 2 m hanging board
  ShrinePillar    -                 1 x 1 x 5 m stone column with glowing runes
  Trophy          -                 the champion's cup (0.9 m)

Usage:
    blender -b -P blender/props.py -- --out <dir>
    python blender/props.py --out <dir>
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import zg_common as zg  # noqa: E402  (imports bpy first)
import bmesh  # noqa: E402
from mathutils import Vector  # noqa: E402

M = zg.material
S, C, B, T = zg.sphere, zg.cylinder, zg.box, zg.torus


def _root(name):
    return zg.empty(name)


def _attach(objs, parent):
    for o in objs:
        zg.parent_keep(o, parent)


def _strip_caps(obj):
    """Remove the n-gon caps of a cylinder/cone (used for the net)."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    caps = [f for f in bm.faces if len(f.verts) > 4]
    bmesh.ops.delete(bm, geom=caps, context="FACES_ONLY")
    bm.to_mesh(obj.data)
    bm.free()


# --------------------------------------------------------------------------- #
def make_basketball():
    root = _root("Basketball")
    orange = M("BallOrange", "#E8772E", 0.75)
    seam = M("BallSeam", "#1E140E", 0.8)
    r = 0.14
    parts = [S("ball", (0, 0, 0), r, mat=orange, segments=32, rings=20)]
    parts.append(T("seam_v", (0, 0, 0), r + 0.001, 0.004, rot=(0, 90, 0), mat=seam, major_seg=48, minor_seg=6))
    parts.append(T("seam_h", (0, 0, 0), r + 0.001, 0.004, mat=seam, major_seg=48, minor_seg=6))
    for sx in (1, -1):
        parts.append(T("seam_c", (sx * 0.09, 0, 0), 0.108, 0.004, rot=(0, 90, 0), mat=seam, major_seg=48, minor_seg=6))
    ball = zg.join(parts, "BallMesh")
    _attach([ball], root)
    return root


def make_hoop():
    root = _root("Hoop")
    pole = M("HoopPole", "#3B4252", 0.4, 0.6)
    pad = M("HoopPad", "#1F5FD6", 0.7)
    board = M("Backboard", "#F7F9FC", 0.15)
    line = M("BoardLine", "#E8772E", 0.5)
    rim = M("Rim", "#FF6A1A", 0.3, 0.5)
    net = M("Net", "#FFFFFF", 0.9)
    objs = []
    objs.append(B("base", (0, 1.1, 0.15), (0.9, 0.9, 0.3), mat=pad, bevel=0.05))
    objs.append(C("pole", (0, 1.1, 1.8), 0.08, 3.6, mat=pole))
    objs.append(B("pole_pad", (0, 1.1, 1.0), (0.3, 0.3, 2.0), mat=pad, bevel=0.04))
    objs.append(B("arm", (0, 0.8, 3.35), (0.12, 0.62, 0.12), mat=pole))
    objs.append(B("board", (0, 0.45, 3.35), (1.6, 0.06, 1.05), mat=board, bevel=0.02))
    # orange shooter's square + border on the front face (y = 0.415)
    fy = 0.416
    for (x, z, w, h) in ((0, 3.85, 1.56, 0.03), (0, 2.85, 1.56, 0.03), (-0.78, 3.35, 0.03, 1.0), (0.78, 3.35, 0.03, 1.0),
                         (0, 3.42, 0.6, 0.03), (0, 3.06, 0.6, 0.03), (-0.29, 3.24, 0.03, 0.36), (0.29, 3.24, 0.03, 0.36)):
        objs.append(B("line", (x, fy, z), (w, 0.01, h), mat=line))
    objs.append(B("bracket", (0, 0.36, 2.98), (0.2, 0.12, 0.06), mat=rim))
    objs.append(T("rim", (0, 0, 3.0), 0.32, 0.02, mat=rim, major_seg=40, minor_seg=8))
    n = zg.cone("net", (0, 0, 2.78), 0.21, 0.31, 0.42, mat=net, verts=20)
    _strip_caps(n)
    objs.append(n)
    for i in range(10):
        a = i / 10 * 2 * math.pi
        top = Vector((math.cos(a) * 0.31, math.sin(a) * 0.31, 2.99))
        bot = Vector((math.cos(a + 0.4) * 0.21, math.sin(a + 0.4) * 0.21, 2.57))
        objs.append(zg.capsule_between("net_cord", top, bot, 0.008, mat=net, verts=6))
    _attach(objs, root)
    return root


def make_pressure_plate():
    root = _root("PressurePlate")
    stone = M("PlateStone", "#565D6B", 0.85)
    glow = M("PlateGlow", "#FFB347", 0.3, emission="#FF9A1F", emission_strength=1.5)
    top_mat = M("PlateTop", "#4AA3FF", 0.45)
    base = B("base", (0, 0, 0.05), (1.6, 1.6, 0.1), mat=stone, bevel=0.03)
    ring = T("rune_ring", (0, 0, 0.1), 0.72, 0.015, scale=(1, 1, 0.5), mat=glow, major_seg=4, minor_seg=4)
    ring.rotation_euler = (0, 0, math.radians(45))
    top = B("PlateTop", (0, 0, 0.14), (1.3, 1.3, 0.08), mat=top_mat, bevel=0.02)
    _attach([base, ring], root)
    top.parent = root
    return root


def make_gate():
    root = _root("Gate")
    stone = M("GateStone", "#4A5162", 0.85)
    stone_dark = M("GateStoneDark", "#373D4B", 0.9)
    glow = M("GateGlow", "#FFB347", 0.3, emission="#FF9A1F", emission_strength=2.0)
    panel_mat = M("GatePanel", "#2C3E66", 0.5, 0.2)
    ball = M("GateEmblem", "#E8772E", 0.6)
    seam = M("GateSeam", "#1E140E", 0.8)
    objs = []
    for sx in (1, -1):
        objs.append(B("pillar", (sx * 1.75, 0, 2.15), (0.5, 0.7, 4.3), mat=stone, bevel=0.04))
        objs.append(B("pillar_cap", (sx * 1.75, 0, 4.35), (0.7, 0.85, 0.2), mat=stone_dark, bevel=0.03))
        objs.append(B("pillar_rune", (sx * 1.75, -0.36, 2.0), (0.12, 0.02, 2.4), mat=glow))
        objs.append(B("pillar_rune_b", (sx * 1.75, 0.36, 2.0), (0.12, 0.02, 2.4), mat=glow))
    objs.append(B("lintel", (0, 0, 3.9), (4.0, 0.7, 0.8), mat=stone, bevel=0.04))
    objs.append(B("lintel_rune", (0, -0.36, 3.9), (2.6, 0.02, 0.1), mat=glow))
    _attach(objs, root)
    panel_parts = [B("panel", (0, 0, 1.75), (3.0, 0.25, 3.5), mat=panel_mat, bevel=0.03)]
    for sy in (1, -1):
        panel_parts.append(C("emblem", (0, sy * 0.13, 1.9), 0.55, 0.04, rot=(90, 0, 0), mat=ball, verts=32))
        panel_parts.append(B("emblem_v", (0, sy * 0.155, 1.9), (0.04, 0.01, 1.1), mat=seam))
        panel_parts.append(B("emblem_h", (0, sy * 0.155, 1.9), (1.1, 0.01, 0.04), mat=seam))
        for gx in (-1.1, 1.1):
            panel_parts.append(B("groove", (gx, sy * 0.13, 1.75), (0.06, 0.02, 3.2), mat=glow))
    panel = zg.join(panel_parts, "GatePanel")
    panel.parent = root
    return root


def make_ball_dispenser():
    root = _root("BallDispenser")
    stone = M("DispStone", "#565D6B", 0.8)
    trim = M("DispTrim", "#2C3E66", 0.5)
    glow = M("DispGlow", "#FFB347", 0.3, emission="#FF9A1F", emission_strength=2.0)
    objs = [
        C("base", (0, 0, 0.08), 0.5, 0.16, mat=trim, verts=8),
        C("column", (0, 0, 0.5), 0.26, 0.8, mat=stone, verts=8),
        T("glow_ring", (0, 0, 0.62), 0.27, 0.025, mat=glow, major_seg=8, minor_seg=6),
        C("top", (0, 0, 0.9), 0.33, 0.06, mat=trim, verts=8),
        T("cup", (0, 0, 0.95), 0.14, 0.035, mat=trim, major_seg=16),
    ]
    _attach(objs, root)
    return root


def make_bounce_pad():
    root = _root("BouncePad")
    base_m = M("PadBase", "#373D4B", 0.6, 0.4)
    top_m = M("PadTop", "#3CE07A", 0.4, emission="#1FB85A", emission_strength=0.8)
    arrow = M("PadArrow", "#FFFFFF", 0.4)
    objs = [C("base", (0, 0, 0.1), 0.85, 0.2, mat=base_m, verts=32),
            T("rim", (0, 0, 0.2), 0.8, 0.05, mat=base_m, major_seg=32)]
    _attach(objs, root)
    top_parts = [C("top", (0, 0, 0.24), 0.7, 0.08, mat=top_m, verts=32)]
    for i in range(3):
        top_parts.append(zg.cone("chevron%d" % i, (0, -0.3 + i * 0.3, 0.29), 0.18, 0.0, 0.02, mat=arrow, verts=3))
    top = zg.join(top_parts, "PadTop")
    top.parent = root
    return root


def make_rebound_board():
    root = _root("ReboundBoard")
    base_m = M("RBBase", "#373D4B", 0.7, 0.3)
    post = M("RBPost", "#8C96A6", 0.4, 0.7)
    face = M("RBFace", "#DDEBFF", 0.1)
    frame = M("RBGlow", "#4AD7FF", 0.3, emission="#29C4FF", emission_strength=2.0)
    arrow = M("RBArrow", "#FFC21A", 0.4)
    base = C("base", (0, 0, 0.1), 0.55, 0.2, mat=base_m, verts=12)
    _attach([base], root)
    pivot = zg.empty("BoardPivot", parent=root)
    parts = [C("post", (0, 0, 0.75), 0.08, 1.1, mat=post),
             B("panel", (0, 0, 2.0), (1.6, 0.12, 1.6), mat=face, bevel=0.03)]
    # glowing frame on the front (-Y) face
    for (x, z, w, h) in ((0, 2.78, 1.6, 0.06), (0, 1.22, 1.6, 0.06), (-0.78, 2.0, 0.06, 1.6), (0.78, 2.0, 0.06, 1.6)):
        parts.append(B("frame", (x, -0.065, z), (w, 0.02, h), mat=frame))
    # a target ring on the face
    parts.append(T("target", (0, -0.07, 2.0), 0.35, 0.03, rot=(90, 0, 0), mat=frame))
    # arrow on top pointing the way the board "throws" (-Y)
    parts.append(B("arrow_shaft", (0, -0.15, 2.9), (0.08, 0.5, 0.06), mat=arrow))
    ah = zg.cone("arrow_head", (0, -0.5, 2.9), 0.16, 0.0, 0.3, rot=(90, 0, 0), mat=arrow, verts=12)
    parts.append(ah)
    board = zg.join(parts, "BoardMesh")
    board.parent = pivot
    return root


def make_bleachers():
    root = _root("Bleachers")
    frame = M("BleacherFrame", "#8C96A6", 0.4, 0.6)
    seat_cols = ["#1F5FD6", "#F4F4F4", "#E8772E"]
    objs = []
    for i in range(3):
        z = 0.5 * (i + 1)
        objs.append(B("tier%d" % i, (0, 0.5 + i * 1.0, z / 2), (10, 1.0, z), mat=frame))
        objs.append(B("seat%d" % i, (0, 0.35 + i * 1.0, z + 0.05), (9.8, 0.5, 0.1),
                      mat=M("Seat%d" % i, seat_cols[i], 0.6)))
    objs.append(B("rail", (0, 3.0, 2.0), (10, 0.06, 1.0), mat=frame))
    _attach(objs, root)
    return root


def make_scoreboard():
    root = _root("Scoreboard")
    body = M("SBBody", "#1E1E24", 0.5)
    screen = M("SBScreen", "#0B0F1A", 0.2)
    led = M("SBLed", "#FFC21A", 0.3, emission="#FFB000", emission_strength=2.5)
    red = M("SBRed", "#FF3B3B", 0.3, emission="#FF2020", emission_strength=2.5)
    objs = [B("body", (0, 0, 0), (4.2, 0.4, 2.1), mat=body, bevel=0.05),
            B("screen_l", (-1.05, -0.21, 0.1), (1.7, 0.02, 1.4), mat=screen),
            B("screen_r", (1.05, -0.21, 0.1), (1.7, 0.02, 1.4), mat=screen),
            B("bar", (0, -0.215, -0.82), (3.8, 0.02, 0.12), mat=led)]
    for sx in (-1, 1):
        objs.append(S("lamp", (sx * 1.95, -0.22, 0.88), 0.07, mat=red))
    objs.append(C("hanger_l", (-1.5, 0, 2.05), 0.03, 2.0, mat=body))
    objs.append(C("hanger_r", (1.5, 0, 2.05), 0.03, 2.0, mat=body))
    _attach(objs, root)
    return root


def make_shrine_pillar():
    root = _root("ShrinePillar")
    stone = M("PillarStone", "#4A5162", 0.85)
    dark = M("PillarDark", "#373D4B", 0.9)
    glow = M("PillarGlow", "#FFB347", 0.3, emission="#FF9A1F", emission_strength=2.0)
    objs = [B("foot", (0, 0, 0.15), (1.3, 1.3, 0.3), mat=dark, bevel=0.04),
            B("shaft", (0, 0, 2.5), (1.0, 1.0, 4.4), mat=stone, bevel=0.04),
            B("cap", (0, 0, 4.85), (1.3, 1.3, 0.3), mat=dark, bevel=0.04)]
    for i in range(4):
        a = i * math.pi / 2
        objs.append(B("rune", (math.sin(a) * 0.51, math.cos(a) * 0.51, 3.2), (0.18, 0.18, 0.9),
                      rot=(0, 0, math.degrees(a)), mat=glow))
        objs.append(S("dot", (math.sin(a) * 0.51, math.cos(a) * 0.51, 2.3), 0.09, mat=glow))
    _attach(objs, root)
    return root


def make_trophy():
    root = _root("Trophy")
    gold = M("TrophyGold", "#FFC21A", 0.2, 0.9)
    wood = M("TrophyBase", "#5B3A1E", 0.6)
    ball = M("TrophyBall", "#E8772E", 0.6)
    objs = [B("plinth", (0, 0, 0.1), (0.5, 0.5, 0.2), mat=wood, bevel=0.02),
            C("foot", (0, 0, 0.25), 0.18, 0.1, mat=gold),
            C("stem", (0, 0, 0.38), 0.05, 0.2, mat=gold),
            zg.cone("cup", (0, 0, 0.6), 0.1, 0.24, 0.3, mat=gold, verts=24),
            T("lip", (0, 0, 0.75), 0.24, 0.02, mat=gold)]
    for sx in (1, -1):
        objs.append(T("handle", (sx * 0.27, 0, 0.62), 0.08, 0.018, rot=(90, 0, 0), mat=gold))
    objs.append(S("ball", (0, 0, 0.86), 0.12, mat=ball))
    _attach(objs, root)
    return root


PROPS = [
    make_basketball, make_hoop, make_pressure_plate, make_gate, make_ball_dispenser, make_bounce_pad,
    make_rebound_board, make_bleachers, make_scoreboard, make_shrine_pillar, make_trophy,
]


def main():
    out = zg.parse_out_dir()
    blend_dir = os.path.join(zg.HERE, "output")
    os.makedirs(blend_dir, exist_ok=True)
    for fn in PROPS:
        zg.reset_scene()
        root = fn()
        objs = [root] + list(root.children_recursive)
        zg.export_fbx(os.path.join(out, root.name + ".fbx"), objs, animated=False)
        zg.save_blend(os.path.join(blend_dir, root.name + ".blend"))


if __name__ == "__main__":
    main()
