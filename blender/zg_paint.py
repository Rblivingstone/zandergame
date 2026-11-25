"""
Hand-painted-style textures for the characters, drawn with anti-aliased vector
strokes (skia-python, `pip install skia-python`). These give the models their
"drawn" anime look: ink-lined eyes with gradient irises and sparkles, blush
hatching, hair with root-to-tip gradients, strand lines and a shine band, and
jerseys with painted trims, numbers and names.

Every painter draws in the same units the mesh UVs are generated from (metres
on the model), so the art lands exactly where the generator expects it.
"""

import math
import os

import skia

TEX_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "textures")


def C(hex_rgb, a=255):
    h = hex_rgb.lstrip("#")
    return skia.ColorSetARGB(a, int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16))


def fill(color, shader=None):
    p = skia.Paint(AntiAlias=True, Color=color, Style=skia.Paint.kFill_Style)
    if shader is not None:
        p.setShader(shader)
    return p


def stroke(color, width, cap=skia.Paint.kRound_Cap):
    p = skia.Paint(AntiAlias=True, Color=color, Style=skia.Paint.kStroke_Style, StrokeWidth=width)
    p.setStrokeCap(cap)
    p.setStrokeJoin(skia.Paint.kRound_Join)
    return p


def oval(cx, cy, rx, ry):
    return skia.Rect.MakeLTRB(cx - rx, cy - ry, cx + rx, cy + ry)


def poly(points, close=False):
    path = skia.Path()
    path.moveTo(*points[0])
    for p in points[1:]:
        path.lineTo(*p)
    if close:
        path.close()
    return path


def smooth(points, close=False):
    """Catmull-Rom through the points -> smooth cubic path."""
    pts = list(points)
    path = skia.Path()
    path.moveTo(*pts[0])
    n = len(pts)
    rng = range(n if close else n - 1)
    for i in rng:
        p0 = pts[(i - 1) % n] if (close or i > 0) else pts[i]
        p1 = pts[i]
        p2 = pts[(i + 1) % n]
        p3 = pts[(i + 2) % n] if (close or i + 2 < n) else p2
        c1 = (p1[0] + (p2[0] - p0[0]) / 6, p1[1] + (p2[1] - p0[1]) / 6)
        c2 = (p2[0] - (p3[0] - p1[0]) / 6, p2[1] - (p3[1] - p1[1]) / 6)
        path.cubicTo(c1[0], c1[1], c2[0], c2[1], p2[0], p2[1])
    if close:
        path.close()
    return path


def ellipse_pts(cx, cy, rx, ry, a0=0.0, a1=2 * math.pi, n=24):
    return [(cx + math.cos(a0 + (a1 - a0) * i / (n - 1)) * rx, cy + math.sin(a0 + (a1 - a0) * i / (n - 1)) * ry)
            for i in range(n)]


def tapered(points, w0, w1, w_mid=None):
    """A filled brush stroke along points whose width tapers (ink-pen look)."""
    n = len(points)
    left, right = [], []
    for i, (x, y) in enumerate(points):
        a = points[max(0, i - 1)]
        b = points[min(n - 1, i + 1)]
        tx, ty = b[0] - a[0], b[1] - a[1]
        ln = math.hypot(tx, ty) or 1.0
        nx, ny = -ty / ln, tx / ln
        t = i / (n - 1)
        if w_mid is None:
            w = w0 + (w1 - w0) * t
        else:
            w = (w0 + (w_mid - w0) * t * 2) if t < 0.5 else (w_mid + (w1 - w_mid) * (t - 0.5) * 2)
        left.append((x + nx * w / 2, y + ny * w / 2))
        right.append((x - nx * w / 2, y - ny * w / 2))
    return smooth(left + list(reversed(right)), close=True)


