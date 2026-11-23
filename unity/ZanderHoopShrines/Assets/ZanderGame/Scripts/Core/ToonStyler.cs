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

        public static void Apply(GameObject root, float outlineWidth)
        {
            if (Toon == null) return;
            foreach (var r in root.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                    if (mats[i] != null) mats[i] = Convert(mats[i], outlineWidth);
                r.sharedMaterials = mats;
            }
        }

        static Material Convert(Material src, float outline)
        {
            int key = src.GetInstanceID() * 31 + Mathf.RoundToInt(outline * 100000);
            if (Cache.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(Toon) { name = src.name };
            if (src.HasProperty("_Color")) m.SetColor("_Color", src.color);
            else if (src.HasProperty("_BaseColor")) m.SetColor("_Color", src.GetColor("_BaseColor"));
            if (src.HasProperty("_MainTex") && src.mainTexture != null) m.mainTexture = src.mainTexture;
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
