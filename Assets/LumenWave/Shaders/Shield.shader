// Resonance shield: a standing wave on a dome, drawn as a Chladni figure.
// For a square plate the nodal pattern of mode (n, m) is
//   cos(nπx)cos(mπy) − cos(mπx)cos(nπy) = 0
// Sand gathers on these nodal lines (where the plate does not move). A standing wave's amplitude
// oscillates in time as cos(ωt) while the nodes stay fixed – exactly what is animated here.
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

            half4 frag(Varyings i) : SV_Target
            {
                // Project the dome onto the plate: unit sphere (radius 0.5) → [-1, 1]².
                float2 p = i.positionOS.xy * 2.0;
                float n = _ModeN, m = _ModeM;
                float chladni = cos(n * PI * p.x) * cos(m * PI * p.y) - cos(m * PI * p.x) * cos(n * PI * p.y);
                float t = _Phase + _Time.y * 5.0;
                float amp = cos(t);                                     // standing wave: nodes fixed, amplitude oscillates
                float nodal = 1.0 - smoothstep(0.0, _LineWidth, abs(chladni));
                float antinode = abs(chladni * amp) * 0.18;
                float3 nrm = normalize(i.normalWS);
                float3 v = GetWorldSpaceNormalizeViewDir(i.positionWS);
                float fres = pow(1.0 - saturate(dot(nrm, v)), 3.0);
                float shimmer = 0.75 + 0.25 * sin(t * 2.0);
                float a = (nodal * 0.9 * shimmer + antinode + fres * 1.3) * _Alpha;
                return half4(_Color.rgb * a, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