class Canvas:
    """A square texture whose drawing units are model metres: x -> u, z -> v."""

    def __init__(self, size_px, u_metres, v_metres, centre_x, centre_z, base_hex, height_px=None):
        self.w = size_px
        self.h = height_px or size_px
        self.surface = skia.Surface(self.w, self.h)
        self.c = self.surface.getCanvas()
        self.c.clear(C(base_hex))
        sx = self.w / u_metres
        sz = self.h / v_metres
        self.c.translate(self.w / 2 - centre_x * sx, self.h / 2 + centre_z * sz)
        self.c.scale(sx, -sz)

    def save(self, name):
        os.makedirs(TEX_DIR, exist_ok=True)
        path = os.path.join(TEX_DIR, name)
        self.surface.makeImageSnapshot().save(path, skia.kPNG)
        print("[ZG] painted", path)
        return path


# --------------------------------------------------------------------------- #
# Anime eye kit (shared by Zander, and the boss uses a variant)
# --------------------------------------------------------------------------- #
def anime_eye(c, ex, ey, side, w=0.056, h=0.066, iris_top="#123E8C", iris_mid="#2E8BF0", iris_low="#9BE0FF",
              ink="#2B1A12", ring="#0E2A5C"):
    """side = +1 for the character's left eye (viewer's right), -1 for the right."""
    # eye white: soft almond, a little flatter on top
    white = smooth([(ex - w, ey + 0.004), (ex - w * 0.55, ey + h * 0.9), (ex + w * 0.3 * side, ey + h),
                    (ex + w, ey + h * 0.35), (ex + w * 0.85, ey - h * 0.55), (ex, ey - h), (ex - w * 0.85, ey - h * 0.55)],
                   close=True)
    c.drawPath(white, fill(C("#FFFFFF")))
    c.save()
    c.clipPath(white, doAntiAlias=True)
    # soft shadow under the upper lid
    c.drawOval(oval(ex, ey + h * 0.95, w * 1.2, h * 0.45), fill(C("#C9D6EE", 200)))
    ix, iy = ex - side * 0.004, ey - 0.008
    irx, iry = w * 0.8, h * 0.86
    grad = skia.GradientShader.MakeLinear([(ix, iy + iry), (ix, iy - iry)],
                                          [C(iris_top), C(iris_mid), C(iris_low)], [0.0, 0.55, 1.0])
    c.drawOval(oval(ix, iy, irx, iry), fill(C(iris_mid), grad))
    c.drawOval(oval(ix, iy, irx * 0.78, iry * 0.8), stroke(C(iris_top, 120), 0.0025))
    c.drawOval(oval(ix, iy, irx, iry), stroke(C(ring), 0.0045))
    # pupil + glow crescent
    c.drawOval(oval(ix, iy + 0.004, irx * 0.45, iry * 0.5), fill(C("#0A1430")))
    c.drawArc(oval(ix, iy - 0.006, irx * 0.62, iry * 0.55), 200, 140, False, stroke(C(iris_low, 200), 0.004))
    # sparkles
    c.drawOval(oval(ix + side * irx * 0.42, iy + iry * 0.42, irx * 0.34, iry * 0.3), fill(C("#FFFFFF")))
    c.drawOval(oval(ix - side * irx * 0.4, iy - iry * 0.45, irx * 0.15, irx * 0.15), fill(C("#FFFFFF")))
    c.drawOval(oval(ix + side * irx * 0.05, iy - iry * 0.62, irx * 0.07, irx * 0.07), fill(C("#FFFFFF", 220)))
    c.restore()
    # thick upper lash line with an outer flick, thin lower lash
    top = [(ex - side * w * 0.98, ey + h * 0.4), (ex - side * w * 0.55, ey + h * 0.92), (ex + side * w * 0.2, ey + h * 1.04),
           (ex + side * w * 0.8, ey + h * 0.7), (ex + side * w * 1.08, ey + h * 0.2)]
    c.drawPath(tapered(top, 0.003, 0.011, 0.014), fill(C(ink)))
    flick_base = (ex + side * w * 0.95, ey + h * 0.45)
    c.drawPath(tapered([flick_base, (ex + side * w * 1.28, ey + h * 0.55)], 0.009, 0.001), fill(C(ink)))
    low = ellipse_pts(ex, ey + 0.002, w * 0.92, h * 0.98, math.radians(-25 if side > 0 else -155),
                      math.radians(-75 if side > 0 else -105), 8)
    c.drawPath(tapered(low, 0.0035, 0.001), fill(C(ink, 200)))
    c.drawPath(smooth(ellipse_pts(ex, ey, w, h, math.radians(200), math.radians(340), 10)),
               stroke(C("#C79A82", 150), 0.0018))


