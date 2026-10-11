// Dash afterimage for the skinned hero: additive, rim-only (Schlick-style Fresnel), so the ghost reads as
// a cyan outline of Umbra's pose rather than a translucent blob. _BaseColor.a is the fade (UmbraGhost).
Shader "LumenWave/UmbraGhost"
{
    Properties
    {
        [HDR] _BaseColor("Color (alpha = fade)", Color) = (0.5, 1.4, 2.4, 1)
        _RimPower("Rim power", Range(0.5, 8)) = 2.2
        _Fill("Inner fill", Range(0, 1)) = 0.05
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Ghost"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _RimPower;
                half _Fill;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                float3 v = GetWorldSpaceNormalizeViewDir(i.positionWS);
                float rim = pow(1.0 - saturate(abs(dot(n, v))), _RimPower);
                return half4(_BaseColor.rgb * _BaseColor.a * (rim + _Fill), 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
