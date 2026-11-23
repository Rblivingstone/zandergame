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
import zg_paint as zp  # noqa: E402
import zg_bake as zb  # noqa: E402
import numpy as np  # noqa: E402
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


def smoothstep(e0, e1, x):
    t = min(1.0, max(0.0, (x - e0) / (e1 - e0)))
    return t * t * (3 - 2 * t)


_DECALS = None


def paint_body_3d(P, N, W):
    """Colour of Zander's body at 3D points P (normals N, arm weights W):
    skin, a white tank top with blue trims, navy shorts with stripes, socks."""
    global _DECALS
    if _DECALS is None:
        _DECALS = zp.zander_decals()
    hx = zb.hexrgb
    x, y, z = P[:, 0], P[:, 1], P[:, 2]
    ax = np.abs(x)
    # 1 = arm/hand. Strict: the torso skin near the armpit is partly bound to the
    # arm bones too, and must still get the jersey.
    # An arm surface either faces away from the body side (front/back/inner side
    # of the arm) or sits well outside the torso; the torso wall under the
    # armpit faces outward and is covered, so the jersey tucks under the arm.
    armw = sum(W.values()) if W else np.zeros(len(P))
    out_x = N[:, 0] * np.sign(P[:, 0])
    is_arm_dir = np.maximum(np.clip((0.45 - out_x) * 4, 0, 1), np.clip((np.abs(P[:, 0]) - 0.15) * 40, 0, 1))
    arm = np.clip((armw - 0.45) * 5, 0, 1) * is_arm_dir
    body = 1 - arm
    soft = zb.soft
    col = np.tile(hx(SKIN), (len(P), 1))

    # ---- shorts: hips + thighs between hem and waist ----
    shorts = soft(z - 0.262) * soft(0.476 - z) * body
    col = zb.lay(col, shorts, hx("#1B2E5E"))
    col = zb.lay(col, shorts * soft(z - 0.455), hx("#24387A"))                       # waistband
    col = zb.lay(col, soft(z - 0.262) * soft(0.278 - z) * body, hx("#2A4590"))      # hem band
    outward = N[:, 0] * np.sign(x)                                                    # outer thigh
    col = zb.lay(col, shorts * soft(outward - 0.86, 0.03) * soft(0.455 - z), hx("#FAFAFA"))
    col = zb.lay(col, shorts * soft(outward - 0.95, 0.02) * soft(0.455 - z), hx("#1F63E0"))

    # ---- tank top ----
    zs = [0.64, 0.655, 0.685, 0.725, 0.77, 0.82]
    xs = [0.3, 0.152, 0.118, 0.092, 0.078, 0.078]
    x_arm = np.interp(z, zs, xs)                     # arm-hole edge (same front & back)
    v_edge = 0.034 * np.clip((z - 0.66) / 0.108, 0, 2)   # front V-neck half width
    d_arm = x_arm - ax                               # > 0 inside the jersey
    d_vneck = np.where(y < 0, ax - v_edge, 1.0)      # > 0 outside the V
    d_vneck = np.where((y < 0) & (z < 0.66), 1.0, d_vneck)
    scoop = (x / 0.034) ** 2 + ((z - 0.77) / 0.055) ** 2
    d_scoop = np.where(y >= 0, (np.sqrt(scoop) - 1) * 0.034, 1.0)
    d_neck = np.minimum(d_vneck, d_scoop)
    d_neck = np.minimum(d_neck, np.where(z > 0.766, ax - 0.034, 1.0))   # straps leave the neck bare
    jersey = soft(z - 0.476) * soft(0.81 - z) * soft(d_arm) * soft(d_neck) * body
    col = zb.lay(col, jersey, hx("#FAFAFA"))
    trim = jersey * np.maximum(soft(0.012 - d_arm) * soft(z - 0.645), soft(0.012 - d_neck))
    col = zb.lay(col, trim, hx("#1F63E0"))
    col = zb.lay(col, jersey * soft(0.5 - z), hx("#1F63E0"))                        # hem band

    # ---- printed decals (8, name, folds, drawstrings) on front / back ----
    front_d, back_d = _DECALS
    facing_f = soft(-N[:, 1] - 0.3, 0.1) * (jersey + shorts).clip(0, 1)
    facing_b = soft(N[:, 1] - 0.3, 0.1) * (jersey + shorts).clip(0, 1)
    for dec, face, mirror in ((front_d, facing_f, False), (back_d, facing_b, True)):
        s = zb.sample_decal(dec, -x if mirror else x, z, zp.DECAL_X0, zp.DECAL_X1, zp.DECAL_Z0, zp.DECAL_Z1)
        a = s[:, 3] * face
        col = col * (1 - a[:, None]) + s[:, :3] * a[:, None]

    # ---- socks ----
    sock = soft(z - 0.075) * soft(0.15 - z) * body
    col = zb.lay(col, sock, hx("#FFFFFF"))
    col = zb.lay(col, sock * soft(z - 0.128) * soft(0.138 - z), hx("#FFC83D"))
    return col


