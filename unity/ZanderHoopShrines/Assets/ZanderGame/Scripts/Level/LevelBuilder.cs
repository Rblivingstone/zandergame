using System.Collections.Generic;
using UnityEngine;

namespace ZanderGame
{
    /// <summary>
    /// Builds the whole game world at runtime. The areas run south -> north along +Z:
    ///
    ///   Lobby (z 0-14)         practice hoop, controls sign
    ///   7 training shrines     JUMP, DASH, DROP, SHOOT, DUNK, HANG, STEAL - each teaches
    ///                          one move needed against the boss
    ///   Championship Court     one-ball game vs KING SWAT
    /// </summary>
    public class LevelBuilder
    {
        readonly Transform _root;
        public readonly List<Room> Rooms = new List<Room>();
        public BossArena Arena { get; private set; }

        static readonly Color Orange = new Color(1f, 0.5f, 0.1f);
        static readonly Color Red = new Color(1f, 0.25f, 0.25f);
        static readonly Color Blue = new Color(0.25f, 0.55f, 1f);
        static readonly Color Green = new Color(0.25f, 0.95f, 0.4f);
        static readonly Color Purple = new Color(0.75f, 0.35f, 1f);
        static readonly Color Gold = new Color(1f, 0.8f, 0.2f);

        public LevelBuilder(Transform root) => _root = root;

        public void BuildAll()
        {
            BuildLobby();
            BuildShrines();
            BuildArena();
        }

        // =====================================================================
        // Shared construction helpers
        // =====================================================================
        GameObject Area(string name) => Build.Group(name, _root);

        /// <summary>
        /// A basketball court floor. Uses the hand-painted court Resources/Textures/Court_[name]
        /// (made by blender/courts.py); falls back to a court generated in code.
        /// </summary>
        void CourtFloor(string name, Transform parent, float x0, float x1, float z0, float z1, float y, Color paint,
            bool hoopNorth, bool hoopSouth = false, bool center = false, float threePt = 6f)
        {
            var painted = Resources.Load<Texture2D>("Textures/Court_" + name);
            if (painted != null)
            {
                painted.wrapMode = TextureWrapMode.Clamp;
                painted.anisoLevel = 8;
                Build.Floor("Floor", parent, new Vector3((x0 + x1) / 2, y, (z0 + z1) / 2), x1 - x0, z1 - z0,
                    Mat.Textured(painted, Vector2.one, 0.55f));
                return;
            }
            var style = ProceduralTextures.CourtStyle.Default(paint);
            style.HoopNorth = hoopNorth;
            style.HoopSouth = hoopSouth;
            style.CenterCircle = center;
            style.ThreePointRadius = threePt;
            var tex = ProceduralTextures.Court(x1 - x0, z1 - z0, style, Mathf.RoundToInt(z0));
            var mat = Mat.Textured(tex, Vector2.one, 0.55f);
            Build.Floor("Floor", parent, new Vector3((x0 + x1) / 2, y, (z0 + z1) / 2), x1 - x0, z1 - z0, mat);
        }

        /// <summary>Painted flagstone floor (corridors, ledges); plain colour if the texture is missing.</summary>
        static Material Flagstones(float w, float l)
        {
            var tex = Resources.Load<Texture2D>("Textures/Flagstones");
            if (tex == null) return Mat.Color("#4E5668", 0.2f);
            tex.wrapMode = TextureWrapMode.Repeat;
            return Mat.Textured(tex, new Vector2(w / 2f, l / 2f), 0.3f);
        }

        void PlainFloor(Transform parent, float x0, float x1, float z0, float z1, float y, string hex)
        {
            Build.Floor("Floor", parent, new Vector3((x0 + x1) / 2, y, (z0 + z1) / 2), x1 - x0, z1 - z0,
                Flagstones(x1 - x0, z1 - z0));
        }

        /// <summary>
        /// A stone wall segment with a dark painted base course and a light capstone ledge
        /// on top (the anime shrine look). Trims have no colliders.
        /// </summary>
        static void StoneWall(string name, Transform parent, Vector3 center, Vector3 size, bool baseCourse = true,
            bool cap = true)
        {
            Build.StoneBox(name, parent, center, size);
            float bottom = center.y - size.y / 2, top = center.y + size.y / 2;
            if (baseCourse && size.y > 1.2f)
                Build.Box(name + "_Base", parent, new Vector3(center.x, bottom + 0.22f, center.z),
                    new Vector3(size.x + 0.12f, 0.44f, size.z + 0.12f), Mat.Color("#363C4D", 0.2f), false);
            if (cap)
            {
                Build.Box(name + "_Cap", parent, new Vector3(center.x, top + 0.09f, center.z),
                    new Vector3(size.x + 0.22f, 0.18f, size.z + 0.22f), Mat.Color("#7C879E", 0.2f), false);
                Build.Box(name + "_CapTrim", parent, new Vector3(center.x, top - 0.04f, center.z),
                    new Vector3(size.x + 0.08f, 0.08f, size.z + 0.08f), Mat.Color("#2C3242", 0.2f), false);
            }
        }

        /// <summary>
        /// Four walls around [x0,x1]x[z0,z1]. southGap/northGap = width of a doorway
        /// centred on x=0 (0 = solid); gapBottom/gapTop are the doorway's height range.
        /// </summary>
        void Walls(Transform parent, float x0, float x1, float z0, float z1, float yBottom, float height,
            float southGap, float northGap, float southGapY0 = 0, float southGapY1 = 4.3f,
            float northGapY0 = 0, float northGapY1 = 4.3f)
        {
            const float t = 1f;
            float yTop = yBottom + height;
            float yMid = (yBottom + yTop) / 2;
            // east / west
            StoneWall("WallW", parent, new Vector3(x0 - t / 2, yMid, (z0 + z1) / 2), new Vector3(t, height, z1 - z0 + 2 * t));
            StoneWall("WallE", parent, new Vector3(x1 + t / 2, yMid, (z0 + z1) / 2), new Vector3(t, height, z1 - z0 + 2 * t));
            EndWall(parent, x0, x1, z0 - t / 2, yBottom, yTop, southGap, southGapY0, southGapY1, "S");
            EndWall(parent, x0, x1, z1 + t / 2, yBottom, yTop, northGap, northGapY0, northGapY1, "N");
            // glowing trim strip along the top of the side walls
            var glow = Mat.Glow(Mat.Hex("#FFB347"), Mat.Hex("#FF9A1F"), 1.2f);
            Build.Box("TrimW", parent, new Vector3(x0 + 0.02f, yBottom + 2.6f, (z0 + z1) / 2), new Vector3(0.06f, 0.12f, z1 - z0), glow, false);
            Build.Box("TrimE", parent, new Vector3(x1 - 0.02f, yBottom + 2.6f, (z0 + z1) / 2), new Vector3(0.06f, 0.12f, z1 - z0), glow, false);
        }

