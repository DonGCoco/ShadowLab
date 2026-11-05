# Interview preparation / 面试说明

Use these explanations only when you can locate and explain the corresponding code. The project used AI assistance; do not describe it as an independently invented algorithm.

## 30-second overview

“I built a small shadow-quality lab in Unity URP using C# and HLSL. It renders a custom spotlight depth map and compares hard shadows, PCF and PCSS. I used it to investigate bias, filtering artifacts and contact hardening, then collected total-frame GPU timings on my Mac. The main learning was how coordinate conventions, receiver depth and sampling choices affect both correctness and cost.”

中文：重点是从深度生成、采样、调试到测量形成完整实现。不要把四个算法按钮当作核心成果；需要解释为什么结果正确，以及在哪些条件下会失败。

## How does the pipeline work?

“First I render opaque casters from the light's view into a linear-depth texture, using a hardware depth buffer to retain the visible surface. In the camera pass, I project each receiver into that map and compare its depth with the stored depth. PCF averages comparison results. PCSS first searches for blockers, estimates their average depth, and uses the separation to choose a filtering footprint.”

定位：Controller、LinearDepth.shader、Receiver.shader。区分光源视角和主相机视角，区分硬件可见性深度与存储的线性深度。

## Why did wide PCF produce floor stripes?

“A wide kernel samples neighboring positions on a sloping receiver. Comparing every tap with the center receiver depth can classify the receiver itself as a blocker. I added a geometric receiver-plane correction that estimates the receiver depth at each sampled texel. It reduced the floor artifacts in the tested views, but local-plane extrapolation still has limits near discontinuities and grazing angles.”

中文：先说明错误比较的原因，再解释逐采样点深度修正。它增加运算，也不能解决所有曲面和轮廓问题。

## Why does a higher sphere create softer shadows?

“With a fixed emitter size, increasing blocker-to-receiver separation increases the estimated penumbra footprint. PCSS uses blocker depth to approximate that relationship. Fixed-size PCF cannot reproduce the same separation-dependent behavior just by keeping one kernel size.”

中文：这是录屏中验证的定性趋势，不代表与物理参考渲染器逐像素一致。搜索和过滤上限会限制最终宽度。

## What did the Mac debugging teach you?

“I had to verify the projection, texture-origin and depth conventions instead of treating the shadow map as a black box. I also separated preview failures from depth-generation failures. Recognizable depth silhouettes and controlled hard-shadow tests were useful checkpoints before judging soft-shadow quality.”

中文：能解释为什么球影位置、杆影缺失和纯色预览需要分开排查。不要笼统说 Metal 有 bug。

## How did you measure performance?

“I captured 300 distinct valid GPU frame timings per mode in a standalone Player, with matching scene conditions. The median increased from about 3.92 ms for Hard to 7.84 ms for PCSS on the M1. These are total-frame measurements from a single run per mode, so I would not call them isolated shader timings or general performance guarantees.”

中文：解释曾出现采样不足，后来读取历史窗口并只对有效数据去重。计数不足的输出保留为诊断，不冒充完整结果。

## What would you improve next?

“Repeat the measurements, inspect pass-level GPU work, and test thin blockers and cap sensitivity. For integration work I would also need alpha clipping, skinned geometry and a broader URP-compatible lighting path.”

不要声称已经具备生产引擎经验。作品的价值是可运行实现、明确调试依据和诚实的测量边界。

