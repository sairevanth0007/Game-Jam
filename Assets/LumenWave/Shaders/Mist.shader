// Drifting mist sheet (vertical quad between depth layers). Alpha-blended fog colour shaped by scrolling
// value noise, fading out towards all quad edges.
Shader "LumenWave/Mist"
{
    Properties
    {
        _Color("Color (a = density)", Color) = (0.1, 0.12, 0.18, 0.5)
        _NoiseScale("Noise scale (x, y)", Vector) = (0.05, 0.25, 0, 0)
        _Speed("Drift speed", Float) = 0.3
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-50" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Mist"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float4 _NoiseScale;
                float _Speed;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 positionWS : TEXCOORD1; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.uv = v.uv;
                return o;
            }

            float hash21(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }
            float vnoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(hash21(i), hash21(i + float2(1, 0)), f.x), lerp(hash21(i + float2(0, 1)), hash21(i + 1), f.x), f.y);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float t = _Time.y * _Speed;
                float2 p = i.positionWS.xy * _NoiseScale.xy;
                float n = vnoise(p + float2(t, 0)) * 0.6 + vnoise(p * 2.3 + float2(-t * 1.7, 3.1)) * 0.4;
                float edgeX = smoothstep(0.0, 0.06, i.uv.x) * smoothstep(1.0, 0.94, i.uv.x);
                float edgeY = smoothstep(0.0, 0.3, i.uv.y) * smoothstep(1.0, 0.4, i.uv.y);
                float a = _Color.a * edgeX * edgeY * saturate(n * 1.5 - 0.15);
                return half4(_Color.rgb, a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