        void EndWall(Transform parent, float x0, float x1, float zc, float yBottom, float yTop, float gap, float gy0,
            float gy1, string tag)
        {
            const float t = 1f;
            float h = yTop - yBottom;
            if (gap <= 0)
            {
                StoneWall("Wall" + tag, parent, new Vector3((x0 + x1) / 2, (yBottom + yTop) / 2, zc), new Vector3(x1 - x0, h, t));
                return;
            }
            float g = gap / 2;
            StoneWall("Wall" + tag + "L", parent, new Vector3((x0 - g) / 2, (yBottom + yTop) / 2, zc), new Vector3(-g - x0, h, t));
            StoneWall("Wall" + tag + "R", parent, new Vector3((x1 + g) / 2, (yBottom + yTop) / 2, zc), new Vector3(x1 - g, h, t));
            if (gy1 < yTop)
                StoneWall("Wall" + tag + "Top", parent, new Vector3(0, (gy1 + yTop) / 2, zc), new Vector3(gap, yTop - gy1, t), false);
            if (gy0 > yBottom)
                Build.StoneBox("Wall" + tag + "Bottom", parent, new Vector3(0, (yBottom + gy0) / 2, zc), new Vector3(gap, gy0 - yBottom, t));
        }

        // ------------------------------------------------------------ roofs --
        static void NoShadows(GameObject go)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>())
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>
        /// A painted coffered ceiling over [x0,x1]x[z0,z1] at height yTop, with wooden beams,
        /// glowing skylight panels and hanging lanterns (the room's real light sources).
        /// The roof casts no shadows so the sun still fills the room softly.
        /// </summary>
        void Roof(Transform parent, float x0, float x1, float z0, float z1, float yTop, bool lanterns = true,
            string lanternHex = "#FFB347", float lanternSpacing = 7.5f)
        {
            var roof = Build.Group("Roof", parent).transform;
            float w = x1 - x0 + 2f, l = z1 - z0 + 2f;
            float cx = (x0 + x1) / 2, cz = (z0 + z1) / 2;
            var tex = Resources.Load<Texture2D>("Textures/Ceiling");
            Material m;
            if (tex != null)
            {
                tex.wrapMode = TextureWrapMode.Repeat;
                m = Mat.Textured(tex, new Vector2(w / 4f, l / 4f), 0.2f);
            }
            else m = Mat.Color("#2A2F3E", 0.1f);
            var ceil = Build.Floor("Ceiling", roof, new Vector3(cx, yTop, cz), w, l, m, 0.6f);
            ceil.transform.localRotation = Quaternion.Euler(180, 0, 0);   // face down into the room
            // beams across the room every 4 m
            var wood = Mat.Color("#5B3A22", 0.2f);
            for (float z = z0 + 2f; z < z1 - 1f; z += 4f)
                Build.Box("Beam", roof, new Vector3(cx, yTop - 0.2f, z), new Vector3(w, 0.4f, 0.45f), wood, false);
            Build.Box("BeamLong", roof, new Vector3(cx, yTop - 0.25f, cz), new Vector3(0.5f, 0.5f, l), wood, false);
            // skylights: glowing panels along the middle (visual source of the daylight)
            var sky = Mat.Glow(Mat.Hex("#FFF4D6"), Mat.Hex("#FFE8B0"), 1.4f);
            for (float z = z0 + 4f; z < z1 - 2f; z += 8f)
                foreach (float x in new[] { cx - w / 4, cx + w / 4 })
                    Build.Box("Skylight", roof, new Vector3(x, yTop - 0.02f, z), new Vector3(2.2f, 0.05f, 2.2f), sky, false);
            if (lanterns)
            {
                int nx = Mathf.Max(1, Mathf.RoundToInt((x1 - x0) / lanternSpacing));
                int nz = Mathf.Max(1, Mathf.RoundToInt((z1 - z0) / lanternSpacing));
                for (int i = 0; i < nx; i++)
                    for (int k = 0; k < nz; k++)
                    {
                        float x = x0 + (i + 0.5f) * (x1 - x0) / nx;
                        float z = z0 + (k + 0.5f) * (z1 - z0) / nz;
                        Lantern(roof, new Vector3(x, yTop, z), Mathf.Min(2.4f, (yTop - 6.2f)), lanternHex);
                    }
            }
            NoShadows(roof.gameObject);
        }

        void Lantern(Transform parent, Vector3 ceilingPoint, float drop, string hex)
        {
            drop = Mathf.Max(0.6f, drop);
            var g = Build.Group("Lantern", parent, ceilingPoint).transform;
            var dark = Mat.Color("#2C2420", 0.2f);
            var glow = Mat.Glow(Mat.Hex(hex), Mat.Hex(hex), 2.2f);
            Build.Box("Chain", g, new Vector3(0, -drop / 2, 0), new Vector3(0.05f, drop, 0.05f), dark, false);
            Build.Prim(PrimitiveType.Cylinder, "CapTop", g, new Vector3(0, -drop - 0.05f, 0), new Vector3(0.5f, 0.06f, 0.5f), dark, false);
            Build.Prim(PrimitiveType.Sphere, "Glow", g, new Vector3(0, -drop - 0.4f, 0), new Vector3(0.55f, 0.65f, 0.55f), glow, false);
            Build.Prim(PrimitiveType.Cylinder, "CapBottom", g, new Vector3(0, -drop - 0.75f, 0), new Vector3(0.36f, 0.05f, 0.36f), dark, false);
            Build.PointLight(g, new Vector3(0, -drop - 0.6f, 0), Mat.Hex(hex), 12f, 1.0f);
        }

