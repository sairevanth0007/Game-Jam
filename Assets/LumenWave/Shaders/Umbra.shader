// Umbra: a pure black silhouette with a thin glowing rim.
// Rim = Schlick's Fresnel approximation F = F0 + (1 - F0)(1 - cos θ)^p, where θ is the angle between
// the surface normal and the view direction. Grazing angles (the silhouette edge) reflect the most.
// The rim colour is HDR so URP bloom makes the outline glow.
Shader "LumenWave/Umbra"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (0.006, 0.006, 0.01, 1)
        [HDR] _RimColor("Rim Color", Color) = (0.6, 0.85, 2.2, 1)
        _RimPower("Rim Power (Schlick exponent)", Range(0.5, 10)) = 4
        _RimF0("Rim F0", Range(0, 1)) = 0.02
        [HDR] _BackLight("Main light back rim", Color) = (0.35, 0.4, 0.6, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

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

            half4 frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                float3 v = GetWorldSpaceNormalizeViewDir(i.positionWS);
                float cosT = saturate(dot(n, v));
                float fresnel = _RimF0 + (1.0 - _RimF0) * pow(1.0 - cosT, _RimPower);
                Light ml = GetMainLight();
                float back = saturate(dot(n, ml.direction)) * pow(1.0 - cosT, 2.0);
                half3 col = _BaseColor.rgb + _RimColor.rgb * fresnel + _BackLight.rgb * ml.color * back;
                col = MixFog(col, i.fogFactor);
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