# --------------------------------------------------------------------------- #
# ZANDER
# --------------------------------------------------------------------------- #
# Face UVs: front planar projection, see zander_character.face_uv()
FACE_U_M, FACE_V_M, FACE_CZ = 0.53, 0.6, 1.07


def paint_zander_face(skin="#FBDCC2"):
    cv = Canvas(1024, FACE_U_M, FACE_V_M, 0.0, FACE_CZ, skin)
    c = cv.c
    ey = 1.0
    # blush with anime hatching
    for s in (1, -1):
        bx, bz = s * 0.15, 0.94
        rg = skia.GradientShader.MakeRadial((bx, bz), 0.04, [C("#F5958A", 170), C("#F5958A", 0)])
        c.save()
        c.translate(bx, bz)
        c.scale(1.0, 0.5)
        c.translate(-bx, -bz)
        c.drawCircle(bx, bz, 0.04, fill(C("#F5958A"), rg))
        c.restore()
        for k in range(3):
            x0 = bx - 0.018 + k * 0.014
            c.drawLine(x0, bz - 0.006, x0 + 0.008, bz + 0.008, stroke(C("#EA7E72", 210), 0.0028))
    for s in (1, -1):
        anime_eye(c, s * 0.095, ey, s)
        # eyebrows: short, slightly raised, friendly
        bx = s * 0.095
        brow = [(bx - s * 0.04, 1.105), (bx - s * 0.005, 1.122), (bx + s * 0.035, 1.112)]
        if s < 0:
            brow = list(reversed(brow))
        c.drawPath(tapered(brow, 0.006, 0.003, 0.011), fill(C("#B9822C")))
    # nose: a tiny ink tick
    c.drawPath(smooth([(-0.004, 0.958), (0.004, 0.948), (0.0, 0.944)]), stroke(C("#D99478"), 0.0035))
    # open happy mouth
    mouth = smooth([(-0.034, 0.914), (-0.018, 0.884), (0.0, 0.874), (0.018, 0.884), (0.034, 0.914),
                    (0.0, 0.909)], close=True)
    c.drawPath(mouth, fill(C("#8E2A2A")))
    c.save()
    c.clipPath(mouth, doAntiAlias=True)
    c.drawOval(oval(0.004, 0.876, 0.022, 0.014), fill(C("#F28A84")))
    c.drawRect(skia.Rect.MakeLTRB(-0.03, 0.904, 0.03, 0.92), fill(C("#FFFFFF")))
    c.restore()
    c.drawPath(mouth, stroke(C("#5A1A1A"), 0.003))
    # chin/ear shading hint
    c.drawPath(smooth([(-0.07, 0.83), (0.0, 0.815), (0.07, 0.83)]), stroke(C("#EDB99B", 120), 0.006))
    return cv.save("zander_face.png")


def paint_hair(name, root="#DDA941", mid="#F7D26A", tip="#FFE9A3", shade="#D19A33", shine="#FFF8DC"):
    """Hair clump texture: u across the clump (0 and 1 are its edges), v from root
    (0) to tip (1). Soft gradient, one shaded side and a single anime shine streak."""
    s = skia.Surface(512, 512)
    c = s.getCanvas()
    grad = skia.GradientShader.MakeLinear([(0, 512), (0, 0)], [C(root), C(mid), C(tip)], [0.0, 0.5, 1.0])
    c.drawRect(skia.Rect.MakeWH(512, 512), fill(C(mid), grad))
    # one side of every clump is in shadow, which separates the clumps visually
    side = skia.GradientShader.MakeLinear([(0, 0), (200, 0)], [C(shade, 220), C(shade, 0)])
    c.drawRect(skia.Rect.MakeWH(512, 512), fill(C(shade), side))
    # a single long shine streak (anime "angel ring" highlight)
    c.drawPath(smooth([(300, 330), (330, 260), (350, 170), (360, 120), (372, 170), (380, 260), (360, 330)], close=True),
               fill(C(shine, 230)))
    # two fine strand lines
    for u, top in ((150, 140), (430, 200)):
        pts = [(u + 6 * math.sin(y * 0.02), y) for y in range(512, top, -16)]
        c.drawPath(tapered(pts, 5, 1), fill(C(shade, 150)))
    path = os.path.join(TEX_DIR, name)
    os.makedirs(TEX_DIR, exist_ok=True)
    s.makeImageSnapshot().save(path, skia.kPNG)
    print("[ZG] painted", path)
    return path