        /// <summary>A hanging cloth banner (alpha-cut painted texture) on a wall. yaw: direction the banner faces.</summary>
        static void Banner(Transform parent, string texName, Vector3 center, float w, float h, float yaw)
        {
            var tex = Resources.Load<Texture2D>("Textures/" + texName);
            if (tex == null) return;
            var go = new GameObject("Banner_" + texName);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localRotation = Quaternion.Euler(0, yaw + 180f, 0);
            var mesh = new Mesh { name = "BannerQuad" };
            mesh.vertices = new[]
            {
                new Vector3(-w / 2, -h / 2, 0), new Vector3(w / 2, -h / 2, 0), new Vector3(w / 2, h / 2, 0), new Vector3(-w / 2, h / 2, 0)
            };
            // viewed from local -Z, +X is on the viewer's right, so u runs with x
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var m = Mat.Textured(tex, Vector2.one, 0.1f);
            if (m.HasProperty("_Cutoff")) m.SetFloat("_Cutoff", 0.5f);
            go.AddComponent<MeshRenderer>().sharedMaterial = m;
            // a little gold rod on top
            Build.Box("Rod", go.transform, new Vector3(0, h / 2 + 0.04f, -0.04f), new Vector3(w + 0.3f, 0.08f, 0.08f),
                Mat.Color("#FFC21A", 0.6f, 0.6f), false);
        }

        /// <summary>Banners on the side walls of a room (inner faces at x0 / x1).</summary>
        void SideBanners(Transform parent, float x0, float x1, float y, float[] zs, string west, string east,
            float w = 1.6f, float h = 3.2f)
        {
            foreach (float z in zs)
            {
                Banner(parent, west, new Vector3(x0 + 0.06f, y, z), w, h, 90f);
                Banner(parent, east, new Vector3(x1 - 0.06f, y, z), w, h, -90f);
            }
        }

        void Corridor(Transform parent, float z0, float z1, float y, float width = 4.4f)
        {
            PlainFloor(parent, -width / 2, width / 2, z0, z1, y, "#4E5668");
            StoneWall("CorW", parent, new Vector3(-width / 2 - 0.5f, y + 3f, (z0 + z1) / 2), new Vector3(1, 6, z1 - z0));
            StoneWall("CorE", parent, new Vector3(width / 2 + 0.5f, y + 3f, (z0 + z1) / 2), new Vector3(1, 6, z1 - z0));
            Roof(parent, -width / 2 - 1f, width / 2 + 1f, z0, z1, y + 6f, false);
            Lantern(parent, new Vector3(0, y + 6f, (z0 + z1) / 2), 1.2f, "#FFB347");
        }

        void Pillars(Transform parent, float x0, float x1, float z0, float z1, float y)
        {
            foreach (var p in new[] { new Vector3(x0 + 1.2f, y, z0 + 1.2f), new Vector3(x1 - 1.2f, y, z0 + 1.2f),
                         new Vector3(x0 + 1.2f, y, z1 - 1.2f), new Vector3(x1 - 1.2f, y, z1 - 1.2f) })
            {
                var m = ModelLibrary.Spawn("ShrinePillar", parent, p);
                if (m == null)
                {
                    Build.StoneBox("Pillar", parent, p + Vector3.up * 2.5f, new Vector3(1, 5, 1));
                    Build.Box("Rune", parent, p + new Vector3(0, 3.2f, 0), new Vector3(1.04f, 0.9f, 1.04f),
                        Mat.Glow(Mat.Hex("#FFB347"), Mat.Hex("#FF9A1F"), 1.5f), false);
                }
                Build.Collider("PillarCol", parent, p + Vector3.up * 2.5f, new Vector3(1.1f, 5f, 1.1f));
                Build.PointLight(parent, p + new Vector3(0, 5.5f, 0), Mat.Hex("#FFB347"), 9f, 0.9f);
            }
        }

        void Sign(Transform parent, string text, Vector3 pos, float size, Color color, float yaw = 0)
        {
            var tm = Build.Label(text, parent, pos, size, color, yaw);
            // dark backing plate for readability
            var lines = text.Split('\n');
            int longest = 1;
            foreach (var l in lines) longest = Mathf.Max(longest, l.Length);
            float w = longest * size * 0.5f + 0.6f;
            float h = lines.Length * size * 1.1f + 0.4f;
            var back = Build.Box("SignBack", parent, pos, new Vector3(w, h, 0.05f), Mat.Color("#1B1F2A", 0.1f), false,
                Quaternion.Euler(0, yaw, 0));
            back.transform.position += back.transform.forward * 0.06f;
        }

        Room NewRoom(string name, string subtitle, float z0, float z1, Vector3 spawn, int difficulty)
        {
            var r = new Room
            {
                Index = Rooms.Count, Name = name, Subtitle = subtitle, ZMin = z0, ZMax = z1, Spawn = spawn,
                SpawnYaw = 0, Difficulty = difficulty
            };
            Rooms.Add(r);
            return r;
        }

