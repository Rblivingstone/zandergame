using UnityEngine;

namespace ZanderGame
{
    /// <summary>
    /// Builds the character visuals: the Blender models when available, otherwise
    /// a cute primitive version with the same look (blonde faux hawk, blue eyes,
    /// blue #8 jersey, red sneakers).
    /// </summary>
    public static class CharacterFactory
    {
        public static Transform CreateZander(Transform parent)
        {
            var model = ModelLibrary.Spawn("Zander", parent, Vector3.zero, 0f, PlayerController.Height, 0.0032f);
            if (model != null)
            {
                return model.transform;
            }
            return BuildPrimitiveZander(parent);
        }

        public static Transform CreateBoss(Transform parent, float height)
        {
            var model = ModelLibrary.Spawn("KingSwat", parent, Vector3.zero, 0f, height, 0.0032f);
            if (model != null)
            {
                return model.transform;
            }
            return BuildPrimitiveBoss(parent, height);
        }

        static GameObject P(PrimitiveType t, string n, Transform parent, Vector3 pos, Vector3 scale, string hex,
            Vector3? rot = null, float smooth = 0.3f)
        {
            return Build.Prim(t, n, parent, pos, scale, Mat.Color(hex, smooth), false,
                rot.HasValue ? Quaternion.Euler(rot.Value) : (Quaternion?)null);
        }

        static Transform Pivot(string name, Transform parent, Vector3 pos)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            return g.transform;
        }

