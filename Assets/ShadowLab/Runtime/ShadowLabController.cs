using UnityEngine;
using UnityEngine.Rendering;

namespace ShadowLab
{
    /// <summary>Explicit mesh-only shadow pass, independent of URP's shadow atlas.</summary>
    public sealed class ShadowLabController : MonoBehaviour
    {
        public enum Filter { Hard, PCF3x3, PCF5x5, PCSS }
        public Transform lightRig;
        public Transform movingObject;
        public Renderer[] casters;
        public Material depthMaterial;
        public Camera displayCamera;
        public Filter filter = Filter.PCF3x3;
        public int resolution = 1024;
        [Range(0, .03f)] public float constantBias = .002f;
        [Range(0, .05f)] public float slopeBias = .004f;
        [Range(.25f, 4)] public float filterRadius = 1;
        public bool animate = true;
        public bool showDepth;
        public bool showPanel = true;
        public bool receiverPlaneBias = true;
        [Range(0, 1)] public float emitterRadius = .3f;
        [Range(4, 128)] public float searchRadiusCap = 32;
        [Range(1, 64)] public float filterRadiusCap = 32;
        [Range(.8f, 3)] public float sphereHeight = 2;
        [Range(.05f, 1)] public float previewMaxDepth = .6f;
        public float nearPlane = .2f;
        public float farPlane = 25;
        public float fieldOfView = 70;
        RenderTexture map;
        RenderTexture depthPreview;
        Mesh previewTriangle;
        MaterialPropertyBlock drawProperties;
        int submittedDraws;
        string depthStatistics = "GPU depth range: not sampled";
        bool readbackPending;
        int lastSubmissionFrame = -1;
        CommandBuffer commands;
        float elapsed;
        Vector3 movingOrigin;
        bool warned;
        Rect panel = new Rect(14, 14, 390, 790);
        Vector2 panelScroll;

        void OnEnable()
        {
            if (movingObject) movingOrigin = movingObject.position;
            commands = new CommandBuffer { name = "ShadowLab custom shadow map" };
            drawProperties = new MaterialPropertyBlock();
            previewTriangle = new Mesh { name = "ShadowLab Preview Triangle",
                hideFlags = HideFlags.HideAndDontSave };
            previewTriangle.vertices = new[] {
                new Vector3(-1,-1,0), new Vector3(-1,3,0), new Vector3(3,-1,0)
            };
            previewTriangle.triangles = new[] { 0, 1, 2 };
            previewTriangle.UploadMeshData(true);
            RenderPipelineManager.beginCameraRendering += RenderShadows;
        }

        void Update()
        {
            if (!animate) {
                if (movingObject)
                    movingObject.position = new Vector3(movingOrigin.x, sphereHeight, movingOrigin.z);
                return;
            }
            elapsed += Time.deltaTime;
            if (lightRig)
            {
                lightRig.position = new Vector3(Mathf.Sin(elapsed * .4f) * 4, 7, -5);
                lightRig.LookAt(new Vector3(0, .5f, 0));
            }
            if (movingObject)
                movingObject.position = new Vector3(movingOrigin.x, sphereHeight + Mathf.Sin(elapsed) * .7f, movingOrigin.z);
        }

        void EnsureMap()
        {
            resolution = Mathf.Clamp(resolution, 256, 4096);
            if (map && map.width == resolution) return;
            ReleaseMap();
            // Linear normalized light-view depth in a COLOR texture. Hardware depth
            // independently selects the frontmost fragment (including reversed Z).
            map = new RenderTexture(resolution, resolution, 24, RenderTextureFormat.RFloat,
                RenderTextureReadWrite.Linear) {
                name = "ShadowLab Linear Depth", filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp, antiAliasing = 1,
                useMipMap = false, autoGenerateMips = false
            };
            if (!map.Create()) Debug.LogError("ShadowLab could not create the shadow map.");
            depthPreview = new RenderTexture(256, 256, 0, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Linear) {
                name = "ShadowLab Grayscale Preview", filterMode = FilterMode.Point
            };
            depthPreview.Create();
        }

