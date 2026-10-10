// Background sky: vertical gradient with a horizon band and a soft moon. Ignores fog (it *is* the fog's backdrop).
Shader "LumenWave/Sky"
{
    Properties
    {
        _TopColor("Top", Color) = (0.01, 0.015, 0.035, 1)
        _HorizonColor("Horizon", Color) = (0.06, 0.09, 0.16, 1)
        _BottomColor("Bottom", Color) = (0.02, 0.03, 0.06, 1)
        _Horizon("Horizon height (uv)", Range(0, 1)) = 0.38
        [HDR] _MoonColor("Moon", Color) = (1.2, 1.3, 1.6, 1)
        _MoonPos("Moon position (uv)", Vector) = (0.7, 0.72, 0, 0)
        _MoonSize("Moon size", Range(0.001, 0.2)) = 0.035
        _Aspect("Quad aspect (width/height)", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry+10" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Sky"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _TopColor;
                half4 _HorizonColor;
                half4 _BottomColor;
                half _Horizon;
                half4 _MoonColor;
                float4 _MoonPos;
                half _MoonSize;
                float _Aspect;
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

            float hash21(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }

            half4 frag(Varyings i) : SV_Target
            {
                float y = i.uv.y;
                half3 col = (y > _Horizon)
                    ? lerp(_HorizonColor.rgb, _TopColor.rgb, smoothstep(_Horizon, 1.0, y))
                    : lerp(_BottomColor.rgb, _HorizonColor.rgb, smoothstep(0.0, _Horizon, y));
                float2 d = (i.uv - _MoonPos.xy) * float2(max(_Aspect, 0.01), 1.0);
                float r = length(d);
                col += _MoonColor.rgb * (smoothstep(_MoonSize, _MoonSize * 0.85, r) + 0.25 * exp(-r * 9.0) + 0.06 * exp(-r * 2.5));
                // Sparse faint stars in the upper sky.
                float2 g = floor(i.uv * float2(210.0 * max(_Aspect, 0.01), 210.0));
                float s = hash21(g);
                col += step(0.9965, s) * smoothstep(_Horizon, 1.0, y) * 0.35 * (0.6 + 0.4 * sin(_Time.y * 2.0 + s * 50.0));
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
