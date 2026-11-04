# Algorithm notes

## Shadow mapping

1. Transform each registered mesh into the virtual spotlight's clip space.
2. Rasterize into a render texture with a depth attachment, selecting the nearest surface.
3. Store `dot(worldPosition - lightPosition, lightForward) / farPlane` in the RFloat color attachment. Clear empty texels to 1.
4. In the visible receiver pass, project the world position using the same GPU projection matrix and map xy from [-1,1] to [0,1].
5. Compute the same normalized linear light depth. Compare it with the stored depth after subtracting a receiver bias.

GPU projection is constructed with `GL.GetGPUProjectionMatrix(projection, true)` for a render-texture destination. Light camera space uses a Z reflection because Unity transforms point forward along +Z while view space faces -Z. The command uses Unity\'s default logical clear depth; it does not manually reverse that parameter. The sampled COLOR depth convention is deliberately independent of hardware depth conventions. Verify vertical orientation and occlusion on Metal before assuming portability.

## Bias

`bias = constantBias + slopeBias * (1 - saturate(dot(N,L)))`

This is a simple normal-angle-based receiver heuristic, not true rasterizer slope-scaled depth bias or a full receiver-plane depth correction. Parameters are normalized by light far distance. Changing the light range changes their world-space interpretation. Too little bias produces acne; too much detaches shadows. PCF can require additional correction near sloped receivers.

## PCF

For each sample, compare receiver depth to sampled depth. Average the resulting visibility values. Do not blur depths and then compare: that is a different algorithm.

Modes: hard=1 comparison, 3x3=9, 5x5=25. Spacing is specified in shadow texels. At larger spacing the fixed sample pattern becomes sparse; this can introduce artifacts rather than producing physically correct soft shadows. Out-of-map samples return lit visibility, potentially lightening the frustum border.

## Limitations

Single perspective light. Opaque MeshRenderers. Lambert diffuse plus fixed ambient; no physically based BRDF, indirect lighting or standard URP lighting. No cascades or temporal filtering. Bounds and near/far clipping can remove shadow casters. RFloat costs four bytes per texel plus a separate depth attachment and platform overhead. Timing the shadow pass alone excludes the PCF cost in the receiver pass.

## References

- Unity custom shader documentation: https://docs.unity3d.com/6000.0/Documentation/Manual/urp/writing-custom-shaders-urp.html
- Unity GPU projection matrix: https://docs.unity3d.com/ScriptReference/GL.GetGPUProjectionMatrix.html
- NVIDIA PCSS integration (future extension): https://developer.download.nvidia.com/whitepapers/2008/PCSS_Integration.pdf
- MJP shadow technique comparison: https://therealmjp.github.io/posts/shadow-maps/

References describe principles and related implementations; the current project does not claim to implement every referenced technique.
