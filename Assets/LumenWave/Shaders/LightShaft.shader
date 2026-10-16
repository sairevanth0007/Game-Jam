// Additive soft light shaft (god rays from the moon/sun, cones under lamps). uv.x across (soft Gaussian
// profile), uv.y along the shaft from the source (0) to its end (1). Slow shimmer so the air feels alive.
Shader "LumenWave/LightShaft"
{
    Properties
    {
        [HDR] _Color("Color", Color) = (0.3, 0.35, 0.45, 1)
        _Softness("Edge softness", Range(1, 12)) = 4
        _Falloff("Falloff along shaft", Range(0.2, 6)) = 1.5
        _Shimmer("Shimmer", Range(0, 1)) = 0.3
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "LightShaft"
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
                half _Softness;
                half _Falloff;
                half _Shimmer;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float seed : TEXCOORD1; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                float3 origin = TransformObjectToWorld(float3(0, 0, 0));
                o.seed = frac(dot(origin, float3(0.137, 0.311, 0.071)));
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float u = i.uv.x * 2 - 1;
                float across = exp(-u * u * _Softness);
                float v = saturate(i.uv.y);
                float along = pow(1 - v, _Falloff) * smoothstep(0.0, 0.08, v);
                float t = _Time.y;
                float sh = 1 - _Shimmer + _Shimmer * (0.5 + 0.5 * sin(t * 0.7 + i.seed * 40 + u * 2.0 + v * 3.0) * sin(t * 0.43 + i.seed * 17));
                return half4(_Color.rgb * (across * along * sh * _Color.a), 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
