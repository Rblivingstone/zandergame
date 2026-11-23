using System.Collections.Generic;
using UnityEngine;

namespace ZanderGame
{
    /// <summary>
    /// Converts imported model materials to the anime cel shader
    /// (ZanderGame/Toon) so characters and props share one consistent look.
    /// Face details (eyes, lashes, blush...) and printed trims get no outline.
    /// </summary>
    public static class ToonStyler
    {
        static Shader _toon;
        static readonly Dictionary<int, Material> Cache = new Dictionary<int, Material>();

        static readonly string[] NoOutline =
        {
            "Blush", "Iris", "Pupil", "Eye", "Lash", "Brow", "Mouth", "Tongue", "Trim", "Seam", "Tooth", "Gem",
            "Glow", "Line", "Nose", "Shine", "Led", "Rune", "Net"
        };

        public static Shader Toon
        {
            get
            {
                if (_toon == null) _toon = Shader.Find("ZanderGame/Toon");
                return _toon;
            }
        }

        static readonly Dictionary<string, Dictionary<string, string>> Manifests =
            new Dictionary<string, Dictionary<string, string>>();

        /// <summary>Material -> painted texture list written by the Blender export
        /// (Resources/Models/[model]_textures.txt).</summary>
        static Dictionary<string, string> Manifest(string model)
        {
            if (string.IsNullOrEmpty(model)) return null;
            if (Manifests.TryGetValue(model, out var map)) return map;
            map = new Dictionary<string, string>();
            var txt = Resources.Load<TextAsset>("Models/" + model + "_textures");
            if (txt != null)
                foreach (var line in txt.text.Split('\n'))
                {
                    int eq = line.IndexOf('=');
                    if (eq > 0) map[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
                }
            Manifests[model] = map;
            return map;
        }

        static Texture2D PaintedTexture(Dictionary<string, string> map, string materialName)
        {
            if (map == null || map.Count == 0) return null;
            string n = materialName.Replace(" (Instance)", "").Trim();
            if (!map.TryGetValue(n, out var tex))
            {
                // Unity sometimes appends suffixes; fall back to a prefix match
                foreach (var kv in map)
                    if (n.StartsWith(kv.Key)) { tex = kv.Value; break; }
            }
            return tex != null ? Resources.Load<Texture2D>("Textures/Models/" + tex) : null;
        }

        public static void Apply(GameObject root, float outlineWidth, string modelName = null)
        {
            if (Toon == null) return;
            _currentMap = Manifest(modelName);
            foreach (var r in root.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                    if (mats[i] != null) mats[i] = Convert(mats[i], outlineWidth);
                r.sharedMaterials = mats;
            }
        }

        static Dictionary<string, string> _currentMap;

        static Material Convert(Material src, float outline)
        {
            int key = src.GetInstanceID() * 31 + Mathf.RoundToInt(outline * 100000);
            if (Cache.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(Toon) { name = src.name };
            if (src.HasProperty("_Color")) m.SetColor("_Color", src.color);
            else if (src.HasProperty("_BaseColor")) m.SetColor("_Color", src.GetColor("_BaseColor"));
            var painted = PaintedTexture(_currentMap, src.name);
            if (painted != null)
            {
                m.mainTexture = painted;
                m.SetColor("_Color", Color.white);   // the painting carries all the colour
            }
            else if (src.HasProperty("_MainTex") && src.mainTexture != null) m.mainTexture = src.mainTexture;
            if (src.HasProperty("_EmissionColor"))
            {
                var e = src.GetColor("_EmissionColor");
                if (e.maxColorComponent > 0.01f) m.SetColor("_EmissionColor", e);
            }
            bool noOutline = false;
            foreach (var t in NoOutline)
                if (src.name.Contains(t)) noOutline = true;
            m.SetFloat("_OutlineWidth", noOutline ? 0f : outline);
            // metal reads better with a cooler, darker shadow
            if (src.name.Contains("Metal")) m.SetColor("_ShadowColor", new Color(0.45f, 0.48f, 0.62f));
            Cache[key] = m;
            return m;
        }
    }
}
