using UnityEngine;

namespace ZanderGame
{
    /// <summary>Helpers for building level geometry out of primitives.</summary>
    public static class Build
    {
        public const int IgnoreRaycastLayer = 2;

        public static GameObject Group(string name, Transform parent, Vector3 pos = default, float yaw = 0f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            return go;
        }

        public static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPos,
            Vector3 scale, Material mat, bool collider = true, Quaternion? rot = null)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = rot ?? Quaternion.identity;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            if (mat != null) r.sharedMaterial = mat;
            if (!collider)
            {
                var c = go.GetComponent<Collider>();
                if (c != null) Object.Destroy(c);
            }
            return go;
        }

        public static GameObject Box(string name, Transform parent, Vector3 center, Vector3 size, Material mat,
            bool collider = true, Quaternion? rot = null) =>
            Prim(PrimitiveType.Cube, name, parent, center, size, mat, collider, rot);

        /// <summary>A box with stone texture tiled to its size.</summary>
        public static GameObject StoneBox(string name, Transform parent, Vector3 center, Vector3 size)
        {
            var tiling = new Vector2(Mathf.Max(1, Mathf.Max(size.x, size.z) / 2f), Mathf.Max(1, size.y / 2f));
            var m = Mat.Textured(ProceduralTextures.Stone(), tiling, 0.1f);
            return Box(name, parent, center, size, m);
        }

        /// <summary>Invisible collider only.</summary>
        public static GameObject Collider(string name, Transform parent, Vector3 center, Vector3 size,
            Quaternion? rot = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localRotation = rot ?? Quaternion.identity;
            var bc = go.AddComponent<BoxCollider>();
            bc.size = size;
            return go;
        }

        /// <summary>Flat textured quad facing up (with proper UVs), plus a box collider under it.</summary>
        public static GameObject Floor(string name, Transform parent, Vector3 center, float width, float length,
            Material mat, float thickness = 1f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            var mesh = new Mesh { name = name + "_quad" };
            float hx = width / 2, hz = length / 2;
            mesh.vertices = new[]
            {
                new Vector3(-hx, 0, -hz), new Vector3(hx, 0, -hz), new Vector3(hx, 0, hz), new Vector3(-hx, 0, hz)
            };
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            var bc = go.AddComponent<BoxCollider>();
            bc.size = new Vector3(width, thickness, length);
            bc.center = new Vector3(0, -thickness / 2, 0);
            // Underside so the floor looks solid from below (pits)
            Box(name + "_under", go.transform, new Vector3(0, -thickness / 2 - 0.01f, 0),
                new Vector3(width, thickness, length), Mat.Color("#3A3F4C", 0.1f), false);
            return go;
        }

        public static TextMesh Label(string text, Transform parent, Vector3 localPos, float size, Color color,
            float yaw = 0f, bool billboard = false)
        {
            var go = new GameObject("Label_" + text.Replace("\n", " "));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.font = Compat.DefaultFont();
            if (tm.font != null) go.GetComponent<MeshRenderer>().sharedMaterial = tm.font.material;
            tm.fontSize = 64;
            tm.characterSize = size / 10f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            if (billboard) go.AddComponent<Billboard>();
            return tm;
        }

        public static Light PointLight(Transform parent, Vector3 pos, Color c, float range, float intensity)
        {
            var go = new GameObject("Light");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = c;
            l.range = range;
            l.intensity = intensity;
            l.shadows = LightShadows.None;
            return l;
        }

        public static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform t in go.transform) SetLayerRecursive(t.gameObject, layer);
        }

        public static void StripColliders(GameObject go)
        {
            foreach (var c in go.GetComponentsInChildren<Collider>()) Object.Destroy(c);
        }
    }

    /// <summary>Keeps a text label facing the camera.</summary>
    public class Billboard : MonoBehaviour
    {
        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;
            var d = transform.position - cam.transform.position;
            d.y = 0;
            if (d.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(d);
        }
    }

    /// <summary>Slow spin + bob for collectibles/trophies.</summary>
    public class Spinner : MonoBehaviour
    {
        public float DegreesPerSecond = 60f;
        public float BobHeight = 0.1f;
        Vector3 _base;

        void Start() => _base = transform.localPosition;

        void Update()
        {
            transform.Rotate(0, DegreesPerSecond * Time.deltaTime, 0, Space.World);
            transform.localPosition = _base + Vector3.up * Mathf.Sin(Time.time * 2f) * BobHeight;
        }
    }
}
