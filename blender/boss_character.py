"""
Generates "KING SWAT", the boss of the Championship Court, in the same soft
chibi-anime style as Zander.

An original robot basketball king: a big round basketball-helmet head with
glowing golden anime eyes, angry brow plates and a toothy grin, a golden crown
with a ruby, gold royal shoulder pads, a flowing red royal cape, a purple #99
jersey over a smooth gunmetal robot body, white shorts, and enormous red
swatting gauntlets. About 3.4 m tall.

Animations: Idle, Walk, Shoot, Stomp, Block, Hit, Taunt, Defeat

Usage:
    blender -b -P blender/boss_character.py -- --out <dir>
    python blender/boss_character.py --out <dir>          (pip "bpy" module)
Writes KingSwat.fbx (+ blender/output/KingSwat.blend).
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import zg_common as zg  # noqa: E402
import zg_anime as za  # noqa: E402
import bmesh  # noqa: E402
from mathutils import Vector  # noqa: E402

BALL = "#EE7A2C"
SEAM = "#2A1A12"
EYE = "#FFD84A"
EYE_CORE = "#FFF6C8"
PUPIL = "#3A1B00"
CROWN = "#FFC21A"
GEM = "#E0245E"
JERSEY = "#6A2DB8"
TRIM = "#FFC21A"
SHORTS = "#F4F4F4"
METAL = "#5E6A80"
METAL_LIGHT = "#9AA6BC"
GLOVE = "#D62828"
BOOT = "#2A1E4A"
CAPE = "#B3122E"
CAPE_IN = "#5A0A1C"
TOOTH = "#FFFFFF"
MOUTH = "#2A0A0A"

HEAD_C = Vector((0, 0, 2.78))
HEAD_RX, HEAD_RY, HEAD_RZ = 0.56, 0.53, 0.55


def build_body(rig):
    M = zg.material
    m = dict(
        ball=M("BossBall", BALL, 0.45, 0.2), seam=M("BossSeam", SEAM, 0.8),
        eye=M("BossEye", EYE, 0.2, emission=EYE, emission_strength=3.0),
        eye_core=M("BossEyeCore", EYE_CORE, 0.2, emission=EYE_CORE, emission_strength=4.0),
        pupil=M("BossPupil", PUPIL, 0.3), crown=M("Crown", CROWN, 0.25, 0.8),
        gem=M("Gem", GEM, 0.1, emission=GEM, emission_strength=1.0), jersey=M("BossJersey", JERSEY, 0.6),
        trim=M("BossTrim", TRIM, 0.4, 0.4), shorts=M("BossShorts", SHORTS, 0.7),
        metal=M("BossMetal", METAL, 0.3, 0.8), metal_light=M("BossMetalLight", METAL_LIGHT, 0.25, 0.8),
        glove=M("Glove", GLOVE, 0.45), boot=M("BossBoot", BOOT, 0.5), cape=M("Cape", CAPE, 0.7),
        cape_in=M("CapeInner", CAPE_IN, 0.8), tooth=M("Tooth", TOOTH, 0.3), mouth=M("BossMouth", MOUTH, 0.6))
    parts = []

    # ---------------------------------------------------------- robot body --
    J, E = [], []
    RS = 1.3

    def j(co, r):
        J.append((co, (r[0] * RS, r[1] * RS)))
        return len(J) - 1

    pelvis = j((0, 0, 1.05), (0.4, 0.3))
    waist = j((0, 0, 1.38), (0.38, 0.29))
    chest = j((0, 0, 1.78), (0.5, 0.36))
    neck0 = j((0, 0, 2.15), (0.19, 0.18))
    neck1 = j((0, 0, 2.38), (0.17, 0.17))
    E += [(pelvis, waist), (waist, chest), (chest, neck0), (neck0, neck1)]
    for sx in (1, -1):
        sh = j((sx * 0.62, 0, 2.02), (0.2, 0.2))
        el = j((sx * 0.8, 0.02, 1.48), (0.15, 0.15))
        wr = j((sx * 0.86, 0, 1.02), (0.14, 0.14))
        E += [(neck0, sh), (sh, el), (el, wr)]
        hip = j((sx * 0.25, 0, 1.0), (0.22, 0.22))
        kn = j((sx * 0.3, 0, 0.56), (0.17, 0.17))
        an = j((sx * 0.32, 0, 0.22), (0.15, 0.15))
        E += [(pelvis, hip), (hip, kn), (kn, an)]
    body = za.skin_body("KingSwat_Frame", J, E, m["metal"], root=pelvis)
    body.data.materials.append(m["jersey"])
    body.data.materials.append(m["shorts"])

    def region(c):
        ax = abs(c.x)
        if 1.3 <= c.z < 2.2 and ax < (0.58 if c.z < 1.6 else 0.42):
            return 1
        if 0.72 <= c.z < 1.3 and ax < 0.62:
            return 2
        return 0
    puff = {1: 0.02, 2: 0.035}
    moved = {}
    for poly in body.data.polygons:
        r = region(poly.center)
        poly.material_index = r
        if r:
            for vi in poly.vertices:
                moved[vi] = max(moved.get(vi, 0.0), puff[r])
    for vi, d in moved.items():
        v = body.data.vertices[vi]
        v.co += v.normal * (d + (0.03 if 0.72 <= v.co.z < 0.82 else 0.0))
    body.data.update()
    za.auto_weights(body, rig)
    parts.append(body)

    pf = za.Projector(body)
    pb = za.Projector(body, from_back=True)
    vneck = [(-0.22, 2.17), (-0.1, 2.02), (0.0, 1.97), (0.1, 2.02), (0.22, 2.17)]
    parts.append(za.fix_normals(pf.strip("vneck", vneck, 0.06, m["trim"], 0.025, "chest"), pf))
    hem = [(x / 20.0 * 1.2 - 0.6, 1.33) for x in range(21)]
    parts.append(za.fix_normals(pf.strip("hem", hem, 0.06, m["trim"], 0.03, "spine"), pf))
    parts.append(za.fix_normals(pb.strip("hem_b", hem, 0.06, m["trim"], 0.03, "spine"), pb))
    # "99"
    for proj in (pf, pb):
        for sx in (1, -1):
            cx = sx * 0.15
            ring = za.ellipse(cx, 1.8, 0.075, 0.07, 20)
            parts.append(za.fix_normals(proj.strip("nine_ring", ring + [ring[0]], 0.04, m["trim"], 0.03, "chest"), proj))
            parts.append(za.fix_normals(proj.strip("nine_tail", [(cx + 0.072, 1.8), (cx + 0.06, 1.62), (cx - 0.02, 1.56)],
                                                   0.04, m["trim"], 0.03, "chest"), proj))

    # royal shoulder pads
    for sx, side in ((1, "L"), (-1, "R")):
        pad = zg.sphere("pauldron." + side, (sx * 0.66, 0, 2.08), 0.3, scale=(1.0, 0.95, 0.62), mat=m["crown"])
        za.deform(pad, lambda v: Vector((v.x, v.y, max(v.z, 1.96))))
        parts.append(za.rigid(pad, "upper_arm." + side))
        parts.append(zg.sphere("pad_gem." + side, (sx * 0.78, -0.2, 2.1), 0.05, mat=m["gem"], bone="upper_arm." + side))
        # huge red swatting gauntlets
        parts.append(zg.torus("cuff." + side, (sx * 0.86, 0, 1.0), 0.17, 0.06, mat=m["crown"], bone="hand." + side))
        glove = zg.sphere("glove." + side, (sx * 0.88, -0.02, 0.8), 0.3, scale=(0.95, 1.0, 1.1), mat=m["glove"],
                          segments=32, rings=20)
        parts.append(za.rigid(glove, "hand." + side))
        parts.append(zg.sphere("thumb." + side, (sx * 0.68, -0.15, 0.86), 0.11, scale=(1, 1, 1.4), mat=m["glove"],
                               bone="hand." + side))
        for k in range(3):
            parts.append(zg.sphere("knuckle." + side, (sx * (0.8 + 0.08 * k), -0.27, 0.92), 0.06, mat=m["glove"],
                                   bone="hand." + side))
        # boots
        x = sx * 0.32
        boot = zg.sphere("boot." + side, (x, -0.1, 0.17), 1.0, scale=(0.22, 0.38, 0.19), mat=m["boot"], segments=32, rings=20)
        za.deform(boot, lambda v: Vector((v.x, v.y, max(v.z, 0.06))))
        parts.append(za.rigid(boot, "foot." + side))
        parts.append(za.rigid(zg.sphere("boot_sole." + side, (x, -0.1, 0.05), 1.0, scale=(0.24, 0.41, 0.06),
                                        mat=m["shorts"]), "foot." + side))
        parts.append(zg.torus("boot_trim." + side, (x, 0.0, 0.3), 0.17, 0.035, mat=m["crown"], bone="foot." + side))
        parts.append(zg.sphere("knee_pad." + side, (x - sx * 0.02, -0.12, 0.56), 0.16, scale=(1, 0.6, 1), mat=m["metal_light"],
                               bone="shin." + side))

    # royal cape (solid so both sides render)
    parts.append(build_cape(m))

    # ------------------------------------------------------- basketball head --
    head = za.sculpt_head("head", HEAD_C, HEAD_RX, HEAD_RY, HEAD_RZ, m["ball"], jaw=0.12, chin=0.04)
    za.rigid(head, "head")
    parts.append(head)
    # seams that avoid the face: the side-to-side ring plus two curved side seams
    parts.append(zg.torus("seam_mid", HEAD_C, 0.548, 0.013, rot=(90, 0, 0), mat=m["seam"], bone="head", major_seg=64))
    for sx in (1, -1):
        parts.append(zg.torus("seam_side", HEAD_C + Vector((sx * 0.42, 0.0, 0)), 0.36, 0.013, rot=(0, 90, 0),
                              mat=m["seam"], bone="head", major_seg=64))
        # robot ear discs
        parts.append(zg.cylinder("ear_disc", (sx * 0.56, 0.02, 2.74), 0.14, 0.08, rot=(0, 90, 0), mat=m["metal_light"],
                                 bone="head", verts=32))
        parts.append(zg.cylinder("ear_glow", (sx * 0.6, 0.02, 2.74), 0.07, 0.02, rot=(0, 90, 0), mat=m["eye"],
                                 bone="head", verts=24))
    parts += build_face(head, m)
    parts += build_crown(m)

    for p in parts:
        if not p.vertex_groups:
            za.rigid(p, "head")
    return zg.join(parts, "KingSwat_Body")


def build_face(head, m):
    pr = za.Projector(head)
    out = []

    def add(o):
        if o is not None:
            out.append(za.fix_normals(o, pr))

    ey = 2.76
    for sx in (1, -1):
        ex = sx * 0.2
        # glowing golden anime eyes, with the top cut flat by the angry brow
        eye = [(x, min(z, ey + 0.06 - sx * (x - ex) * 0.5)) for x, z in za.ellipse(ex, ey, 0.12, 0.13, 40)]
        add(pr.shape("eye", eye, m["eye"], 0.006, "head"))
        add(pr.shape("eye_core", za.ellipse(ex - sx * 0.02, ey - 0.02, 0.055, 0.07, 24), m["eye_core"], 0.009, "head"))
        add(pr.shape("pupil", za.ellipse(ex - sx * 0.025, ey - 0.03, 0.03, 0.05, 20), m["pupil"], 0.012, "head"))
        add(pr.shape("shine", za.ellipse(ex + sx * 0.03, ey + 0.03, 0.022, 0.022, 16), m["eye_core"], 0.014, "head"))
        brow = [(ex + sx * 0.16, ey + 0.16), (ex, ey + 0.1), (ex - sx * 0.12, ey + 0.04)]
        add(pr.strip("brow", brow, [0.05, 0.07, 0.06], m["seam"], 0.016, "head"))
    # smug toothy grin
    grin = [(-0.2, 2.56)] + za.arc(0.0, 2.58, 0.2, 0.12, math.radians(190), math.radians(350), 16) + [(0.2, 2.6)]
    add(pr.shape("grin", grin, m["mouth"], 0.006, "head"))
    teeth = [(-0.17, 2.57), (0.17, 2.6), (0.15, 2.53), (-0.15, 2.51)]
    add(pr.shape("teeth", teeth, m["tooth"], 0.01, "head"))
    for k in range(-3, 4):
        add(pr.strip("tooth_gap", [(k * 0.045, 2.6), (k * 0.045, 2.505)], 0.008, m["mouth"], 0.013, "head"))
    return out


def build_crown(m):
    out = []
    top = HEAD_C.z + HEAD_RZ * 0.82
    out.append(zg.cylinder("crown_band", (0, 0, top), 0.36, 0.18, mat=m["crown"], verts=40))
    out.append(zg.torus("crown_rim", (0, 0, top - 0.09), 0.36, 0.03, mat=m["crown"], major_seg=40))
    out.append(zg.torus("crown_rim2", (0, 0, top + 0.09), 0.36, 0.025, mat=m["crown"], major_seg=40))
    for i in range(7):
        a = i / 7 * 2 * math.pi - math.pi / 2
        base = Vector((math.cos(a) * 0.34, math.sin(a) * 0.34, top + 0.06))
        out_dir = Vector((math.cos(a), math.sin(a), 0)) * 0.05
        out.append(za.hair_lock("crown_point", base, base + Vector((0, 0, 0.12)) + out_dir, base + Vector((0, 0, 0.26)) + out_dir,
                                0.09, 0.03, m["crown"], flat_axis=(-math.sin(a), math.cos(a), 0), segs=6, ring=8))
        out.append(zg.sphere("crown_ball", base + Vector((0, 0, 0.28)) + out_dir, 0.035, mat=m["crown"]))
    out.append(zg.sphere("crown_gem", (0, -0.37, top), 0.07, scale=(1, 0.5, 1.25), mat=m["gem"]))
    for p in out:
        za.rigid(p, "head")
    return out


def build_cape(m):
    """A flowing cape hanging from the shoulders down the back."""
    rows, cols = 14, 16
    verts, faces = [], []
    for r in range(rows):
        t = r / (rows - 1)
        z = 2.1 - t * 1.6
        half_w = 0.5 + 0.35 * t
        for c in range(cols):
            u = c / (cols - 1) * 2 - 1
            x = u * half_w
            fold = 0.04 * t * math.sin(u * math.pi * 3)
            y = 0.38 + 0.18 * t + 0.25 * (1 - u * u) * (0.3 + 0.7 * t) * 0.5 + fold
            verts.append((x, y, z))
    for r in range(rows - 1):
        for c in range(cols - 1):
            a = r * cols + c
            faces.append((a, a + 1, a + cols + 1, a + cols))
    me = __import__("bpy").data.meshes.new("cape")
    me.from_pydata(verts, [], faces)
    obj = __import__("bpy").data.objects.new("cape", me)
    zg.link(obj)
    obj.data.materials.append(m["cape"])
    obj.data.materials.append(m["cape_in"])
    zg.set_active(obj)
    sol = obj.modifiers.new("solid", "SOLIDIFY")
    sol.thickness = 0.03
    sol.material_offset = 1
    import bpy
    bpy.ops.object.modifier_apply(modifier=sol.name)
    za.subdivide(obj, 1)
    # gold trim along the bottom edge
    trim = zg.cylinder("cape_trim", (0, 0, 0), 1, 1, mat=m["crown"])
    bpy.data.objects.remove(trim)
    pts = [Vector(verts[(rows - 1) * cols + c]) + Vector((0, 0.0, 0.02)) for c in range(cols)]
    tr = za.hair_lock("cape_trim", pts[0], pts[cols // 2] + Vector((0, 0.05, 0)), pts[-1], 0.03, 0.03, m["crown"],
                      bone=None, segs=16, ring=6, tip_power=0.0)
    za.rigid(obj, "chest")
    za.rigid(tr, "chest")
    return zg.join([obj, tr], "cape")


BONES = [
    ("root", (0, 0, 0), (0, 0, 0.3), None),
    ("hips", (0, 0, 1.0), (0, 0, 1.32), "root"),
    ("spine", (0, 0, 1.32), (0, 0, 1.65), "hips"),
    ("chest", (0, 0, 1.65), (0, 0, 2.15), "spine"),
    ("neck", (0, 0, 2.15), (0, 0, 2.32), "chest"),
    ("head", (0, 0, 2.32), (0, 0, 3.35), "neck"),
]
for _sx, _n in ((1, "L"), (-1, "R")):
    BONES += [
        ("upper_arm." + _n, (_sx * 0.62, 0, 2.02), (_sx * 0.8, 0.02, 1.48), "chest"),
        ("forearm." + _n, (_sx * 0.8, 0.02, 1.48), (_sx * 0.86, 0, 1.02), "upper_arm." + _n),
        ("hand." + _n, (_sx * 0.86, 0, 1.02), (_sx * 0.88, -0.02, 0.6), "forearm." + _n),
        ("thigh." + _n, (_sx * 0.25, 0, 1.0), (_sx * 0.3, 0, 0.56), "hips"),
        ("shin." + _n, (_sx * 0.3, 0, 0.56), (_sx * 0.32, 0, 0.22), "thigh." + _n),
        ("foot." + _n, (_sx * 0.32, 0, 0.22), (_sx * 0.32, -0.4, 0.08), "shin." + _n),
    ]


def anim_idle(rig):
    a = zg.ActionBuilder(rig, "Idle", 60)
    for f, t in ((1, 0), (16, 0.5), (31, 1), (46, 0.5), (60, 0)):
        a.pose(f, rot={"spine": (4 + 3 * t, 0, 0), "head": (-4 * t, 0, 4 * math.sin(t * math.pi)),
                       "upper_arm.L": (-10, 0, -12 - 4 * t), "upper_arm.R": (-10, 0, 12 + 4 * t),
                       "forearm.L": (-25, 0, 0), "forearm.R": (-25, 0, 0),
                       "thigh.L": (-10, 0, -5), "thigh.R": (-10, 0, 5), "shin.L": (18, 0, 0), "shin.R": (18, 0, 0),
                       "foot.L": (-8, 0, 0), "foot.R": (-8, 0, 0)},
               loc={"hips": (0, 0, -0.04 * t)})
    return a.finish()


def anim_walk(rig):
    a = zg.ActionBuilder(rig, "Walk", 33)
    for i in range(0, 33, 4):
        ph = i / 32 * 2 * math.pi
        s, c = math.sin(ph), math.cos(ph)
        a.pose(i + 1, rot={"hips": (0, 0, 8 * s), "spine": (8, 0, -6 * s), "head": (-6, 0, 4 * s),
                           "thigh.L": (-30 * s, 0, 0), "thigh.R": (30 * s, 0, 0),
                           "shin.L": (15 + 30 * max(0, -c), 0, 0), "shin.R": (15 + 30 * max(0, c), 0, 0),
                           "upper_arm.L": (25 * s, 0, -14), "upper_arm.R": (-25 * s, 0, 14),
                           "forearm.L": (-30, 0, 0), "forearm.R": (-30, 0, 0)},
               loc={"hips": (0, 0, 0.05 * abs(c) - 0.04)})
    return a.finish()


def anim_shoot(rig):
    a = zg.ActionBuilder(rig, "Shoot", 30, loop=False)
    load = {"spine": (12, 0, 0), "thigh.L": (-30, 0, 0), "thigh.R": (-30, 0, 0), "shin.L": (55, 0, 0),
            "shin.R": (55, 0, 0), "foot.L": (-20, 0, 0), "foot.R": (-20, 0, 0),
            "upper_arm.R": (-100, 0, 20), "forearm.R": (-90, 0, 0), "upper_arm.L": (-90, 0, -25),
            "forearm.L": (-80, 0, 0)}
    a.pose(1, rot=load, loc={"hips": (0, 0, -0.18)})
    up = {"spine": (-6, 0, 0), "head": (-10, 0, 0), "upper_arm.R": (-135, 0, -40), "forearm.R": (-35, 0, 0),
          "hand.R": (-40, 0, 0), "upper_arm.L": (-120, 0, 40), "forearm.L": (-50, 0, 0)}
    a.pose(12, rot=up, loc={"hips": (0, 0, 0.12)})
    a.pose(30, rot=up, loc={"hips": (0, 0, 0)})
    return a.finish()


def anim_stomp(rig):
    a = zg.ActionBuilder(rig, "Stomp", 36, loop=False)
    lift = {"spine": (-8, 0, 0), "head": (-15, 0, 0), "thigh.R": (-85, 0, 0), "shin.R": (80, 0, 0),
            "upper_arm.L": (-60, 0, -60), "upper_arm.R": (-60, 0, 60), "forearm.L": (-60, 0, 0),
            "forearm.R": (-60, 0, 0)}
    a.pose(1, rot={})
    a.pose(14, rot=lift, loc={"hips": (0, 0, 0.1)})
    slam = {"spine": (25, 0, 0), "head": (10, 0, 0), "thigh.L": (-40, 0, 0), "thigh.R": (-40, 0, 0),
            "shin.L": (60, 0, 0), "shin.R": (60, 0, 0), "foot.L": (-20, 0, 0), "foot.R": (-20, 0, 0),
            "upper_arm.L": (-20, 0, -40), "upper_arm.R": (-20, 0, 40)}
    a.pose(18, rot=slam, loc={"hips": (0, 0, -0.25)})
    a.pose(36, rot={}, loc={"hips": (0, 0, 0)})
    return a.finish()


def anim_block(rig):
    a = zg.ActionBuilder(rig, "Block", 30)
    for i, f in enumerate((1, 11, 21, 30)):
        w = (-1) ** i
        a.pose(f, rot={"spine": (-5, 0, 5 * w), "head": (-10, 0, 0),
                       "upper_arm.L": (-150, 0, 42 + 8 * w), "upper_arm.R": (-150, 0, -42 - 8 * w),
                       "forearm.L": (-5, 0, 0), "forearm.R": (-5, 0, 0),
                       "thigh.L": (-20, 0, -12), "thigh.R": (-20, 0, 12), "shin.L": (35, 0, 0), "shin.R": (35, 0, 0)},
               loc={"hips": (0, 0, 0.05 * abs(w) - 0.1)})
    return a.finish()


def anim_hit(rig):
    a = zg.ActionBuilder(rig, "Hit", 24, loop=False)
    a.pose(1, rot={})
    a.pose(6, rot={"spine": (-25, 0, 10), "head": (-30, 0, -15), "upper_arm.L": (30, 0, -70),
                   "upper_arm.R": (30, 0, 70), "thigh.L": (-25, 0, 0), "shin.L": (40, 0, 0)},
           loc={"hips": (0, 0.15, 0)})
    a.pose(24, rot={})
    return a.finish()


def anim_taunt(rig):
    a = zg.ActionBuilder(rig, "Taunt", 40, loop=False)
    for i, f in enumerate((1, 10, 20, 30, 40)):
        w = (-1) ** i
        a.pose(f, rot={"spine": (-10, 0, 0), "head": (-20, 0, 15 * w),
                       "upper_arm.L": (-90, 0, -80 + 20 * w), "upper_arm.R": (-90, 0, 80 + 20 * w),
                       "forearm.L": (-90 + 30 * w, 0, 0), "forearm.R": (-90 - 30 * w, 0, 0)},
               loc={"hips": (0, 0, 0.08 * (1 + w) / 2)})
    return a.finish()


def anim_defeat(rig):
    a = zg.ActionBuilder(rig, "Defeat", 50, loop=False)
    a.pose(1, rot={})
    a.pose(15, rot={"spine": (-20, 0, 0), "head": (-30, 0, 0), "upper_arm.L": (-40, 0, -60),
                    "upper_arm.R": (-40, 0, 60)}, loc={"hips": (0, 0.1, 0)})
    sit = {"spine": (30, 0, 0), "head": (35, 0, 0), "thigh.L": (-90, 0, -20), "thigh.R": (-90, 0, 20),
           "shin.L": (20, 0, 0), "shin.R": (20, 0, 0),
           "upper_arm.L": (10, 0, -10), "upper_arm.R": (10, 0, 10), "forearm.L": (-20, 0, 0), "forearm.R": (-20, 0, 0)}
    a.pose(32, rot=sit, loc={"hips": (0, 0.1, -0.75)})
    a.pose(50, rot=sit, loc={"hips": (0, 0.1, -0.75)})
    return a.finish()


def main():
    out = zg.parse_out_dir()
    zg.reset_scene()
    rig = zg.build_armature("KingSwatRig", BONES)
    body = build_body(rig)
    zg.skin_to(body, rig)
    actions = [anim_idle(rig), anim_walk(rig), anim_shoot(rig), anim_stomp(rig), anim_block(rig),
               anim_hit(rig), anim_taunt(rig), anim_defeat(rig)]
    zg.push_actions_to_nla(rig, actions)
    rig.animation_data.action = actions[0]
    zg.export_fbx(os.path.join(out, "KingSwat.fbx"), [rig, body], animated=True)
    blend_dir = os.path.join(zg.HERE, "output")
    os.makedirs(blend_dir, exist_ok=True)
    zg.save_blend(os.path.join(blend_dir, "KingSwat.blend"))


if __name__ == "__main__":
    main()