def face_uv(co):
    """Front planar projection for the painted face; the back of the head maps
    to plain skin at the texture's edge so the face never appears twice."""
    u = 0.5 + co.x / zp.FACE_U_M
    v = 0.5 + (co.z - zp.FACE_CZ) / zp.FACE_V_M
    if co.y > 0:
        u = 0.5 + math.copysign(0.49, co.x)
    return (min(max(u, 0.01), 0.99), min(max(v, 0.01), 0.99))


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
    # hand-painted textures (eyes, mouth, blush, hair strands, jersey art)
    m["face"] = za.tex_material("Face", zp.paint_zander_face(SKIN))
    parts = []

    # ------------------------------------------------------------ body skin --
    # One continuous smooth body. Skin AND clothes are painted onto it in 3D
    # (see paint_body_3d), so the tank top wraps around the torso, under the
    # arms and over the shoulders with clean drawn edges.
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
    body = za.skin_body("Zander_Body_Skin", J, E, m["skin"], root=pelvis, subdiv=3)
    za.auto_weights(body, rig)
    zb.unwrap(body)
    arm_groups = [g.name for g in body.vertex_groups if "arm" in g.name or "hand" in g.name]
    tex = zb.bake(body, 2048, paint_body_3d, "zander_body.png", groups=arm_groups)
    body.data.materials.clear()
    body.data.materials.append(za.tex_material("Body", tex, 0.65))
    # puff the clothes out a touch (smoothly, so there are no steps)
    gidx = {g.index: g.name for g in body.vertex_groups}
    for v in body.data.vertices:
        armw = sum(g.weight for g in v.groups if gidx[g.group] in arm_groups)
        if armw > 0.5:
            continue
        z = v.co.z
        d = 0.011 * smoothstep(0.255, 0.3, z) * (1 - smoothstep(0.47, 0.5, z))
        d += 0.005 * smoothstep(0.47, 0.5, z) * (1 - smoothstep(0.66, 0.74, z))
        v.co += v.normal * d
    body.data.update()
    parts.append(body)

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
    parts.append(zg.torus("wristband", (0.184, 0.0, 0.47), 0.035, 0.012, rot=(0, 6, 0), mat=m["trim"], bone="forearm.L"))

    # ------------------------------------------------------------------ head --
    head = za.sculpt_head("head", HEAD_C, HEAD_RX, HEAD_RY, HEAD_RZ, m["face"], jaw=0.2, chin=0.06)
    za.rigid(head, "head")
    # a small, soft chibi nose bump (plus a hint of a bridge) right under the painted nose
    for v in head.data.vertices:
        if v.co.y >= 0:
            continue
        d2 = (v.co.x / 0.019) ** 2 + ((v.co.z - 0.958) / 0.022) ** 2
        bridge = (v.co.x / 0.012) ** 2 + ((v.co.z - 0.985) / 0.03) ** 2
        v.co.y -= 0.03 * math.exp(-d2) + 0.009 * math.exp(-bridge)
    head.data.update()
    za.set_uvs(head, face_uv)
    parts.append(head)
    for sx in (1, -1):
        parts.append(zg.sphere("ear", (sx * 0.232, 0.015, 1.0), 0.05, scale=(0.45, 0.75, 1.05), rot=(0, 0, sx * -10),
                               mat=m["skin"], bone="head"))
        parts.append(zg.sphere("ear_in", (sx * 0.245, 0.01, 1.0), 0.03, scale=(0.3, 0.6, 0.85), mat=m["skin_shade"], bone="head"))
    parts += build_hair(head, m)

    for p in parts:
        if not p.vertex_groups:
            za.rigid(p, "head")
    return zg.join(parts, "Zander_Body")


