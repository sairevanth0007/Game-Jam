// Expanding sound-wave ring on a quad (additive). _Radius is the ring radius as a fraction of the quad
// half-size (0..1); a faint trailing wake behind the wavefront hints at the propagating pressure wave.
Shader "LumenWave/Wave"
{
    Properties
    {
        [HDR] _Color("Color", Color) = (0.6, 1.0, 2.0, 1)
        _Radius("Radius", Range(0, 1)) = 0.5
        _Thickness("Thickness", Range(0.002, 0.5)) = 0.04
        _Alpha("Alpha", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Wave"
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
                half _Radius;
                half _Thickness;
                half _Alpha;
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
                float d = length(i.uv - 0.5) * 2.0;
                float x = (d - _Radius) / max(_Thickness, 1e-3);
                float ring = exp(-x * x);
                float wake = (d < _Radius) ? 0.12 * exp(-(_Radius - d) * 6.0) * (0.6 + 0.4 * sin(d * 60.0)) : 0.0;
                float edge = smoothstep(1.0, 0.92, d);
                return half4(_Color.rgb * (ring + wake) * _Alpha * edge, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