        static Transform BuildPrimitiveZander(Transform parent)
        {
            var root = new GameObject("Zander_Primitive").transform;
            root.SetParent(parent, false);
            var rig = root.gameObject.AddComponent<CharacterRig>();
            var body = Pivot("Body", root, Vector3.zero);
            rig.Body = body;
            const string skin = "#F6C9A4", hair = "#F4D35E", jersey = "#1F5FD6";

            // legs
            rig.LegL = Pivot("LegL", body, new Vector3(0.08f, 0.52f, 0));
            rig.LegR = Pivot("LegR", body, new Vector3(-0.08f, 0.52f, 0));
            foreach (var leg in new[] { rig.LegL, rig.LegR })
            {
                P(PrimitiveType.Capsule, "Leg", leg, new Vector3(0, -0.22f, 0), new Vector3(0.1f, 0.2f, 0.1f), skin);
                P(PrimitiveType.Cylinder, "Sock", leg, new Vector3(0, -0.4f, 0), new Vector3(0.1f, 0.05f, 0.1f), "#FFFFFF");
                P(PrimitiveType.Sphere, "Shoe", leg, new Vector3(0, -0.47f, 0.04f), new Vector3(0.14f, 0.1f, 0.25f), "#E23B3B");
            }
            // shorts + jersey
            P(PrimitiveType.Sphere, "Shorts", body, new Vector3(0, 0.55f, 0), new Vector3(0.33f, 0.2f, 0.24f), "#1A4FB5");
            P(PrimitiveType.Sphere, "Jersey", body, new Vector3(0, 0.73f, 0), new Vector3(0.33f, 0.36f, 0.25f), jersey);
            P(PrimitiveType.Cylinder, "Badge", body, new Vector3(0, 0.76f, 0.118f), new Vector3(0.14f, 0.01f, 0.14f), "#FFFFFF",
                new Vector3(90, 0, 0));
            Build.Label("Z", body, new Vector3(0, 0.76f, 0.13f), 0.12f, Mat.Hex("#FFC21A"), 180f);
            Build.Label("8", body, new Vector3(0, 0.76f, -0.13f), 0.16f, Mat.Hex("#FFC21A"), 0f);

            // arms
            rig.ArmL = Pivot("ArmL", body, new Vector3(0.2f, 0.84f, 0));
            rig.ArmR = Pivot("ArmR", body, new Vector3(-0.2f, 0.84f, 0));
            foreach (var arm in new[] { rig.ArmL, rig.ArmR })
            {
                P(PrimitiveType.Capsule, "Arm", arm, new Vector3(0, -0.16f, 0), new Vector3(0.085f, 0.17f, 0.085f), skin);
                P(PrimitiveType.Sphere, "Hand", arm, new Vector3(0, -0.36f, 0), Vector3.one * 0.1f, skin);
            }
            rig.HandR = rig.ArmR.Find("Hand");

            // head
            var head = Pivot("Head", body, new Vector3(0, 1.1f, 0));
            rig.Head = head;
            P(PrimitiveType.Sphere, "Skull", head, Vector3.zero, Vector3.one * 0.47f, skin);
            for (int s = -1; s <= 1; s += 2)
            {
                P(PrimitiveType.Sphere, "Ear", head, new Vector3(s * 0.23f, -0.01f, 0), new Vector3(0.05f, 0.11f, 0.08f), skin);
                P(PrimitiveType.Sphere, "EyeWhite", head, new Vector3(s * 0.085f, 0.015f, 0.2f), new Vector3(0.11f, 0.14f, 0.06f), "#FFFFFF", null, 0.8f);
                P(PrimitiveType.Sphere, "Iris", head, new Vector3(s * 0.085f, 0.01f, 0.225f), new Vector3(0.075f, 0.09f, 0.03f), "#2F7DE1", null, 0.9f);
                P(PrimitiveType.Sphere, "Pupil", head, new Vector3(s * 0.085f, 0.01f, 0.237f), new Vector3(0.04f, 0.05f, 0.015f), "#141414", null, 0.9f);
                P(PrimitiveType.Sphere, "Shine", head, new Vector3(s * 0.085f + 0.015f, 0.035f, 0.245f), Vector3.one * 0.018f, "#FFFFFF");
                P(PrimitiveType.Cube, "Brow", head, new Vector3(s * 0.09f, 0.1f, 0.2f), new Vector3(0.075f, 0.018f, 0.02f), "#C99A35",
                    new Vector3(0, 0, s * 10));
                P(PrimitiveType.Sphere, "Cheek", head, new Vector3(s * 0.14f, -0.06f, 0.17f), new Vector3(0.06f, 0.04f, 0.02f), "#F4A3A0");
            }
            P(PrimitiveType.Sphere, "Nose", head, new Vector3(0, -0.035f, 0.228f), new Vector3(0.05f, 0.04f, 0.04f), "#E9A987");
            P(PrimitiveType.Cube, "Smile", head, new Vector3(0, -0.1f, 0.205f), new Vector3(0.1f, 0.02f, 0.02f), "#9C2F2F");
            for (int s = -1; s <= 1; s += 2)
                P(PrimitiveType.Cube, "SmileEnd", head, new Vector3(s * 0.055f, -0.09f, 0.2f), new Vector3(0.03f, 0.02f, 0.02f),
                    "#9C2F2F", new Vector3(0, 0, -s * 35));

            // FAUX HAWK: short sides + a spiky ridge down the middle
            var cap = P(PrimitiveType.Sphere, "HairCap", head, new Vector3(0, 0.05f, -0.015f), new Vector3(0.49f, 0.42f, 0.49f), "#D8B04A");
            cap.transform.localScale = new Vector3(0.49f, 0.42f, 0.49f);
            P(PrimitiveType.Sphere, "HawkBase", head, new Vector3(0, 0.15f, -0.01f), new Vector3(0.2f, 0.26f, 0.48f), hair);
            for (int i = 0; i < 7; i++)
            {
                float a = Mathf.Lerp(-0.6f, 0.85f, i / 6f);
                var pos = new Vector3(0, Mathf.Cos(a) * 0.25f + 0.02f, Mathf.Sin(-a) * 0.25f);
                float h = Mathf.Lerp(0.16f, 0.08f, i / 6f);
                var spike = P(PrimitiveType.Cube, "Spike" + i, head, pos, new Vector3(0.07f, h, 0.07f), hair,
                    new Vector3(-a * Mathf.Rad2Deg * 0.55f - 8f, 45, 0));
                spike.transform.localRotation = Quaternion.Euler(-a * Mathf.Rad2Deg * 0.55f + 8f, 0, 0) * Quaternion.Euler(0, 45, 0);
            }
            return root;
        }