def paint_hair_3d(P, N, W):
    """Hair colour painted across the whole hairdo in 3D: darker roots and
    undersides, light tips, and one anime 'halo' highlight band around the head."""
    hx = zb.hexrgb
    c = np.array(HEAD_C)
    rel = P - c
    r = np.linalg.norm(rel / np.array([HEAD_RX, HEAD_RY, HEAD_RZ]), axis=1)
    out = np.clip((r - 1.05) / 0.55, 0, 1)                      # 0 at the scalp, 1 at the tips
    col = hx("#E2B04A")[None, :] * (1 - out[:, None]) + hx("#F9DA78")[None, :] * out[:, None]
    tip = np.clip((out - 0.55) / 0.45, 0, 1)[:, None]
    col = col * (1 - tip) + hx("#FFEDB0")[None, :] * tip
    # undersides in shadow
    under = np.clip(-N[:, 2] * 1.6, 0, 1)[:, None] * 0.35
    col = col * (1 - under) + hx("#C99434")[None, :] * under
    # halo highlight: a zig-zag band at a fixed elevation around the head
    elev = np.degrees(np.arcsin(np.clip(rel[:, 2] / np.maximum(np.linalg.norm(rel, axis=1), 1e-6), -1, 1)))
    azim = np.arctan2(rel[:, 0], -rel[:, 1])
    centre = 40 + 1.8 * np.sin(azim * 11)
    band = zb.soft(2.2 - np.abs(elev - centre), 1.6) * (0.55 + 0.45 * np.cos(azim * 3) ** 2)
    facing = np.clip((N * (rel / np.maximum(np.linalg.norm(rel, axis=1, keepdims=True), 1e-6))).sum(1) * 2 - 0.4, 0, 1)
    halo = band * facing * 0.7
    col = col * (1 - halo[:, None]) + hx("#FFF8DC")[None, :] * halo[:, None]
    return col


