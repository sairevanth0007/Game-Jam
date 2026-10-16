// Echo Path bridge: a frozen sound wave turned into solid shadow. Dark body, glowing sine rim along the
// top edge (uv.y → 1) that ripples at the note's frequency, and a noisy dissolve driven by _Fade (0 = solid, 1 = gone).
Shader "LumenWave/EchoBridge"
{
    Properties
    {
        _BaseColor("Base", Color) = (0.01, 0.012, 0.02, 1)
        [HDR] _Color("Rim glow", Color) = (0.6, 1.2, 2.4, 1)
        _Fade("Fade", Range(0, 1)) = 0
        _Frequency("Ripple frequency", Float) = 3
        _Dir("Travel direction", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _Color;
                half _Fade;
                half _Frequency;
                half _Dir;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 positionWS : TEXCOORD1; half fogFactor : TEXCOORD2; };

            float hash21(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }
            float vnoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(hash21(i), hash21(i + float2(1, 0)), u.x), lerp(hash21(i + float2(0, 1)), hash21(i + float2(1, 1)), u.x), u.y);
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.uv = v.uv;
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float n = vnoise(i.positionWS.xy * 3.0 + i.positionWS.z) * 0.7 + vnoise(i.positionWS.xy * 9.0) * 0.3;
                clip(n - _Fade * 1.05);
                float dissolveEdge = 1.0 - smoothstep(0.0, 0.08, n - _Fade * 1.05);
                float ripple = 0.5 + 0.5 * sin(i.positionWS.x * _Frequency * 6.2831 - _Time.y * 5.0);
                // Wave packets travelling along the bridge, away from the singer.
                float xs = i.positionWS.x * _Dir;
                float packet = pow(saturate(0.5 + 0.5 * sin(xs * 1.6 - _Time.y * 9.0)), 10.0);
                float packet2 = pow(saturate(0.5 + 0.5 * sin(xs * 0.7 - _Time.y * 5.0 + 1.3)), 6.0);
                float rim = pow(saturate(i.uv.y), 10.0) * (0.55 + 0.7 * ripple + 1.6 * packet) + pow(saturate(1.0 - i.uv.y), 14.0) * (0.35 + 0.8 * packet2);
                // Faint horizontal "sound lines" inside the dark body, lit as packets pass.
                float lines = pow(saturate(0.5 + 0.5 * sin(i.uv.y * 46.0)), 24.0) * (0.06 + 0.35 * packet) * step(i.uv.y, 0.97) * step(0.03, i.uv.y);
                float inner = pow(saturate(i.uv.y), 3.0) * 0.12;
                half3 col = _BaseColor.rgb + _Color.rgb * (rim + lines + inner + dissolveEdge * 1.5 * step(0.001, _Fade));
                col = MixFog(col, i.fogFactor);
                return half4(col, 1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/SHADOWCASTER"
        UsePass "Universal Render Pipeline/Lit/DEPTHONLY"
    }
    FallBack Off
}
