# ShadowLab

A focused real-time shadow-mapping study for Unity URP, written in C# and HLSL.

**Status: v0.4 source revision, pending Unity / Metal validation. User recordings show basic cast shadows and PCF switching working after v0.3 + UV correction; wide PCF caused receiver stripes and the old depth preview remained flat. v0.4 addresses these two issues. No GPU benchmark results or PCSS implementation are claimed.**

## Implemented in source

- Independent spotlight shadow map, explicitly drawing opaque MeshRenderers.
- Linear light-view depth stored in RFloat; separate hardware depth for visibility.
- Hard shadows, 3x3 PCF and 5x5 PCF (1 / 9 / 25 depth comparisons).
- Adjustable constant bias, normal-angle-based bias, filter spacing and map resolution.
- Optional perspective-correct geometric receiver-plane depth per PCF texel.
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

## Update an existing demo to v0.4

Exit Play mode and replace these files, preserving their existing .meta files:

- `Assets/ShadowLab/Runtime/ShadowLabController.cs`
- `Assets/ShadowLab/Shaders/Receiver.shader`
- `Assets/ShadowLab/Shaders/LinearDepth.shader`

No scene regeneration is required. After compilation the panel should say **v0.4**. Press **Baseline** to enable receiver-plane correction. Freeze animation, select **512**, set spacing to **2.1**, and compare **5x5 PCF** with **Receiver-plane depth correction** off / on. Correction should reduce false self-shadow stripes on the ground without erasing real object shadows; this visual acceptance check is still pending.

Enable **Show shadow map**. The preview uses an explicit clip-space triangle with a dedicated texture binding, rather than legacy Blit. **Preview max depth** changes display contrast only. Magenta means the preview target was cleared but its draw did not complete. An all-white preview can indicate empty / far data; use **Read GPU depth range** to distinguish stored values from a display failure.

Receiver-plane correction adds shader arithmetic; measure its cost rather than assuming the old 1/9/25 sample counts predict speed. It uses the local geometric triangle plane, with bounded extrapolation near grazing light angles. It is not a complete cure for curvature, silhouettes, sparse PCF sampling, or large footprints.

## CPU geometry checks

```sh
python3 -m unittest discover -s Tests -v
```

Five tests verify top/bottom-origin projection, known tilted receiver planes across 512/1024/2048 maps, reproduction of false self-shadow without correction, preservation of a real blocker, and preview-triangle coverage. They do **not** compile Unity shaders or validate Metal rendering.