# Clothes UVs: two drawn panels. Front: u = 0.25 + x, back: u = 0.75 - x
# (so both read correctly from the viewer's side); v = height. x in metres.
CLOTH_Z0, CLOTH_Z1 = 0.25, 0.77


def _stroke_letters(c, text, x, y, h, color, width):
    """Tiny built-in stroke font (block capitals) so no font files are needed."""
    glyphs = {
        "Z": [[(0, 1), (1, 1), (0, 0), (1, 0)]],
        "A": [[(0, 0), (0.5, 1), (1, 0)], [(0.22, 0.42), (0.78, 0.42)]],
        "N": [[(0, 0), (0, 1), (1, 0), (1, 1)]],
        "D": [[(0, 0), (0, 1), (0.55, 1), (0.95, 0.75), (0.95, 0.25), (0.55, 0), (0, 0)]],
        "E": [[(1, 1), (0, 1), (0, 0), (1, 0)], [(0, 0.5), (0.75, 0.5)]],
        "R": [[(0, 0), (0, 1), (0.7, 1), (0.95, 0.8), (0.7, 0.55), (0, 0.55)], [(0.45, 0.55), (1, 0)]],
        "K": [[(0, 0), (0, 1)], [(1, 1), (0, 0.45)], [(0.35, 0.62), (1, 0)]],
        "I": [[(0.5, 0), (0.5, 1)], [(0.2, 1), (0.8, 1)], [(0.2, 0), (0.8, 0)]],
        "G": [[(0.95, 0.85), (0.6, 1), (0.25, 1), (0, 0.7), (0, 0.3), (0.25, 0), (0.7, 0), (1, 0.25), (1, 0.45), (0.55, 0.45)]],
        "S": [[(1, 0.9), (0.6, 1), (0.1, 0.9), (0.05, 0.62), (0.95, 0.38), (0.9, 0.1), (0.4, 0), (0, 0.1)]],
        "W": [[(0, 1), (0.25, 0), (0.5, 0.6), (0.75, 0), (1, 1)]],
        "T": [[(0, 1), (1, 1)], [(0.5, 1), (0.5, 0)]],
    }
    adv = h * 0.85
    total = adv * len(text) - h * 0.2
    x0 = x - total / 2
    for i, ch in enumerate(text):
        for line in glyphs.get(ch, []):
            pts = [(x0 + i * adv + px * h * 0.65, y + py * h) for px, py in line]
            c.drawPath(poly(pts), stroke(color, width))


def _number(c, digits, cx, cy, h, fill_hex, outline_hex, width, outline_width):
    """Big jersey numbers drawn as strokes (supports 8 and 9)."""
    adv = h * 0.62
    x0 = cx - adv * (len(digits) - 1) / 2
    for i, d in enumerate(digits):
        x = x0 + i * adv
        if d == "8":
            paths = [skia.Path().addOval(oval(x, cy + h * 0.25, h * 0.2, h * 0.22)),
                     skia.Path().addOval(oval(x, cy - h * 0.22, h * 0.25, h * 0.26))]
        else:  # 9
            ring = skia.Path().addOval(oval(x, cy + h * 0.2, h * 0.24, h * 0.27))
            tail = smooth([(x + h * 0.24, cy + h * 0.2), (x + h * 0.22, cy - h * 0.2), (x, cy - h * 0.48),
                           (x - h * 0.2, cy - h * 0.42)])
            paths = [ring, tail]
        for p in paths:
            c.drawPath(p, stroke(C(outline_hex), outline_width))
        for p in paths:
            c.drawPath(p, stroke(C(fill_hex), width))