        // =====================================================================
        // LOBBY
        // =====================================================================
        void BuildLobby()
        {
            var a = Area("Lobby").transform;
            const float x0 = -8, x1 = 8, z0 = 0, z1 = 14;
            CourtFloor("Lobby", a, x0, x1, z0, z1, 0, Mat.Hex("#1F5FD6"), false);
            Walls(a, x0, x1, z0, z1, 0, 9, 0, 3.6f);
            Roof(a, x0, x1, z0, z1, 9f);
            SideBanners(a, x0, x1, 5.6f, new[] { 10.5f }, "Banner_Zander", "Banner_Zander");
            Pillars(a, x0, x1, z0, z1, 0);
            var room = NewRoom("Lobby", "Warm up those shooting hands!", -10, 16, new Vector3(0, 0.05f, 3), 0);
            room.IsLobby = true;
            room.Objective = "Practice! Grab the ball, hold SHOOT and let go in the GREEN zone. Then head north!";
            room.Hints = new[]
            {
                "Walk into a basketball to pick it up.",
                "Hold {SHOOT}. Let go when the meter is in the GREEN zone for a PERFECT shot!",
                "Get close to a hoop with the ball and press {DUNK} to DUNK! Hold {DUNK} to hang on the rim.",
                "When you're ready, walk through the door to the north."
            };
            var hoop = Hoop.Create("Practice Hoop", a, new Vector3(-6.4f, 0, 8), 90, Orange, HoopMode.Counter, "PRACTICE");
            room.Hoops.Add(hoop);
            room.Dispensers.Add(BallDispenser.Create("PracticeRack", a, new Vector3(-2, 0, 6.5f), 2));
            Sign(a, "ZANDER'S\nHOOP SHRINES", new Vector3(0, 5.6f, z1 - 0.12f), 0.9f, Gold, 0);
            Sign(a, "CONTROLLER:  MOVE: LEFT STICK   LOOK: RIGHT STICK   JUMP: A\nSHOOT: HOLD RT   DUNK: X   DASH: LT   DROP: B   USE: Y\nKEYBOARD:  WASD  SPACE  SHIFT  LEFT MOUSE  C=DUNK  Q  E  H",
                new Vector3(x1 - 0.12f, 3.2f, 7), 0.32f, Color.white, 90);
            Sign(a, "Learn 7 moves in 7 shrines...\nthen beat KING SWAT!", new Vector3(x0 + 0.12f, 3.4f, 4), 0.4f, Gold, -90);
            Corridor(a, 14, 18, 0);
        }

        // =====================================================================
        // THE SEVEN TRAINING SHRINES
        // Each shrine teaches ONE move Zander needs against King Swat, in an order
        // where every room builds on the last:
        //   1 JUMP  2 DASH  3 DROP  4 SHOOT  5 DUNK  6 HANG  7 STEAL
        // Rooms are laid out south -> north along +Z, joined by short corridors.
        // =====================================================================
        float _z = 18f;          // where the next shrine starts (after the lobby corridor)
        int _shrineNo;

        class Shell
        {
            public Transform A;
            public Room Room;
            public float X0, X1, Z0, Z1;
        }

        /// <summary>Floor, walls, roof, pillars, banners, sign and room record for one shrine.</summary>
        Shell Shrine(string key, string title, string teaches, float w, float l, string paint, bool hoopNorth,
            float wallH = 12f, float pit0 = 0f, float pit1 = 0f)
        {
            _shrineNo++;
            var s = new Shell { A = Area("Shrine" + _shrineNo + "_" + key).transform, X0 = -w / 2, X1 = w / 2, Z0 = _z, Z1 = _z + l };
            var a = s.A;
            bool pit = pit1 > pit0;
            const float pitY = -9f;
            if (pit)
            {
                CourtFloor(key + "South", a, s.X0, s.X1, s.Z0, s.Z0 + pit0, 0, Mat.Hex(paint), false);
                CourtFloor(key + "North", a, s.X0, s.X1, s.Z0 + pit1, s.Z1, 0, Mat.Hex(paint), hoopNorth);
                var pitMat = Mat.Glow(Mat.Hex("#0B1630"), Mat.Hex("#1E4BFF"), 0.6f);
                Build.Box("PitGlow", a, new Vector3(0, pitY, s.Z0 + (pit0 + pit1) / 2), new Vector3(w, 0.2f, pit1 - pit0), pitMat);
                Build.StoneBox("PitWallS", a, new Vector3(0, pitY / 2 - 0.5f, s.Z0 + pit0 + 0.01f), new Vector3(w, -pitY - 1f, 0.02f));
                Build.StoneBox("PitWallN", a, new Vector3(0, pitY / 2 - 0.5f, s.Z0 + pit1 - 0.01f), new Vector3(w, -pitY - 1f, 0.02f));
                Build.PointLight(a, new Vector3(0, -3, s.Z0 + (pit0 + pit1) / 2), Mat.Hex("#1E4BFF"), 14f, 2f);
                Walls(a, s.X0, s.X1, s.Z0, s.Z1, pitY, wallH - pitY, 3.6f, 3.0f, 0, 4.3f, 0, 4.3f);
            }
            else
            {
                CourtFloor(key, a, s.X0, s.X1, s.Z0, s.Z1, 0, Mat.Hex(paint), hoopNorth);
                Walls(a, s.X0, s.X1, s.Z0, s.Z1, 0, wallH, 3.6f, 3.0f);
                Pillars(a, s.X0, s.X1, s.Z0, s.Z1, 0);
            }
            Roof(a, s.X0, s.X1, s.Z0, s.Z1, wallH);
            SideBanners(a, s.X0, s.X1, wallH - 4.5f, new[] { s.Z0 + 4f }, "Banner_Shrine", "Banner_Shrine");
            Sign(a, "SHRINE " + _shrineNo + "\n" + title.ToUpperInvariant(), new Vector3(0, 5.6f, s.Z0 + 0.12f), 0.7f, Gold, 180);
            s.Room = NewRoom("Shrine " + _shrineNo + ": " + title, teaches, s.Z0 - 2f, s.Z1 + 2f, new Vector3(0, 0.05f, s.Z0 + 2f), _shrineNo);
            Corridor(a, s.Z1, s.Z1 + 4f, 0);
            _z = s.Z1 + 4f;
            return s;
        }

        Gate ExitGate(Shell s, List<Signal> inputs, bool latch = true)
        {
            var g = Gate.Create("ExitGate", s.A, new Vector3(0, 0, s.Z1 + 0.5f), 0, inputs, latch);
            s.Room.Exit = g;
            return g;
        }

        void Step(Transform a, string name, Vector3 center, Vector3 size)
        {
            Build.StoneBox(name, a, center, size);
            Build.Box(name + "_Top", a, center + Vector3.up * (size.y / 2 + 0.01f), new Vector3(size.x, 0.02f, size.z),
                Mat.Color("#7C879E", 0.2f), false);
        }

        void BuildShrines()
        {
            BuildJumpShrine();
            BuildDashShrine();
            BuildDropShrine();
            BuildShootShrine();
            BuildDunkShrine();
            BuildHangShrine();
            BuildStealShrine();
        }