def build_hair(head, m):
    """Chibi-anime faux hawk built from ~90 small, flat locks packed in layers
    over a soft hair mass, all flowing up toward the ridge and back. The whole
    hairdo is then painted in 3D as one piece (gradient + halo highlight)."""
    import random
    rnd = random.Random(8)
    c = HEAD_C
    parts = []

    def nd(co):
        d = co - c
        return Vector((d.x / HEAD_RX, d.y / HEAD_RY, d.z / HEAD_RZ))

    def hair_g(d):
        """> 0 where there is hair. The sides come right down and wrap around
        the ears (sideburns in front, hair behind), the forehead stays clear."""
        front = max(0.0, -d.y)
        back = max(0.0, d.y)
        side = min(1.0, max(0.0, (abs(d.x) - 0.45) / 0.45))
        g = d.z - (0.02 + 0.36 * front * (1 - side * 0.75) - 0.62 * back - 0.5 * side)
        if abs(d.x) > 0.55:                      # cut-out around the ear
            e = ((d.y - 0.06) / 0.3) ** 2 + ((d.z + 0.28) / 0.33) ** 2
            g = min(g, (e - 1.0) * 0.25)
        return g

    def hairline(d):
        return hair_g(d) > 0

    # the soft base mass
    o = head.copy()
    o.data = head.data.copy()
    o.name = "hair_mass"
    zg.link(o)
    o.vertex_groups.clear()
    za.subdivide(o, 2)          # dense, so the hairline and the ear cut-out are smooth curves
    def hair_amount(d):
        t = min(1.0, max(0.0, (hair_g(d) + 0.03) / 0.12))
        return t * t * (3 - 2 * t)
    for v in o.data.vertices:
        k = 0.985 + (1.1 - 0.985) * hair_amount(nd(v.co))   # smooth hairline (no steps)
        v.co = c + (v.co - c) * k + Vector((0, 0, 0.012 * (k - 0.985) / 0.115))
    o.data.update()
    parts.append(o)

    def scalp(a, x_off):
        d = Vector((x_off, -math.sin(a), math.cos(a)))
        d.normalize()
        return d

    def flow(a, x_off):
        """Faux-hawk flow: top locks rise toward the centre ridge and back,
        side locks sweep back, front locks flick up."""
        ax = abs(x_off)
        sgn = 1 if x_off >= 0 else -1
        up = max(0.0, 1.0 - ax * 1.1)
        if a > 0.85:                                   # front row: up and a little forward
            return Vector((-sgn * 0.25 * ax, -0.35, 1.0)).normalized()
        return Vector((-sgn * 0.45 * up + sgn * 0.12 * (1 - up), 0.55 + 0.4 * (1 - up), up * 1.0 - 0.35 * (1 - up))).normalized()

    rows = 11
    for ri in range(rows):
        a = 1.0 - ri * (2.25 / (rows - 1))             # front (1.0) -> back (-1.25)
        n_cols = 9
        for ci in range(n_cols):
            x_off = -0.95 + ci * (1.9 / (n_cols - 1)) + (0.1 if ri % 2 else 0.0)
            if abs(x_off) > 1.0:
                continue
            d = scalp(a, x_off)
            base = c + Vector((d.x * HEAD_RX, d.y * HEAD_RY, d.z * HEAD_RZ)) * 1.07
            if not hairline(nd(base)):
                continue
            nrm = d
            sw = flow(a, x_off)
            centre = max(0.0, 1.0 - abs(x_off) * 1.15)
            L = (0.055 + 0.105 * centre) * rnd.uniform(0.8, 1.2)
            if a < -0.6:
                L *= 0.8
            w = 0.07 * rnd.uniform(0.85, 1.15)
            p0 = base - nrm * 0.03
            p1 = base + (nrm * 0.55 + sw * 0.45).normalized() * L * 0.5
            p2 = base + (nrm * 0.15 + sw).normalized() * L + Vector((0, 0, 0.25 * L * centre))
            side = sw.cross(nrm)
            if side.length < 1e-3:
                side = Vector((1, 0, 0))
            parts.append(za.hair_lock("lock", p0, p1, p2, w, w * 0.22, m["hair"], flat_axis=tuple(side.normalized()),
                                      segs=7, ring=8, tip_power=1.5))
    # short side locks: sideburns in front of the ear, layers above and behind it
    for sx in (1, -1):
        for a, lat in ((0.75, 1.15), (0.42, 1.15), (0.1, 1.15), (-0.22, 1.15), (-0.55, 1.15), (-0.85, 1.15),
                       (0.55, 2.0), (0.3, 2.4), (-0.35, 2.2), (-0.6, 1.9), (-0.85, 1.6), (-1.05, 1.3),
                       (0.45, 3.2), (-0.55, 3.0), (-0.8, 2.6)):
            d = scalp(a, sx * lat)
            base = c + Vector((d.x * HEAD_RX, d.y * HEAD_RY, d.z * HEAD_RZ)) * 1.06
            if not hairline(nd(base)):
                continue
            sw = Vector((sx * 0.25, 0.6, -0.75)).normalized()
            L = 0.075 * rnd.uniform(0.85, 1.15)
            p0 = base - d * 0.03
            p1 = base + (d * 0.6 + sw * 0.4).normalized() * L * 0.5
            p2 = base + (d * 0.2 + sw).normalized() * L
            parts.append(za.hair_lock("side", p0, p1, p2, 0.07, 0.016, m["hair"], flat_axis=(0, 1, 0), segs=6, ring=8,
                                      tip_power=1.5))
    # bangs over the forehead
    for x_off, sx in ((-0.42, 0.3), (-0.14, 0.12), (0.16, -0.1), (0.44, -0.3)):
        d = scalp(1.08, x_off)
        base = c + Vector((d.x * HEAD_RX, d.y * HEAD_RY, d.z * HEAD_RZ)) * 1.06
        sw = Vector((sx, -0.55, -0.8)).normalized()
        L = 0.1
        p0 = base - d * 0.03
        p1 = base + (d * 0.6 + sw * 0.4).normalized() * L * 0.5
        p2 = base + sw * L + Vector((0, -0.01, 0.015))
        parts.append(za.hair_lock("bang", p0, p1, p2, 0.08, 0.018, m["hair"], flat_axis=(1, 0, 0), segs=7, ring=8,
                                  tip_power=1.5))
    hair = zg.join(parts, "Hair")
    hair.data.materials.clear()
    zb.unwrap(hair, margin=0.003)
    hair.data.materials.append(za.tex_material("HairPainted", zb.bake(hair, 1024, paint_hair_3d, "zander_hair.png"), 0.45))
    for p in hair.data.polygons:
        p.material_index = 0
    hair.vertex_groups.clear()
    return [za.rigid(hair, "head")]


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
def lefty(rot):
    """Zander is LEFT-handed: mirror a right-handed pose (swap .L/.R bones and
    flip the Y/Z rotations)."""
    out = {}
    for k, (rx, ry, rz) in rot.items():
        k2 = k[:-2] + (".L" if k.endswith(".R") else ".R") if k.endswith((".L", ".R")) else k
        out[k2] = (rx, -ry, -rz)
    return out


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
    return (lefty(r) if with_ball else r), loc


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
        a.pose(i + 1, rot=lefty(r), loc={"hips": (0, 0, -0.05 - 0.01 * bounce)})
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
    a.pose(1, rot=lefty(set_), loc={"hips": (0, 0, -0.07)})
    rise = {"spine": (-4, 0, 0), "head": (-15, 0, 0),
            "upper_arm.R": (-165, 0, 8), "forearm.R": (-35, 0, 0), "hand.R": (20, 0, 0),
            "upper_arm.L": (-140, 0, -20), "forearm.L": (-50, 0, 0)}
    a.pose(8, rot=lefty(rise), loc={"hips": (0, 0, 0.03)})
    follow = dict(rise)
    follow.update({"forearm.R": (-10, 0, 0), "hand.R": (-60, 0, 0), "head": (-18, 0, 0)})
    a.pose(13, rot=lefty(follow), loc={"hips": (0, 0, 0.04)})
    a.pose(24, rot=lefty(follow), loc={"hips": (0, 0, 0.0)})
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