        void RenderShadows(ScriptableRenderContext context, Camera camera)
        {
            if (camera != displayCamera || !lightRig || !depthMaterial) return;
            if (!SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RFloat))
            {
                if (!warned) Debug.LogError("ShadowLab requires RFloat render textures.");
                warned = true;
                return;
            }
            EnsureMap();
            nearPlane = Mathf.Max(.01f, nearPlane);
            farPlane = Mathf.Max(nearPlane + .1f, farPlane);
            // Unity camera space looks along -Z. Transform.forward points +Z.
            Matrix4x4 view = Matrix4x4.Scale(new Vector3(1, 1, -1)) * lightRig.worldToLocalMatrix;
            Matrix4x4 projection = GL.GetGPUProjectionMatrix(
                Matrix4x4.Perspective(fieldOfView, 1, nearPlane, farPlane), true);
            Matrix4x4 vp = projection * view;
            commands.Clear();
            commands.BeginSample("ShadowLab.ShadowMap");
            commands.SetRenderTarget(map);
            commands.SetViewport(new Rect(0, 0, resolution, resolution));
            // ClearRenderTarget uses Unity's logical clear depth. Keep its default
            // far value instead of manually applying reversed-Z a second time.
            commands.ClearRenderTarget(true, true, Color.white);
            commands.SetGlobalMatrix("_SL_LightVP", vp);
            commands.SetGlobalMatrix("_SL_LightVPInverse", vp.inverse);
            commands.SetGlobalFloat("_SL_ReceiverPlane", receiverPlaneBias ? 1f : 0f);
            commands.SetGlobalFloat("_SL_NearPlane", nearPlane);
            commands.SetGlobalVector("_SL_PCSS", new Vector4(emitterRadius,
                Mathf.Tan(fieldOfView * .5f * Mathf.Deg2Rad), searchRadiusCap, filterRadiusCap));
            commands.SetGlobalVector("_SL_LightPosition", lightRig.position);
            commands.SetGlobalVector("_SL_LightForward", lightRig.forward);
            commands.SetGlobalFloat("_SL_Far", farPlane);
            submittedDraws = 0;
            if (casters != null)
                foreach (Renderer caster in casters)
                {
                    if (!caster || !caster.enabled || !caster.gameObject.activeInHierarchy) continue;
                    var meshFilter = caster.GetComponent<MeshFilter>();
                    if (!meshFilter || !meshFilter.sharedMesh) continue;
                    // Supply object transform explicitly: do not depend on Renderer state
                    // established by the display camera or SRP Batcher.
                    drawProperties.Clear();
                    drawProperties.SetMatrix("_SL_ObjectToWorld", caster.localToWorldMatrix);
                    for (int submesh = 0; submesh < meshFilter.sharedMesh.subMeshCount; submesh++)
                    {
                        commands.DrawMesh(meshFilter.sharedMesh, Matrix4x4.identity,
                            depthMaterial, submesh, 0, drawProperties);
                        submittedDraws++;
                    }
                }
            commands.EndSample("ShadowLab.ShadowMap");
            if (showDepth && depthMaterial.passCount > 1 && previewTriangle)
            {
                commands.BeginSample("ShadowLab.DepthPreview");
                commands.SetRenderTarget(depthPreview);
                commands.SetViewport(new Rect(0, 0, depthPreview.width, depthPreview.height));
                // Magenta remains visible if the explicit preview pass fails.
                commands.ClearRenderTarget(false, true, Color.magenta);
                commands.SetGlobalTexture("_SL_PreviewSource", map);
                commands.SetGlobalVector("_SL_PreviewRange",
                    new Vector4(0, Mathf.Max(.05f, previewMaxDepth), 0, 0));
                commands.DrawMesh(previewTriangle, Matrix4x4.identity, depthMaterial, 0, 1);
                commands.EndSample("ShadowLab.DepthPreview");
            }
            commands.SetGlobalTexture("_SL_ShadowMap", map);
            commands.SetGlobalVector("_SL_MapInfo", new Vector4(1f / resolution, 1f / resolution,
                filterRadius, (int)filter));
            commands.SetGlobalVector("_SL_Bias", new Vector4(constantBias, slopeBias,
                nearPlane / farPlane, Mathf.Cos(fieldOfView * .5f * Mathf.Deg2Rad)));
            // Queue on the active SRP context so writes precede URP receiver draws.
            // URP owns Submit(); never submit this context ourselves.
            context.ExecuteCommandBuffer(commands);
            lastSubmissionFrame = Time.frameCount;
        }

        void OnGUI()
        {
            if (!showPanel) return;
            panel.height = Mathf.Min(790, Mathf.Max(200, Screen.height - 28));
            panel = GUI.Window(GetInstanceID(), panel, DrawPanel, "ShadowLab | custom shadows");
            if (showDepth && depthPreview) {
                float size = Mathf.Min(300, Screen.width * .3f);
                GUI.DrawTexture(new Rect(Screen.width - size - 16, 16, size, size), depthPreview,
                    ScaleMode.ScaleToFit, false);
                GUI.Label(new Rect(Screen.width - size - 16, size + 20, size, 50),
                    "Linear light depth (white = far / empty)");
            }
        }