def paint_zander_clothes(skin="#FBDCC2"):
    """Jersey + shorts drawn as a FRONT panel (left half) and BACK panel (right
    half), exactly like drawing a tank top on paper. Skin is painted outside the
    garment outline, so neckline and arm holes are smooth drawn curves."""
    cv = Canvas(1024, 1.0, CLOTH_Z1 - CLOTH_Z0, 0.5, (CLOTH_Z0 + CLOTH_Z1) / 2, "#FAFAFA")
    c = cv.c
    blue, navy, white, sk = C("#1F63E0"), C("#152650"), C("#FAFAFA"), C(skin)
    short_c = C("#1B2E5E")

    for panel, cu, back in (("front", 0.25, False), ("back", 0.75, True)):
        def P(x, z, cu=cu, back=back):
            return (cu - x if back else cu + x, z)
        L, R = cu - 0.25, cu + 0.25
        # ---- shorts ----
        c.drawRect(skia.Rect.MakeLTRB(L, CLOTH_Z0 - 0.05, R, 0.476), fill(short_c))
        for sx in (1, -1):        # side stripes on the outer thigh
            x0, _ = P(sx * 0.125, 0)
            c.drawRect(skia.Rect.MakeLTRB(x0 - 0.016, 0.262, x0 + 0.016, 0.476), fill(white))
            c.drawRect(skia.Rect.MakeLTRB(x0 - 0.006, 0.262, x0 + 0.006, 0.476), fill(blue))
        c.drawRect(skia.Rect.MakeLTRB(L, 0.262, R, 0.278), fill(C("#2A4590")))        # hem band
        c.drawRect(skia.Rect.MakeLTRB(L, CLOTH_Z0 - 0.05, R, 0.262), fill(sk))        # legs below the hem
        c.drawLine(L, 0.262, R, 0.262, stroke(navy, 0.003))
        c.drawRect(skia.Rect.MakeLTRB(L, 0.455, R, 0.476), fill(C("#24387A")))        # waistband
        if not back:
            c.drawPath(smooth([P(-0.006, 0.462), P(-0.014, 0.44), P(-0.01, 0.425)]), stroke(white, 0.004))
            c.drawPath(smooth([P(0.006, 0.462), P(0.014, 0.44), P(0.01, 0.425)]), stroke(white, 0.004))
            c.drawPath(smooth([P(0.0, 0.455), P(0.003, 0.40), P(-0.004, 0.36)]), stroke(C("#13234A", 160), 0.004))
        for sx in (1, -1):        # fabric folds
            c.drawPath(smooth([P(sx * 0.07, 0.29), P(sx * 0.075, 0.33), P(sx * 0.068, 0.37)]), stroke(C("#13234A", 150), 0.004))
        # ---- tank top ----
        c.drawRect(skia.Rect.MakeLTRB(L, 0.476, R, 0.5), fill(blue))                  # hem band
        c.drawRect(skia.Rect.MakeLTRB(L, 0.6, R, CLOTH_Z1 + 0.05), fill(sk))          # skin above the armpits...
        neck = ([P(-0.034, 0.768), P(0.0, 0.715), P(0.034, 0.768)] if back else
                [P(-0.034, 0.768), P(0.0, 0.66), P(0.034, 0.768)])
        arm_r = [P(0.074, 0.768), P(0.085, 0.71), P(0.108, 0.655), P(0.14, 0.615), P(0.2, 0.6)]
        arm_l = [P(-0.2, 0.6), P(-0.14, 0.615), P(-0.108, 0.655), P(-0.085, 0.71), P(-0.074, 0.768)]
        body = arm_l + [P(-0.034, 0.768)] + neck[1:-1] + [P(0.034, 0.768)] + arm_r + [P(0.2, 0.59), P(-0.2, 0.59)]
        c.drawPath(smooth(body, close=True), fill(white))                             # ...then the tank top
        c.drawPath(smooth(arm_l[1:]), stroke(blue, 0.012))
        c.drawPath(smooth(arm_r[:-1]), stroke(blue, 0.012))
        c.drawPath(smooth(neck) if back else poly(neck), stroke(blue, 0.013))
        c.drawLine(*P(-0.074, 0.768), *P(-0.034, 0.768), stroke(blue, 0.008))
        c.drawLine(*P(0.034, 0.768), *P(0.074, 0.768), stroke(blue, 0.008))
        for sx in (1, -1):        # soft folds
            c.drawPath(smooth([P(sx * 0.09, 0.51), P(sx * 0.097, 0.54), P(sx * 0.092, 0.57)]), stroke(C("#C9D3E6", 220), 0.005))
        # numbers and the name on the back
        if back:
            _number(c, "8", cu, 0.575, 0.11, "#1F63E0", "#152650", 0.017, 0.027)
            _stroke_letters(c, "ZANDER", cu, 0.648, 0.02, navy, 0.0045)
        else:
            _number(c, "8", cu, 0.58, 0.095, "#1F63E0", "#152650", 0.016, 0.026)
    return cv.save("zander_clothes.png")


