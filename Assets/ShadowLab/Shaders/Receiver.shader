Shader "ShadowLab/Receiver"
{
    Properties { _BaseColor("Base Color", Color) = (.7,.7,.7,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "ShadowLabForward"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Back
            ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
            CBUFFER_END
            TEXTURE2D(_SL_ShadowMap);
            SAMPLER(sampler_SL_ShadowMap);
            float4x4 _SL_LightVP;
            float3 _SL_LightPosition, _SL_LightForward;
            float _SL_Far;
            float4 _SL_MapInfo; // inverse size xy, filter spacing z, mode w
            float4 _SL_Bias; // constant, slope, normalized near, cone cosine
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }
            float CompareDepth(float2 uv, float receiverDepth)
            {
                if (any(uv < 0) || any(uv > 1)) return 1;
                float storedDepth = SAMPLE_TEXTURE2D_LOD(_SL_ShadowMap,
                    sampler_SL_ShadowMap, uv, 0).r;
                return receiverDepth <= storedDepth ? 1.0 : 0.0;
            }
            float Visibility(float3 positionWS, float ndl)
            {
                float4 clip = mul(_SL_LightVP, float4(positionWS, 1));
                if (clip.w <= 0) return 1;
                float2 uv = clip.xy / clip.w * .5 + .5;
                float d = dot(positionWS - _SL_LightPosition, _SL_LightForward) / _SL_Far;
                if (any(uv < 0) || any(uv > 1) || d < _SL_Bias.z || d > 1) return 1;
                // Bias in normalized LINEAR light depth, not hardware depth.
                d -= _SL_Bias.x + _SL_Bias.y * (1 - ndl);
                int radius = (int)_SL_MapInfo.w; // 0=1 tap; 1=9; 2=25
                float result = 0;
                [loop] for (int y = -radius; y <= radius; y++)
                    [loop] for (int x = -radius; x <= radius; x++)
                        result += CompareDepth(uv + float2(x,y) * _SL_MapInfo.xy * _SL_MapInfo.z, d);
                float width = 2 * radius + 1;
                return result / (width * width);
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float3 n = normalize(input.normalWS);
                float3 toLight = _SL_LightPosition - input.positionWS;
                float distanceSquared = max(dot(toLight, toLight), .01);
                float3 l = toLight * rsqrt(distanceSquared);
                float ndl = saturate(dot(n, l));
                float cone = dot(-l, _SL_LightForward);
                float spot = smoothstep(_SL_Bias.w, min(1.0, _SL_Bias.w + .08), cone);
                float viewDepth = dot(input.positionWS - _SL_LightPosition, _SL_LightForward);
                spot *= step(_SL_Bias.z * _SL_Far, viewDepth) * step(viewDepth, _SL_Far);
                float visibility = Visibility(input.positionWS, ndl);
                float illumination = ndl * visibility * spot * (55 / distanceSquared);
                return half4(_BaseColor.rgb * (.13 + illumination * float3(1,.92,.8)), 1);
            }
            ENDHLSL
        }
    }
}