        // ---------------------------------------------------------------- 1 JUMP
        void BuildJumpShrine()
        {
            var s = Shrine("Jump", "Hop To It", "JUMP", 18, 26, "#1F63E0", false);
            var a = s.A;
            float z = s.Z0;
            s.Room.Objective = "Press {JUMP} to JUMP. Climb up and stand on all 3 blue plates to open the gate.";
            s.Room.Hints = new[]
            {
                "Press {JUMP} to jump. You can steer while you're in the air!",
                "Climb the stone steps one at a time - each one is a small jump up.",
                "From the top step, jump across the gap to the other platform.",
                "Jump over the low walls to reach the last plate."
            };
            // staircase -> plate 1
            Step(a, "Step1", new Vector3(-4.5f, 0.4f, z + 7f), new Vector3(3f, 0.8f, 3f));
            Step(a, "Step2", new Vector3(-4.5f, 0.8f, z + 10f), new Vector3(3f, 1.6f, 3f));
            Step(a, "Step3", new Vector3(-4.5f, 1.2f, z + 13f), new Vector3(3f, 2.4f, 3f));
            var p1 = PressurePlate.Create("Plate1", a, new Vector3(-4.5f, 2.4f, z + 13f), PlateKind.Feet, latch: true);
            // gap jump -> plate 2
            Step(a, "Island", new Vector3(1.0f, 1.2f, z + 13f), new Vector3(3f, 2.4f, 3f));
            var p2 = PressurePlate.Create("Plate2", a, new Vector3(1.0f, 2.4f, z + 13f), PlateKind.Feet, latch: true);
            // hurdles -> plate 3
            Step(a, "HurdleLow", new Vector3(0, 0.45f, z + 18.5f), new Vector3(18f, 0.9f, 0.6f));
            Step(a, "HurdleHigh", new Vector3(0, 0.6f, z + 21f), new Vector3(18f, 1.2f, 0.6f));
            var p3 = PressurePlate.Create("Plate3", a, new Vector3(5f, 0, z + 23.5f), PlateKind.Feet, latch: true);
            ExitGate(s, new List<Signal> { p1, p2, p3 });
        }

        // ---------------------------------------------------------------- 2 DASH
        void BuildDashShrine()
        {
            var s = Shrine("Dash", "Beat the Gate", "DASH + STAMINA", 16, 46, "#E8772E", false);
            var a = s.A;
            float z = s.Z0;
            s.Room.Objective = "Hold {SPRINT} to DASH. Step on a plate, then dash through its gate before it shuts!";
            s.Room.Hints = new[]
            {
                "Walking is too slow - hold {SPRINT} to DASH. Watch the stamina wheel above your head.",
                "The wheel refills as soon as you let go of {SPRINT}.",
                "The second plate makes you stand still for a moment - that's your chance to refill stamina!",
                "If the wheel runs out you're OUT OF BREATH and slow. Let it refill completely, then try again."
            };
            var t1 = TimedSwitch.Create("DashPlate1", a, new Vector3(0, 0, z + 3f), 0f, 3.2f);
            // inner wall with a timed gate
            StoneWall("CrossW", a, new Vector3((s.X0 - 2f) / 2f, 3f, z + 24f), new Vector3(-s.X0 - 2f, 6f, 1f));
            StoneWall("CrossE", a, new Vector3((s.X1 + 2f) / 2f, 3f, z + 24f), new Vector3(s.X1 - 2f, 6f, 1f));
            var g1 = Gate.Create("TimedGate", a, new Vector3(0, 0, z + 24f), 0, new List<Signal> { t1 }, false);
            g1.Panel.Speed = 9f;
            g1.LatchWhenPassed = true;
            var t2 = TimedSwitch.Create("DashPlate2", a, new Vector3(0, 0, z + 26.5f), 1.5f, 3.0f);
            var exit = ExitGate(s, new List<Signal> { t2 }, false);
            exit.Panel.Speed = 9f;
            exit.LatchWhenPassed = true;
            Sign(a, "DASH!", new Vector3(s.X0 + 0.12f, 3f, z + 12f), 0.6f, Gold, -90);
        }

        // ---------------------------------------------------------------- 3 DROP
        void BuildDropShrine()
        {
            const float pit0 = 11f, pit1 = 20f;
            var s = Shrine("Drop", "Hand-Off", "DROP + PICK UP", 20, 32, "#2FBF68", false, 12f, pit0, pit1);
            var a = s.A;
            float z = s.Z0;
            s.Room.Objective = "Press {DROP} next to an ORANGE plate to put a ball on it. Fill both far plates!";
            s.Room.Hints = new[]
            {
                "Walk into a ball to pick it up. Stand next to an ORANGE plate and press {DROP} to place it.",
                "A ball on the first plate raises the bridge. Take it off and the bridge sinks!",
                "The rack makes a new ball after you take one. Carry more balls across the bridge.",
                "Need a ball back? Stand by its plate and press {USE} to pick it up."
            };
            var rack = BallDispenser.Create("Rack", a, new Vector3(6, 0, z + 4f), 3);
            rack.RespawnDelay = 1.2f;
            s.Room.Dispensers.Add(rack);
            var bridgePlate = PressurePlate.Create("BridgePlate", a, new Vector3(-6, 0, z + 7f), PlateKind.Ball);
            var bridge = Build.Group("Bridge", a, new Vector3(0, -6f, z + (pit0 + pit1) / 2));
            var deck = new Vector3(4, 0.5f, pit1 - pit0 + 0.6f);
            if (ModelLibrary.Spawn("Bridge", bridge.transform, Vector3.zero) != null)
                Build.Collider("DeckCol", bridge.transform, Vector3.zero, deck);
            else Build.Box("Deck", bridge.transform, Vector3.zero, deck, Mat.Color("#2C3E66", 0.4f));
            bridge.AddComponent<Rigidbody>().isKinematic = true;
            var bm = bridge.AddComponent<SignalMover>();
            bm.Inputs.Inputs.Add(bridgePlate);
            bm.Latch = false;
            bm.OffLocal = new Vector3(0, -6f, z + (pit0 + pit1) / 2);
            bm.OnLocal = new Vector3(0, -0.25f, z + (pit0 + pit1) / 2);
            bm.Speed = 5f;
            var pb = PressurePlate.Create("PlateB", a, new Vector3(-5, 0, z + 25f), PlateKind.Ball);
            var pc = PressurePlate.Create("PlateC", a, new Vector3(5, 0, z + 27f), PlateKind.Ball);
            ExitGate(s, new List<Signal> { pb, pc });
        }

