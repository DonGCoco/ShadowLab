# Repeated GPU analysis

Use a standalone macOS Player with Frame Timing Stats enabled. This procedure uses the existing capture button; no Unity source changes or rebuild are required if your current Player already contains the collector.

1. Keep animation OFF between ALL captures, depth preview OFF, and camera/light fixed. Choose Baseline before starting; then change ONLY mode. Keep map at 1024, spacing 1, emitter radius 0.30, height 2, caps 32. Preserve your recorded output resolution, render scale, MSAA and VSync.
2. Keep the Player focused. Do not record video, resize, move the camera/light or change Inspector parameters. Keep power and thermal conditions consistent.
3. Capture three rounds, varying order to reduce order effects:
   - Round 1: Hard, PCF3x3, PCF5x5, PCSS
   - Round 2: PCSS, PCF5x5, PCF3x3, Hard
   - Round 3: PCF3x3, Hard, PCSS, PCF5x5
4. For every mode use **Record 300 GPU frames (current mode)**. Keep the exported folder with samples.csv, conditions.json and summary.txt. Collect all 12 folders inside a NEW repeated-captures directory; exclude old exploratory runs.
5. From the repository root, run:

```bash
python3 Tools/analyze_gpu_runs.py /path/to/repeated-captures > gpu-report.md
```

If validation fails, do not publish the redirected report. Inspect the stated problem and recapture as needed; do not change metadata to make mismatched runs pass.

The script requires at least three complete standalone captures per mode, 300 unique valid timestamps per run, positive finite GPU timings, and matching recorded conditions apart from mode. It trusts the collector's completeness flag for availability. It cannot detect unrecorded thermal/scene changes, transient changes, or verify GPU output correctness.

It reports the median of run medians, the min/max run medians, the median of run P95s and absolute total-frame difference against Hard. It does not pool all frames into independent trials or claim statistical significance. Total-frame differences must not be labelled isolated PCSS/shadow-pass cost. Reference-renderer accuracy and per-pass profiling remain separate work.

The existing single-run results remain historical single-run evidence. New performance conclusions require real repeated captures; no new GPU measurements are included in this change.
