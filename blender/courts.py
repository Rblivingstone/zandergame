"""
Paints the basketball courts and the shrine stone in the same hand-painted
anime style as the characters and props, and writes them into the Unity
project (Assets/ZanderGame/Resources/Textures). The game loads these; if they
are missing it falls back to simpler textures generated in code.

    python blender/courts.py            (needs: pip install skia-python numpy)

Court layouts must match LevelBuilder.cs (sizes, hoop ends, three-point radius).
"""

import math
import os
import random

import skia

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "..", "unity", "ZanderHoopShrines", "Assets", "ZanderGame", "Resources", "Textures")
PX_PER_M = 40


def C(h, a=255):
    h = h.lstrip("#")
    return skia.ColorSetARGB(a, int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16))


def mix(h1, h2, t):
    a = [int(h1[i:i + 2], 16) for i in (1, 3, 5)]
    b = [int(h2[i:i + 2], 16) for i in (1, 3, 5)]
    return "#%02X%02X%02X" % tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))


def shade(h, k):
    v = [min(255, max(0, int(int(h[i:i + 2], 16) * k))) for i in (1, 3, 5)]
    return "#%02X%02X%02X" % tuple(v)


def fill(c, shader=None):
    p = skia.Paint(AntiAlias=True, Color=c)
    if shader is not None:
        p.setShader(shader)
    return p


def stroke(c, w, cap=skia.Paint.kButt_Cap):
    p = skia.Paint(AntiAlias=True, Color=c, Style=skia.Paint.kStroke_Style, StrokeWidth=w)
    p.setStrokeCap(cap)
    p.setStrokeJoin(skia.Paint.kRound_Join)
    return p


def save(surface, name):
    os.makedirs(OUT, exist_ok=True)
    path = os.path.join(OUT, name)
    surface.makeImageSnapshot().save(path, skia.kPNG)
    print("[ZG] painted", os.path.normpath(path))
    return path


# --------------------------------------------------------------------------- #
# Courts
# --------------------------------------------------------------------------- #
WOOD_A, WOOD_B = "#EDBE7E", "#E2AE6A"
SEAM = "#B98048"
LINE = "#FFFFFF"


def court(name, width, length, paint, hoop_north=True, hoop_south=False, center=False, three=6.0,
          emblem="ball", border=0.6):
    """Court in metres; texture u = x (width), v = z (length); north = top of image is v=1."""
    W, H = int(width * PX_PER_M), int(length * PX_PER_M)
    s = skia.Surface(W, H)
    c = s.getCanvas()
    # metres, origin bottom-left, z up (so the image's top row is the NORTH end)
    c.translate(0, H)
    c.scale(PX_PER_M, -PX_PER_M)
    rnd = random.Random(hash(name) & 0xffff)

    # ---- hardwood planks running along the length ----
    plank = 0.22
    n = int(width / plank) + 2
    for i in range(n):
        x = i * plank
        col = mix(WOOD_A, WOOD_B, rnd.random() * 0.8)
        c.drawRect(skia.Rect.MakeLTRB(x, 0, x + plank, length), fill(C(col)))
        # staggered end joints
        z = rnd.uniform(0, 2.4)
        while z < length:
            c.drawLine(x, z, x + plank, z, stroke(C(SEAM, 110), 0.012))
            z += rnd.uniform(1.8, 2.8)
        c.drawLine(x, 0, x, length, stroke(C(SEAM, 140), 0.014))

    # ---- painted border (apron) ----
    pc = paint
    dark = shade(pc, 0.72)
    edge = skia.Path()
    edge.addRect(skia.Rect.MakeLTRB(0, 0, width, length))
    edge.addRect(skia.Rect.MakeLTRB(border, border, width - border, length - border))
    edge.setFillType(skia.PathFillType.kEvenOdd)
    c.drawPath(edge, fill(C(dark)))

    # ---- painted keys, hoop markings ----
    cx = width / 2

    def outlined(path, w=0.075):
        c.drawPath(path, stroke(C("#3A2A1A", 70), w + 0.035))
        c.drawPath(path, stroke(C(LINE), w))

    for north in (True, False):
        if north and not hoop_north or (not north and not hoop_south):
            continue
        base = length - border if north else border
        d = -1 if north else 1
        rim = base + d * 1.6
        key_len, key_w = 5.0, 3.6
        z0, z1 = sorted((base, base + d * key_len))
        key = skia.Rect.MakeLTRB(cx - key_w / 2, z0, cx + key_w / 2, z1)
        grad = skia.GradientShader.MakeLinear([(cx, base), (cx, base + d * key_len)],
                                              [C(shade(pc, 0.92)), C(shade(pc, 1.12))])
        c.drawRect(key, fill(C(pc), grad))
        outlined(skia.Path().addRect(key))
        ft = base + d * key_len
        arc = skia.Path()
        arc.addArc(skia.Rect.MakeLTRB(cx - 1.8, ft - 1.8, cx + 1.8, ft + 1.8), 0 if not north else 180, 180)
        outlined(arc)
        # three point line: straight corners + arc
        corner_x = min(three, width / 2 - border - 0.6)
        ang = math.degrees(math.asin(min(1.0, corner_x / three)))
        p3 = skia.Path()
        p3.moveTo(cx - corner_x, base)
        zc = rim + d * math.cos(math.radians(ang)) * three
        p3.lineTo(cx - corner_x, zc)
        oval = skia.Rect.MakeLTRB(cx - three, rim - three, cx + three, rim + three)
        start = 90 + ang if not north else 270 - ang
        sweep = -2 * ang if not north else 2 * ang
        p3.arcTo(oval, start, sweep, False)
        p3.lineTo(cx + corner_x, base)
        outlined(p3)
        # restricted-area arc
        ra = skia.Path()
        ra.addArc(skia.Rect.MakeLTRB(cx - 1.25, rim - 1.25, cx + 1.25, rim + 1.25), 0 if not north else 180, 180)
        outlined(ra, 0.06)

    # ---- boundary + centre ----
    outlined(skia.Path().addRect(skia.Rect.MakeLTRB(border, border, width - border, length - border)))
    mid = length / 2
    if center:
        outlined(skia.Path().moveTo(border, mid).lineTo(width - border, mid))
        c.drawCircle(cx, mid, 1.8, fill(C(shade(pc, 1.05))))
        outlined(skia.Path().addCircle(cx, mid, 1.8))
    _emblem(c, cx, mid, 1.3 if center else 1.1, emblem, pc)

    # ---- anime floor sheen: soft diagonal light streaks + gentle vignette ----
    for k, (u, wdt) in enumerate(((0.25, 2.5), (0.55, 1.2), (0.78, 3.2))):
        x0 = width * u
        g = skia.GradientShader.MakeLinear([(x0 - wdt, 0), (x0 + wdt, 0)],
                                           [C("#FFFFFF", 0), C("#FFFFFF", 46 if k != 1 else 30), C("#FFFFFF", 0)],
                                           [0.0, 0.5, 1.0])
        c.save()
        c.translate(width / 2, length / 2)
        c.rotate(18)
        c.translate(-width / 2, -length / 2)
        c.drawRect(skia.Rect.MakeLTRB(-length, -length, width + length, 2 * length), fill(C("#FFFFFF"), g))
        c.restore()
    vg = skia.GradientShader.MakeRadial((cx, mid), max(width, length) * 0.75, [C("#000000", 0), C("#3A1E00", 60)],
                                        [0.55, 1.0])
    c.drawRect(skia.Rect.MakeLTRB(0, 0, width, length), fill(C("#000000"), vg))
    return save(s, "Court_%s.png" % name)


