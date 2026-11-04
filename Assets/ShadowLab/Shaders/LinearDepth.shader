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
            #pragma vertex PreviewVert
            #pragma fragment Preview
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_SL_PreviewSource);
            SAMPLER(sampler_SL_PreviewSource);
            float2 _SL_PreviewRange;
            struct PreviewAttributes { float3 positionOS : POSITION; };
            struct PreviewVaryings {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };
            PreviewVaryings PreviewVert(PreviewAttributes input)
            {
                PreviewVaryings output;
                // Explicit clip-space triangle: no camera matrices or legacy Blit.
                output.positionCS = float4(input.positionOS.xy, 0, 1);
                output.uv = input.positionOS.xy * .5 + .5;
                #if UNITY_UV_STARTS_AT_TOP
                    output.uv.y = 1.0 - output.uv.y;
                #endif
                return output;
            }
            float4 Preview(PreviewVaryings input) : SV_Target
            {
                float d = SAMPLE_TEXTURE2D_LOD(_SL_PreviewSource,
                    sampler_SL_PreviewSource, input.uv, 0).r;
                float grey = saturate((d - _SL_PreviewRange.x) /
                    max(_SL_PreviewRange.y - _SL_PreviewRange.x, .001));
                // Empty texels remain white; display range affects preview only.
                if (d >= .9999) grey = 1;
                return float4(grey, grey, grey, 1);
            }
            ENDHLSL
        }
    }
}
