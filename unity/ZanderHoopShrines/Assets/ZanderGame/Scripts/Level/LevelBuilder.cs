using System.Collections.Generic;
using UnityEngine;

namespace ZanderGame
{
    /// <summary>
    /// Builds the whole game world at runtime. The areas run south -> north along +Z:
    ///
    ///   Lobby (z 0-14)         practice hoop, controls sign
    ///   Shrine 1 (z 18-42)     "Warm-Up"         EASY   - plates + a shot
    ///   Shrine 2 (z 46-78)     "Bounce & Dunk"   MEDIUM - bounce pad, dunk, lift, timed hoops
    ///   Shrine 3 (z 86-122)    "Chain Reaction"  HARD   - ball plate bridge, rebound boards, order
    ///   Championship Court (z 130-170) shooting contest vs KING SWAT
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
            BuildShrine1();
            BuildShrine2();
            BuildShrine3();
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
            Sign(a, "CONTROLLER:  MOVE: LEFT STICK   LOOK: RIGHT STICK   JUMP: A\nSHOOT: HOLD RT   DUNK: X   SPRINT: LT   DROP: B   USE: Y\nKEYBOARD:  WASD  SPACE  SHIFT  LEFT MOUSE  C=DUNK  Q  E  H",
                new Vector3(x1 - 0.12f, 3.2f, 7), 0.32f, Color.white, 90);
            Sign(a, "Beat 3 shrines...\nthen beat KING SWAT!", new Vector3(x0 + 0.12f, 3.4f, 4), 0.4f, Gold, -90);
            Corridor(a, 14, 18, 0);
        }

        // =====================================================================
        // SHRINE 1 - WARM-UP (easy)
        //   Gate needs: the hoop lit + a ball resting on the ORANGE ball plate.
        //   But the only ball is locked in a glass cage that opens when Zander
        //   stands on the BLUE step plate.
        // =====================================================================
        void BuildShrine1()
        {
            var a = Area("Shrine1").transform;
            const float x0 = -9, x1 = 9, z0 = 18, z1 = 42;
            CourtFloor("Shrine1", a, x0, x1, z0, z1, 0, Mat.Hex("#E8772E"), true);
            Walls(a, x0, x1, z0, z1, 0, 11, 3.6f, 3.0f);
            Roof(a, x0, x1, z0, z1, 11f);
            SideBanners(a, x0, x1, 6.5f, new[] { 22f, 36f }, "Banner_Shrine", "Banner_Shrine");
            Pillars(a, x0, x1, z0, z1, 0);
            var room = NewRoom("Shrine 1: Warm-Up", "Plates and Hoops", 16, 44, new Vector3(0, 0.05f, 20), 1);
            room.Objective = "Light the hoop AND put a ball on the orange BALL plate to open the gate.";
            room.Hints = new[]
            {
                "The ball is locked in a glass cage. Stand on the BLUE plate to open it.",
                "Shoot the ball into the hoop to light it up.",
                "Get a ball again (the rack makes a new one) and stand by the ORANGE plate. Press {DROP} to place it.",
                "When both crystals over the gate glow, the gate opens!"
            };

            var stepPlate = PressurePlate.Create("Step Plate", a, new Vector3(-5, 0, 25), PlateKind.Feet, latch: true);
            var rack = BallDispenser.Create("Rack", a, new Vector3(5, 0, 25), 1);
            room.Dispensers.Add(rack);
            BuildCage(a, new Vector3(5, 0, 25), stepPlate);

            var hoop = Hoop.Create("Shrine Hoop", a, new Vector3(0, 0, 38.4f), 180, Orange, HoopMode.Latch, "SHOOT ME!");
            room.Hoops.Add(hoop);
            var ballPlate = PressurePlate.Create("Ball Plate", a, new Vector3(5.5f, 0, 33), PlateKind.Ball);

            room.Exit = Gate.Create("Gate1", a, new Vector3(0, 0, z1 + 0.5f), 0, new List<Signal> { hoop, ballPlate });
            Sign(a, "SHRINE 1\nWARM-UP", new Vector3(0, 5.6f, z0 + 0.12f), 0.7f, Gold, 180);
            Sign(a, "BLUE plates = stand on them\nORANGE plates = put a ball on them", new Vector3(x0 + 0.12f, 2.6f, 30),
                0.33f, Color.white, -90);
            Corridor(a, 42, 46, 0);
        }

        void BuildCage(Transform parent, Vector3 pos, Signal opener)
        {
            var cage = Build.Group("Cage", parent, pos);
            var mover = cage.AddComponent<SignalMover>();
            mover.Inputs.Inputs.Add(opener);
            mover.Latch = true;
            mover.OffLocal = pos;
            mover.OnLocal = pos + Vector3.down * 2.7f;
            mover.Speed = 2.5f;
            var glass = Mat.Glass(new Color(0.6f, 0.85f, 1f, 0.35f));
            var frame = Mat.Color("#2C3E66", 0.4f);
            const float s = 1.7f, h = 2.5f;
            for (int i = 0; i < 4; i++)
            {
                float ang = i * 90f;
                var rot = Quaternion.Euler(0, ang, 0);
                var p = rot * new Vector3(0, h / 2, s / 2);
                Build.Box("Glass", cage.transform, p, new Vector3(s, h, 0.06f), glass, true, rot);
                Build.Box("Post", cage.transform, rot * new Vector3(s / 2, h / 2, s / 2), new Vector3(0.12f, h, 0.12f), frame, false);
            }
            Build.Box("Roof", cage.transform, new Vector3(0, h, 0), new Vector3(s + 0.1f, 0.1f, s + 0.1f), frame);
            cage.AddComponent<Rigidbody>().isKinematic = true;
        }

        // =====================================================================
        // SHRINE 2 - BOUNCE & DUNK (medium)
        //   1. A DUNK-ONLY hoop hangs high on a tower. Carry a ball onto the bounce
        //      pad and press SHOOT in mid-air to dunk -> powers the lift.
        //   2. Ride the lift up to the ledge.
        //   3. Two TIMED hoops (one on the ledge, one far below) must be lit at the
        //      same time - each only stays lit for 12 seconds!
        // =====================================================================
        void BuildShrine2()
        {
            var a = Area("Shrine2").transform;
            const float x0 = -11, x1 = 11, z0 = 46, z1 = 78;
            const float ledgeZ = 66, ledgeY = 4;
            CourtFloor("Shrine2", a, x0, x1, z0, z1, 0, Mat.Hex("#3CE07A"), false, true);
            Walls(a, x0, x1, z0, z1, 0, 13, 3.6f, 3.0f, 0, 4.3f, ledgeY, ledgeY + 4.3f);
            Roof(a, x0, x1, z0, z1, 13f);
            SideBanners(a, x0, x1, 8.5f, new[] { 50f, 60f }, "Banner_Shrine", "Banner_Shrine");
            Pillars(a, x0, x1, z0, ledgeZ, 0);
            var room = NewRoom("Shrine 2: Bounce & Dunk", "Fly high!", 44, 80, new Vector3(0, 0.05f, 48), 2);
            room.Objective = "Dunk the high hoop to power the lift, then light BOTH timed hoops at the same time.";
            room.Hints = new[]
            {
                "Grab a ball, then run onto the green BOUNCE pad. It throws you way up high!",
                "While flying up next to the purple DUNK ONLY hoop, press {DUNK} to slam dunk it!",
                "The dunk powers the lift on the right side. Ride it up to the ledge.",
                "Timed hoops only stay lit for 12 seconds. Light the one on the ledge, grab another ball, then shoot the one far below!",
                "Tip: the rack on the ledge makes a new ball quickly. Hold SHOOT and aim the camera at the hoop."
            };

            // ledge
            Build.StoneBox("Ledge", a, new Vector3(0, ledgeY / 2, (ledgeZ + z1) / 2), new Vector3(x1 - x0, ledgeY, z1 - ledgeZ));
            var ledgeFloor = Build.Floor("LedgeTop", a, new Vector3(0, ledgeY + 0.01f, (ledgeZ + z1) / 2), x1 - x0, z1 - ledgeZ,
                Flagstones(x1 - x0, z1 - ledgeZ), 0.2f);
            ledgeFloor.name = "LedgeTop";
            var glow = Mat.Glow(Mat.Hex("#FFB347"), Mat.Hex("#FF9A1F"), 1.2f);
            Build.Box("LedgeTrim", a, new Vector3(0, ledgeY - 0.1f, ledgeZ - 0.02f), new Vector3(x1 - x0, 0.12f, 0.06f), glow, false);

            // ground-floor ball rack
            room.Dispensers.Add(BallDispenser.Create("Rack", a, new Vector3(-1.5f, 0, 52), 1));

            // bounce pad + dunk tower
            BouncePad.Create("Bounce Pad", a, new Vector3(-6, 0, 57.4f), 5.8f);
            StoneWall("DunkTower", a, new Vector3(-6, 1.7f, 59.9f), new Vector3(1.4f, 3.4f, 1.4f));
            var dunkHoop = Hoop.Create("Dunk Hoop", a, new Vector3(-6, 3.4f, 58.7f), 180, Purple, HoopMode.Latch, "DUNK ONLY");
            dunkHoop.DunkOnly = true;
            room.Hoops.Add(dunkHoop);

            // lift
            var lift = Build.Group("Lift", a, new Vector3(7, -0.2f, 64.4f));
            Material liftGlow;
            var liftModel = ModelLibrary.Spawn("LiftPlatform", lift.transform, Vector3.zero);
            var liftGlows = liftModel != null ? ModelLibrary.Materials(liftModel, "LiftGlow") : null;
            if (liftGlows != null && liftGlows.Count > 0)
            {
                liftGlow = liftGlows[0];
                Build.Collider("LiftDeckCol", lift.transform, Vector3.zero, new Vector3(3, 0.4f, 3));
            }
            else
            {
                if (liftModel != null) Object.Destroy(liftModel);
                liftGlow = Mat.Glow(Mat.Hex("#4E5668"), Color.black, 1f);
                Build.Box("LiftDeck", lift.transform, Vector3.zero, new Vector3(3, 0.4f, 3), Mat.Color("#2C3E66", 0.4f));
                Build.Box("LiftGlow", lift.transform, new Vector3(0, 0.21f, 0), new Vector3(2.4f, 0.02f, 2.4f), liftGlow, false);
            }
            lift.AddComponent<Rigidbody>().isKinematic = true;
            var mp = lift.AddComponent<MovingPlatform>();
            mp.A = new Vector3(7, -0.2f, 64.4f);
            mp.B = new Vector3(7, ledgeY - 0.2f, 64.4f);
            mp.Speed = 1.6f;
            mp.Pause = 2f;
            mp.Inputs.Inputs.Add(dunkHoop);
            mp.SetGlow(liftGlow);
            Build.Label("LIFT", lift.transform, new Vector3(0, 1.2f, 0), 0.35f, Green, 0, true);

            // ledge rack + timed hoops
            room.Dispensers.Add(BallDispenser.Create("LedgeRack", a, new Vector3(0, ledgeY, 70), 2));
            var t1 = Hoop.Create("Ledge Hoop", a, new Vector3(8.2f, ledgeY, 75), -90, Gold, HoopMode.Timed, "TIMED");
            t1.LitDuration = 12f;
            var t2 = Hoop.Create("Low Hoop", a, new Vector3(-7, 0, 49.6f), 0, Gold, HoopMode.Timed, "TIMED");
            t2.LitDuration = 12f;
            room.Hoops.Add(t1);
            room.Hoops.Add(t2);

            room.Exit = Gate.Create("Gate2", a, new Vector3(0, ledgeY, z1 + 0.5f), 0, new List<Signal> { t1, t2 });
            Sign(a, "SHRINE 2\nBOUNCE & DUNK", new Vector3(0, 5.6f, z0 + 0.12f), 0.7f, Gold, 180);
            Sign(a, "Light BOTH gold hoops\nat the same time!", new Vector3(0, ledgeY + 5.4f, z1 - 0.12f), 0.45f, Color.white, 0);

            // downhill corridor to shrine 3
            var c = Build.Group("Ramp", a).transform;
            float rz0 = 78, rz1 = 86;
            var d = new Vector3(0, -ledgeY, rz1 - rz0).normalized;
            var n = Vector3.Cross(d, Vector3.right).normalized;
            if (n.y < 0) n = -n;
            var rampCenter = new Vector3(0, ledgeY / 2, (rz0 + rz1) / 2) - n * 0.25f;
            var len = new Vector2(rz1 - rz0, ledgeY).magnitude;
            Build.Box("RampDeck", c, rampCenter, new Vector3(4.4f, 0.5f, len + 0.4f), Flagstones(4.4f, len + 0.4f), true,
                Quaternion.LookRotation(d, n));
            StoneWall("RampW", c, new Vector3(-2.7f, 3.5f, (rz0 + rz1) / 2), new Vector3(1, 9, rz1 - rz0));
            StoneWall("RampE", c, new Vector3(2.7f, 3.5f, (rz0 + rz1) / 2), new Vector3(1, 9, rz1 - rz0));
            Roof(c, -3.2f, 3.2f, rz0, rz1, 8f, false);
            Lantern(c, new Vector3(0, 8f, 82), 1.2f, "#FFB347");
        }

        // =====================================================================
        // SHRINE 3 - CHAIN REACTION (hard)
        //   1. A bottomless pit splits the room. Hold a ball on the ORANGE plate to
        //      raise the bridge (take it off and the bridge sinks!).
        //   2. Across the pit: score GREEN -> RED -> BLUE in that order.
        //      RED is a normal shot. GREEN (high on a tower) and BLUE (facing the
        //      wall) can only be reached with a bank shot off a ROTATING REBOUND
        //      BOARD - press E to turn each board until its laser turns green.
        // =====================================================================
        void BuildShrine3()
        {
            var a = Area("Shrine3").transform;
            const float x0 = -12, x1 = 12, z0 = 86, z1 = 122;
            const float pit0 = 98, pit1 = 106;
            const float pitY = -9f;
            CourtFloor("Shrine3South", a, x0, x1, z0, pit0, 0, Mat.Hex("#C86BFF"), false);
            CourtFloor("Shrine3North", a, x0, x1, pit1, z1, 0, Mat.Hex("#C86BFF"), true);
            // pit
            var pitMat = Mat.Glow(Mat.Hex("#0B1630"), Mat.Hex("#1E4BFF"), 0.6f);
            Build.Box("PitGlow", a, new Vector3(0, pitY, (pit0 + pit1) / 2), new Vector3(x1 - x0, 0.2f, pit1 - pit0), pitMat);
            Build.StoneBox("PitWallS", a, new Vector3(0, pitY / 2 - 0.5f, pit0 + 0.01f), new Vector3(x1 - x0, -pitY - 1f, 0.02f));
            Build.StoneBox("PitWallN", a, new Vector3(0, pitY / 2 - 0.5f, pit1 - 0.01f), new Vector3(x1 - x0, -pitY - 1f, 0.02f));
            Build.PointLight(a, new Vector3(0, -3, 102), Mat.Hex("#1E4BFF"), 14f, 2f);
            Walls(a, x0, x1, z0, z1, pitY, 13 - pitY, 3.6f, 3.0f, 0, 4.3f, 0, 4.3f);
            Roof(a, x0, x1, z0, z1, 13f);
            SideBanners(a, x0, x1, 8.5f, new[] { 92f, 114f }, "Banner_Shrine", "Banner_Shrine");
            Pillars(a, x0, x1, z0, pit0, 0);
            var room = NewRoom("Shrine 3: Chain Reaction", "Think before you shoot!", 80, 124, new Vector3(0, 0.05f, 88), 3);
            room.Objective = "Raise the bridge, then score GREEN, RED, BLUE in order. Use the Rebound Boards!";
            room.Hints = new[]
            {
                "Put a ball on the ORANGE plate ({DROP}) to raise the bridge. It only stays up while the ball is there!",
                "Look at the ORDER totem: 1 = GREEN, 2 = RED, 3 = BLUE.",
                "GREEN and BLUE can't be hit directly. Shoot a REBOUND BOARD and it fires the ball for you.",
                "Stand next to a board and press {USE} to turn it. When its laser turns GREEN it's aiming at a target.",
                "Shoot the wrong hoop and the order resets - no worries, just try again!"
            };

            // south side: rack + ball plate -> bridge
            room.Dispensers.Add(BallDispenser.Create("Rack", a, new Vector3(7, 0, 90), 2));
            var plate = PressurePlate.Create("Bridge Plate", a, new Vector3(-7, 0, 93), PlateKind.Ball);
            var bridge = Build.Group("Bridge", a, new Vector3(0, -6f, (pit0 + pit1) / 2));
            var deckSize = new Vector3(4, 0.5f, pit1 - pit0 + 0.6f);
            if (ModelLibrary.Spawn("Bridge", bridge.transform, Vector3.zero) != null)
                Build.Collider("DeckCol", bridge.transform, Vector3.zero, deckSize);
            else
            {
                Build.Box("Deck", bridge.transform, Vector3.zero, new Vector3(4, 0.5f, pit1 - pit0 + 0.6f), Mat.Color("#2C3E66", 0.4f));
                Build.Box("Edge", bridge.transform, new Vector3(-1.95f, 0.26f, 0), new Vector3(0.1f, 0.04f, pit1 - pit0),
                    Mat.Glow(Mat.Hex("#FF8A1F"), Mat.Hex("#FF8A1F"), 1.5f), false);
                Build.Box("Edge", bridge.transform, new Vector3(1.95f, 0.26f, 0), new Vector3(0.1f, 0.04f, pit1 - pit0),
                    Mat.Glow(Mat.Hex("#FF8A1F"), Mat.Hex("#FF8A1F"), 1.5f), false);
            }
            bridge.AddComponent<Rigidbody>().isKinematic = true;
            var bm = bridge.AddComponent<SignalMover>();
            bm.Inputs.Inputs.Add(plate);
            bm.Latch = false;
            bm.OffLocal = new Vector3(0, -6f, (pit0 + pit1) / 2);
            bm.OnLocal = new Vector3(0, -0.25f, (pit0 + pit1) / 2);
            bm.Speed = 5f;
            Sign(a, "Ball on plate = bridge UP", new Vector3(-7, 2.4f, 94.6f), 0.3f, PressurePlate.KindColor(PlateKind.Ball), 0);

            // north side
            room.Dispensers.Add(BallDispenser.Create("Rack2", a, new Vector3(2.5f, 0, 108.5f), 2));

            var red = Hoop.Create("Red Hoop", a, new Vector3(-6.5f, 0, 120), 180, Red, HoopMode.Latch, "RED");
            var blue = Hoop.Create("Blue Hoop", a, new Vector3(10.4f, 0, 113), 90, Blue, HoopMode.Latch, "BLUE");
            blue.RequiresRebound = true;
            StoneWall("GreenTower", a, new Vector3(-9, 2.5f, 110.2f), new Vector3(2.4f, 5f, 2.4f));
            var green = Hoop.Create("Green Hoop", a, new Vector3(-9, 5f, 110.5f), 0, Green, HoopMode.Latch, "GREEN");
            green.RequiresRebound = true;
            room.Hoops.Add(red);
            room.Hoops.Add(blue);
            room.Hoops.Add(green);

            // rebound boards (start pointing at nothing)
            var rb1 = ReboundBoard.Create("Rebound Board A", a, new Vector3(10, 0, 120.3f), 0);
            var rb2 = ReboundBoard.Create("Rebound Board B", a, new Vector3(-2.5f, 0, 117.5f), 180);
            rb1.ConeDegrees = 20f;
            rb2.ConeDegrees = 20f;


            var seqGo = new GameObject("HoopSequence");
            seqGo.transform.SetParent(a, false);
            var seq = seqGo.AddComponent<HoopSequence>();
            seq.Setup(new List<Hoop> { green, red, blue }, a, new Vector3(4.5f, 0, 112), 180);

            room.Exit = Gate.Create("Gate3", a, new Vector3(0, 0, z1 + 0.5f), 0, new List<Signal> { seq });
            Sign(a, "SHRINE 3\nCHAIN REACTION", new Vector3(0, 5.6f, z0 + 0.12f), 0.7f, Gold, 180);
            Corridor(a, 122, 130, 0);
        }

        // =====================================================================
        // CHAMPIONSHIP COURT - shooting contest vs KING SWAT
        // =====================================================================
        void BuildArena()
        {
            var a = Area("Arena").transform;
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
            var room = NewRoom("Championship Court", "VS KING SWAT", 124, 172, new Vector3(4, 0.05f, 134.5f), 4);
            room.IsBoss = true;
            room.Objective = "Score 15 points before KING SWAT does! Shots from far away are worth 3!";
            room.Hints = new[]
            {
                "Shoot at YOUR hoop (the north end). Shots from outside the big arc are worth 3 points!",
                "When King Swat STOMPS, jump over the shockwave ring!",
                "When he puts his arms up he will SWAT your shot - wait until he stops blocking.",
                "Later the hoop starts moving - aim assist will lead it for you. Time your release!",
                "Struggling? Press {EASY} for Super Shots."
            };

            var arena = new BossArena { Room = room, CourtCenter = new Vector3(0, 0, 150), BossSpawn = new Vector3(0, 0, 157),
                StartLineZ = 136.5f };

            // player hoop on a sliding rig
            var rig = Build.Group("PlayerHoopRig", a, new Vector3(0, 0, 166.4f));
            arena.PlayerHoopRig = rig.AddComponent<MovingHoopRig>();
            arena.PlayerHoop = Hoop.Create("Your Hoop", rig.transform, Vector3.zero, 180, Blue, HoopMode.Counter, "ZANDER");
            arena.BossHoop = Hoop.Create("King's Hoop", a, new Vector3(0, 0, 133.6f), 0, Purple, HoopMode.Counter, "KING SWAT");
            arena.BossHoop.AcceptPlayerShots = false;
            arena.BossHoop.AimAssist = false;
            room.Hoops.Add(arena.PlayerHoop);
            room.Hoops.Add(arena.BossHoop);

            foreach (var p in new[] { new Vector3(-9, 0, 151), new Vector3(9, 0, 151), new Vector3(0, 0, 145) })
            {
                var d = BallDispenser.Create("Rack", a, p, 1);
                d.RespawnDelay = 0.4f;
                arena.Racks.Add(d);
                room.Dispensers.Add(d);
            }

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
