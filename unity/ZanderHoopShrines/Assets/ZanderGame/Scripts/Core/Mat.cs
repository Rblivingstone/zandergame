using System.Collections.Generic;
using UnityEngine;

namespace ZanderGame
{
    /// <summary>Runtime material factory (Built-in render pipeline "Standard" shader).</summary>
    public static class Mat
    {
        static Shader _std;
        static Shader _unlit;
        static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();

        public static Shader Std
        {
            get
            {
                if (_std == null)
                {
                    var baseMat = Resources.Load<Material>("Materials/ZG_Base");
                    _std = baseMat != null ? baseMat.shader : null;
                    if (_std == null) _std = Shader.Find("Standard");
                    if (_std == null) _std = Shader.Find("Universal Render Pipeline/Lit");
                    if (_std == null) _std = Shader.Find("Diffuse");
                }
                return _std;
            }
        }

        public static Shader Unlit
        {
            get
            {
                if (_unlit == null) _unlit = Shader.Find("Sprites/Default");
                if (_unlit == null) _unlit = Shader.Find("Unlit/Color");
                return _unlit;
            }
        }

        public static Color Hex(string hex)
        {
            if (!hex.StartsWith("#")) hex = "#" + hex;
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }

        public static Material Color(Color c, float smoothness = 0.3f, float metallic = 0f)
        {
            string key = "c" + c + smoothness + metallic;
            if (Cache.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(Std) { color = c, name = "ZG_" + ColorUtility.ToHtmlStringRGB(c) };
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            Cache[key] = m;
            return m;
        }

        public static Material Color(string hex, float smoothness = 0.3f, float metallic = 0f) =>
            Color(Hex(hex), smoothness, metallic);

        /// <summary>A NEW (not cached) glowing material - safe to animate per object.</summary>
        public static Material Glow(Color baseColor, Color emission, float intensity = 1.5f)
        {
            var m = new Material(Std) { color = baseColor, name = "ZG_Glow" };
            SetEmission(m, emission * intensity);
            return m;
        }

        public static Material Unique(Color c, float smoothness = 0.3f)
        {
            var m = new Material(Std) { color = c, name = "ZG_Unique" };
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            return m;
        }

        public static void SetEmission(Material m, Color emission)
        {
            if (m == null) return;
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", emission);
        }

        public static Material Glass(Color c)
        {
            string key = "glass" + c;
            if (Cache.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(Std) { color = c, name = "ZG_Glass" };
            MakeTransparent(m);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.9f);
            Cache[key] = m;
            return m;
        }

        public static void MakeTransparent(Material m)
        {
            if (!m.HasProperty("_Mode")) return;
            m.SetFloat("_Mode", 3);
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON");
            m.DisableKeyword("_ALPHABLEND_ON");
            m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = 3000;
        }

        public static Material Textured(Texture2D tex, Vector2 tiling, float smoothness = 0.35f)
        {
            var m = new Material(Std) { mainTexture = tex, name = "ZG_Tex_" + tex.name };
            m.mainTextureScale = tiling;
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            return m;
        }

        public static Material Line(Color c)
        {
            var m = new Material(Unlit) { color = c, name = "ZG_Line" };
            return m;
        }
    }
}
