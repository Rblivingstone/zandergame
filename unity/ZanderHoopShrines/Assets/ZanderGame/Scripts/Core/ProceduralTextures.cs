using UnityEngine;

namespace ZanderGame
{
    /// <summary>
    /// Generates the hardwood basketball-court floors (with painted lines) and the
    /// stone shrine wall texture at runtime, so the project needs no image files.
    /// </summary>
    public static class ProceduralTextures
    {
        const int PxPerMeter = 24;

        public struct CourtStyle
        {
            public bool HoopNorth, HoopSouth, CenterCircle;
            public Color Paint, Lines, WoodA, WoodB;
            public float ThreePointRadius;

            public static CourtStyle Default(Color paint) => new CourtStyle
            {
                HoopNorth = true,
                HoopSouth = false,
                CenterCircle = false,
                Paint = paint,
                Lines = Color.white,
                WoodA = Mat.Hex("#D9A066"),
                WoodB = Mat.Hex("#C98E55"),
                ThreePointRadius = 6f
            };
        }

        /// <summary>u runs along X (width), v along Z (length). v=1 is the NORTH end.</summary>
        public static Texture2D Court(float width, float length, CourtStyle style, int seed = 7)
        {
            int w = Mathf.Clamp(Mathf.RoundToInt(width * PxPerMeter), 16, 2048);
            int h = Mathf.Clamp(Mathf.RoundToInt(length * PxPerMeter), 16, 2048);
            var px = new Color32[w * h];
            var rng = new System.Random(seed);

            // --- planks running along Z ---
            float plankW = 0.25f;
            int plankCount = Mathf.CeilToInt(width / plankW) + 1;
            var plankTint = new float[plankCount];
            var plankJoint = new float[plankCount];
            for (int i = 0; i < plankCount; i++)
            {
                plankTint[i] = (float)rng.NextDouble();
                plankJoint[i] = (float)rng.NextDouble() * 3f;
            }
            for (int y = 0; y < h; y++)
            {
                float zm = y / (float)PxPerMeter;
                for (int x = 0; x < w; x++)
                {
                    float xm = x / (float)PxPerMeter;
                    int p = Mathf.FloorToInt(xm / plankW);
                    float t = plankTint[Mathf.Clamp(p, 0, plankCount - 1)];
                    Color c = Color.Lerp(style.WoodA, style.WoodB, t);
                    float grain = Mathf.PerlinNoise(xm * 3f + p * 13.1f, zm * 0.6f) * 0.08f - 0.04f;
                    c *= 1f + grain;
                    float inPlank = xm - p * plankW;
                    bool seam = inPlank < 0.012f;
                    float joint = Mathf.Repeat(zm + plankJoint[Mathf.Clamp(p, 0, plankCount - 1)], 3f);
                    if (seam || joint < 0.012f) c *= 0.82f;
                    c.a = 1f;
                    px[y * w + x] = c;
                }
            }

            var tex = new Tex(px, w, h);
            float lineW = 0.08f;
            float inset = 0.5f;
            Color paint = style.Paint;
            paint.a = 0.55f;

            // painted keys + hoop markings
            for (int end = 0; end < 2; end++)
            {
                bool north = end == 0;
                if (north && !style.HoopNorth) continue;
                if (!north && !style.HoopSouth) continue;
                float baseZ = north ? length - inset : inset;
                float dir = north ? -1f : 1f;
                float rimZ = baseZ + dir * 1.6f;
                float keyLen = 5.0f, keyW = 3.6f;
                float z0 = Mathf.Min(baseZ, baseZ + dir * keyLen), z1 = Mathf.Max(baseZ, baseZ + dir * keyLen);
                tex.FillRect(width / 2 - keyW / 2, z0, width / 2 + keyW / 2, z1, paint, true);
                tex.RectOutline(width / 2 - keyW / 2, z0, width / 2 + keyW / 2, z1, lineW, style.Lines);
                float ftZ = baseZ + dir * keyLen;
                tex.Circle(width / 2, ftZ, 1.8f, lineW, style.Lines, (dx, dz) => dz * dir >= 0);
                // three point arc (and straight corners)
                float r = style.ThreePointRadius;
                float cornerX = Mathf.Min(r, width / 2 - inset - 0.6f);
                tex.Circle(width / 2, rimZ, r, lineW, style.Lines,
                    (dx, dz) => dz * dir >= 0 && Mathf.Abs(dx) <= cornerX);
                float zc = rimZ;
                float zcEnd = baseZ;
                tex.FillRect(width / 2 - cornerX - lineW / 2, Mathf.Min(zc, zcEnd), width / 2 - cornerX + lineW / 2,
                    Mathf.Max(zc, zcEnd), style.Lines, false);
                tex.FillRect(width / 2 + cornerX - lineW / 2, Mathf.Min(zc, zcEnd), width / 2 + cornerX + lineW / 2,
                    Mathf.Max(zc, zcEnd), style.Lines, false);
            }

            // boundary
            tex.RectOutline(inset, inset, width - inset, length - inset, lineW, style.Lines);
            if (style.CenterCircle)
            {
                tex.FillRect(inset, length / 2 - lineW / 2, width - inset, length / 2 + lineW / 2, style.Lines, false);
                tex.Circle(width / 2, length / 2, 1.8f, lineW, style.Lines, null);
                Color c = style.Paint;
                c.a = 0.6f;
                tex.Disc(width / 2, length / 2, 1.8f - lineW, c);
            }

            var t2d = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = "Court", wrapMode = TextureWrapMode.Clamp };
            t2d.SetPixels32(px);
            t2d.Apply(true);
            t2d.anisoLevel = 4;
            return t2d;
        }

