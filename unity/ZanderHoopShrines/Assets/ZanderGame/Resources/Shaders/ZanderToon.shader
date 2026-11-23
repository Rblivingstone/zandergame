// Anime-style cel shading with ink outlines for Zander's Hoop Shrines
// (Built-in Render Pipeline). Applied at runtime to every Blender model.
//  - two-tone light/shadow with a soft edge and a coloured shadow tint
//  - subtle rim light
//  - screen-space constant-width inverted-hull outline
// Lives in a Resources folder so it is always included in builds.
Shader "ZanderGame/Toon"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Texture", 2D) = "white" {}
        [HDR] _EmissionColor ("Emission", Color) = (0,0,0,0)
        _ShadowColor ("Shadow Tint", Color) = (0.68,0.64,0.82,1)
        _ShadeStep ("Shade Step", Range(-1,1)) = 0.1
        _Softness ("Shade Softness", Range(0.001,0.5)) = 0.05
        _RimColor ("Rim Color", Color) = (1,1,1,1)
        _RimPower ("Rim Power", Range(0.5,8)) = 3.5
        _RimStrength ("Rim Strength", Range(0,1)) = 0.22
        _OutlineColor ("Outline Color", Color) = (0.14,0.09,0.1,1)
        _OutlineWidth ("Outline Width", Range(0,0.02)) = 0.0035
        _Cutoff ("Alpha Cutoff (banners)", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }

        Pass
        {
            Name "OUTLINE"
            Cull Front
            ZWrite On
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            float _OutlineWidth;
            fixed4 _OutlineColor;
            fixed4 _Color;
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct v2f { float4 pos : SV_POSITION; UNITY_FOG_COORDS(0) };
            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                float3 n = mul((float3x3)UNITY_MATRIX_IT_MV, v.normal);
                float2 off = TransformViewToProjection(n.xy);
                float len = max(length(off), 1e-5);
                float aspect = _ScreenParams.y / _ScreenParams.x;
                off = off / len;
                off.x *= aspect;
                o.pos.xy += off * _OutlineWidth * min(o.pos.w, 12.0) * 2.0;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }
            fixed4 frag (v2f i) : SV_Target
            {
                clip(_OutlineWidth - 1e-6);   // no outline requested: draw nothing
                fixed4 c = _OutlineColor;
                // outlines pick up a hint of the surface colour, like anime ink lines
                c.rgb = lerp(c.rgb, c.rgb * _Color.rgb, 0.35);
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }

        CGPROGRAM
        #pragma surface surf Toon fullforwardshadows alphatest:_Cutoff addshadow
        #pragma target 3.0
        sampler2D _MainTex;
        fixed4 _Color;
        fixed4 _EmissionColor;
        fixed4 _ShadowColor;
        half _ShadeStep;
        half _Softness;
        fixed4 _RimColor;
        half _RimPower;
        half _RimStrength;

        struct Input { float2 uv_MainTex; float3 viewDir; };

        half4 LightingToon (SurfaceOutput s, half3 lightDir, half atten)
        {
            half ndl = dot(s.Normal, lightDir);
            half lit = smoothstep(_ShadeStep - _Softness, _ShadeStep + _Softness, ndl);
            half4 c;
        #ifdef UNITY_PASS_FORWARDADD
            // extra lights (lanterns, lamps): a cel-shaded pool of light that fades with
            // distance (atten). No shadow-tint fill here, or every light would brighten
            // everything in its range.
            c.rgb = s.Albedo * _LightColor0.rgb * lit * atten;
        #else
            // main light (sun): two-tone cel shading with a coloured shadow side
            half shadow = smoothstep(0.25, 0.6, atten);
            half3 shade = lerp(_ShadowColor.rgb, half3(1, 1, 1), lit * shadow);
            c.rgb = s.Albedo * _LightColor0.rgb * shade;
        #endif
            c.a = s.Alpha;
            return c;
        }

        void surf (Input IN, inout SurfaceOutput o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            o.Alpha = c.a;
            half rim = 1.0 - saturate(dot(normalize(IN.viewDir), o.Normal));
            o.Emission = _EmissionColor.rgb + _RimColor.rgb * pow(rim, _RimPower) * _RimStrength * c.rgb;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
