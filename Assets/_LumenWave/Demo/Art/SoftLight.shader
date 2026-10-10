Shader "LumenWave/Soft Light"
{
    Properties
    {
        _Intensity ("Emission brightness", Range(0,8)) = 2.4
        _Radial ("Emitter halo", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha One
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct Attributes { float4 position : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct Varyings { float4 position : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            float _Intensity, _Radial;
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.position);
                output.uv = input.uv; output.color = input.color;
                return output;
            }
            float4 frag(Varyings input) : SV_Target
            {
                float2 p = input.uv * 2 - 1;
                float radius = dot(p,p);
                float side = saturate(1 - p.x*p.x);
                float beam = exp2(-3 * p.x*p.x) * side*side;
                beam *= smoothstep(0,.07,input.uv.y) * (1-smoothstep(.76,1,input.uv.y));
                float halo = exp2(-5 * radius) * pow(saturate(1-radius),2);
                float profile = lerp(beam,halo,saturate(_Radial));
                float white = lerp(.025 * exp2(-12*p.x*p.x), .6 * exp2(-18*radius), saturate(_Radial));
                float3 chroma = input.color.rgb * input.color.rgb;
                float3 emission = lerp(chroma, float3(1,1,1), white) * _Intensity;
                return float4(emission, input.color.a * profile);
            }
            ENDCG
        }
    }
}
