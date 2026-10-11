// Umbra: a pure black silhouette with a thin glowing rim (works on MeshRenderer and SkinnedMeshRenderer).
// Rim = Schlick's Fresnel approximation F = F0 + (1 - F0)(1 - cos θ)^p, where θ is the angle between
// the surface normal and the view direction. Grazing angles (the silhouette edge) reflect the most.
// The rim is weighted toward the key light (moon) direction, so it reads as a crisp cool edge on the
// light-facing side and fades on the far side. A faint top-down ambient term ("subsurface") lets
// overlapping limbs separate. The rim colour is HDR so URP bloom makes it glow.
// Dissolve: value noise + height gradient; pixels below the threshold are clipped and a hot edge glows
// just above it (death = dissolve into motes, respawn = reverse).
Shader "LumenWave/Umbra"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (0.006, 0.006, 0.01, 1)
        [HDR] _RimColor("Rim Color", Color) = (0.6, 0.85, 2.2, 1)
        _RimPower("Rim Power (Schlick exponent)", Range(0.5, 10)) = 5.5
        _RimF0("Rim F0", Range(0, 1)) = 0.02
        [HDR] _BackLight("Main light back rim", Color) = (0.35, 0.4, 0.6, 1)
        _RimStrength("Rim strength", Range(0, 2)) = 0.12
        _KeyDir("Key light direction (screen XY)", Vector) = (-0.45, 0.85, 0, 0)
        _KeyAmbient("Rim on the unlit side", Range(0, 1)) = 0.3
        [HDR] _Ambient("Ambient / subsurface", Color) = (0, 0, 0, 1)
        _Dissolve("Dissolve", Range(0, 1)) = 0
        [HDR] _DissolveEdge("Dissolve edge", Color) = (1.2, 2.2, 3.2, 1)
        _DissolveMinY("Dissolve min Y (world)", Float) = 0
        _DissolveHeight("Dissolve height", Float) = 2
        _DissolveScale("Dissolve noise scale", Float) = 9
        _FogAmount("Fog amount", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            // Mark Umbra's pixels so the outline pass (LumenWave/UmbraOutline) only draws OUTSIDE the
            // union of all body parts: one clean silhouette edge, no inner outlines between limbs.
            Stencil { Ref 2 Comp Always Pass Replace }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _RimColor;
                half _RimPower;
                half _RimF0;
                half4 _BackLight;
                half _RimStrength;
                float4 _KeyDir;
                half _KeyAmbient;
                half4 _Ambient;
                half _Dissolve;
                half4 _DissolveEdge;
                float _DissolveMinY;
                float _DissolveHeight;
                float _DissolveScale;
                half _FogAmount;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half fogFactor : TEXCOORD2;
                float3 positionOS : TEXCOORD3;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.positionOS = v.positionOS.xyz;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            float hash3(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float vnoise(float3 x)
            {
                float3 i = floor(x), f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(lerp(hash3(i + float3(0, 0, 0)), hash3(i + float3(1, 0, 0)), f.x),
                                 lerp(hash3(i + float3(0, 1, 0)), hash3(i + float3(1, 1, 0)), f.x), f.y),
                            lerp(lerp(hash3(i + float3(0, 0, 1)), hash3(i + float3(1, 0, 1)), f.x),
                                 lerp(hash3(i + float3(0, 1, 1)), hash3(i + float3(1, 1, 1)), f.x), f.y), f.z);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                float3 v = GetWorldSpaceNormalizeViewDir(i.positionWS);
                float cosT = saturate(dot(n, v));
                float fresnel = _RimF0 + (1.0 - _RimF0) * pow(1.0 - cosT, _RimPower);
                // Light-facing weight from the screen-plane normal vs the key direction.
                float2 nxy = n.xy + 1e-4;
                float key = saturate(dot(normalize(nxy), normalize(_KeyDir.xy + 1e-4)) * 0.5 + 0.5);
                float keyW = _KeyAmbient + (1.0 - _KeyAmbient) * key * key;
                half3 col = _BaseColor.rgb + _RimColor.rgb * fresnel * _RimStrength * keyW * _BackLight.a;
                // Faint top-lit ambient: separates overlapping limbs without lifting the silhouette.
                col += _Ambient.rgb * (0.35 + 0.65 * saturate(n.y * 0.5 + 0.5)) * (1.0 - cosT * 0.6);

                if (_Dissolve > 0.001)
                {
                    float h = saturate((i.positionWS.y - _DissolveMinY) / max(_DissolveHeight, 1e-3));
                    float nz = vnoise(i.positionWS * _DissolveScale) * 0.6 + vnoise(i.positionWS * _DissolveScale * 2.7) * 0.4;
                    float val = nz * 0.55 + h * 0.45;            // feet survive longest; it burns away from the top
                    val = 1.0 - val;
                    float thr = _Dissolve * 1.12 - 0.06;
                    clip(val - thr);
                    float edge = 1.0 - saturate((val - thr) / 0.07);
                    col += _DissolveEdge.rgb * edge * edge;
                }
                // Silhouette stays inky: only part of the scene fog is applied (1 = full fog, old look).
                col = lerp(col, MixFog(col, i.fogFactor), _FogAmount);
                return half4(col, 1);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/SHADOWCASTER"
        UsePass "Universal Render Pipeline/Lit/DEPTHONLY"
        UsePass "Universal Render Pipeline/Lit/DEPTHNORMALS"
    }
    FallBack Off
}
