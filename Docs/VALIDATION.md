# Validation and measurement

## Current evidence

Only source-level checks have been performed in the authoring environment. Unity Editor, a shader compiler for Unity, and a macOS Metal GPU were unavailable. All checkboxes below remain pending until run on real hardware.

## First-run checklist

- [ ] Record Editor version, URP version, Mac model / GPU, macOS version and graphics API.
- [ ] Both shaders compile; Console has no errors.
- [ ] Scene builder generates and saves the scene without errors.
- [ ] Play produces visible geometry and shadows; no pink materials.
- [ ] Shadow map preview contains scene silhouettes, not a blank white texture.
- [ ] Pausing animation holds both light and sphere steady.
- [ ] The cube's shadow is on the side opposite the light. Move light in X and Z to check map orientation.
- [ ] Sphere height changes its shadow position / scale appropriately.
- [ ] Zero-bias preset exposes surface artifacts on at least one test view.
- [ ] Excessive bias visibly detaches at least one contact shadow.
- [ ] Hard, 3x3 and 5x5 modes change edge filtering, without moving the underlying shadow.
- [ ] Resolution and filter sliders update without errors.
- [ ] Exit / enter Play repeatedly; no persistent render texture or command buffer leak.
- [ ] Save / reopen generated scene and repeat.

## Performance procedure

Freeze animation. Keep Game view resolution, camera, light pose, scene and quality settings fixed. Warm up shaders, then measure a consistent multi-frame interval. Measure builds when possible; record whether Editor or Player was used.

Use Unity Profiler GPU module if supported by the selected macOS configuration, or a Metal GPU capture / platform tool. A command-buffer GPU sample names shadow generation, but **PCF runs in the receiver pass**, so record receiver / total GPU costs too. Frame rate and CPU frame time are not GPU timing substitutes. If a tool cannot report GPU timing, explicitly record timing as unavailable.

| Mode | Map | Filter spacing | Map GPU ms | Receiver GPU ms | Total GPU ms | Edge / motion observations |
|---|---|---|---|---|---|---|
| Hard | 1024 | 1 | pending | pending | pending | pending |
| 3x3 PCF | 1024 | 1 | pending | pending | pending | pending |
| 5x5 PCF | 1024 | 1 | pending | pending | pending | pending |

Repeat at 512 and 2048. Preserve raw captures and settings. Do not interpret shader comparison counts as measured GPU speedups.
