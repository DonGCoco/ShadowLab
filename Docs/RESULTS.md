# Initial macOS GPU results

These measurements come from a standalone Player capture, separate from the demonstration recording.

| Mode | Valid samples | Median total GPU ms | P95 total GPU ms |
|---|---:|---:|---:|
| Hard | 300 | 3.923396 | 5.379083 |
| PCF3x3 | 300 | 4.560938 | 5.892083 |
| PCF5x5 | 300 | 5.516063 | 6.974125 |
| PCSS | 300 | 7.836792 | 9.545000 |

## Conditions and collection

- Apple M1; Metal; Unity 6000.3.24f1; standalone macOS Player.
- Output and camera dimensions 2560×1600; render scale 1; MSAA 1; vSync 0.
- Shadow map 1024²; constant bias 0.002; slope parameter 0.004; receiver-plane correction enabled.
- PCF spacing 1; PCSS emitter radius 0.30 world units; search and filter caps 32 texels; sphere center height 2.
- Matching camera and light pose across all four condition files.
- Motion paused, depth preview disabled and capture UI reduced during collection.
- FrameTimingManager total-frame GPU timing, warm-up followed by 300 distinct positive samples. All four exports report complete and 100% observed sample availability.

## Interpretation

In this scene and run, increasing filtering work increased total-frame GPU time. PCSS was about 3.913 ms above Hard. That difference includes the effect of the changed mode on the complete frame; it is not a directly measured shadow-pass duration.

This is one run per mode on one device. No repeat-run variance, confidence interval or broader scene / device coverage is established. Do not compare directly with earlier Editor captures at different output dimensions. The 95% availability acceptance threshold in the collector is a project rule, not an API guarantee.

## Raw exports

Each mode directory contains conditions.json, samples.csv and summary.txt:

- [Hard](../Results/macos-m1-single-run/Hard)
- [PCF3x3](../Results/macos-m1-single-run/PCF3x3)
- [PCF5x5](../Results/macos-m1-single-run/PCF5x5)
- [PCSS](../Results/macos-m1-single-run/PCSS)
- [Aggregate](../Results/macos-m1-single-run/aggregate.json)

## Next measurement work

Repeat runs in alternating mode order, report variation, and use an appropriate GPU capture to inspect individual passes. Keep scene, output size, correction setting and light pose fixed. Check cap sensitivity and thin-caster visibility separately from speed.

