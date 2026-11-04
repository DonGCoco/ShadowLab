# Performance capture and presentation

## Mac measurement path

Unity 6.3's GPU Usage Profiler module does not support macOS Metal. Use FrameTimingManager for **total-frame GPU time**, and Xcode / Metal tools if per-pass detail is required. Neither total GPU time nor FPS isolates the shadow algorithm.

References:
- https://docs.unity.com/en-us/engine/6000.3/manual/analysis/graphics-performance-profiling/profile-rendering/profiler-gpu
- https://docs.unity.com/en-us/engine/6000.0/manual/analysis/graphics-performance-profiling/profile-rendering/frame-timing-manager/manager
- https://docs.unity.com/en-us/engine/6000.0/script-reference/unityeditor/playersettings/enableframetimingstats

Metal tile-based scheduling can make reported GPU frame time exceed wall frame time. Do not silently replace zero / unsupported GPU timing with a CPU or FPS-derived value.

## Install v0.6 capture tooling over v0.5

Replace Runtime/ShadowLabController.cs and add Runtime/ShadowLabBenchmark.cs (plus supplied .meta). Keep the existing scene, materials and shaders. The renderer / PCSS algorithm has not changed in this revision.

Before Play, enable **Edit > Project Settings > Player > Other Settings > Rendering > Frame Timing Stats**. If the option is hard to find, use Project Settings search for `Frame Timing`. Restart Play after changing it. This repository does not change your Player Settings automatically.

Scroll down inside the ShadowLab panel to **Record 300 GPU frames (current mode)**. Capture tools have not yet been compiled or run on the user's host.

## Fixed conditions

Choose a fixed Game view pixel resolution (for example 1280x720), not Free Aspect. Keep camera, light, scene, render scale, MSAA, map resolution and all bias / PCSS controls fixed. Use 1024 shadow resolution, spacing 1, emitter radius 0.30, sphere height 2 and search/filter caps 32. Disable animation and preview. For subsequent runs only change the shadow mode.

Close unrelated rendering-heavy windows, keep power / thermal conditions consistent and do not record the screen while timing. The first numbers from Editor Play mode are exploratory; record a standalone Player result before presenting performance claims. This tool works at runtime, so it can also be used in a Player built with Frame Timing Stats enabled. Do not disable VSync or change global quality settings without recording that choice.

## Record

1. Select Hard.
2. Scroll down and click **Record 300 GPU frames (current mode)**.
3. The capture freezes animation, disables preview and replaces the mode-dependent panel with the same small HUD for all modes, warms up for 3 seconds, then gathers 300 distinct, positive GPU timing samples. The sample phase times out after 30 seconds.
4. Do not change Game view size, Inspector values, camera or light during capture. Finish-condition metadata is compared with start conditions; a mismatch discards the run. The tool does not detect transient changes that are later reverted.
5. On completion use **Open benchmark folder**; copy the run folder.
6. Repeat PCF3x3, PCF5x5 and PCSS at the same conditions. Repeat the complete sequence three times to assess noise.

The capture restores animation/preview/panel after completion or cancellation. Each folder contains:
- samples.csv: raw total GPU milliseconds and separately labeled CPU frame milliseconds.
- conditions.json: Unity, GPU, OS, graphics API, Editor/Player, dimensions and rendering parameters.
- summary.txt: total-GPU median and nearest-rank P95.

If some timings are valid but the target is not reached, raw valid samples are exported as diagnostics. A run is marked complete only with 300 valid samples and at least 95% availability among unique observed frames. This threshold is a conservative project rule, not a Unity guarantee. Low availability can bias the retained sample distribution; do not use diagnostic-only medians as benchmark results. If all GPU timings are unavailable / zero, no sample CSV is exported. Report unavailable, then use an appropriate platform capture tool. The benchmark labels itself as total-frame timing and makes no isolated-shadow-pass claim. The named ShadowLab.ShadowMap sample covers generation only; PCF/PCSS work runs in the receiver shader.

## Result table (no measurements yet)

| Mode | Valid samples | Median total GPU ms | P95 total GPU ms | Environment |
|---|---|---|---|---|
| Hard | pending | pending | pending | pending |
| PCF3x3 | pending | pending | pending | pending |
| PCF5x5 | pending | pending | pending | pending |
| PCSS | pending | pending | pending | pending |

Do not report a mode speedup when differences are within run-to-run variability. Do not infer an FPS benefit when another frame bottleneck dominates.

## 60-90 second demo outline

1. Show fixed scene: Hard, PCF3x3, PCF5x5, PCSS (about 20 seconds).
2. Freeze all conditions; vary PCSS emitter radius 0 / 0.30 / 0.60 (15 seconds).
3. Fix radius 0.30; vary sphere center height 0.8 / 2.0 / 3.0 (15 seconds).
4. Show receiver-plane correction off/on in wide PCF at 512 (10 seconds).
5. Show the depth preview briefly (10 seconds).
6. If actual validated benchmark results exist, show the measured quality/cost comparison; otherwise say measurement is pending.

For visual captures, hide unrelated windows and use consistent framing. Explain that the project reproduces established shadow algorithms and explores implementation tradeoffs, not a novel shadow algorithm.

## Timing collector follow-up

A user Editor capture obtained 208 valid samples and reported 21298 invalid observations in 30 seconds. The previous collector read only the latest frame and retired its timestamp even when GPU time was invalid. The corrected collector requests up to 16 historical timings, excludes pre-measurement history and deduplicates only accepted valid samples. Partial results retain raw data with explicit diagnostic-only status. The change has not been run on the host; it cannot fix a platform that does not provide GPU timings.

If the Editor still reports sparse or zero results, test a standalone macOS Player before spending time on all four modes:

1. Exit Play and save the generated scene.
2. Open File > Build Profiles, select a macOS build profile (create one if needed), and switch to it if necessary.
3. Include the generated ShadowLab scene in Scene List; for this demo use only that scene.
4. Keep Frame Timing Stats enabled. Use a normal Player build for final measurements; a Development Build can be used for diagnosis, with results labeled accordingly.
5. Choose Build And Run, saving the .app in a separate Builds folder outside Assets.
6. In the running app choose the same fixed conditions and record Hard first. Keep the app focused; do not resize while recording.
7. If valid timing remains sparse, keep diagnostic exports and use Xcode/Metal profiling. Do not extend timeouts or lower quality thresholds merely to obtain publishable-looking numbers.
