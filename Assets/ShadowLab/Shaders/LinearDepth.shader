Shader "ShadowLab/LinearDepth"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Cull Back
            ZWrite On
            ZTest LEqual
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float4x4 _SL_LightVP;
            float3 _SL_LightPosition, _SL_LightForward;
            float _SL_Far;
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = mul(_SL_LightVP, float4(output.positionWS, 1));
                return output;
            }
            float4 Frag(Varyings input) : SV_Target
            {
                float d = saturate(dot(input.positionWS - _SL_LightPosition,
                    _SL_LightForward) / _SL_Far);
                return float4(d, d, d, 1);
            }
            ENDHLSL
        }
    }
}
