# ShadowLab

A focused real-time shadow-mapping study for Unity URP, written in C# and HLSL.

**Status: initial implementation. Not yet compiled or visually validated in Unity / on macOS Metal. No benchmark results or completed PCSS implementation are claimed.**

## Implemented in source

- Independent spotlight shadow map, explicitly drawing opaque MeshRenderers.
- Linear light-view depth stored in RFloat; separate hardware depth for visibility.
- Hard shadows, 3x3 PCF and 5x5 PCF (1 / 9 / 25 depth comparisons).
- Adjustable constant bias, normal-angle-based bias, filter spacing and map resolution.
- Runtime controls, light / sphere animation and shadow-map preview.
- Editor scene builder: ground, contact cube, floating sphere, thin poles, slope and stairs.
- Named GPU command-buffer sample: `ShadowLab.ShadowMap`.

## Start on Mac

This repository is an **Assets source module**, not a complete Unity Hub project. This keeps the user's chosen Editor and URP versions under their control.

1. Clone or download this repository.
2. In Unity Hub create a **Universal 3D (URP)** project using Unity 6. Record the exact Editor and URP package versions in your validation notes. Use Metal on macOS.
3. Copy this repository's `Assets/ShadowLab` folder into that project's `Assets` folder. Do not replace the project's Packages or ProjectSettings.
4. Wait for compilation. If there are errors, save the first Console error and your versions before continuing.
5. Select **ShadowLab > Create Demo Scene** in Unity's top menu. Choose a new scene path.
6. Open **Game** view and press **Play**. Use a Game view at least 900x650 so the controls fit.
7. Pause animation for repeatable comparisons. Toggle Hard / PCF 3x3 / PCF 5x5, map resolution, bias and shadow-map preview.

The controller updates the shadow map for the generated display camera in Play mode. Scene view is not a supported preview. Additional objects must use the receiver material and be registered in the controller's Casters list to cast shadows. Only opaque, static MeshRenderer geometry is supported; transform animation is supported. There is no alpha clipping, skinned deformation, instancing or built-in URP lighting integration in this version.

## Architecture

- `Runtime/ShadowLabController.cs`: allocate / release resources, construct light matrices, issue the shadow pass and set globals.
- `Shaders/LinearDepth.shader`: light-view projection and normalized linear depth output.
- `Shaders/Receiver.shader`: custom spotlight diffuse lighting, depth comparison, bias and PCF.
- `Editor/ShadowLabSceneBuilder.cs`: create a reproducible scene and persistent materials.

The map is rendered before URP camera rendering via an explicit command buffer. It does not reuse URP's shadow atlas, `Camera.Render`, replacement shaders or a legacy ScriptableRenderPass. No RenderGraph compatibility setting is required by design, but actual Unity-version compatibility remains to be verified.

## Scope and honesty

This is an educational rendering implementation, with AI-assisted code generation. Reproducing established shadow algorithms is not a novel algorithm contribution. Read and understand the code, document fixes and record actual results before presenting it as a validated portfolio project.

PCF averages visibility comparisons; a bigger kernel alone does not model contact-hardening penumbrae. **PCSS is a future milestone**, after hard shadows and PCF pass visual and performance checks.

See [validation checklist](Docs/VALIDATION.md), [algorithm notes](Docs/ALGORITHMS.md) and [roadmap](Docs/ROADMAP.md).
