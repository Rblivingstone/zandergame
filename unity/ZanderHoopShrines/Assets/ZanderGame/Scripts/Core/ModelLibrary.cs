using System.Collections.Generic;
using UnityEngine;

namespace ZanderGame
{
    /// <summary>
    /// Loads the Blender-generated FBX models from Resources/Models. Every caller
    /// has a primitive fallback, so the game still runs if a model is missing.
    /// </summary>
    public static class ModelLibrary
    {
        /// <summary>
        /// Extra yaw applied to every imported model. The Blender scripts export so that
        /// models face Unity +Z; flip to 180 if your importer settings disagree.
        /// </summary>
        public static float ModelYawOffset = 0f;

        static readonly Dictionary<string, GameObject> Cache = new Dictionary<string, GameObject>();

        public static GameObject Prefab(string name)
        {
            if (Cache.TryGetValue(name, out var p)) return p;
            p = Resources.Load<GameObject>("Models/" + name);
            Cache[name] = p;
            return p;
        }

        public static bool Has(string name) => Prefab(name) != null;

        /// <summary>
        /// Instantiates a model under a fresh container. If targetHeight &gt; 0 the
        /// model is uniformly scaled so its renderer bounds match that height and its
        /// lowest point sits on the container origin.
        /// </summary>
        public static GameObject Spawn(string name, Transform parent, Vector3 localPos, float yaw = 0f,
            float targetHeight = -1f, float outline = 0.0018f)
        {
            var prefab = Prefab(name);
            if (prefab == null) return null;
            var container = new GameObject(name);
            container.transform.SetParent(parent, false);
            container.transform.localPosition = localPos;
            container.transform.localRotation = Quaternion.Euler(0, yaw, 0);

            var inst = Object.Instantiate(prefab, container.transform, false);
            inst.name = name + "_Model";
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.Euler(0, ModelYawOffset, 0) * inst.transform.localRotation;
            Build.StripColliders(inst);
            ToonStyler.Apply(inst, outline);

            if (targetHeight > 0f)
            {
                var b = WorldBounds(inst);
                if (b.size.y > 0.001f)
                {
                    float s = targetHeight / b.size.y;
                    inst.transform.localScale *= s;
                    b = WorldBounds(inst);
                    float bottomLocal = container.transform.InverseTransformPoint(b.min).y;
                    inst.transform.localPosition += Vector3.up * -bottomLocal;
                }
            }
            return container;
        }

        public static Bounds WorldBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        public static Transform FindDeep(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            foreach (Transform c in root)
            {
                var r = FindDeep(c, name);
                if (r != null) return r;
            }
            return null;
        }

        /// <summary>Finds a child whose name contains the token (case-insensitive).</summary>
        public static Transform FindContains(Transform root, string token)
        {
            if (root == null) return null;
            if (root.name.ToLowerInvariant().Contains(token.ToLowerInvariant())) return root;
            foreach (Transform c in root)
            {
                var r = FindContains(c, token);
                if (r != null) return r;
            }
            return null;
        }

        /// <summary>Re-tints every material whose name contains the token.</summary>
        public static void Tint(GameObject root, string token, Color color, Color? emission = null)
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>())
            {
                var mats = r.materials; // instanced copies
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null || !mats[i].name.Contains(token)) continue;
                    mats[i].color = color;
                    if (emission.HasValue) Mat.SetEmission(mats[i], emission.Value);
                    changed = true;
                }
                if (changed) r.materials = mats;
            }
        }

        /// <summary>Collects the (instanced) materials whose name contains token.</summary>
        public static List<Material> Materials(GameObject root, string token)
        {
            var list = new List<Material>();
            foreach (var r in root.GetComponentsInChildren<Renderer>())
                foreach (var m in r.materials)
                    if (m != null && m.name.Contains(token)) list.Add(m);
            return list;
        }
    }
}
