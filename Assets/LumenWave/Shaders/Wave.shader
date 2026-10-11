// Expanding sound-wave ring on a quad (additive). _Radius is the ring radius as a fraction of the quad
// half-size (0..1). The look of a pressure wave: a hot, sharp leading edge with a slight chromatic split
// (red just outside, blue just inside, like light bent by the compressed air), a softer compression zone
// behind it, a faint trailing rarefaction ring and a fine wake. A little angular wobble keeps it from
// reading as a perfect hoop.
Shader "LumenWave/Wave"
{
    Properties
    {
        [HDR] _Color("Color", Color) = (0.6, 1.0, 2.0, 1)
        _Radius("Radius", Range(0, 1)) = 0.5
        _Thickness("Thickness", Range(0.002, 0.5)) = 0.04
        _Alpha("Alpha", Range(0, 1)) = 1
        _Trail("Trailing ring (fraction of radius)", Range(0, 1)) = 0.72
        _Wobble("Wobble", Range(0, 0.05)) = 0.008
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
                half _Trail;
                half _Wobble;
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
                float2 c = i.uv - 0.5;
                float d0 = length(c) * 2.0;
                float ang = atan2(c.y, c.x);
                float t = _Time.y;
                float d = d0 + _Wobble * (sin(ang * 7.0 + t * 6.0) * 0.6 + sin(ang * 13.0 - t * 9.0) * 0.4);
                float th = max(_Thickness, 1e-3);

                // Leading edge: sharp outside, softer compression zone inside.
                float x = (d - _Radius) / th;
                float edge = x > 0.0 ? exp(-x * x * 3.0) : exp(-x * x * 1.1);
                float core = exp(-x * x * 6.0);                          // white-hot centre line

                // Chromatic split around the edge.
                float xr = (d - _Radius - th * 0.7) / th;
                float xb = (d - _Radius + th * 0.9) / th;
                float3 chroma = float3(1.0, 0.35, 0.25) * exp(-xr * xr * 2.0) * 0.55 + float3(0.25, 0.45, 1.3) * exp(-xb * xb * 2.0) * 0.5;

                // Trailing (rarefaction) ring and fine wake.
                float tr = _Radius * _Trail;
                float xt = (d - tr) / (th * 1.8);
                float trail = exp(-xt * xt) * 0.28;
                float wake = (d < _Radius) ? 0.05 * exp(-(_Radius - d) * 5.0) * (0.6 + 0.4 * sin(d * 70.0 - t * 25.0)) : 0.0;

                float fadeOut = smoothstep(1.0, 0.92, d0);
                float3 col = _Color.rgb * (edge * 0.7 + trail + wake) + chroma * length(_Color.rgb) * 0.45 + core * float3(1.0, 1.0, 1.0) * 0.9;
                return half4(col * _Alpha * fadeOut, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