        // --------------------------------------------------------------- 4 SHOOT
        void BuildShootShrine()
        {
            var s = Shrine("Shoot", "Sharpshooter", "SHOOT", 20, 32, "#A65BE0", true);
            var a = s.A;
            float z = s.Z0;
            s.Room.Objective = "Hold {SHOOT}, let go in the GREEN. Light the CLOSE, LONG and MOVING hoops.";
            s.Room.Hints = new[]
            {
                "Hold {SHOOT} and watch the meter. Let go when it's in the GREEN for a perfect shot.",
                "Point the camera at a hoop - the green brackets show what you're aiming at.",
                "The LONG hoop only counts shots from behind its glowing line.",
                "The MOVING hoop slides - let go just as it starts moving toward you."
            };
            var rack = BallDispenser.Create("Rack", a, new Vector3(0, 0, z + 6f), 2);
            rack.RespawnDelay = 0.6f;
            s.Room.Dispensers.Add(rack);
            var close = Hoop.Create("Close Hoop", a, new Vector3(s.X0 + 1.6f, 0, z + 13f), 90, Green, HoopMode.Latch, "CLOSE");
            var spot = Mat.Glow(Green, Green, 1.2f);
            TimedSwitch.FloorRing(a, new Vector3(s.X0 + 5.5f, 0.02f, z + 13f), 0.8f, spot, 0, 360, 20);
            var lng = Hoop.Create("Long Hoop", a, new Vector3(-4.5f, 0, s.Z1 - 1.6f), 180, Orange, HoopMode.Latch, "LONG");
            lng.MinShotDistance = 6.5f;
            var line = Mat.Glow(Orange, Orange, 1.5f);
            TimedSwitch.FloorRing(a, new Vector3(-4.5f, 0.02f, s.Z1 - 1.6f), 6.5f, line, 125f, 235f, 32);
            var rigGo = Build.Group("MovingHoopRig", a, new Vector3(5f, 0, z + 21f));
            var rig = rigGo.AddComponent<MovingHoopRig>();
            rig.Amplitude = 2.5f;
            rig.Speed = 0.8f;
            rig.Moving = true;
            var moving = Hoop.Create("Moving Hoop", rigGo.transform, Vector3.zero, 180, Blue, HoopMode.Latch, "MOVING");
            s.Room.Hoops.Add(close);
            s.Room.Hoops.Add(lng);
            s.Room.Hoops.Add(moving);
            ExitGate(s, new List<Signal> { close, lng, moving });
        }

        // ---------------------------------------------------------------- 5 DUNK
        void BuildDunkShrine()
        {
            var s = Shrine("Dunk", "Rim Rocker", "DUNK", 20, 30, "#FF5D8F", true);
            var a = s.A;
            float z = s.Z0;
            s.Room.Objective = "Get close with the ball and press {DUNK}. Dunk all 3 DUNK ONLY hoops!";
            s.Room.Hints = new[]
            {
                "Carry a ball close to a hoop and press {DUNK} - Zander leaps up and slams it!",
                "The platform hoop is too high from the floor. Jump onto the platform first.",
                "The tallest hoop needs the BOUNCE pad: bounce up, then press {DUNK} in the air.",
                "Sometimes a dunk clangs off the rim - just try again!"
            };
            var rack = BallDispenser.Create("Rack", a, new Vector3(0, 0, z + 5f), 2);
            s.Room.Dispensers.Add(rack);
            var d1 = Hoop.Create("Floor Dunk", a, new Vector3(s.X0 + 1.6f, 0, z + 12f), 90, Purple, HoopMode.Latch, "DUNK 1");
            d1.DunkOnly = true;
            Step(a, "DunkStep", new Vector3(2.0f, 0.4f, z + 12f), new Vector3(1.6f, 0.8f, 3f));
            Step(a, "DunkPlatform", new Vector3(5.6f, 0.8f, z + 12f), new Vector3(5.6f, 1.6f, 5f));
            var d2 = Hoop.Create("Platform Dunk", a, new Vector3(7.0f, 1.6f, z + 12f), -90, Purple, HoopMode.Latch, "DUNK 2");
            d2.DunkOnly = true;
            BouncePad.Create("Bounce Pad", a, new Vector3(0, 0, z + 20.4f), 5.8f);
            StoneWall("DunkTower", a, new Vector3(0, 1.7f, z + 22.9f), new Vector3(1.4f, 3.4f, 1.4f));
            var d3 = Hoop.Create("High Dunk", a, new Vector3(0, 3.4f, z + 21.7f), 180, Purple, HoopMode.Latch, "DUNK 3");
            d3.DunkOnly = true;
            s.Room.Hoops.Add(d1);
            s.Room.Hoops.Add(d2);
            s.Room.Hoops.Add(d3);
            ExitGate(s, new List<Signal> { d1, d2, d3 });
        }

