// Soft additive glow sprite (eyes, halos, lamp cores). Use on a Quad; does not need vertex colours.
Shader "LumenWave/Glow"
{
    Properties
    {
        [HDR] _Color("Color", Color) = (1.5, 1.5, 1.5, 1)
        _Falloff("Falloff", Range(0.5, 8)) = 2
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Glow"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Falloff;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float d = saturate(length(i.uv - 0.5) * 2.0);
                float g = pow(1.0 - d, _Falloff) + 0.6 * pow(1.0 - d, _Falloff * 6.0);
                return half4(_Color.rgb * g * _Color.a, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
