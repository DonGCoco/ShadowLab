# Validation and measurement

## Current evidence

User screenshots and recordings show Unity 6.3 LTS 6000.3.24f1 / Metal, generated scene and working controls. v0.3 default depth clear plus receiver UV correction produced recognizable ground shadows from poles, sphere and blocks. Three fixed-scene screenshots show a hard edge versus wider PCF transitions, but 5x5 PCF at 512 / spacing 2.1 produced curved receiver stripes. The preview remained uniform gray. Mac model, macOS and URP package version remain unrecorded.

v0.4 contains receiver-plane correction and a new explicit preview draw. Five CPU mathematical regressions pass, but Unity compilation, Metal visual checks and performance checks for v0.4 are **pending**. Historical screenshots are evidence for previous revisions only.

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

## Mac observation and v0.3 follow-up

The user's v0.2 screenshot confirms Unity 6.3 LTS 6000.3.24f1, Metal, 15 shadow draw submissions, and a uniform grayscale map with absent cast shadows. This is a failed rendering check. v0.3 removes the explicit reversed-Z depth clear and adds on-demand GPU min/max and coverage readback. Compilation and rendering of v0.3 remain pending. A written-pixel ratio is based on linear depth < 0.9999 and is a diagnostic heuristic, not a full correctness test.

## Receiver UV correction

After v0.3 the user's video shows cast shadows, but also incorrect large diagonal dark regions and sphere-surface artifacts. The receiver converted GPU clip XY to [0,1] without accounting for a top-origin raster API. The corrected receiver flips sample V under UNITY_UV_STARTS_AT_TOP. This is specific to this module's GPU-projection path; do not copy the condition blindly into unrelated rendering paths. Visual verification is still pending. Check ground shadows against light position with Hard mode and frozen animation before judging PCF. Thin poles may require a closer view / higher map resolution, but missing shadows must not be attributed to resolution until coordinate correctness is verified.

## v0.4 acceptance checks

- [ ] Existing scene loads after replacing the three source files; panel says v0.4.
- [ ] Freeze animation, select 512 / spacing 2.1 / PCF5x5; toggle Receiver-plane depth correction. Ground stripes decrease with correction on.
- [ ] Sphere, pole, cube and ramp cast shadows remain; correction does not make true blockers disappear.
- [ ] Repeat at 1024 / 2048 and light poses including grazing slopes; inspect silhouettes and sphere facets for new artifacts.
- [ ] Hard mode still has distinct edges; corrected PCF widens transitions. Sparse low-resolution sampling artifacts are not mislabeled as PCSS.
- [ ] Show shadow map displays recognizable silhouettes and depth variation; contrast slider affects preview only.
- [ ] GPU readback range agrees with empty / populated map interpretation.
- [ ] Disable preview before timing; compare receiver-plane correction off/on at fixed camera, light, map and filter.
- [ ] Repeat Play entry/exit and resolution switches with no resource errors.

## v0.4 observations and v0.5 PCSS checklist

User screenshots show hard / 3x3 / 5x5 filtering at 512 with receiver-plane correction enabled and no previously obvious curved floor stripes in those views. A subsequent screenshot shows recognizable geometry in the depth preview. These are limited scene observations, not broad angle / performance validation.

v0.5 has 10 passing CPU geometry checks, but its Unity compilation and PCSS appearance remain pending.

- [ ] Import v0.5 without Console errors; old Hard / PCF results are preserved.
- [ ] PCSS emitter radius 0 matches Hard at the same pose, map and biases.
- [ ] Larger emitter radius broadens penumbrae at fixed receiver / blocker geometry.
- [ ] Raising sphere center height from 0.8 to 2.0 to 3.0 changes separation and softness as expected.
- [ ] Thin poles do not disappear unexpectedly; inspect sparse blocker misses.
- [ ] Vary search and filter caps to detect truncated footprints.
- [ ] No ground stripes reappear with correction on; test extreme light angles.
- [ ] Inspect motion shimmer / pattern artifacts and document limitations.
- [ ] Disable preview, freeze motion and profile GPU cost separately; no measurements yet.
- [ ] Exercise panel scrolling on a small Game view.