        // ---------------------------------------------------------------- 6 HANG
        void BuildHangShrine()
        {
            var s = Shrine("Hang", "Hang Time", "HANG ON THE RIM", 20, 30, "#16A3A3", true);
            var a = s.A;
            float z = s.Z0;
            s.Room.Objective = "Dunk, then KEEP HOLDING {DUNK} to hang. Hang for 2, 4 and 6 seconds!";
            s.Room.Hints = new[]
            {
                "After you dunk, keep holding {DUNK} to hang on the rim.",
                "Each hoop shows a countdown while you hang. Let go too early and it resets.",
                "The middle hoop is on a platform - jump up there first.",
                "The tall hoop needs the BOUNCE pad, then dunk AND hang!"
            };
            var rack = BallDispenser.Create("Rack", a, new Vector3(0, 0, z + 5f), 2);
            s.Room.Dispensers.Add(rack);
            var h1 = Hoop.Create("Hang 2", a, new Vector3(s.X0 + 1.6f, 0, z + 12f), 90, Green, HoopMode.Latch, "HANG 2s");
            h1.DunkOnly = true;
            h1.LightByHang = true;
            h1.HangSeconds = 2f;
            Step(a, "HangStep", new Vector3(2.0f, 0.4f, z + 12f), new Vector3(1.6f, 0.8f, 3f));
            Step(a, "HangPlatform", new Vector3(5.6f, 0.8f, z + 12f), new Vector3(5.6f, 1.6f, 5f));
            var h2 = Hoop.Create("Hang 4", a, new Vector3(7.0f, 1.6f, z + 12f), -90, Gold, HoopMode.Latch, "HANG 4s");
            h2.DunkOnly = true;
            h2.LightByHang = true;
            h2.HangSeconds = 4f;
            BouncePad.Create("Bounce Pad", a, new Vector3(0, 0, z + 20.4f), 5.8f);
            StoneWall("HangTower", a, new Vector3(0, 1.7f, z + 22.9f), new Vector3(1.4f, 3.4f, 1.4f));
            var h3 = Hoop.Create("Hang 6", a, new Vector3(0, 3.4f, z + 21.7f), 180, Red, HoopMode.Latch, "HANG 6s");
            h3.DunkOnly = true;
            h3.LightByHang = true;
            h3.HangSeconds = 6f;
            s.Room.Hoops.Add(h1);
            s.Room.Hoops.Add(h2);
            s.Room.Hoops.Add(h3);
            ExitGate(s, new List<Signal> { h1, h2, h3 });
        }

        // --------------------------------------------------------------- 7 STEAL
        void BuildStealShrine()
        {
            var s = Shrine("Steal", "Pickpocket", "STEAL + DODGE", 22, 32, "#E0245E", true);
            var a = s.A;
            float z = s.Z0;
            s.Room.Objective = "Get next to a dribbling bot and press {USE} to STEAL. Score with each bot's ball!";
            s.Room.Hints = new[]
            {
                "Walk right up to a bot while it dribbles and press {USE} to steal the ball.",
                "Score the stolen ball in the hoop of the same colour.",
                "The SPEEDY bot runs away - DASH ({SPRINT}) to catch it.",
                "After you steal from SPEEDY, it chases you and lunges - DASH away when it winds up!"
            };
            var hoopA = Hoop.Create("Bot Hoop A", a, new Vector3(-6.5f, 0, s.Z1 - 1.6f), 180, Orange, HoopMode.Latch, "SLOWPOKE");
            var hoopB = Hoop.Create("Bot Hoop B", a, new Vector3(6.5f, 0, s.Z1 - 1.6f), 180, Blue, HoopMode.Latch, "SPEEDY");
            var botA = SparringBot.Create("Slowpoke", a, new Vector3(-5f, 0, z + 11f), 3f, Orange);
            botA.PatrolSpeed = 1.8f;
            botA.Chance = 0.9f;
            botA.Target = hoopA;
            var botB = SparringBot.Create("Speedy", a, new Vector3(5f, 0, z + 15f), 4f, Blue);
            botB.PatrolSpeed = 3.5f;
            botB.FleeSpeed = 6.2f;      // faster than walking (5.2), slower than dashing (9.5)
            botB.ChaseSpeed = 4.6f;
            botB.LungeChance = 0.4f;
            botB.Chance = 0.7f;
            botB.Target = hoopB;
            foreach (var b in new[] { botA, botB })
            {
                b.MinX = s.X0 + 1f;
                b.MaxX = s.X1 - 1f;
                b.MinZ = z + 2f;
                b.MaxZ = s.Z1 - 4f;
            }
            s.Room.Hoops.Add(hoopA);
            s.Room.Hoops.Add(hoopB);
            ExitGate(s, new List<Signal> { hoopA, hoopB });
        }

