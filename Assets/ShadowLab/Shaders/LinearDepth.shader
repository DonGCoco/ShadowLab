Shader "ShadowLab/LinearDepth"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            // Two-sided reference pass avoids render-target projection winding ambiguity.
            Cull Off
            ZWrite On
            ZTest LEqual
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float4x4 _SL_LightVP;
            float4x4 _SL_ObjectToWorld;
            float3 _SL_LightPosition, _SL_LightForward;
            float _SL_Far;
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = mul(_SL_ObjectToWorld, input.positionOS).xyz;
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
        // Convert single-channel RFloat to RGB for IMGUI. Raw RFloat previews
        // appear red because missing G/B channels are zero, not because of an error.
        Pass
        {
            Name "GrayscalePreview"
            Cull Off
            ZWrite Off
            ZTest Always
            HLSLPROGRAM
            #pragma vertex vert_img
            #pragma fragment Preview
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 Preview(v2f_img input) : SV_Target
            {
                float d = tex2D(_MainTex, input.uv).r;
                return float4(d, d, d, 1);
            }
            ENDHLSL
        }
    }
}
