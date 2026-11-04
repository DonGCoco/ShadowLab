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
        readonly FrameTiming[] latest = new FrameTiming[1];
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
                // Drain delayed data and warm the selected shader / fixed pose.
                double warmUntil = Time.realtimeSinceStartupAsDouble + 3;
                while (Time.realtimeSinceStartupAsDouble < warmUntil) {
                    Status = "Warming up (3 seconds)...";
                    FrameTimingManager.CaptureFrameTimings();
                    yield return null;
                    if (FrameTimingManager.GetLatestTimings(1, latest) > 0)
                        seen.Add(latest[0].frameStartTimestamp);
                }
                string before = JsonUtility.ToJson(Snapshot(owner), true);
                double deadline = Time.realtimeSinceStartupAsDouble + 30;
                int invalid = 0;
                while (samples.Count < 300 && Time.realtimeSinceStartupAsDouble < deadline) {
                    FrameTimingManager.CaptureFrameTimings();
                    yield return null;
                    if (FrameTimingManager.GetLatestTimings(1, latest) == 0) continue;
                    FrameTiming timing = latest[0];
                    if (timing.frameStartTimestamp == 0 || !seen.Add(timing.frameStartTimestamp)) continue;
                    if (timing.gpuFrameTime <= 0 || double.IsNaN(timing.gpuFrameTime) ||
                        double.IsInfinity(timing.gpuFrameTime)) { invalid++; continue; }
                    samples.Add(timing);
                    Status = $"Total GPU capture: {samples.Count}/300";
                }
                if (samples.Count != 300) {
                    Status = $"No complete result: {samples.Count}/300 valid GPU samples, {invalid} invalid. No FPS substitution.";
                    yield break;
                }
                if (before != JsonUtility.ToJson(Snapshot(owner), true)) {
                    Status = "Conditions changed during capture; discarded. Keep Game view / Inspector fixed.";
                    yield break;
                }
                Export(before);
            }
            finally { Restore(); }
        }

        void Export(string conditions)
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
                double median = (gpu[149] + gpu[150]) * .5;
                double p95 = gpu[(int)Math.Ceiling(gpu.Count * .95) - 1];
                File.WriteAllText(Path.Combine(LastFolder, "samples.csv"), csv.ToString());
                File.WriteAllText(Path.Combine(LastFolder, "conditions.json"), conditions);
                File.WriteAllText(Path.Combine(LastFolder, "summary.txt"),
                    $"FrameTimingManager total-frame GPU timing; not isolated shadows.\nValid samples: 300\nMedian GPU ms: {median.ToString("F6", CultureInfo.InvariantCulture)}\nP95 GPU ms: {p95.ToString("F6", CultureInfo.InvariantCulture)}\nEditor overhead / GPU scheduling may affect results.\n");
                Status = $"Saved: median {median:F3} ms, P95 {p95:F3} ms (total GPU)";
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
