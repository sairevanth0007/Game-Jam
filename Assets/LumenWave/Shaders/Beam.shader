// Volumetric-looking light beam (additive). Mesh UVs: u = across the beam (0..1), v = along it (0 at source, 1 at end).
// Beer–Lambert law: light travelling a distance d through fog with extinction coefficient σ keeps I = I0·e^(−σd).
// Dust inside the beam is a scrolling value noise so the shaft feels like it scatters through particles.
Shader "LumenWave/Beam"
{
    Properties
    {
        [HDR] _Color("Color", Color) = (2, 0.3, 0.2, 1)
        _Intensity("Intensity", Float) = 1
        _FogSigma("Fog extinction sigma", Float) = 0.06
        _Length("Beam length (world units)", Float) = 10
        _Flicker("Flicker", Range(0, 1)) = 0.15
        _Core("Core sharpness", Range(1, 60)) = 18
        _PathStart("Optical path before this segment", Float) = 0
        _NearRadius("Inverse-square reference distance", Float) = 3
        _HotCore("White-hot core (lethal)", Range(0, 1)) = 0
        _Wobble("Heat shimmer", Range(0, 0.3)) = 0
        _Pulse("Pulse", Range(0, 1)) = 0
        _Dust("Dust amount", Range(0, 1)) = 0.6
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Beam"
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
                half _Intensity;
                half _FogSigma;
                half _Length;
                half _Flicker;
                half _Core;
                float _PathStart;
                float _NearRadius;
                half _HotCore;
                half _Wobble;
                half _Pulse;
                half _Dust;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 positionWS : TEXCOORD1; };

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
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float along = i.uv.y * _Length;
                float att = exp(-_FogSigma * along);                    // Beer–Lambert
                // Inverse square along the whole optical path: brighter and wider near the source.
                float path = _PathStart + along;
                float r0 = max(0.1, _NearRadius);
                float inv = r0 * r0 / (r0 * r0 + path * path);
                float near = saturate(0.35 + 0.65 * inv * 1.6);
                // Heat shimmer: the centre line wobbles with scrolling noise (lethal beams).
                float wob = (vnoise(float2(along * 2.7 - _Time.y * 9.0, _Time.y * 3.0)) - 0.5) * 2.0 * _Wobble;
                float across = abs((i.uv.x + wob) * 2.0 - 1.0);
                float widthK = lerp(0.75, 1.25, near);
                float ax = across / widthK;
                float body = exp(-ax * ax * 3.0);                        // soft gaussian shaft
                float core = exp(-ax * ax * _Core);                      // hot centre line
                float ends = smoothstep(0.0, 0.04, i.uv.y) * smoothstep(1.0, 0.85, i.uv.y);
                float dust = vnoise(float2(i.positionWS.x * 2.3 + i.positionWS.y * 0.7, i.positionWS.y * 2.3 - _Time.y * 0.6));
                dust = lerp(1.0, 0.55 + 0.9 * dust, _Dust);
                float flick = 1.0 - _Flicker * (0.5 + 0.5 * sin(_Time.y * 31.0 + along * 0.7)) * vnoise(float2(_Time.y * 7.0, 0));
                float pulse = 1.0 + _Pulse * 0.35 * sin(_Time.y * 9.0);
                float a = (body * 0.45 * dust + core * 1.1) * att * ends * flick * _Intensity * lerp(0.55, 1.35, near) * pulse;
                // Lethal: the core burns white-hot, the edges stay red-orange.
                half3 hot = lerp(_Color.rgb, max(_Color.rgb, half3(1, 0.85, 0.7) * max(_Color.r, max(_Color.g, _Color.b))) * 1.2, core * core * core * _HotCore);
                // Lethal edges lean orange (heat), the body stays clearly red.
                hot = lerp(hot, hot * half3(1.0, 1.0 + 0.6 * (1 - core), 1.0), _HotCore * body);
                return half4(hot * a, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