# --------------------------------------------------------------------------- #
# KING SWAT
# --------------------------------------------------------------------------- #
BOSS_FACE_U_M, BOSS_FACE_V_M, BOSS_FACE_CZ = 1.2, 1.3, 2.78


def _ball_leather(c, seed):
    import random
    rnd = random.Random(seed)
    for _ in range(2200):
        x = rnd.uniform(-0.6, 0.6)
        z = rnd.uniform(2.12, 3.44)
        c.drawCircle(x, z, 0.004, fill(C("#D9661F", 120)))


def paint_boss_face():
    """2048x1024 atlas: left half = the front of the ball-head (face), right half
    = the back of the ball (seams only). See boss_character.boss_face_uv()."""
    cv = Canvas(1024, BOSS_FACE_U_M, BOSS_FACE_V_M, 0.0, BOSS_FACE_CZ, "#EE7A2C")
    c = cv.c
    _ball_leather(c, 4)
    # front seams (the side curves of a basketball)
    for s in (1, -1):
        c.drawPath(smooth(ellipse_pts(s * 0.62, 2.78, 0.3, 0.62, math.radians(90), math.radians(270), 20) if s > 0 else
                          ellipse_pts(s * 0.62, 2.78, 0.3, 0.62, math.radians(-90), math.radians(90), 20)),
                   stroke(C("#2A1A12"), 0.018))
    c.drawLine(0.0, 3.4, 0.0, 3.05, stroke(C("#2A1A12"), 0.018))
    c.drawLine(0.0, 2.48, 0.0, 2.2, stroke(C("#2A1A12"), 0.018))
    # angry glowing eyes under heavy brows
    for s in (1, -1):
        ex, ey = s * 0.2, 2.76
        eye = smooth([(ex - s * 0.13, ey + 0.02), (ex - s * 0.02, ey + 0.07), (ex + s * 0.12, ey + 0.11),
                      (ex + s * 0.13, ey - 0.06), (ex, ey - 0.12), (ex - s * 0.11, ey - 0.07)], close=True)
        glow = skia.GradientShader.MakeRadial((ex, ey - 0.02), 0.14, [C("#FFF6C8"), C("#FFD84A"), C("#FF9F1A")],
                                              [0.0, 0.5, 1.0])
        c.drawPath(eye, fill(C("#FFD84A"), glow))
        c.drawOval(oval(ex - s * 0.025, ey - 0.03, 0.03, 0.05), fill(C("#3A1B00")))
        c.drawOval(oval(ex - s * 0.01, ey - 0.005, 0.012, 0.014), fill(C("#FFFFFF")))
        c.drawPath(eye, stroke(C("#1A0C04"), 0.012))
        brow = [(ex + s * 0.17, ey + 0.2), (ex + s * 0.02, ey + 0.14), (ex - s * 0.15, ey + 0.07)]
        c.drawPath(tapered(brow, 0.03, 0.05, 0.075), fill(C("#2A1A12")))
    # smug toothy grin
    grin = smooth([(-0.22, 2.58), (-0.12, 2.5), (0.04, 2.47), (0.18, 2.52), (0.24, 2.62), (0.0, 2.57)], close=True)
    c.drawPath(grin, fill(C("#2A0A0A")))
    c.save()
    c.clipPath(grin, doAntiAlias=True)
    c.drawRect(skia.Rect.MakeLTRB(-0.3, 2.54, 0.3, 2.65), fill(C("#FFFFFF")))
    for k in range(-5, 6):
        c.drawLine(k * 0.04, 2.5, k * 0.04, 2.65, stroke(C("#B8B8C8"), 0.006))
    c.drawRect(skia.Rect.MakeLTRB(-0.3, 2.44, 0.3, 2.5), fill(C("#FFFFFF")))
    c.restore()
    c.drawPath(grin, stroke(C("#1A0C04"), 0.012))
    # back of the ball
    bk = Canvas(1024, BOSS_FACE_U_M, BOSS_FACE_V_M, 0.0, BOSS_FACE_CZ, "#EE7A2C")
    _ball_leather(bk.c, 9)
    for s_ in (1, -1):
        pts = (ellipse_pts(s_ * 0.62, 2.78, 0.3, 0.62, math.radians(90), math.radians(270), 20) if s_ > 0 else
               ellipse_pts(s_ * 0.62, 2.78, 0.3, 0.62, math.radians(-90), math.radians(90), 20))
        bk.c.drawPath(smooth(pts), stroke(C("#2A1A12"), 0.018))
    bk.c.drawLine(0.0, 3.45, 0.0, 2.1, stroke(C("#2A1A12"), 0.018))
    atlas = skia.Surface(2048, 1024)
    a = atlas.getCanvas()
    a.drawImage(cv.surface.makeImageSnapshot(), 0, 0)
    a.drawImage(bk.surface.makeImageSnapshot(), 1024, 0)
    os.makedirs(TEX_DIR, exist_ok=True)
    path = os.path.join(TEX_DIR, "kingswat_face.png")
    atlas.makeImageSnapshot().save(path, skia.kPNG)
    print("[ZG] painted", path)
    return path


