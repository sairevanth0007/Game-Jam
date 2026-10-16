// Level stone: procedural world-space masonry (offset brick courses, mortar, per-block tone, hairline
// cracks, grain) lit by URP's PBR path, so beams and lamps really light the walls (Forward+ cluster loop).
// A vertical gradient below the walkable top edge (_TopY, per block) fades the face into the fog,
// keeping the platform lip readable while the mass beneath sinks into darkness.
Shader "LumenWave/StoneFace"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (0.05, 0.055, 0.07, 1)
        [HDR] _EdgeColor("Top edge tint", Color) = (0.4, 0.6, 0.9, 1)
        _TopY("Top Y (world)", Float) = 0
        _Smoothness("Smoothness", Range(0, 1)) = 0.18
        _BrickSize("Brick size (w, h)", Vector) = (1.15, 0.5, 0, 0)
        _Fade("Darkening depth below top", Float) = 4.5
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _EdgeColor;
                float _TopY;
                half _Smoothness;
                float4 _BrickSize;
                float _Fade;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half fogFactor : TEXCOORD2;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            float hash21(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }
            float vnoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(hash21(i), hash21(i + float2(1, 0)), f.x), lerp(hash21(i + float2(0, 1)), hash21(i + 1), f.x), f.y);
            }

            // Returns (tone multiplier, mortar 0..1) for a 2D masonry coordinate.
            float2 masonry(float2 uv)
            {
                float2 bs = _BrickSize.xy;
                float row = floor(uv.y / bs.y);
                uv.x += fmod(abs(row), 2) * bs.x * 0.5;
                float2 cell = floor(uv / bs);
                float2 f = frac(uv / bs) * bs;              // position inside brick in world units
                float edge = min(min(f.x, bs.x - f.x), min(f.y, bs.y - f.y));
                float mortar = 1 - smoothstep(0.015, 0.045, edge);
                float tone = 0.75 + 0.5 * hash21(cell + 7.1);
                return float2(tone, mortar);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                float3 an = abs(n);
                float3 wp = i.positionWS;
                // Pick the 2D projection by dominant normal axis (cube faces).
                float2 uv = an.z > 0.5 ? wp.xy : (an.y > 0.5 ? wp.xz : wp.zy);

                float2 m = masonry(uv);
                float grain = vnoise(uv * 9) * 0.5 + vnoise(uv * 23) * 0.5;
                float crackN = vnoise(uv * 2.3 + 17);
                float crack = 1 - smoothstep(0.0, 0.018, abs(crackN - 0.5));
                crack *= step(0.55, vnoise(uv * 0.7 + 3));   // cracks only in some areas

                half3 albedo = _BaseColor.rgb * m.x * (0.8 + 0.4 * grain);
                albedo *= 1 - 0.55 * m.y;                     // recessed mortar
                albedo *= 1 - 0.6 * crack;

                // Vertical gradient: the face fades into darkness below the walkable lip.
                float below = max(0, _TopY - wp.y);
                float fade = lerp(1.0, 0.4, saturate(below / max(0.01, _Fade)));
                albedo *= fade;
                // Masonry pattern without fade/lighting (used for the faint bounce light below).
                float pattern = m.x * (0.75 + 0.5 * grain) * (1 - 0.8 * m.y) * (1 - 0.7 * crack);

                InputData inputData = (InputData)0;
                inputData.positionWS = wp;
                inputData.positionCS = i.positionCS;
                inputData.normalWS = n;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(wp);
                inputData.shadowCoord = TransformWorldToShadowCoord(wp);
                inputData.fogCoord = i.fogFactor;
                inputData.bakedGI = SampleSH(n);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData s = (SurfaceData)0;
                s.albedo = albedo;
                s.metallic = 0;
                s.specular = 0;
                s.smoothness = _Smoothness * (1 - m.y);
                s.normalTS = half3(0, 0, 1);
                s.occlusion = 1 - 0.4 * m.y;
                s.alpha = 1;
                // Soft moonlit lip just below the top edge on the camera-facing face.
                float lip = exp(-below * 4.0) * step(0.5, an.z);
                // Faint mood-tinted bounce light so the masonry reads in the dark (camera-facing faces only),
                // plus a soft moonlit lip just below the walkable edge.
                float face = step(0.5, an.z);
                s.emission = _EdgeColor.rgb * (lip * 0.10 + face * pattern * 0.028 * fade);

                half4 col = UniversalFragmentPBR(inputData, s);
                col.rgb = MixFog(col.rgb, i.fogFactor);
                return half4(col.rgb, 1);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/SHADOWCASTER"
        UsePass "Universal Render Pipeline/Lit/DEPTHONLY"
        UsePass "Universal Render Pipeline/Lit/DEPTHNORMALS"
    }
    FallBack Off
}