        static Transform BuildPrimitiveBoss(Transform parent, float height)
        {
            var root = new GameObject("KingSwat_Primitive").transform;
            root.SetParent(parent, false);
            float k = height / 3.4f;
            root.localScale = Vector3.one * k;
            var rig = root.gameObject.AddComponent<CharacterRig>();
            var body = Pivot("Body", root, Vector3.zero);
            rig.Body = body;
            rig.LegL = Pivot("LegL", body, new Vector3(0.3f, 1.05f, 0));
            rig.LegR = Pivot("LegR", body, new Vector3(-0.3f, 1.05f, 0));
            foreach (var leg in new[] { rig.LegL, rig.LegR })
            {
                P(PrimitiveType.Capsule, "Leg", leg, new Vector3(0, -0.5f, 0), new Vector3(0.3f, 0.5f, 0.3f), "#8C96A6");
                P(PrimitiveType.Sphere, "Shoe", leg, new Vector3(0, -0.95f, 0.1f), new Vector3(0.4f, 0.25f, 0.7f), "#1E1E24");
            }
            P(PrimitiveType.Sphere, "Shorts", body, new Vector3(0, 1.1f, 0), new Vector3(1.2f, 0.5f, 0.9f), "#F4F4F4");
            P(PrimitiveType.Sphere, "Torso", body, new Vector3(0, 1.8f, 0), new Vector3(1.5f, 1.1f, 1.1f), "#6A2DB8");
            Build.Label("99", body, new Vector3(0, 1.9f, 0.56f), 0.5f, Mat.Hex("#FFC21A"), 180f);
            rig.ArmL = Pivot("ArmL", body, new Vector3(0.86f, 2.15f, 0));
            rig.ArmR = Pivot("ArmR", body, new Vector3(-0.86f, 2.15f, 0));
            foreach (var arm in new[] { rig.ArmL, rig.ArmR })
            {
                P(PrimitiveType.Capsule, "Arm", arm, new Vector3(0, -0.6f, 0), new Vector3(0.32f, 0.6f, 0.32f), "#8C96A6");
                P(PrimitiveType.Sphere, "Glove", arm, new Vector3(0, -1.4f, 0), new Vector3(0.55f, 0.6f, 0.55f), "#D62828");
            }
            rig.HandR = rig.ArmR.Find("Glove");
            var head = Pivot("Head", body, new Vector3(0, 2.86f, 0));
            rig.Head = head;
            P(PrimitiveType.Sphere, "Ball", head, Vector3.zero, Vector3.one * 1.12f, "#E8772E");
            P(PrimitiveType.Cylinder, "SeamH", head, Vector3.zero, new Vector3(1.14f, 0.01f, 1.14f), "#2A1A12");
            P(PrimitiveType.Cylinder, "SeamV", head, Vector3.zero, new Vector3(1.14f, 0.01f, 1.14f), "#2A1A12", new Vector3(0, 0, 90));
            for (int s = -1; s <= 1; s += 2)
            {
                var eye = P(PrimitiveType.Sphere, "Eye", head, new Vector3(s * 0.2f, 0.08f, 0.5f), new Vector3(0.2f, 0.16f, 0.1f), "#FFE45C");
                Mat.SetEmission(eye.GetComponent<Renderer>().material, Mat.Hex("#FFE45C") * 2f);
                P(PrimitiveType.Cube, "Brow", head, new Vector3(s * 0.2f, 0.22f, 0.5f), new Vector3(0.26f, 0.07f, 0.07f), "#2A1A12",
                    new Vector3(0, 0, -s * 22));
            }
            P(PrimitiveType.Cube, "Grin", head, new Vector3(0, -0.18f, 0.52f), new Vector3(0.36f, 0.08f, 0.05f), "#FFFFFF");
            P(PrimitiveType.Cylinder, "Crown", head, new Vector3(0, 0.5f, 0), new Vector3(0.68f, 0.08f, 0.68f), "#FFC21A", null, 0.8f);
            for (int i = 0; i < 6; i++)
            {
                float a = i / 6f * Mathf.PI * 2;
                P(PrimitiveType.Cube, "Point", head, new Vector3(Mathf.Cos(a) * 0.3f, 0.66f, Mathf.Sin(a) * 0.3f),
                    new Vector3(0.1f, 0.2f, 0.1f), "#FFC21A", new Vector3(0, -a * Mathf.Rad2Deg, 45), 0.8f);
            }
            return root;
        }
    }
}