def paint_boss_clothes():
    cv = Canvas(1024, 1.0, 1.6, 0.5, 1.5, "#6A2DB8")
    c = cv.c
    gold, dark = C("#FFC21A"), C("#3E1478")
    m = 1.0 / 3.2
    c.drawRect(skia.Rect.MakeLTRB(-0.1, 0.6, 1.1, 1.31), fill(C("#F4F4F4")))          # shorts
    for u in (0.0, 0.5, 1.0):
        c.drawRect(skia.Rect.MakeLTRB(u - 0.03, 0.7, u + 0.03, 1.31), fill(C("#6A2DB8")))
        c.drawRect(skia.Rect.MakeLTRB(u - 0.012, 0.7, u + 0.012, 1.31), fill(gold))
    c.drawRect(skia.Rect.MakeLTRB(-0.1, 1.28, 1.1, 1.36), fill(gold))                 # hem
    for u0 in (0.0, 0.5, 1.0):
        c.drawPath(smooth(ellipse_pts(u0, 2.25, 0.08, 0.38, math.radians(180), math.radians(360), 16)), stroke(gold, 0.05))
    c.drawPath(poly([(0.25 - 0.22 * m, 2.25), (0.25, 1.97), (0.25 + 0.22 * m, 2.25)]), stroke(gold, 0.05))
    _number(c, "99", 0.25, 1.68, 0.34, "#FFC21A", "#3E1478", 0.05, 0.08)
    _number(c, "99", 0.75, 1.62, 0.38, "#FFC21A", "#3E1478", 0.05, 0.08)
    _stroke_letters(c, "KING", 0.75, 1.9, 0.08, gold, 0.016)
    for u, z in ((0.13, 1.42), (0.37, 1.42), (0.63, 1.43), (0.87, 1.42)):
        c.drawPath(smooth([(u, z), (u + 0.01, z + 0.1), (u, z + 0.2)]), stroke(dark, 0.012))
    return cv.save("kingswat_clothes.png")


if __name__ == "__main__":
    paint_zander_face()
    paint_hair("zander_hair.png")
    paint_zander_clothes()
    paint_boss_face()
    paint_boss_clothes()


