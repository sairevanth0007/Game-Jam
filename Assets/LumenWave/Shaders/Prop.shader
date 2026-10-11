// Background / midground scenery (Kenney models, procedural gears, ridges). Cheap lighting: ambient (SH) +
// wrapped main light + a moon-side rim, then atmospheric perspective: every layer fades towards the fog
// colour by a per-layer amount (_Atmo) plus low-lying mist (more fog near the ground of the layer).
// Vertex colour multiplies the albedo (ridges use it; imported models have white).
Shader "LumenWave/Prop"
{
    Properties
    {
        _Color("Albedo", Color) = (0.1, 0.1, 0.12, 1)
        [HDR] _RimColor("Rim (moon side)", Color) = (0.3, 0.4, 0.6, 1)
        _FogColor("Fog colour", Color) = (0.05, 0.06, 0.1, 1)
        _Atmo("Atmosphere (0 = clear, 1 = fog)", Range(0, 1)) = 0.3
        _MistTop("Mist top (world y)", Float) = 0
        _MistRange("Mist height", Float) = 6
        _MistAmount("Mist amount", Range(0, 1)) = 0.5
        _LightAmount("Main light amount", Range(0, 2)) = 0.6
        [HDR] _Emission("Emission", Color) = (0, 0, 0, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Pass
        {
            Name "Prop"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _RimColor;
                half4 _FogColor;
                half _Atmo;
                float _MistTop;
                float _MistRange;
                half _MistAmount;
                half _LightAmount;
                half4 _Emission;
            CBUFFER_END

            float4 _LW_MoonDir;   // world direction towards the moon (set by SceneRig)

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; half4 color : TEXCOORD2; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.color = v.color;
                return o;
            }

            half4 frag(Varyings i, bool front : SV_IsFrontFace) : SV_Target
            {
                float3 N = normalize(i.normalWS) * (front ? 1 : -1);
                float3 V = GetWorldSpaceNormalizeViewDir(i.positionWS);
                half3 albedo = _Color.rgb * i.color.rgb;
                Light ml = GetMainLight();
                half wrap = saturate(dot(N, ml.direction) * 0.6 + 0.4);
                half3 lit = albedo * (SampleSH(N) + ml.color * wrap * _LightAmount);
                float3 md = normalize(_LW_MoonDir.xyz + float3(0, 0, 1e-4));
                half rim = pow(1 - saturate(dot(N, V)), 3.0) * saturate(dot(N, md) * 0.7 + 0.3);
                lit += _RimColor.rgb * rim + _Emission.rgb;
                half mist = _MistAmount * saturate((_MistTop - i.positionWS.y) / max(0.01, _MistRange));
                half a = saturate(_Atmo + (1 - _Atmo) * mist);
                return half4(lerp(lit, _FogColor.rgb, a), 1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Unlit/DEPTHONLY"
    }
    FallBack Off
}