        // =====================================================================
        // CHAMPIONSHIP COURT - shooting contest vs KING SWAT
        // =====================================================================
        void BuildArena()
        {
            // the arena is authored at z 130..170 and moved to sit after the last shrine
            var a = Area("Arena").transform;
            float dz = _z - 130f;
            a.localPosition = new Vector3(0, 0, dz);
            const float x0 = -15, x1 = 15, z0 = 130, z1 = 170;
            CourtFloor("Arena", a, x0, x1, z0, z1, 0, Mat.Hex("#6A2DB8"), true, true, true, 6.5f);
            Walls(a, x0, x1, z0, z1, 0, 14, 3.6f, 0);
            Roof(a, x0, x1, z0, z1, 14f, true, "#FFE6B0", 9f);
            // royal banners: King Swat purple on his (south) half, Zander blue on the north half
            SideBanners(a, x0, x1, 9.5f, new[] { 136f, 143f }, "Banner_King", "Banner_King", 2.2f, 4.4f);
            SideBanners(a, x0, x1, 9.5f, new[] { 157f, 164f }, "Banner_Zander", "Banner_Zander", 2.2f, 4.4f);
            foreach (float x in new[] { -7f, 7f })
            {
                Banner(a, "Banner_Zander", new Vector3(x, 9.5f, z1 - 0.06f), 2.4f, 4.8f, 180f);
                Banner(a, "Banner_King", new Vector3(x, 9.5f, z0 + 0.06f), 2.4f, 4.8f, 0f);
            }
            // gold royal trim bands and stadium light rigs
            var gold = Mat.Glow(Mat.Hex("#FFC21A"), Mat.Hex("#FFB000"), 0.8f);
            foreach (float x in new[] { x0 + 0.03f, x1 - 0.03f })
                Build.Box("RoyalBand", a, new Vector3(x, 6.6f, (z0 + z1) / 2), new Vector3(0.07f, 0.18f, z1 - z0), gold, false);
            var rigMat = Mat.Color("#1E1E24", 0.3f);
            var lamp = Mat.Glow(Mat.Hex("#FFFFFF"), Mat.Hex("#FFF6DD"), 2.5f);
            foreach (float z in new[] { 140f, 160f })
                foreach (float x in new[] { -9f, 9f })
                {
                    Build.Box("LightRig", a, new Vector3(x, 13.2f, z), new Vector3(3.2f, 0.35f, 1.0f), rigMat, false);
                    for (int k = -1; k <= 1; k++)
                        Build.Prim(PrimitiveType.Cylinder, "Lamp", a, new Vector3(x + k * 1.0f, 13.0f, z), new Vector3(0.7f, 0.05f, 0.7f),
                            lamp, false);
                }
            var room = NewRoom("Championship Court", "VS KING SWAT", 126 + dz, 172 + dz, new Vector3(4, 0.05f, 134.5f + dz), 8);
            room.IsBoss = true;
            room.Objective = "One ball, first to 15! Score at the NORTH hoop - shots from outside the big arc are worth 3!";
            room.Hints = new[]
            {
                "There's only ONE ball. After a basket, the other player gets it under that hoop.",
                "King Swat plays tight defence: DASH ({SPRINT}) past him to get an open shot - watch your stamina wheel!",
                "If he raises his arms, he may SWAT your shot - shoot from further away or dunk ({DUNK})!",
                "When King Swat winds up for a steal, run! When HE dribbles, get close and press {USE} to steal it back.",
                "Missed shots are loose balls - race him to the rebound!",
                "Struggling? Press {EASY} for Super Shots (he steals and swats less too)."
            };

            var arena = new BossArena { Room = room, CourtCenter = new Vector3(0, 0, 150 + dz), BossSpawn = new Vector3(0, 0, 157 + dz),
                StartLineZ = 136.5f + dz };

            // player hoop on a sliding rig
            var rig = Build.Group("PlayerHoopRig", a, new Vector3(0, 0, 166.4f));
            arena.PlayerHoopRig = rig.AddComponent<MovingHoopRig>();
            arena.PlayerHoop = Hoop.Create("Your Hoop", rig.transform, Vector3.zero, 180, Blue, HoopMode.Counter, "ZANDER");
            arena.BossHoop = Hoop.Create("King's Hoop", a, new Vector3(0, 0, 133.6f), 0, Purple, HoopMode.Counter, "KING SWAT");
            arena.BossHoop.AcceptPlayerShots = false;
            arena.BossHoop.AimAssist = false;
            room.Hoops.Add(arena.PlayerHoop);
            room.Hoops.Add(arena.BossHoop);

            // one game ball only: the ShootingContest creates and manages it (no racks here)

            // bleachers + crowd
            foreach (var side in new[] { -1, 1 })
            {
                var pos = new Vector3(side * 11.5f, 0, 150);
                float yaw = side < 0 ? 90 : -90;
                var m = ModelLibrary.Spawn("Bleachers", a, pos, yaw);
                ModelLibrary.FaceByBounds(m, -1f);  // tiers rise away from the court (local -Z)
                var grp = Build.Group("BleacherCols", a, pos, yaw);
                for (int i = 0; i < 3; i++)
                {
                    float h = 0.5f * (i + 1);
                    Build.Collider("Tier" + i, grp.transform, new Vector3(0, h / 2, -0.5f - i), new Vector3(10, h, 1));
                    if (m == null)
                        Build.Box("TierVis" + i, grp.transform, new Vector3(0, h / 2, -0.5f - i), new Vector3(10, h, 1),
                            Mat.Color(i == 0 ? "#1F5FD6" : i == 1 ? "#F4F4F4" : "#E8772E"), false);
                    for (int f = 0; f < 7; f++)
                    {
                        var fan = new GameObject("Fan");
                        fan.transform.SetParent(grp.transform, false);
                        fan.transform.localPosition = new Vector3(-4.2f + f * 1.4f + Random.Range(-0.2f, 0.2f), h + 0.1f, -0.4f - i);
                        var col = Color.HSVToRGB(Random.value, 0.6f, 0.95f);
                        Build.Prim(PrimitiveType.Capsule, "Body", fan.transform, new Vector3(0, 0.45f, 0), new Vector3(0.45f, 0.45f, 0.45f),
                            Mat.Color(col), false);
                        Build.Prim(PrimitiveType.Sphere, "Head", fan.transform, new Vector3(0, 1.05f, 0), Vector3.one * 0.36f,
                            Mat.Color(Color.HSVToRGB(0.07f, Random.Range(0.2f, 0.6f), Random.Range(0.4f, 1f))), false);
                        arena.Fans.Add(fan.transform);
                    }
                }
            }

            // scoreboard
            var sbPos = new Vector3(0, 8.5f, 150);
            var sb = ModelLibrary.Spawn("Scoreboard", a, sbPos, 180);
            ModelLibrary.FaceByMaterial(sb, "Led", 1f);   // screens + LED bar on the front (+Z)
            if (sb == null) Build.Box("Scoreboard", a, sbPos, new Vector3(4.2f, 2.1f, 0.4f), Mat.Color("#1E1E24"), false);
            arena.ScoreboardText = Build.Label("ZANDER 0 - 0 KING", a, sbPos + new Vector3(0, 0.1f, -0.25f), 0.4f, Gold, 0);

            // trophy spot + barrier that closes behind Zander during the contest
            var trophy = new GameObject("TrophySpot");
            trophy.transform.SetParent(a, false);
            trophy.transform.localPosition = new Vector3(0, 0, 150);
            arena.TrophySpot = trophy.transform;
            var barrier = Build.Box("Barrier", a, new Vector3(0, 2.15f, 130), new Vector3(3.6f, 4.3f, 0.3f),
                Mat.Glass(new Color(0.8f, 0.4f, 1f, 0.35f)));
            barrier.SetActive(false);
            arena.Barrier = barrier;

            // lights
            for (int i = 0; i < 4; i++)
                Build.PointLight(a, new Vector3(i % 2 == 0 ? -8 : 8, 11, i < 2 ? 140 : 160), Color.white, 24f, 1.6f);
            Sign(a, "CHAMPIONSHIP COURT", new Vector3(0, 8.5f, z1 - 0.12f), 0.8f, Gold, 0);
            Arena = arena;
        }
    }
}