# --------------------------------------------------------------------------- #
# Transparent vector "decals" placed onto 3D garments by zg_bake
# (drawing units: x and z in metres, as seen looking at that side of the model)
# --------------------------------------------------------------------------- #
DECAL_X0, DECAL_X1, DECAL_Z0, DECAL_Z1 = -0.25, 0.25, 0.24, 0.8


def _decal_canvas(px=1024, bounds=None):
    x0, x1, z0, z1 = bounds or (DECAL_X0, DECAL_X1, DECAL_Z0, DECAL_Z1)
    w = px
    h = int(px * (z1 - z0) / (x1 - x0))
    surf = skia.Surface(w, h)
    c = surf.getCanvas()
    c.clear(skia.ColorTRANSPARENT)
    sx = w / (x1 - x0)
    sz = h / (z1 - z0)
    c.translate(-x0 * sx, z1 * sz)
    c.scale(sx, -sz)
    return surf, c


def _to_float(surf):
    import numpy as np
    arr = surf.makeImageSnapshot().toarray(colorType=skia.kRGBA_8888_ColorType, alphaType=skia.kUnpremul_AlphaType)
    return arr.astype(np.float32) / 255.0


def zander_decals():
    """Front: big 8, drawstrings, folds. Back: ZANDER + big 8, folds."""
    navy = C("#152650")
    front, c = _decal_canvas()
    _number(c, "8", 0.0, 0.585, 0.095, "#1F63E0", "#152650", 0.016, 0.026)
    for sx in (1, -1):
        c.drawPath(smooth([(sx * 0.085, 0.51), (sx * 0.092, 0.54), (sx * 0.087, 0.57)]), stroke(C("#C9D3E6", 230), 0.005))
        c.drawPath(smooth([(sx * 0.07, 0.29), (sx * 0.075, 0.33), (sx * 0.068, 0.37)]), stroke(C("#13234A", 170), 0.004))
    c.drawPath(smooth([(-0.006, 0.462), (-0.014, 0.44), (-0.01, 0.425)]), stroke(C("#FFFFFF"), 0.004))
    c.drawPath(smooth([(0.006, 0.462), (0.014, 0.44), (0.01, 0.425)]), stroke(C("#FFFFFF"), 0.004))
    c.drawPath(smooth([(0.0, 0.452), (0.003, 0.40), (-0.004, 0.36)]), stroke(C("#13234A", 170), 0.004))
    back, b = _decal_canvas()
    _number(b, "8", 0.0, 0.58, 0.11, "#1F63E0", "#152650", 0.017, 0.027)
    _stroke_letters(b, "ZANDER", 0.0, 0.652, 0.02, navy, 0.0045)
    for sx in (1, -1):
        b.drawPath(smooth([(sx * 0.085, 0.51), (sx * 0.092, 0.54), (sx * 0.087, 0.57)]), stroke(C("#C9D3E6", 230), 0.005))
        b.drawPath(smooth([(sx * 0.07, 0.29), (sx * 0.075, 0.33), (sx * 0.068, 0.37)]), stroke(C("#13234A", 170), 0.004))
    return _to_float(front), _to_float(back)


BOSS_DECAL_BOUNDS = (-0.9, 0.9, 0.6, 2.4)


def boss_decals():
    """King Swat: gold 99 on the front; KING + 99 on the back; jersey folds."""
    front, c = _decal_canvas(1024, BOSS_DECAL_BOUNDS)
    _number(c, "99", 0.0, 1.7, 0.34, "#FFC21A", "#3E1478", 0.05, 0.08)
    back, b = _decal_canvas(1024, BOSS_DECAL_BOUNDS)
    _number(b, "99", 0.0, 1.66, 0.38, "#FFC21A", "#3E1478", 0.05, 0.08)
    _stroke_letters(b, "KING", 0.0, 1.93, 0.09, C("#FFC21A"), 0.018)
    for cv in (c, b):
        for sx in (1, -1):
            cv.drawPath(smooth([(sx * 0.42, 1.42), (sx * 0.44, 1.52), (sx * 0.42, 1.62)]), stroke(C("#3E1478", 200), 0.014))
    return _to_float(front), _to_float(back)
