// Foreground silhouettes (grass, roots, vines, chains, ruin chunks) that pass between camera and playfield.
// Near-black with pre-blurred soft edges: uv.x in [-1, 1] across a shape (0 = core, |1| = edge), uv.y = edge
// softness (fraction of the half-width that fades out). Vertex alpha scales opacity.
Shader "LumenWave/SoftSilhouette"
{
    Properties
    {
        _Color("Color", Color) = (0.005, 0.006, 0.01, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+50" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "SoftSilhouette"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float e = saturate((1 - abs(i.uv.x)) / max(0.01, i.uv.y));
                e = e * e * (3 - 2 * e);
                return half4(_Color.rgb * i.color.rgb, _Color.a * i.color.a * e);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
