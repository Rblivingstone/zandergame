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
            ToonStyler.Apply(inst, outline, name);

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

        // ------------------------------------------------------------------ facing
        // Every model is authored to face local +Z. Importer settings can flip an FBX
        // 180 degrees, so these checks measure the loaded model and turn it round if
        // needed (and log it), so hoops, boards, characters... always face the right way.

        static void Flip(GameObject container, string why)
        {
            if (container == null || container.transform.childCount == 0) return;
            var inst = container.transform.GetChild(0);
            inst.RotateAround(container.transform.position, container.transform.up, 180f);
            Debug.LogWarning("[ZanderGame] Turned model '" + container.name + "' around to face forward (" + why + ").");
        }

        /// <summary>The model's overall bounds centre should lie on the given side of local Z.</summary>
        public static void FaceByBounds(GameObject container, float expectedZSign)
        {
            if (container == null) return;
            var b = WorldBounds(container);
            float z = container.transform.InverseTransformPoint(b.center).z;
            if (Mathf.Abs(z) > 0.02f && Mathf.Sign(z) != Mathf.Sign(expectedZSign)) Flip(container, "bounds");
        }

        /// <summary>The parts using a material whose name contains token should lie on the given side of local Z.</summary>
        public static void FaceByMaterial(GameObject container, string token, float expectedZSign)
        {
            if (container == null) return;
            foreach (var r in container.GetComponentsInChildren<Renderer>())
            {
                Mesh mesh = r is SkinnedMeshRenderer sk ? sk.sharedMesh :
                    r.GetComponent<MeshFilter>() != null ? r.GetComponent<MeshFilter>().sharedMesh : null;
                if (mesh == null) continue;
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length && i < mesh.subMeshCount; i++)
                {
                    if (mats[i] == null || !mats[i].name.Contains(token)) continue;
                    var c = r.transform.TransformPoint(mesh.GetSubMesh(i).bounds.center);
                    float z = container.transform.InverseTransformPoint(c).z;
                    if (Mathf.Abs(z) > 0.01f && Mathf.Sign(z) != Mathf.Sign(expectedZSign)) Flip(container, token);
                    return;
                }
            }
        }

        /// <summary>
        /// Characters: facing +Z, the character's LEFT hand is on the -X side
        /// (Unity is left-handed). If it isn't, the model faces backwards.
        /// </summary>
        public static void FaceByLeftHand(GameObject container, string leftHandBone = "hand.L")
        {
            if (container == null) return;
            var hand = FindDeep(container.transform, leftHandBone);
            if (hand == null) return;
            float x = container.transform.InverseTransformPoint(hand.position).x;
            if (Mathf.Abs(x) > 0.02f && x > 0f) Flip(container, "left hand on the wrong side");
        }

        /// <summary>
        /// Characters: after the animation is playing, the shoes' toes must point the
        /// way the character walks (container +Z). Measures the skinned shoe vertices
        /// in their animated pose, so it can't be fooled by bind poses or axis tricks.
        /// Returns true if the model was turned around.
        /// </summary>
        public static bool FaceByFeet(GameObject container, string shoeToken, string leftFoot = "foot.L",
            string rightFoot = "foot.R")
        {
            if (container == null) return false;
            var fl = FindDeep(container.transform, leftFoot);
            var fr = FindDeep(container.transform, rightFoot);
            if (fl == null || fr == null) return false;
            Vector3 ankles = (fl.position + fr.position) * 0.5f;
            Vector3 sum = Vector3.zero;
            int count = 0;
            foreach (var smr in container.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var mats = smr.sharedMaterials;
                var baked = new Mesh();
                smr.BakeMesh(baked);
                var verts = baked.vertices;
                var m = smr.transform.localToWorldMatrix;
                // BakeMesh output is in the renderer's local space without its scale
                var noScale = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
                for (int i = 0; i < mats.Length && i < baked.subMeshCount; i++)
                {
                    if (mats[i] == null || !mats[i].name.Contains(shoeToken)) continue;
                    foreach (int vi in baked.GetTriangles(i))
                    {
                        sum += noScale.MultiplyPoint3x4(verts[vi]);
                        count++;
                    }
                }
                Object.Destroy(baked);
            }
            if (count == 0) return false;
            Vector3 toes = sum / count - ankles;
            toes = container.transform.InverseTransformDirection(toes);
            if (toes.z < -0.005f)
            {
                Flip(container, "toes pointed backwards");
                return true;
            }
            return false;
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
