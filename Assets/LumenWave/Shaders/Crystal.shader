// Faceted glowing crystal (scenery): flat-shaded facets, inner glow in _Color, iridescent thin-film fresnel
// (hue shifts with view angle and height) controlled by _Iridescence. Unlit and cheap; fog applied.
// Vertex colour .r = 0 at the root, 1 at the tip (glow grows towards the tip).
Shader "LumenWave/Crystal"
{
    Properties
    {
        [HDR] _Color("Glow colour", Color) = (0.3, 1.0, 0.6, 1)
        _Base("Body colour", Color) = (0.02, 0.05, 0.05, 1)
        _Iridescence("Iridescence", Range(0, 2)) = 0.4
        _Atmo("Atmosphere", Range(0, 1)) = 0
        _FogColor("Fog colour", Color) = (0.05, 0.06, 0.1, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Pass
        {
            Name "Crystal"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _Base;
                half _Iridescence;
                half _Atmo;
                half4 _FogColor;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; half4 color : TEXCOORD2; half fog : TEXCOORD3; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.color = v.color;
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half3 hue(float h) { return saturate(abs(frac(h + float3(0, 2.0 / 3.0, 1.0 / 3.0)) * 6 - 3) - 1); }

            half4 frag(Varyings i) : SV_Target
            {
                float3 N = normalize(i.normalWS);
                float3 V = GetWorldSpaceNormalizeViewDir(i.positionWS);
                float ndv = saturate(dot(N, V));
                float fres = pow(1 - ndv, 2.0);
                half tip = i.color.r;
                half3 col = _Base.rgb + _Color.rgb * (0.2 + 0.8 * tip) * (0.3 + 0.7 * ndv);
                col += hue(ndv * 1.3 + i.positionWS.y * 0.15 + _Time.y * 0.03) * fres * _Iridescence;
                col += _Color.rgb * fres * 0.5;
                col = lerp(col, _FogColor.rgb, _Atmo);
                col = MixFog(col, i.fog);
                return half4(col, 1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Unlit/DEPTHONLY"
    }
    FallBack Off
}
