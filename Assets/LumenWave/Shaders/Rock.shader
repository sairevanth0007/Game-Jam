// Playfield rock (platform bodies and their moss/snow caps). World-space triplanar ambientCG colour + normal
// maps lit by URP's PBR path (Forward+, so beams and lamps light the stone). Vertex data from PlatformVisual:
//   uv1.x = depth below the walkable top (world units) -> the mass sinks into darkness below the lip,
//   color.rgb = baked cavity/AO, color.a = "lip" (front top edge of a cap) -> faint mood-tinted rim for readability.
// Top-facing surfaces use the cap textures (moss/snow/ice) and can be wet (glossy).
Shader "LumenWave/Rock"
{
    Properties
    {
        _MainTex("Side colour", 2D) = "grey" {}
        [Normal] _BumpMap("Side normal", 2D) = "bump" {}
        _CapTex("Top colour", 2D) = "grey" {}
        [Normal] _CapBump("Top normal", 2D) = "bump" {}
        _Tint("Side tint", Color) = (0.5, 0.5, 0.55, 1)
        _CapTint("Top tint", Color) = (0.4, 0.5, 0.35, 1)
        _Scale("Side texture size (world units)", Float) = 3
        _CapScale("Top texture size (world units)", Float) = 2
        _NormalStrength("Normal strength", Range(0, 2)) = 1
        _Smoothness("Smoothness", Range(0, 1)) = 0.15
        _WetSmoothness("Top (wet) smoothness", Range(0, 1)) = 0.5
        _DarkDepth("Darkening depth", Float) = 3
        _DarkMin("Darkest factor", Range(0, 1)) = 0.12
        [HDR] _LipColor("Lip rim colour", Color) = (0.3, 0.5, 0.7, 1)
        [HDR] _Bounce("Camera-side bounce light", Color) = (0.02, 0.025, 0.035, 1)
        [HDR] _FillColor("Moon fill (upper left, camera side)", Color) = (0.3, 0.33, 0.4, 1)
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

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
            TEXTURE2D(_CapTex); SAMPLER(sampler_CapTex);
            TEXTURE2D(_CapBump); SAMPLER(sampler_CapBump);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Tint;
                half4 _CapTint;
                float _Scale;
                float _CapScale;
                half _NormalStrength;
                half _Smoothness;
                half _WetSmoothness;
                float _DarkDepth;
                half _DarkMin;
                half4 _LipColor;
                half4 _Bounce;
                half4 _FillColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                float2 uv1 : TEXCOORD1;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half4 color : TEXCOORD2;
                float2 depth : TEXCOORD3;
                half fogFactor : TEXCOORD4;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.color = v.color;
                o.depth = v.uv1;
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

            half4 frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                float3 wp = i.positionWS;
                float3 w = pow(abs(n), 4.0);
                w /= max(1e-4, w.x + w.y + w.z);
                float top = smoothstep(0.45, 0.8, n.y);

                float s = 1.0 / max(0.01, _Scale), cs = 1.0 / max(0.01, _CapScale);
                float2 uvX = wp.zy * s, uvZ = wp.xy * s;
                float2 uvY = wp.xz * lerp(s, cs, top);

                // Colour
                half3 cX = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvX).rgb;
                half3 cZ = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvZ).rgb;
                half3 cYs = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvY).rgb;
                half3 cYc = SAMPLE_TEXTURE2D(_CapTex, sampler_CapTex, uvY).rgb;
                // Tints are absolute (linear) mean albedos: normalise each texture by its own average (last mip),
                // so any ambientCG set can be dropped in and keep a controlled brightness.
                half3 avgS = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, float2(0.5, 0.5), 12).rgb;
                half3 avgC = SAMPLE_TEXTURE2D_LOD(_CapTex, sampler_CapTex, float2(0.5, 0.5), 12).rgb;
                half lumS = max(0.02, dot(avgS, half3(0.2126, 0.7152, 0.0722)));
                half lumC = max(0.02, dot(avgC, half3(0.2126, 0.7152, 0.0722)));
                // Limbo-style detail band: texture/normal detail only within ~1.5 m of an exposed edge (top lip
                // strongest, sides/undersides weaker); the interior of big masses becomes a calm dark silhouette.
                float topBand = 1 - smoothstep(0.25, 1.6, i.depth.x);
                float sideBand = 1 - smoothstep(0.1, 1.2, i.depth.y);
                float detail = max(topBand, sideBand * 0.6);
                half texK = lerp(0.12, 0.7, detail);           // also lowers the overall texture contrast
                half3 side = lerp(avgS, cX * w.x + cZ * w.z + cYs * w.y * (1 - top), texK) / lumS * _Tint.rgb;
                half3 cap = lerp(avgC, cYc, 0.8) / lumC * _CapTint.rgb;
                half3 albedo = side * (1 - top * w.y) + cap * (top * w.y);
                // Large-scale tone variation breaks texture repetition.
                float big = vnoise(wp.xy * 0.13 + 3.1) * 0.6 + vnoise(wp.xy * 0.41) * 0.4;
                albedo *= 0.75 + 0.5 * big;

                // Normals (whiteout triplanar blend)
                half3 tX = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uvX), _NormalStrength);
                half3 tZ = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uvZ), _NormalStrength);
                half3 tY = top > 0.5
                    ? UnpackNormalScale(SAMPLE_TEXTURE2D(_CapBump, sampler_CapBump, uvY), _NormalStrength)
                    : UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uvY), _NormalStrength);
                tX = half3(tX.xy + n.zy, abs(tX.z) * n.x);
                tY = half3(tY.xy + n.xz, abs(tY.z) * n.y);
                tZ = half3(tZ.xy + n.xy, abs(tZ.z) * n.z);
                float3 N = normalize(tX.zyx * w.x + tY.xzy * w.y + tZ.xyz * w.z);
                N = normalize(lerp(n, N, saturate(detail + top)));

                // Sink into darkness below the walkable lip.
                float d = saturate(i.depth.x / max(0.01, _DarkDepth));
                float dark = lerp(1.0, _DarkMin, d * d * (3 - 2 * d));
                dark = max(dark, 0.45 * sideBand);   // exposed side edges keep a lit rim band
                half ao = i.color.r;
                albedo *= dark * ao * lerp(0.45, 1.0, detail);

                InputData inputData = (InputData)0;
                inputData.positionWS = wp;
                inputData.positionCS = i.positionCS;
                inputData.normalWS = N;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(wp);
                inputData.shadowCoord = TransformWorldToShadowCoord(wp);
                inputData.fogCoord = i.fogFactor;
                inputData.bakedGI = SampleSH(N);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                float wet = top * smoothstep(0.35, 0.75, vnoise(wp.xz * 0.9 + 7.0));
                SurfaceData sd = (SurfaceData)0;
                sd.albedo = albedo;
                sd.metallic = 0;
                sd.specular = 0;
                sd.smoothness = lerp(_Smoothness, _WetSmoothness, wet);
                sd.normalTS = half3(0, 0, 1);
                sd.occlusion = ao;
                sd.alpha = 1;
                // Faint camera-side bounce so the texture reads in the dark, plus the readable lip rim.
                float face = saturate(-n.z);
                // Stylised moon fill from the upper left on the camera side: makes the normal-mapped texture and the
                // bevels read on the faces the player looks at (the real directional light is kept dim for mood).
                float fill = saturate(dot(N, normalize(float3(-0.45, 0.75, -0.55))));
                fill = fill * (0.6 + 0.4 * fill);
                sd.emission = _Bounce.rgb * albedo * 12.0 * face + albedo * _FillColor.rgb * fill * lerp(0.35, 1.0, dark) + _LipColor.rgb * i.color.a;

                half4 col = UniversalFragmentPBR(inputData, sd);
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
