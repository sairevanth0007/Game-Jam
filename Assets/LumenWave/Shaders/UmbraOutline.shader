// Umbra's glowing rim, drawn as an inverted hull (back faces pushed out along the normal).
// Stencil test NotEqual 2 means it only appears outside pixels already covered by Umbra's body,
// so overlapping limbs read as ONE silhouette with a thin bright edge (Shadow Fight / Limbo look).
// Brightness follows a Schlick-style falloff toward a key direction, so the rim is strongest on the
// side facing the moon and fades on the other side.
Shader "LumenWave/UmbraOutline"
{
    Properties
    {
        [HDR] _RimColor("Rim Color", Color) = (0.55, 0.95, 1.4, 1)
        _Width("Width (world units)", Range(0, 0.1)) = 0.022
        _KeyDir("Key direction", Vector) = (-0.45, 0.85, 0, 0)
        _Ambient("Unlit side brightness", Range(0, 1)) = 0.35
        _KeyPower("Key falloff exponent", Range(1, 8)) = 2
        _WidthUnlit("Width factor on the unlit side", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry+20" }
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite On
            Stencil { Ref 2 Comp NotEqual }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _RimColor;
                float _Width;
                float4 _KeyDir;
                half _Ambient;
                half _KeyPower;
                half _WidthUnlit;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; half fogFactor : TEXCOORD1; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 nWS = normalize(TransformObjectToWorldNormal(v.normalOS));
                float3 pWS = TransformObjectToWorld(v.positionOS.xyz);
                // Push out only in the screen plane (X/Y) so the hull doesn't poke toward the camera.
                float3 push = float3(nWS.xy, 0);
                float len = max(length(push), 1e-4);
                float kv = saturate(dot(push.xy / len, normalize(_KeyDir.xy)) * 0.5 + 0.5);
                pWS += push / len * _Width * saturate(len * 3) * lerp(_WidthUnlit, 1.0, kv);
                o.positionCS = TransformWorldToHClip(pWS);
                o.normalWS = nWS;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float k = saturate(dot(normalize(i.normalWS.xy + 1e-4), normalize(_KeyDir.xy)) * 0.5 + 0.5);
                float lit = _Ambient + (1 - _Ambient) * pow(k, _KeyPower);
                half3 col = _RimColor.rgb * lit;
                col = MixFog(col, i.fogFactor * 0.5 + 0.5);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
