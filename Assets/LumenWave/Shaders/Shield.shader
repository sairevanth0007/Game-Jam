// Resonance shield: a standing wave on a dome, drawn as a Chladni figure.
// For a square plate the nodal pattern of mode (n, m) is
//   cos(nπx)cos(mπy) − cos(mπx)cos(nπy) = 0
// Sand gathers on these nodal lines (where the plate does not move). A standing wave's amplitude
// oscillates in time as cos(ωt) while the nodes stay fixed – exactly what is animated here.
// On top: a thin-film (iridescent) Fresnel edge, a refraction-like shimmer (the pattern is warped by a
// travelling interference field, as if seen through moving air) and up to four hit ripples
// (_HitN.xyz = object-space direction of the impact, _HitN.w = age in seconds; < 0 = unused).
Shader "LumenWave/Shield"
{
    Properties
    {
        [HDR] _Color("Color", Color) = (0.5, 1.0, 2.2, 1)
        _ModeN("Mode N", Float) = 3
        _ModeM("Mode M", Float) = 5
        _Phase("Phase", Float) = 0
        _Alpha("Alpha", Range(0, 1)) = 0.85
        _LineWidth("Nodal line width", Range(0.01, 0.5)) = 0.12
        _Hit0("Hit 0", Vector) = (0, 0, -1, -1)
        _Hit1("Hit 1", Vector) = (0, 0, -1, -1)
        _Hit2("Hit 2", Vector) = (0, 0, -1, -1)
        _Hit3("Hit 3", Vector) = (0, 0, -1, -1)
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Shield"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _ModeN;
                half _ModeM;
                half _Phase;
                half _Alpha;
                half _LineWidth;
                float4 _Hit0;
                float4 _Hit1;
                float4 _Hit2;
                float4 _Hit3;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.positionOS = v.positionOS.xyz;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            // Expanding ring on the sphere around an impact direction: angular distance vs. age.
            float HitRipple(float3 dirOS, float4 hit)
            {
                if (hit.w < 0.0 || hit.w > 0.9) return 0.0;
                float ang = acos(clamp(dot(dirOS, normalize(hit.xyz)), -1.0, 1.0));   // 0..π
                float front = hit.w * 4.2;                                               // radians / s
                float x = (ang - front) / 0.16;
                float ring = exp(-x * x);
                float trail = (ang < front) ? 0.35 * exp(-(front - ang) * 3.0) * (0.5 + 0.5 * cos((front - ang) * 30.0)) : 0.0;
                float spot = exp(-ang * ang * 40.0) * saturate(1.0 - hit.w * 5.0) * 0.8;
                return (ring + trail) * (1.0 - hit.w / 0.9) + spot;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float t = _Phase + _Time.y * 5.0;
                // Refraction-like shimmer: warp the plate coordinates with a slow travelling interference field.
                float2 p = i.positionOS.xy * 2.0;
                float2 warp = float2(sin(p.y * 5.0 + _Time.y * 3.1) + sin((p.x + p.y) * 7.0 - _Time.y * 4.3),
                                     cos(p.x * 6.0 - _Time.y * 2.7) + sin((p.x - p.y) * 8.0 + _Time.y * 3.7)) * 0.025;
                float2 q = p + warp;
                float n = _ModeN, m = _ModeM;
                float chladni = cos(n * PI * q.x) * cos(m * PI * q.y) - cos(m * PI * q.x) * cos(n * PI * q.y);
                float amp = cos(t);                                     // standing wave: nodes fixed, amplitude oscillates
                float nodal = 1.0 - smoothstep(0.0, _LineWidth, abs(chladni));
                float antinode = abs(chladni * amp) * 0.16;

                float3 nrm = normalize(i.normalWS);
                float3 v = GetWorldSpaceNormalizeViewDir(i.positionWS);
                float ndv = saturate(dot(nrm, v));
                float fres = pow(1.0 - ndv, 3.0);
                float rim = pow(1.0 - ndv, 7.0);

                // Thin-film iridescence on the edge: hue cycles with viewing angle and time.
                float film = (1.0 - ndv) * 6.0 + _Time.y * 0.8;
                float3 irid = 0.5 + 0.5 * cos(6.2831 * (film + float3(0.0, 0.33, 0.67)));
                float3 edgeCol = lerp(_Color.rgb, irid * length(_Color.rgb) * 0.8, 0.45);

                // Faint moving caustic bands inside (sells "looking through" the dome).
                float caustic = pow(saturate(0.5 + 0.5 * sin(dot(q, float2(9.0, 4.0)) + _Time.y * 2.0 + warp.x * 40.0)), 16.0) * 0.05;

                float3 dirOS = normalize(i.positionOS);
                float hits = HitRipple(dirOS, _Hit0) + HitRipple(dirOS, _Hit1) + HitRipple(dirOS, _Hit2) + HitRipple(dirOS, _Hit3);

                float shimmer = 0.75 + 0.25 * sin(t * 2.0);
                float3 col = _Color.rgb * (nodal * 0.85 * shimmer + antinode + caustic) * (1.0 - fres * 0.3)
                           + edgeCol * (fres * 0.75 + rim * 1.1)
                           + float3(1.0, 1.0, 1.0) * hits * 0.6 + _Color.rgb * hits * 0.5;
                return half4(col * _Alpha, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
