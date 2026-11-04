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
            float4x4 _SL_LightVPInverse;
            float _SL_ReceiverPlane;
            float4 _SL_PCSS; // emitter radius (world), tan(fov/2), search cap, filter cap
            float _SL_NearPlane;
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
            float2 ShadowUV(float4 clip)
            {
                float2 uv = clip.xy / clip.w * .5 + .5;
                #if UNITY_UV_STARTS_AT_TOP
                    uv.y = 1.0 - uv.y;
                #endif
                return uv;
            }

            // Use the center of the actual point-sampled texel, not just the
            // requested offset. They differ at low resolution / wide spacing.
            float ReceiverDepthAtTexel(float2 texelUV, float centerDepth,
                float3 receiverPosition, float3 planeNormal)
            {
                float referenceDepth = centerDepth;
                if (_SL_ReceiverPlane > .5)
                {
                    float2 ndc = texelUV * 2 - 1;
                    #if UNITY_UV_STARTS_AT_TOP
                        ndc.y = -ndc.y;
                    #endif
                    // Any interior clip Z reconstructs a point on this light ray.
                    float4 pointH = mul(_SL_LightVPInverse, float4(ndc, .5, 1));
                    float3 ray = pointH.xyz / pointH.w - _SL_LightPosition;
                    float denominator = dot(planeNormal, ray);
                    float numerator = dot(planeNormal, receiverPosition - _SL_LightPosition);
                    if (abs(denominator) > 1e-6)
                    {
                        float t = numerator / denominator;
                        float planeDepth = dot(t * ray, _SL_LightForward) / _SL_Far;
                        // Near-grazing rays may intersect the extended plane far
                        // away. Bound extrapolation; fall back to ordinary bias.
                        if (t > 0 && planeDepth > 0 && planeDepth < 1 &&
                            abs(planeDepth - centerDepth) < .05)
                            referenceDepth = planeDepth;
                    }
                }
                return referenceDepth;
            }

            float2 TexelCenter(float2 uv)
            {
                return (floor(uv / _SL_MapInfo.xy) + .5) * _SL_MapInfo.xy;
            }

            float CompareReceiverPlane(float2 requestedUV, float centerDepth,
                float3 receiverPosition, float3 planeNormal, float bias)
            {
                if (any(requestedUV < 0) || any(requestedUV >= 1)) return 1;
                float2 texelUV = TexelCenter(requestedUV);
                float referenceDepth = ReceiverDepthAtTexel(texelUV, centerDepth,
                    receiverPosition, planeNormal);
                float storedDepth = SAMPLE_TEXTURE2D_LOD(_SL_ShadowMap,
                    sampler_SL_ShadowMap, texelUV, 0).r;
                return referenceDepth - bias <= storedDepth ? 1.0 : 0.0;
            }

            // Fixed Vogel spiral, not random per-frame noise or a Poisson pattern.
            // Include the center explicitly so a small blocker in the umbra is sampled.
            float2 DiskSample(int index)
            {
                if (index == 0) return float2(0,0);
                float radius = sqrt((float(index) - .5) / 31.0);
                float angle = float(index) * 2.39996323;
                float sn, cs;
                sincos(angle, sn, cs);
                return radius * float2(cs, sn);
            }

            float PCSSVisibility(float2 uv, float d, float3 positionWS,
                float3 planeNormal, float bias)
            {
                if (_SL_PCSS.x <= 1e-5)
                    return CompareReceiverPlane(uv, d, positionWS, planeNormal, bias);
                float receiverDistance = max(d * _SL_Far, _SL_NearPlane);
                float projectionScale = 2 * max(_SL_PCSS.y, .001);
                // Perspective spotlight search footprint at the near plane.
                float searchUV = _SL_PCSS.x * max(receiverDistance - _SL_NearPlane, 0) /
                    (projectionScale * _SL_NearPlane * receiverDistance);
                searchUV = min(searchUV, _SL_PCSS.z * _SL_MapInfo.x);
                float blockerSum = 0;
                int blockerCount = 0;
                [loop] for (int i = 0; i < 32; i++)
                {
                    float2 sampleUV = uv + DiskSample(i) * searchUV;
                    if (any(sampleUV < 0) || any(sampleUV >= 1)) continue;
                    float2 texelUV = TexelCenter(sampleUV);
                    float referenceDepth = ReceiverDepthAtTexel(texelUV, d, positionWS, planeNormal);
                    float stored = SAMPLE_TEXTURE2D_LOD(_SL_ShadowMap,
                        sampler_SL_ShadowMap, texelUV, 0).r;
                    if (stored < .9999 && stored < referenceDepth - bias)
                    {
                        blockerSum += stored * _SL_Far;
                        blockerCount++;
                    }
                }
                if (blockerCount == 0) return 1;
                float blockerDistance = max(blockerSum / float(blockerCount), _SL_NearPlane);
                // Similar triangles: penumbra world radius at receiver, then
                // project using the RECEIVER distance, not the light near plane.
                float penumbraWorld = _SL_PCSS.x *
                    max(receiverDistance - blockerDistance, 0) / blockerDistance;
                float filterUV = penumbraWorld / (projectionScale * receiverDistance);
                filterUV = min(filterUV, _SL_PCSS.w * _SL_MapInfo.x);
                float visibility = 0;
                [loop] for (int j = 0; j < 32; j++)
                    visibility += CompareReceiverPlane(uv + DiskSample(j) * filterUV,
                        d, positionWS, planeNormal, bias);
                return visibility / 32.0;
            }

            float Visibility(float3 positionWS, float ndl, float3 planeNormal)
            {
                float4 clip = mul(_SL_LightVP, float4(positionWS, 1));
                if (clip.w <= 0) return 1;
                float2 uv = ShadowUV(clip);
                float d = dot(positionWS - _SL_LightPosition, _SL_LightForward) / _SL_Far;
                if (any(uv < 0) || any(uv >= 1) || d < _SL_Bias.z || d > 1) return 1;
                float bias = _SL_Bias.x + _SL_Bias.y * (1 - ndl);
                if (_SL_MapInfo.w > 2.5)
                    return PCSSVisibility(uv, d, positionWS, planeNormal, bias);
                int radius = (int)_SL_MapInfo.w;
                float result = 0;
                [loop] for (int y = -radius; y <= radius; y++)
                    [loop] for (int x = -radius; x <= radius; x++)
                    {
                        float2 sampleUV = uv + float2(x,y) * _SL_MapInfo.xy * _SL_MapInfo.z;
                        result += CompareReceiverPlane(sampleUV, d, positionWS, planeNormal, bias);
                    }
                float width = 2 * radius + 1;
                return result / (width * width);
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float3 n = normalize(input.normalWS);
                // Geometric receiver plane, computed before any divergent shadow
                // branches. Smooth shading normals are not the triangle's plane.
                float3 planeCross = cross(ddx(input.positionWS), ddy(input.positionWS));
                float planeLengthSq = dot(planeCross, planeCross);
                float3 planeNormal = planeLengthSq > 1e-16 ?
                    planeCross * rsqrt(max(planeLengthSq, 1e-16)) : n;
                float3 toLight = _SL_LightPosition - input.positionWS;
                float distanceSquared = max(dot(toLight, toLight), .01);
                float3 l = toLight * rsqrt(distanceSquared);
                float ndl = saturate(dot(n, l));
                float cone = dot(-l, _SL_LightForward);
                float spot = smoothstep(_SL_Bias.w, min(1.0, _SL_Bias.w + .08), cone);
                float viewDepth = dot(input.positionWS - _SL_LightPosition, _SL_LightForward);
                spot *= step(_SL_Bias.z * _SL_Far, viewDepth) * step(viewDepth, _SL_Far);
                float visibility = Visibility(input.positionWS, ndl, planeNormal);
                float illumination = ndl * visibility * spot * (55 / distanceSquared);
                return half4(_BaseColor.rgb * (.13 + illumination * float3(1,.92,.8)), 1);
            }
            ENDHLSL
        }
    }
}