def _emblem(c, x, z, r, kind, paint):
    c.drawCircle(x, z, r + 0.12, fill(C("#FFC21A")))
    c.drawCircle(x, z, r, fill(C(shade(paint, 0.85))))
    # basketball
    br = r * 0.62
    g = skia.GradientShader.MakeRadial((x - br * 0.3, z + br * 0.35), br * 1.4, [C("#FFA04A"), C("#E8772E"), C("#C25A18")],
                                       [0.0, 0.55, 1.0])
    c.drawCircle(x, z, br, fill(C("#E8772E"), g))
    seam = stroke(C("#2A1A12"), br * 0.07, skia.Paint.kRound_Cap)
    c.drawLine(x - br, z, x + br, z, seam)
    c.drawLine(x, z - br, x, z + br, seam)
    for sx in (1, -1):
        p = skia.Path()
        p.addArc(skia.Rect.MakeLTRB(x + sx * br * 0.55 - br * 0.8, z - br * 0.95, x + sx * br * 0.55 + br * 0.8, z + br * 0.95),
                 90 if sx > 0 else -90, 180)
        c.drawPath(p, seam)
    c.drawCircle(x, z, br, stroke(C("#2A1A12"), br * 0.06))
    c.drawCircle(x - br * 0.35, z + br * 0.4, br * 0.14, fill(C("#FFFFFF", 170)))   # anime shine
    if kind == "crown":
        cw, ch = br * 1.2, br * 0.6
        base_z = z + br * 0.85
        crown = skia.Path()
        crown.moveTo(x - cw / 2, base_z)
        for k, (px, pz) in enumerate(((-0.5, 1.0), (-0.25, 0.45), (0.0, 1.1), (0.25, 0.45), (0.5, 1.0))):
            crown.lineTo(x + px * cw, base_z + pz * ch)
        crown.lineTo(x + cw / 2, base_z)
        crown.close()
        c.drawPath(crown, fill(C("#FFC21A")))
        c.drawPath(crown, stroke(C("#7A4B00"), br * 0.05))
    c.drawCircle(x, z, r + 0.12, stroke(C("#3A2A1A", 90), 0.04))


