using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ShadowLab
{
    // Total-frame timings only. No claim of isolated shadow-pass timing.
    public sealed class ShadowLabBenchmark : MonoBehaviour
    {
        public bool Running { get; private set; }
        public string Status { get; private set; } = "No capture yet";
        public string LastFolder { get; private set; }
        readonly FrameTiming[] latest = new FrameTiming[16];
        readonly List<FrameTiming> samples = new List<FrameTiming>();
        readonly HashSet<ulong> seen = new HashSet<ulong>();
        ShadowLabController owner;
        bool savedAnimate, savedPreview, savedPanel;

        [Serializable]
        sealed class Conditions
        {
            public string unity, os, gpu, graphicsApi, mode, cameraPose, lightPose;
            public bool editor, receiverCorrection;
            public int screenWidth, screenHeight, cameraWidth, cameraHeight, mapResolution, msaa, vSync;
            public float renderScale, bias, slopeBias, spacing, emitterRadius, sphereHeight, searchCap, filterCap;
        }

        static Conditions Snapshot(ShadowLabController c)
        {
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            return new Conditions {
                unity = Application.unityVersion, os = SystemInfo.operatingSystem,
                gpu = SystemInfo.graphicsDeviceName, graphicsApi = SystemInfo.graphicsDeviceType.ToString(),
                mode = c.filter.ToString(), editor = Application.isEditor,
                cameraPose = c.displayCamera.transform.localToWorldMatrix.ToString("F5"),
                lightPose = c.lightRig.localToWorldMatrix.ToString("F5"),
                screenWidth = Screen.width, screenHeight = Screen.height,
                cameraWidth = c.displayCamera.pixelWidth, cameraHeight = c.displayCamera.pixelHeight,
                mapResolution = c.resolution, msaa = pipeline ? pipeline.msaaSampleCount : 0,
                renderScale = pipeline ? pipeline.renderScale : 1, vSync = QualitySettings.vSyncCount,
                receiverCorrection = c.receiverPlaneBias, bias = c.constantBias, slopeBias = c.slopeBias,
                spacing = c.filterRadius, emitterRadius = c.emitterRadius, sphereHeight = c.sphereHeight,
                searchCap = c.searchRadiusCap, filterCap = c.filterRadiusCap
            };
        }

        public void Begin(ShadowLabController controller)
        {
            LastFolder = null;
            if (Running || !controller || !controller.displayCamera || !controller.lightRig) return;
            if (!FrameTimingManager.IsFeatureEnabled()) {
                Status = "Enable Frame Timing Stats in Player Settings, then restart Play.";
                return;
            }
            owner = controller;
            savedAnimate = owner.animate; savedPreview = owner.showDepth; savedPanel = owner.showPanel;
            owner.animate = false; owner.showDepth = false; owner.showPanel = false;
            Running = true;
            StartCoroutine(Capture());
        }

        IEnumerator Capture()
        {
            samples.Clear(); seen.Clear();
            try {
                // Drain timing history from the warm-up period.
                ulong warmCutoff = 0;
                double warmUntil = Time.realtimeSinceStartupAsDouble + 3;
                while (Time.realtimeSinceStartupAsDouble < warmUntil) {
                    Status = "Warming up (3 seconds)...";
                    FrameTimingManager.CaptureFrameTimings();
                    yield return null;
                    uint count = FrameTimingManager.GetLatestTimings((uint)latest.Length, latest);
                    for (int i = 0; i < (int)count; i++)
                        if (latest[i].frameStartTimestamp > warmCutoff)
                            warmCutoff = latest[i].frameStartTimestamp;
                }
                string before = JsonUtility.ToJson(Snapshot(owner), true);
                double deadline = Time.realtimeSinceStartupAsDouble + 30;
                var observed = new HashSet<ulong>();
                while (samples.Count < 300 && Time.realtimeSinceStartupAsDouble < deadline) {
                    FrameTimingManager.CaptureFrameTimings();
                    yield return null;
                    uint count = FrameTimingManager.GetLatestTimings((uint)latest.Length, latest);
                    // Unity returns newest first. Process the whole returned window,
                    // oldest first, so completed older GPU results are not skipped.
                    for (int i = (int)count - 1; i >= 0 && samples.Count < 300; i--) {
                        FrameTiming timing = latest[i];
                        ulong stamp = timing.frameStartTimestamp;
                        if (stamp == 0 || stamp <= warmCutoff || seen.Contains(stamp)) continue;
                        observed.Add(stamp);
                        if (timing.gpuFrameTime <= 0 || double.IsNaN(timing.gpuFrameTime) ||
                            double.IsInfinity(timing.gpuFrameTime)) continue;
                        // Only retire a timestamp once its GPU timing is VALID.
                        // A zero observation may be returned with completed data later.
                        seen.Add(stamp);
                        samples.Add(timing);
                    }
                    Status = $"GPU samples: {samples.Count}/300 | observed frames {observed.Count}";
                }
                if (before != JsonUtility.ToJson(Snapshot(owner), true)) {
                    Status = "Conditions changed during capture; discarded. Keep Game view / Inspector fixed.";
                    yield break;
                }
                if (samples.Count == 0) {
                    Status = "No valid GPU timings. Test a standalone macOS Player; no FPS fallback.";
                    yield break;
                }
                Export(before, observed.Count);
            }
            finally { Restore(); }
        }

        void Export(string conditions, int observedCount)
        {
            try {
                string name = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture) +
                    "_" + owner.filter + "_" + Guid.NewGuid().ToString("N").Substring(0,6);
                LastFolder = Path.Combine(Application.persistentDataPath, "ShadowLabBenchmarks", name);
                Directory.CreateDirectory(LastFolder);
                var csv = new StringBuilder("sample,frame_start_timestamp,total_gpu_ms,cpu_frame_ms\n");
                var gpu = new List<double>();
                for (int i = 0; i < samples.Count; i++) {
                    FrameTiming t = samples[i];
                    csv.Append(i).Append(',').Append(t.frameStartTimestamp).Append(',')
                        .Append(t.gpuFrameTime.ToString("F6", CultureInfo.InvariantCulture)).Append(',')
                        .Append(t.cpuFrameTime.ToString("F6", CultureInfo.InvariantCulture)).Append('\n');
                    gpu.Add(t.gpuFrameTime);
                }
                gpu.Sort();
                int middle = gpu.Count / 2;
                double median = gpu.Count % 2 == 0 ?
                    (gpu[middle - 1] + gpu[middle]) * .5 : gpu[middle];
                double availability = (double)gpu.Count / Math.Max(gpu.Count, observedCount);
                bool complete = gpu.Count == 300 && availability >= .95;
                double p95 = gpu[(int)Math.Ceiling(gpu.Count * .95) - 1];
                File.WriteAllText(Path.Combine(LastFolder, "samples.csv"), csv.ToString());
                File.WriteAllText(Path.Combine(LastFolder, "conditions.json"), conditions);
                File.WriteAllText(Path.Combine(LastFolder, "summary.txt"),
                    $"FrameTimingManager total-frame GPU timing; not isolated shadows.\nComplete benchmark: {complete}\nValid samples: {gpu.Count}\nObserved unique frames: {observedCount}\nAvailability: {availability.ToString("P2", CultureInfo.InvariantCulture)}\nInsufficient samples or low availability: diagnostics only, do not use for performance claims.\nMedian of VALID GPU samples ms: {median.ToString("F6", CultureInfo.InvariantCulture)}\nP95 GPU ms: {p95.ToString("F6", CultureInfo.InvariantCulture)}\nEditor overhead / GPU scheduling may affect results.\n");
                Status = complete ?
                    $"Saved 300 samples: median {median:F3} ms, P95 {p95:F3} ms (total GPU)" :
                    $"Saved DIAGNOSTICS: {gpu.Count}/300 samples, {availability:P1} availability. Test a macOS Player.";
                Debug.Log("ShadowLab benchmark saved: " + LastFolder);
            } catch (Exception ex) {
                Status = "Could not save benchmark: " + ex.Message;
                Debug.LogException(ex);
            }
        }

        void Restore()
        {
            if (owner) { owner.animate = savedAnimate; owner.showDepth = savedPreview; owner.showPanel = savedPanel; }
            Running = false;
        }

        public void Cancel()
        {
            StopAllCoroutines();
            Restore();
            Status = "Capture cancelled";
        }

        void OnGUI()
        {
            if (!Running) return;
            // Same small HUD for all modes; hide mode-specific controls while timing.
            GUI.Box(new Rect(14,14,350,80), Status);
            if (GUI.Button(new Rect(24,49,330,28), "Cancel capture")) Cancel();
        }

        void OnDisable() { if (Running) Cancel(); }
    }
}
