Shader "Custom/ShadowOnly"
{
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Blend Zero OneMinusSrcAlpha
        ZWrite Off
        ColorMask RGB

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        struct Input { float2 uv_MainTex; };

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            o.Albedo = 0;
            o.Alpha = 0;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