        static Texture2D _stone;

        public static Texture2D Stone()
        {
            if (_stone != null) return _stone;
            // hand-painted stone from blender/courts.py, if present
            var painted = Resources.Load<Texture2D>("Textures/StoneWall");
            if (painted != null)
            {
                painted.wrapMode = TextureWrapMode.Repeat;
                _stone = painted;
                return _stone;
            }
            const int n = 256;
            var px = new Color32[n * n];
            Color mortar = Mat.Hex("#2E3340");
            Color a = Mat.Hex("#4E5668"), b = Mat.Hex("#5C6578");
            var rng = new System.Random(3);
            float[] tint = new float[64];
            for (int i = 0; i < tint.Length; i++) tint[i] = (float)rng.NextDouble();
            for (int y = 0; y < n; y++)
            {
                int row = y / 32;
                int off = (row % 2) * 32;
                for (int x = 0; x < n; x++)
                {
                    int col = ((x + off) % n) / 64;
                    bool m = (y % 32) < 3 || ((x + off) % 64) < 3;
                    Color c = m ? mortar : Color.Lerp(a, b, tint[(row * 4 + col) % 64]);
                    c *= 1f + (Mathf.PerlinNoise(x * 0.08f, y * 0.08f) - 0.5f) * 0.15f;
                    c.a = 1;
                    px[y * n + x] = c;
                }
            }
            _stone = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = "Stone", wrapMode = TextureWrapMode.Repeat };
            _stone.SetPixels32(px);
            _stone.Apply(true);
            return _stone;
        }

        /// <summary>Tiny raster helper working in metres.</summary>
        class Tex
        {
            readonly Color32[] _px;
            readonly int _w, _h;

            public Tex(Color32[] px, int w, int h)
            {
                _px = px;
                _w = w;
                _h = h;
            }

            void Blend(int x, int y, Color c)
            {
                if (x < 0 || y < 0 || x >= _w || y >= _h) return;
                int i = y * _w + x;
                Color d = _px[i];
                Color o = Color.Lerp(d, c, c.a);
                o.a = 1;
                _px[i] = o;
            }

            public void FillRect(float x0, float z0, float x1, float z1, Color c, bool blend)
            {
                int ax = Mathf.RoundToInt(x0 * PxPerMeter), bx = Mathf.RoundToInt(x1 * PxPerMeter);
                int ay = Mathf.RoundToInt(z0 * PxPerMeter), by = Mathf.RoundToInt(z1 * PxPerMeter);
                for (int y = ay; y < by; y++)
                    for (int x = ax; x < bx; x++)
                    {
                        if (blend) Blend(x, y, c);
                        else Blend(x, y, new Color(c.r, c.g, c.b, 1));
                    }
            }

            public void RectOutline(float x0, float z0, float x1, float z1, float t, Color c)
            {
                FillRect(x0, z0, x1, z0 + t, c, false);
                FillRect(x0, z1 - t, x1, z1, c, false);
                FillRect(x0, z0, x0 + t, z1, c, false);
                FillRect(x1 - t, z0, x1, z1, c, false);
            }

            public void Circle(float cx, float cz, float r, float t, Color c, System.Func<float, float, bool> keep)
            {
                int x0 = Mathf.FloorToInt((cx - r - t) * PxPerMeter), x1 = Mathf.CeilToInt((cx + r + t) * PxPerMeter);
                int y0 = Mathf.FloorToInt((cz - r - t) * PxPerMeter), y1 = Mathf.CeilToInt((cz + r + t) * PxPerMeter);
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        float dx = x / (float)PxPerMeter - cx, dz = y / (float)PxPerMeter - cz;
                        float d = Mathf.Sqrt(dx * dx + dz * dz);
                        if (Mathf.Abs(d - r) > t / 2) continue;
                        if (keep != null && !keep(dx, dz)) continue;
                        Blend(x, y, new Color(c.r, c.g, c.b, 1));
                    }
            }

            public void Disc(float cx, float cz, float r, Color c)
            {
                int x0 = Mathf.FloorToInt((cx - r) * PxPerMeter), x1 = Mathf.CeilToInt((cx + r) * PxPerMeter);
                int y0 = Mathf.FloorToInt((cz - r) * PxPerMeter), y1 = Mathf.CeilToInt((cz + r) * PxPerMeter);
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        float dx = x / (float)PxPerMeter - cx, dz = y / (float)PxPerMeter - cz;
                        if (dx * dx + dz * dz <= r * r) Blend(x, y, c);
                    }
            }
        }
    }
}
