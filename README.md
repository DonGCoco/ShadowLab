# ShadowLab

A focused real-time shadow-mapping study for Unity URP, written in C# and HLSL.

**Status: v0.6 adds GPU frame capture tooling (unverified on host). User testing confirms PCSS softness responds to emitter size and sphere height in the demo scene. GPU performance data has not been collected.**

## Implemented in source

- Independent spotlight shadow map, explicitly drawing opaque MeshRenderers.
- Linear light-view depth stored in RFloat; separate hardware depth for visibility.
- Hard shadows, 3x3 PCF and 5x5 PCF (1 / 9 / 25 depth comparisons).
- Initial PCSS: 32-sample blocker search and up to 32 visibility samples on a fixed Vogel disk, adjustable emitter radius and footprint caps.
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

PCF averages visibility comparisons; a bigger kernel alone does not model contact-hardening penumbrae. PCSS estimates a variable footprint from blocker depth and emitter radius. It is an approximate single-map shadow method; the light's diffuse term is still a point spotlight, not physically integrated area lighting.

See [validation checklist](Docs/VALIDATION.md), [algorithm notes](Docs/ALGORITHMS.md) and [roadmap](Docs/ROADMAP.md).

## Update an existing demo to v0.5

Exit Play mode and replace these files, preserving their existing .meta files:

- `Assets/ShadowLab/Runtime/ShadowLabController.cs`
- `Assets/ShadowLab/Shaders/Receiver.shader`
- `Assets/ShadowLab/Shaders/LinearDepth.shader`

No scene regeneration is required. After compilation the panel should say **v0.5**. Press **Baseline** to enable receiver-plane correction. Freeze animation, select **512**, set spacing to **2.1**, and compare **5x5 PCF** with **Receiver-plane depth correction** off / on. Correction should reduce false self-shadow stripes on the ground without erasing real object shadows; the user's v0.4 screenshots showed this improvement in the tested scene.

Enable **Show shadow map**. The preview uses an explicit clip-space triangle with a dedicated texture binding, rather than legacy Blit. **Preview max depth** changes display contrast only. Magenta means the preview target was cleared but its draw did not complete. An all-white preview can indicate empty / far data; use **Read GPU depth range** to distinguish stored values from a display failure.

Receiver-plane correction adds shader arithmetic; measure its cost rather than assuming the old 1/9/25 sample counts predict speed. It uses the local geometric triangle plane, with bounded extrapolation near grazing light angles. It is not a complete cure for curvature, silhouettes, sparse PCF sampling, or large footprints.

## CPU geometry checks

```sh
python3 -m unittest discover -s Tests -v
```

Ten tests verify top/bottom-origin projection, known tilted receiver planes across 512/1024/2048 maps, reproduction of false self-shadow without correction, preservation of a real blocker, preview-triangle coverage, contact / point-light limits, known penumbra geometry, monotonicity, scale invariance and disk bounds. They do **not** compile Unity shaders or validate Metal rendering.

## PCSS first run

Update only Controller.cs and Receiver.shader over v0.4; the v0.4 LinearDepth.shader remains current. Preserve .meta files and the existing scene.

1. Press Baseline, disable animation, select 1024, leave receiver-plane correction on.
2. Select PCSS. Start with emitter radius 0.30, search cap 32 and filter cap 32 texels.
3. At fixed sphere center height 2.0, compare emitter radius 0 / 0.15 / 0.30 / 0.60.
4. At fixed emitter radius 0.30, compare sphere center heights 0.8 / 2.0 / 3.0.
5. Compare Hard / PCF / PCSS. PCF spacing does not control PCSS.
6. Re-enable animation to inspect stability; disable preview and stop diagnostic readbacks before timing.

The expected trend is sharper contact and broader penumbrae with greater separation or emitter size. This trend is **not yet visually verified in this revision**. Fixed sampling avoids deliberately injected frame-to-frame randomness, but sampling artifacts / motion shimmer remain possible. Sparse blocker search can miss thin occluders. Texel caps deliberately truncate the theoretical footprints, so large emitter settings and cap values affect accuracy. For a cap-sensitivity check, vary caps at a fixed emitter size and pose; do not treat capped output as a physically exact area-light shadow.

## Performance and demo preparation (v0.6)

Add `Runtime/ShadowLabBenchmark.cs` and update `Runtime/ShadowLabController.cs` over v0.5. No scene or shader changes are required. Enable Frame Timing Stats in Player Settings before Play. Scroll to **Record 300 GPU frames (current mode)**. The exporter records total-frame GPU timing, not isolated shadow shader time; unsupported timing does not fall back to FPS.

Follow [measurement and demo instructions](Docs/BENCHMARK.md). Capture code still needs Unity compilation and host validation. The existing 10 CPU tests cover rendering geometry only, not GPU collection or file export.