        void DrawPanel(int id)
        {
            panelScroll = GUILayout.BeginScrollView(panelScroll, GUILayout.Height(panel.height - 40));
            GUILayout.Label("ShadowLab v0.5 | PCF + PCSS");
            GUILayout.Label($"Shadow draw submissions: {submittedDraws} | frame {lastSubmissionFrame}");
            filter = (Filter)GUILayout.SelectionGrid((int)filter,
                new[] { "Hard", "PCF 3x3", "PCF 5x5", "PCSS" }, 4);
            GUILayout.Label($"Active: {filter} | {resolution}px");
            int selected = resolution <= 512 ? 0 : resolution <= 1024 ? 1 : 2;
            selected = GUILayout.SelectionGrid(selected, new[] { "512", "1024", "2048" }, 3);
            resolution = new[] { 512, 1024, 2048 }[selected];
            GUILayout.Label($"Constant bias: {constantBias:F4}");
            constantBias = GUILayout.HorizontalSlider(constantBias, 0, .03f);
            GUILayout.Label($"Slope bias: {slopeBias:F4}");
            slopeBias = GUILayout.HorizontalSlider(slopeBias, 0, .05f);
            GUILayout.Label($"PCF radius / texel spacing: {filterRadius:F2}");
            filterRadius = GUILayout.HorizontalSlider(filterRadius, .25f, 4);
            if (filter == Filter.PCSS) {
                GUILayout.Label($"Emitter radius: {emitterRadius:F2} world units");
                emitterRadius = GUILayout.HorizontalSlider(emitterRadius, 0, 1);
                GUILayout.Label("32 blocker + up to 32 filter depth samples");
                GUILayout.Label($"Search cap: {searchRadiusCap:F0} texels");
                searchRadiusCap = GUILayout.HorizontalSlider(searchRadiusCap, 4, 128);
                GUILayout.Label($"Filter cap: {filterRadiusCap:F0} texels");
                filterRadiusCap = GUILayout.HorizontalSlider(filterRadiusCap, 1, 64);
                GUILayout.Label("PCF spacing above does not control PCSS.");
            }
            animate = GUILayout.Toggle(animate, "Animate light and floating sphere");
            if (!animate) {
                GUILayout.Label($"Sphere center height: {sphereHeight:F2}");
                sphereHeight = GUILayout.HorizontalSlider(sphereHeight, .8f, 3);
            }
            receiverPlaneBias = GUILayout.Toggle(receiverPlaneBias, "Receiver-plane depth correction");
            showDepth = GUILayout.Toggle(showDepth, "Show shadow map");
            if (showDepth) {
                GUILayout.Label($"Preview max depth: {previewMaxDepth:F2} (display only)");
                previewMaxDepth = GUILayout.HorizontalSlider(previewMaxDepth, .05f, 1f);
            }
            GUILayout.Space(8);
            if (GUILayout.Button("Preset: expose shadow acne")) {
                constantBias = 0; slopeBias = 0; receiverPlaneBias = false; filter = Filter.Hard;
            }
            if (GUILayout.Button("Preset: excessive bias / detached shadow")) {
                constantBias = .025f; slopeBias = .02f; filter = Filter.Hard;
            }
            if (GUILayout.Button("Preset: baseline")) {
                constantBias = .002f; slopeBias = .004f; filterRadius = 1;
                filter = Filter.PCF3x3; resolution = 1024; receiverPlaneBias = true;
            }
            GUILayout.Space(8);
            GUI.enabled = !readbackPending && map;
            if (GUILayout.Button("Read GPU depth range")) ReadDepthRange();
            GUI.enabled = true;
            GUILayout.Label(depthStatistics);
            GUILayout.Space(8);
            GUILayout.Label("Frame time / FPS is NOT GPU shadow timing.\nUse Unity Profiler / GPU capture.\nPCSS: initial implementation, unverified.\nMac visual validation: pending.");
            GUILayout.EndScrollView();
            GUI.DragWindow(new Rect(0, 0, 390, 22));
        }

        void ReadDepthRange()
        {
            if (!map || readbackPending) return;
            if (!SystemInfo.supportsAsyncGPUReadback) {
                depthStatistics = "GPU readback unavailable on this device";
                return;
            }
            readbackPending = true;
            depthStatistics = "GPU depth range: reading...";
            AsyncGPUReadback.Request(map, 0, TextureFormat.RFloat, request => {
                readbackPending = false;
                if (!this || !isActiveAndEnabled) return;
                if (request.hasError) {
                    depthStatistics = "GPU depth readback failed; check Console";
                    return;
                }
                var data = request.GetData<float>();
                float minimum = float.PositiveInfinity, maximum = float.NegativeInfinity;
                int covered = 0, invalid = 0;
                for (int i = 0; i < data.Length; i++) {
                    float value = data[i];
                    if (float.IsNaN(value) || float.IsInfinity(value)) { invalid++; continue; }
                    minimum = Mathf.Min(minimum, value);
                    maximum = Mathf.Max(maximum, value);
                    if (value < .9999f) covered++;
                }
                depthStatistics = $"GPU depth: {minimum:F4} .. {maximum:F4}\n" +
                    $"Written pixels: {100f * covered / Mathf.Max(1, data.Length):F1}% | invalid {invalid}";
            });
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= RenderShadows;
            commands?.Release(); commands = null;
            if (previewTriangle) {
                if (Application.isPlaying) Destroy(previewTriangle); else DestroyImmediate(previewTriangle);
                previewTriangle = null;
            }
            Shader.SetGlobalTexture("_SL_ShadowMap", Texture2D.whiteTexture);
            ReleaseMap();
        }
        void ReleaseMap()
        {
            if (depthPreview) {
                depthPreview.Release();
                if (Application.isPlaying) Destroy(depthPreview); else DestroyImmediate(depthPreview);
                depthPreview = null;
            }
            if (!map) return;
            map.Release();
            if (Application.isPlaying) Destroy(map); else DestroyImmediate(map);
            map = null;
        }
    }
}