# --------------------------------------------------------------------------- #
# Stone (walls) and flagstones (corridors / ledges)
# --------------------------------------------------------------------------- #
STONE = ["#5D6880", "#56617A", "#626E87", "#59647D", "#4F5A72"]
MORTAR = "#262B38"


def _block(c, r, col, rnd, px):
    """One painted stone block: flat colour, light top-left bevel, dark bottom-right, a shine tick."""
    rr = skia.RRect.MakeRectXY(r, px * 0.012, px * 0.012)
    c.drawRRect(rr, fill(C(col)))
    w, h = r.width(), r.height()
    b = min(w, h) * 0.12
    top = skia.Path()
    top.moveTo(r.left(), r.top())
    top.lineTo(r.right(), r.top())
    top.lineTo(r.right() - b, r.top() + b)
    top.lineTo(r.left() + b, r.top() + b)
    top.lineTo(r.left() + b, r.bottom() - b)
    top.lineTo(r.left(), r.bottom())
    top.close()
    c.drawPath(top, fill(C(shade(col, 1.16))))
    bot = skia.Path()
    bot.moveTo(r.right(), r.bottom())
    bot.lineTo(r.left(), r.bottom())
    bot.lineTo(r.left() + b, r.bottom() - b)
    bot.lineTo(r.right() - b, r.bottom() - b)
    bot.lineTo(r.right() - b, r.top() + b)
    bot.lineTo(r.right(), r.top())
    bot.close()
    c.drawPath(bot, fill(C(shade(col, 0.8))))
    if rnd.random() < 0.45:   # anime shine ticks
        x0 = r.left() + w * rnd.uniform(0.15, 0.3)
        y0 = r.top() + h * rnd.uniform(0.25, 0.35)
        for k in range(2):
            c.drawLine(x0 + k * w * 0.07, y0 + h * 0.18, x0 + k * w * 0.07 + w * 0.05, y0,
                       stroke(C("#FFFFFF", 110), px * 0.006, skia.Paint.kRound_Cap))
    if rnd.random() < 0.18:   # a little crack
        x = r.left() + w * rnd.uniform(0.5, 0.8)
        p = skia.Path()
        p.moveTo(x, r.bottom())
        p.lineTo(x - w * 0.05, r.bottom() - h * 0.3)
        p.lineTo(x + w * 0.02, r.bottom() - h * 0.5)
        c.drawPath(p, stroke(C(MORTAR, 160), px * 0.005, skia.Paint.kRound_Cap))


def stone_wall(size=1024):
    """One tile = 2 x 2 m of wall: 4 rows of 1 m x 0.5 m blocks, seamless."""
    s = skia.Surface(size, size)
    c = s.getCanvas()
    c.clear(C(MORTAR))
    rnd = random.Random(11)
    rows, bw = 4, size / 2
    bh = size / rows
    gap = size * 0.012
    for row in range(rows):
        off = (bw / 2) if row % 2 else 0
        for k in range(-1, 3):
            x = k * bw + off
            col = rnd.choice(STONE)
            _block(c, skia.Rect.MakeLTRB(x + gap / 2, row * bh + gap / 2, x + bw - gap / 2, (row + 1) * bh - gap / 2),
                   col, rnd, size)
    return save(s, "StoneWall.png")


def flagstones(size=1024):
    """One tile = 2 x 2 m of floor: big square flagstones, seamless."""
    s = skia.Surface(size, size)
    c = s.getCanvas()
    c.clear(C(MORTAR))
    rnd = random.Random(5)
    gap = size * 0.014
    cells = [(0, 0, 0.5, 0.5), (0.5, 0, 1, 0.5), (0, 0.5, 0.25, 1), (0.25, 0.5, 0.75, 1), (0.75, 0.5, 1, 1)]
    for x0, y0, x1, y1 in cells:
        col = rnd.choice(["#6A7488", "#646E83", "#707A8F", "#5F697E"])
        _block(c, skia.Rect.MakeLTRB(x0 * size + gap / 2, y0 * size + gap / 2, x1 * size - gap / 2, y1 * size - gap / 2),
               col, rnd, size)
    return save(s, "Flagstones.png")


# Court list - must match LevelBuilder.cs
COURTS = [
    ("Lobby", 16, 14, "#1F5FD6", False, False, False, 6.0, "ball"),
    ("Shrine1", 18, 24, "#E8772E", True, False, False, 6.0, "ball"),
    ("Shrine2", 22, 32, "#2FBF68", False, True, False, 6.0, "ball"),
    ("Shrine3South", 24, 12, "#A65BE0", False, False, False, 6.0, "ball"),
    ("Shrine3North", 24, 16, "#A65BE0", True, False, False, 6.0, "ball"),
    ("Arena", 30, 40, "#6A2DB8", True, True, True, 6.5, "crown"),
]


def main():
    for name, w, l, paint, n, so, cen, three, emb in COURTS:
        court(name, w, l, paint, n, so, cen, three, emb)
    stone_wall()
    flagstones()


if __name__ == "__main__":
    main()
