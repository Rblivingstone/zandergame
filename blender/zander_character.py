"""
Generates "Zander", the hero of Zander's Hoop Shrines, in a soft CHIBI-ANIME
style (big round head, huge sparkly eyes, chunky spiky hair, short rounded body
and big sneakers - think anime sticker art).

Look: spiky blonde FAUX HAWK (tall chunky locks swept up the middle, short
sides), big BLUE anime eyes with highlights, rosy cheeks and an open smile.
White basketball jersey with blue trim and a big #8, navy shorts with white
stripes, white socks and red high-tops.

Everything is smooth and organic: the body is one continuous mesh (skin
modifier + subdivision) with smooth skin weights; the clothes are regions of
that same surface, so nothing pokes through; the face is projected onto the
head. In Unity the ZanderGame/Toon shader adds cel shading and ink outlines.

Animations (one Blender action each, exported as FBX takes):
    Idle, Run, DribbleRun, Dribble, Jump, Shoot, Dunk, Celebrate, Hit

Usage (Blender 3.6+ / 4.x / 5.x):
    blender -b -P blender/zander_character.py -- --out <dir>
or with the bpy module from PyPI:
    python blender/zander_character.py --out <dir>
Writes Zander.fbx (and blender/output/Zander.blend).
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import zg_common as zg  # noqa: E402
import zg_anime as za  # noqa: E402
from mathutils import Vector  # noqa: E402

# --------------------------------------------------------------------------- #
# Palette
# --------------------------------------------------------------------------- #
SKIN = "#FBDCC2"
SKIN_SHADE = "#F0BFA0"
BLUSH = "#F7AFA0"
HAIR = "#F7D26A"         # golden blonde
HAIR_LIGHT = "#FFE9A0"   # highlight locks
HAIR_SIDE = "#E3B652"    # short faded sides
BROW = "#C08A2E"
EYE_WHITE = "#FFFFFF"
IRIS = "#2E8BF0"         # big blue eyes
IRIS_DARK = "#174C9E"
IRIS_LIGHT = "#8FD0FF"
PUPIL = "#0C1630"
LASH = "#2E2018"
MOUTH = "#9A3434"
TONGUE = "#F07C78"
JERSEY = "#FAFAFA"
TRIM = "#1F63E0"
SHORTS = "#1B2E5E"
SHORTS_TRIM = "#FFFFFF"
SHOE = "#E23B3B"
SOLE = "#FAFAFA"
SOCK = "#FFFFFF"

HEAD_C = Vector((0.0, 0.0, 1.07))
HEAD_RX, HEAD_RY, HEAD_RZ = 0.24, 0.232, 0.25


def build_body(rig):
    M = zg.material
    m = dict(
        skin=M("Skin", SKIN, 0.6), skin_shade=M("SkinShade", SKIN_SHADE, 0.6), blush=M("Blush", BLUSH, 0.7),
        hair=M("Hair", HAIR, 0.45), hair_light=M("HairLight", HAIR_LIGHT, 0.45), hair_side=M("HairSide", HAIR_SIDE, 0.55),
        brow=M("Brow", BROW, 0.6), white=M("EyeWhite", EYE_WHITE, 0.2), iris=M("Iris", IRIS, 0.15),
        iris_dark=M("IrisDark", IRIS_DARK, 0.15), iris_light=M("IrisLight", IRIS_LIGHT, 0.15),
        pupil=M("Pupil", PUPIL, 0.1), lash=M("Lash", LASH, 0.5), mouth=M("Mouth", MOUTH, 0.5),
        tongue=M("Tongue", TONGUE, 0.5), jersey=M("Jersey", JERSEY, 0.65), trim=M("JerseyTrim", TRIM, 0.6),
        shorts=M("Shorts", SHORTS, 0.7), shorts_trim=M("ShortsTrim", SHORTS_TRIM, 0.6), shoe=M("Shoe", SHOE, 0.45),
        sole=M("Sole", SOLE, 0.5), sock=M("Sock", SOCK, 0.8))
    parts = []

    # ------------------------------------------------------------ body skin --
    J, E = [], []
    RS = 1.3  # skin modifier + subdivision shrink the radii; compensate

    def j(co, r):
        J.append((co, (r[0] * RS, r[1] * RS)))
        return len(J) - 1

    pelvis = j((0, 0, 0.42), (0.098, 0.076))
    waist = j((0, 0, 0.52), (0.094, 0.072))
    chest = j((0, 0, 0.635), (0.104, 0.076))
    neck0 = j((0, 0.004, 0.735), (0.044, 0.042))
    neck1 = j((0, 0.01, 0.84), (0.04, 0.04))
    E += [(pelvis, waist), (waist, chest), (chest, neck0), (neck0, neck1)]
    for sx in (1, -1):
        sh = j((sx * 0.125, 0, 0.715), (0.044, 0.044))
        el = j((sx * 0.168, 0.004, 0.575), (0.034, 0.034))
        wr = j((sx * 0.186, 0, 0.45), (0.03, 0.028))
        palm = j((sx * 0.192, -0.004, 0.405), (0.042, 0.03))
        tip = j((sx * 0.195, -0.01, 0.355), (0.034, 0.026))
        thumb = j((sx * 0.165, -0.035, 0.415), (0.017, 0.017))
        E += [(neck0, sh), (sh, el), (el, wr), (wr, palm), (palm, tip), (palm, thumb)]
        hip = j((sx * 0.058, 0, 0.39), (0.058, 0.058))
        kn = j((sx * 0.066, 0, 0.225), (0.044, 0.044))
        calf = j((sx * 0.068, 0.006, 0.16), (0.045, 0.047))
        an = j((sx * 0.07, 0.004, 0.08), (0.036, 0.036))
        E += [(pelvis, hip), (hip, kn), (kn, calf), (calf, an)]
    body = za.skin_body("Zander_Body_Skin", J, E, m["skin"], root=pelvis)
    for k in ("jersey", "shorts", "sock"):
        body.data.materials.append(m[k])           # slots 1, 2, 3

    def region(c):
        ax = abs(c.x)
        if 0.475 <= c.z < 0.75 and ax < (0.128 if c.z < 0.67 else 0.098):
            return 1                                # tank-top jersey
        if 0.255 <= c.z < 0.475 and ax < 0.14:
            return 2                                # shorts
        if 0.075 <= c.z < 0.15:
            return 3                                # socks
        return 0
    puff = {1: 0.005, 2: 0.012, 3: 0.004}
    moved = {}
    for poly in body.data.polygons:
        r = region(poly.center)
        poly.material_index = r
        if r:
            for vi in poly.vertices:
                moved[vi] = max(moved.get(vi, 0.0), puff[r])
    for vi, d in moved.items():
        v = body.data.vertices[vi]
        extra = 0.0
        v.co += v.normal * (d + extra)
    body.data.update()
    za.auto_weights(body, rig)
    parts.append(body)

    # jersey + shorts trims and the big number 8
    pf = za.Projector(body)
    pb = za.Projector(body, from_back=True)
    vneck = [(-0.06, 0.745), (-0.03, 0.70), (0.0, 0.675), (0.03, 0.70), (0.06, 0.745)]
    parts.append(za.fix_normals(pf.strip("vneck", vneck, 0.016, m["trim"], 0.006, "chest"), pf))
    hem = [(x / 20.0 * 0.28 - 0.14, 0.485) for x in range(21)]
    parts.append(za.fix_normals(pf.strip("hem", hem, 0.014, m["trim"], 0.009, "spine"), pf))
    parts.append(za.fix_normals(pb.strip("hem_b", hem, 0.014, m["trim"], 0.009, "spine"), pb))
    for proj, nm, sc in ((pf, "eight_f", 1.0), (pb, "eight_b", 1.2)):
        for k, (cz, rx, rz) in enumerate(((0.62, 0.022, 0.019), (0.573, 0.027, 0.024))):
            ring = za.ellipse(0, 0.62 + (cz - 0.62) * sc, rx * sc, rz * sc, 20)
            parts.append(za.fix_normals(proj.strip(nm + str(k), ring + [ring[0]], 0.011 * sc, m["trim"], 0.007, "chest"),
                                        proj))

    # --------------------------------------------------------- big sneakers --
    for sx, side in ((1, "L"), (-1, "R")):
        x = sx * 0.07
        bone = "foot." + side
        shoe = zg.sphere("shoe." + side, (x, -0.035, 0.058), 1.0, scale=(0.064, 0.118, 0.055), mat=m["shoe"],
                         segments=32, rings=20)
        za.deform(shoe, lambda v: Vector((v.x, v.y, max(v.z, 0.024))))
        parts.append(za.rigid(shoe, bone))
        top = zg.cylinder("shoe_top." + side, (x, 0.004, 0.1), 0.05, 0.075, mat=m["shoe"], verts=24)
        parts.append(za.rigid(za.subdivide(top, 1), bone))
        parts.append(zg.torus("shoe_collar." + side, (x, 0.004, 0.134), 0.047, 0.011, mat=m["sole"], bone=bone))
        sole = zg.sphere("sole." + side, (x, -0.035, 0.018), 1.0, scale=(0.069, 0.125, 0.02), mat=m["sole"])
        parts.append(za.rigid(sole, bone))
        parts.append(zg.sphere("toecap." + side, (x, -0.12, 0.04), 1.0, scale=(0.048, 0.038, 0.03), mat=m["sole"], bone=bone))
        parts.append(zg.sphere("swoosh." + side, (x + sx * 0.06, -0.035, 0.065), 1.0, scale=(0.007, 0.05, 0.014),
                               rot=(-15, 0, 0), mat=m["sole"], bone=bone))
    parts.append(zg.torus("wristband", (-0.184, 0.0, 0.47), 0.035, 0.012, rot=(0, -6, 0), mat=m["trim"], bone="forearm.R"))

    # ------------------------------------------------------------------ head --
    head = za.sculpt_head("head", HEAD_C, HEAD_RX, HEAD_RY, HEAD_RZ, m["skin"], jaw=0.2, chin=0.06)
    za.rigid(head, "head")
    parts.append(head)
    for sx in (1, -1):
        parts.append(zg.sphere("ear", (sx * 0.232, 0.015, 1.0), 0.05, scale=(0.45, 0.75, 1.05), rot=(0, 0, sx * -10),
                               mat=m["skin"], bone="head"))
        parts.append(zg.sphere("ear_in", (sx * 0.245, 0.01, 1.0), 0.03, scale=(0.3, 0.6, 0.85), mat=m["skin_shade"], bone="head"))
    parts += build_face(head, m)
    parts += build_hair(head, m)

    for p in parts:
        if not p.vertex_groups:
            za.rigid(p, "head")
    # trims and numbers printed on the clothes follow the body's smooth weights
    for p in parts:
        if p.name.startswith(("vneck", "hem", "eight")):
            p.vertex_groups.clear()
            za.auto_weights(p, rig, only={"hips", "spine", "chest", "neck"})
    return zg.join(parts, "Zander_Body")


def build_face(head, m):
    pr = za.Projector(head)
    out = []

    def add(o):
        if o is not None:
            out.append(za.fix_normals(o, pr))

    ey = 1.005
    for sx in (1, -1):
        ex = sx * 0.095
        ix = ex - sx * 0.004
        # big tall anime eyes
        add(pr.shape("eye_white", za.ellipse(ex, ey, 0.058, 0.068, 40), m["white"], 0.002, "head"))
        add(pr.shape("iris", za.ellipse(ix, ey - 0.006, 0.047, 0.06, 40), m["iris"], 0.004, "head"))
        add(pr.shape("iris_band", za.ellipse(ix, ey + 0.022, 0.044, 0.03, 32), m["iris_dark"], 0.005, "head"))
        add(pr.shape("iris_glow", za.ellipse(ix, ey - 0.036, 0.03, 0.018, 24), m["iris_light"], 0.005, "head"))
        add(pr.shape("pupil", za.ellipse(ix, ey - 0.004, 0.022, 0.03, 24), m["pupil"], 0.006, "head"))
        add(pr.shape("shine_big", za.ellipse(ix + sx * 0.018, ey + 0.022, 0.016, 0.019, 20), m["white"], 0.0075, "head"))
        add(pr.shape("shine_small", za.ellipse(ix - sx * 0.02, ey - 0.03, 0.008, 0.008, 14), m["white"], 0.0075, "head"))
        # thick upper lash line + outer flick, lower lash hint
        a0, a1 = (math.radians(165), math.radians(12)) if sx > 0 else (math.radians(15), math.radians(168))
        lash = za.arc(ex, ey + 0.004, 0.062, 0.07, a0, a1, 16)
        widths = [0.006 + 0.008 * math.sin(math.pi * i / 15) for i in range(16)]
        if sx > 0:
            lash.append((ex + 0.07, ey + 0.03))
            widths.append(0.003)
        else:
            lash.insert(0, (ex - 0.07, ey + 0.03))
            widths.insert(0, 0.003)
        add(pr.strip("lash_upper", lash, widths, m["lash"], 0.007, "head"))
        lo = za.arc(ex, ey - 0.004, 0.052, 0.066, math.radians(-35 if sx > 0 else -145),
                    math.radians(-70 if sx > 0 else -110), 6)
        add(pr.strip("lash_lower", lo, 0.004, m["lash"], 0.0035, "head"))
        # short thick brows (mostly peeking out under the hair)
        brow = [(ex - sx * 0.035, 1.112), (ex, 1.12), (ex + sx * 0.035, 1.113)]
        add(pr.strip("brow", brow, [0.012, 0.014, 0.008], m["brow"], 0.003, "head"))
        add(pr.shape("blush", za.ellipse(sx * 0.15, 0.935, 0.032, 0.014, 20), m["blush"], 0.0018, "head"))
    # tiny nose
    add(pr.shape("nose", za.ellipse(0, 0.952, 0.006, 0.005, 10), m["skin_shade"], 0.002, "head"))
    # open happy smile (D shape) with a little tongue
    mouth = [(-0.034, 0.912)] + za.arc(0, 0.912, 0.034, 0.03, math.radians(185), math.radians(355), 14) + [(0.034, 0.912)]
    add(pr.shape("mouth", mouth, m["mouth"], 0.002, "head"))
    add(pr.shape("tongue", za.ellipse(0.004, 0.892, 0.017, 0.009, 16), m["tongue"], 0.003, "head"))
    return out


def build_hair(head, m):
    c = HEAD_C
    out = []

    def nd(co):
        d = co - c
        return Vector((d.x / HEAD_RX, d.y / HEAD_RY, d.z / HEAD_RZ))

    def shell(name, scale, keep, mat):
        o = head.copy()
        o.data = head.data.copy()
        o.name = name
        zg.link(o)
        o.vertex_groups.clear()
        za.deform(o, lambda v: c + (v - c) * scale)
        # tuck vertices outside the hairline just under the skin instead of deleting
        # them, so the hairline stays a smooth curve rather than jagged steps
        for v in o.data.vertices:
            if not keep(nd(c + (v.co - c) / scale)):
                v.co = c + (v.co - c) / scale * 0.985
        o.data.update()
        o.data.materials.clear()
        o.data.materials.append(mat)
        return za.rigid(o, "head")

    def cap_keep(d):
        front = max(0.0, -d.y)
        back = max(0.0, d.y)
        limit = 0.05 + 0.33 * front - 0.6 * back
        if abs(d.x) > 0.72 and -0.5 < d.z < 0.15 and d.y < 0.4:
            return False   # ears
        return d.z > limit

    out.append(shell("hair_cap", 1.04, cap_keep, m["hair_side"]))
    out.append(shell("hair_volume", 1.1, lambda d: d.z > 0.3 - 0.25 * max(0.0, d.y) and abs(d.x) < 0.78 and d.y > -0.75,
                     m["hair"]))

    def on_skull(a, x_off, lift=1.06):
        d = Vector((x_off, -math.sin(a), math.cos(a)))
        d.normalize()
        return c + Vector((d.x * HEAD_RX, d.y * HEAD_RY, d.z * HEAD_RZ)) * lift, d

    def lock(base, nrm, sweep, L, w, t, mat, curl=0.0, inward=0.0, x_off=0.0):
        p0 = base - nrm * 0.03
        p1 = base + (nrm * 0.7 + sweep * 0.3).normalized() * L * 0.5 + Vector((-x_off * inward * L * 0.4, 0, 0))
        p2 = base + (nrm * 0.3 + sweep).normalized() * L + Vector((-x_off * inward * L, 0, curl * L))
        out.append(za.hair_lock("lock", p0, p1, p2, w, t, mat, flat_axis=(0, 1, 0), segs=8, ring=8, tip_power=1.1))

    # 1) the faux-hawk ridge: tall chunky spikes, the front one flicks up + forward
    ridge = [(1.02, 0.14, (0, -1.0, 0.55)), (0.8, 0.17, (0, -0.6, 0.8)), (0.55, 0.18, (0, -0.15, 1.0)),
             (0.28, 0.17, (0, 0.3, 1.0)), (0.0, 0.16, (0, 0.6, 0.85)), (-0.3, 0.14, (0, 0.85, 0.55)),
             (-0.62, 0.12, (0, 0.95, 0.2)), (-0.95, 0.1, (0, 0.8, -0.35))]
    for i, (a, L, sw) in enumerate(ridge):
        base, nrm = on_skull(a, 0.0)
        lock(base, nrm, Vector(sw).normalized(), L, 0.125, 0.06, m["hair_light"] if i % 2 == 0 else m["hair"])
    # 2) inner rows lean in toward the ridge (that's what makes it a faux hawk)
    for sx in (1, -1):
        for a, L in ((0.95, 0.12), (0.7, 0.14), (0.45, 0.14), (0.2, 0.14), (-0.05, 0.13), (-0.3, 0.12),
                     (-0.55, 0.11), (-0.8, 0.1), (-1.05, 0.08)):
            x_off = sx * 0.4
            base, nrm = on_skull(a, x_off)
            sw = Vector((0, -0.5 if a > 0.6 else 0.7, 0.9)).normalized()
            lock(base, nrm, sw, L, 0.1, 0.045, m["hair"], inward=1.0, x_off=x_off)
    # 4) a little fringe over the forehead
    for x_off, sw in ((-0.35, (0.25, -1.0, -0.5)), (0.3, (-0.2, -1.0, -0.6)), (0.0, (0.05, -1.0, -0.35))):
        base, nrm = on_skull(1.12, x_off, 1.05)
        lock(base, nrm, Vector(sw).normalized(), 0.09, 0.07, 0.025, m["hair"])
    return out


BONES = [
    ("root", (0, 0, 0), (0, 0, 0.1), None),
    ("hips", (0, 0, 0.4), (0, 0, 0.5), "root"),
    ("spine", (0, 0, 0.5), (0, 0, 0.62), "hips"),
    ("chest", (0, 0, 0.62), (0, 0, 0.74), "spine"),
    ("neck", (0, 0, 0.74), (0, 0, 0.82), "chest"),
    ("head", (0, 0, 0.82), (0, 0, 1.32), "neck"),
]
for _sx, _n in ((1, "L"), (-1, "R")):
    BONES += [
        ("upper_arm." + _n, (_sx * 0.125, 0, 0.715), (_sx * 0.168, 0.004, 0.575), "chest"),
        ("forearm." + _n, (_sx * 0.168, 0.004, 0.575), (_sx * 0.186, 0, 0.45), "upper_arm." + _n),
        ("hand." + _n, (_sx * 0.186, 0, 0.45), (_sx * 0.195, -0.01, 0.355), "forearm." + _n),
        ("thigh." + _n, (_sx * 0.058, 0, 0.4), (_sx * 0.066, 0, 0.225), "hips"),
        ("shin." + _n, (_sx * 0.066, 0, 0.225), (_sx * 0.07, 0.004, 0.08), "thigh." + _n),
        ("foot." + _n, (_sx * 0.07, 0.004, 0.08), (_sx * 0.07, -0.13, 0.03), "shin." + _n),
    ]


# --------------------------------------------------------------------------- #
# Animations
#   down-pointing bones: forward swing = -X, outward: L = -Z, R = +Z
#   up-pointing bones:   lean forward = +X
#   shin knee bend = +X, forearm bend (hand forward) = -X
# --------------------------------------------------------------------------- #
def arms_rest(spread=8):
    return {"upper_arm.L": (0, 0, -spread), "upper_arm.R": (0, 0, spread)}


def anim_idle(rig):
    a = zg.ActionBuilder(rig, "Idle", 60, loop=True)
    for f, t in ((1, 0.0), (16, 0.5), (31, 1.0), (46, 0.5), (60, 0.0)):
        r = arms_rest(8 + 3 * t)
        r.update({
            "spine": (2 * t, 0, 0), "head": (-3 * t, 2 * math.sin(t * math.pi), 0),
            "forearm.L": (-10 - 4 * t, 0, 0), "forearm.R": (-10 - 4 * t, 0, 0),
        })
        a.pose(f, rot=r, loc={"hips": (0, 0, -0.008 * t)})
    return a.finish()


def _run_pose(phase, with_ball=False):
    s = math.sin(phase)
    c = math.cos(phase)
    r = {
        "hips": (0, 0, 6 * s),
        "spine": (12, 0, -5 * s),
        "head": (-8, 0, 3 * s),
        "thigh.L": (-45 * s, 0, 0), "thigh.R": (45 * s, 0, 0),
        "shin.L": (35 + 35 * max(0, -c) * 1.4, 0, 0), "shin.R": (35 + 35 * max(0, c) * 1.4, 0, 0),
        "foot.L": (-10 * s, 0, 0), "foot.R": (10 * s, 0, 0),
        "upper_arm.L": (50 * s, 0, -10), "forearm.L": (-70, 0, 0),
    }
    if with_ball:
        bounce = abs(math.cos(phase))
        r.update({"upper_arm.R": (-35 + 15 * bounce, 0, 18), "forearm.R": (-40 - 25 * bounce, 0, 0),
                  "hand.R": (-25 * bounce, 0, 0)})
    else:
        r.update({"upper_arm.R": (-50 * s, 0, 10), "forearm.R": (-70, 0, 0)})
    loc = {"hips": (0, 0, 0.03 * abs(c) - 0.02)}
    return r, loc


def anim_run(rig, name="Run", with_ball=False):
    a = zg.ActionBuilder(rig, name, 21, loop=True)
    for i in range(0, 21, 2):
        phase = (i / 20.0) * 2 * math.pi
        r, loc = _run_pose(phase, with_ball)
        a.pose(i + 1, rot=r, loc=loc)
    return a.finish()


def anim_dribble(rig):
    # 30 frames = two dribbles (ball bounce every 0.5 s in the game code)
    a = zg.ActionBuilder(rig, "Dribble", 31, loop=True)
    for i in range(0, 31, 3):
        ph = (i / 30.0) * 4 * math.pi
        bounce = abs(math.cos(ph / 2))      # 1 = hand high, 0 = pushing down
        r = {
            "hips": (0, 0, 0), "spine": (14, 0, 0), "head": (-12, 0, 0),
            "thigh.L": (-25, 0, -4), "thigh.R": (-20, 0, 4),
            "shin.L": (40, 0, 0), "shin.R": (35, 0, 0),
            "foot.L": (-12, 0, 0), "foot.R": (-12, 0, 0),
            "upper_arm.L": (-25, 0, -25), "forearm.L": (-45, 0, 0),
            "upper_arm.R": (-30 + 12 * bounce, 0, 20), "forearm.R": (-35 - 30 * bounce, 0, 0),
            "hand.R": (-30 * bounce, 0, 0),
        }
        a.pose(i + 1, rot=r, loc={"hips": (0, 0, -0.05 - 0.01 * bounce)})
    return a.finish()


def anim_jump(rig):
    a = zg.ActionBuilder(rig, "Jump", 20, loop=False)
    crouch = {"spine": (15, 0, 0), "thigh.L": (-40, 0, 0), "thigh.R": (-40, 0, 0),
              "shin.L": (70, 0, 0), "shin.R": (70, 0, 0), "foot.L": (-25, 0, 0), "foot.R": (-25, 0, 0),
              "upper_arm.L": (30, 0, -10), "upper_arm.R": (30, 0, 10)}
    a.pose(1, rot=crouch, loc={"hips": (0, 0, -0.08)})
    air = {"spine": (-5, 0, 0), "head": (-10, 0, 0),
           "thigh.L": (-55, 0, 0), "thigh.R": (-20, 0, 0), "shin.L": (90, 0, 0), "shin.R": (40, 0, 0),
           "upper_arm.L": (-150, 0, -25), "upper_arm.R": (-150, 0, 25),
           "forearm.L": (-20, 0, 0), "forearm.R": (-20, 0, 0)}
    a.pose(6, rot=air)
    a.pose(20, rot=air)
    return a.finish()


def anim_shoot(rig):
    a = zg.ActionBuilder(rig, "Shoot", 24, loop=False)
    set_ = {"spine": (8, 0, 0), "thigh.L": (-35, 0, 0), "thigh.R": (-35, 0, 0),
            "shin.L": (60, 0, 0), "shin.R": (60, 0, 0), "foot.L": (-20, 0, 0), "foot.R": (-20, 0, 0),
            "upper_arm.R": (-110, 0, 15), "forearm.R": (-100, 0, 0), "hand.R": (40, 0, 0),
            "upper_arm.L": (-100, 0, -30), "forearm.L": (-90, 0, 0)}
    a.pose(1, rot=set_, loc={"hips": (0, 0, -0.07)})
    rise = {"spine": (-4, 0, 0), "head": (-15, 0, 0),
            "upper_arm.R": (-165, 0, 8), "forearm.R": (-35, 0, 0), "hand.R": (20, 0, 0),
            "upper_arm.L": (-140, 0, -20), "forearm.L": (-50, 0, 0)}
    a.pose(8, rot=rise, loc={"hips": (0, 0, 0.03)})
    follow = dict(rise)
    follow.update({"forearm.R": (-10, 0, 0), "hand.R": (-60, 0, 0), "head": (-18, 0, 0)})
    a.pose(13, rot=follow, loc={"hips": (0, 0, 0.04)})
    a.pose(24, rot=follow, loc={"hips": (0, 0, 0.0)})
    return a.finish()


def anim_dunk(rig):
    a = zg.ActionBuilder(rig, "Dunk", 24, loop=False)
    up = {"spine": (-12, 0, 0), "head": (-20, 0, 0),
          "upper_arm.L": (-175, 0, -5), "upper_arm.R": (-175, 0, 5),
          "thigh.L": (-60, 0, 0), "thigh.R": (-30, 0, 0), "shin.L": (95, 0, 0), "shin.R": (60, 0, 0)}
    a.pose(1, rot=up)
    slam = {"spine": (25, 0, 0), "head": (5, 0, 0),
            "upper_arm.L": (-120, 0, -10), "upper_arm.R": (-120, 0, 10),
            "forearm.L": (-30, 0, 0), "forearm.R": (-30, 0, 0), "hand.L": (-40, 0, 0), "hand.R": (-40, 0, 0),
            "thigh.L": (-20, 0, 0), "thigh.R": (-20, 0, 0), "shin.L": (40, 0, 0), "shin.R": (40, 0, 0)}
    a.pose(7, rot=slam)
    hang = dict(slam)
    hang.update({"upper_arm.L": (-170, 0, -8), "upper_arm.R": (-170, 0, 8), "spine": (5, 0, 0),
                 "forearm.L": (0, 0, 0), "forearm.R": (0, 0, 0)})
    a.pose(14, rot=hang)
    a.pose(24, rot=hang)
    return a.finish()


def anim_celebrate(rig):
    a = zg.ActionBuilder(rig, "Celebrate", 31, loop=True)
    for i in range(0, 31, 3):
        t = i / 30.0
        hop = abs(math.sin(t * 2 * math.pi))
        pump = math.sin(t * 4 * math.pi)
        r = {"spine": (-6, 0, 0), "head": (-18, 0, 5 * pump),
             "upper_arm.L": (-160 - 10 * pump, 0, -25), "forearm.L": (-30 + 20 * pump, 0, 0),
             "upper_arm.R": (-160 + 10 * pump, 0, 25), "forearm.R": (-30 - 20 * pump, 0, 0),
             "thigh.L": (-15 * hop, 0, -6), "thigh.R": (-15 * hop, 0, 6),
             "shin.L": (30 * hop, 0, 0), "shin.R": (30 * hop, 0, 0)}
        a.pose(i + 1, rot=r, loc={"hips": (0, 0, 0.09 * hop)})
    return a.finish()


def anim_hit(rig):
    a = zg.ActionBuilder(rig, "Hit", 18, loop=False)
    a.pose(1, rot=arms_rest())
    knocked = {"spine": (-22, 0, 0), "head": (-25, 0, 0),
               "upper_arm.L": (40, 0, -55), "upper_arm.R": (40, 0, 55),
               "forearm.L": (-30, 0, 0), "forearm.R": (-30, 0, 0),
               "thigh.L": (-30, 0, 0), "shin.L": (40, 0, 0)}
    a.pose(5, rot=knocked, loc={"hips": (0, 0.05, -0.03)})
    a.pose(18, rot=arms_rest(), loc={"hips": (0, 0, 0)})
    return a.finish()


def main():
    out = zg.parse_out_dir()
    zg.reset_scene()
    rig = zg.build_armature("ZanderRig", BONES)
    body = build_body(rig)
    zg.skin_to(body, rig)

    actions = [
        anim_idle(rig), anim_run(rig), anim_run(rig, "DribbleRun", with_ball=True),
        anim_dribble(rig), anim_jump(rig), anim_shoot(rig), anim_dunk(rig),
        anim_celebrate(rig), anim_hit(rig),
    ]
    zg.push_actions_to_nla(rig, actions)
    rig.animation_data.action = actions[0]

    zg.export_fbx(os.path.join(out, "Zander.fbx"), [rig, body], animated=True)
    blend_dir = os.path.join(zg.HERE, "output")
    os.makedirs(blend_dir, exist_ok=True)
    zg.save_blend(os.path.join(blend_dir, "Zander.blend"))


if __name__ == "__main__":
    main()
