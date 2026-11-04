using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ShadowLab.Editor
{
    public static class ShadowLabSceneBuilder
    {
        const string Root = "Assets/ShadowLab/Generated";

        [MenuItem("ShadowLab/Create Demo Scene")]
        public static void CreateDemoScene()
        {
            if (!(GraphicsSettings.currentRenderPipeline is UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset)) {
                EditorUtility.DisplayDialog("URP required", "Create a Universal 3D project first, then copy Assets/ShadowLab into it.", "OK");
                return;
            }
            Shader receiverShader = Shader.Find("ShadowLab/Receiver");
            Shader depthShader = Shader.Find("ShadowLab/LinearDepth");
            if (!receiverShader || !depthShader) {
                EditorUtility.DisplayDialog("Shaders missing", "Wait for import to finish and check Console errors.", "OK");
                return;
            }
            string path = EditorUtility.SaveFilePanelInProject("Save ShadowLab scene",
                "ShadowLabDemo", "unity", "Choose a new scene path. Existing scenes are not overwritten automatically.");
            if (string.IsNullOrEmpty(path)) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) && !EditorUtility.DisplayDialog(
                "Replace scene?", "This will replace the selected scene asset.", "Replace", "Cancel")) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!AssetDatabase.IsValidFolder(Root)) AssetDatabase.CreateFolder("Assets/ShadowLab", "Generated");
            // Unique assets preserve any previous generated scene's materials.
            string depthPath = AssetDatabase.GenerateUniqueAssetPath(Root + "/LinearDepth.mat");
            var depthMaterial = new Material(depthShader);
            AssetDatabase.CreateAsset(depthMaterial, depthPath);
            Material stone = MakeMaterial(receiverShader, "Stone", new Color(.55f,.62f,.68f));
            Material orange = MakeMaterial(receiverShader, "Copper", new Color(.8f,.3f,.12f));
            Material pale = MakeMaterial(receiverShader, "Ivory", new Color(.8f,.78f,.64f));
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var casters = new List<Renderer>();
            Add("Ground", PrimitiveType.Cube, new Vector3(0,-.2f,0), new Vector3(12,.4f,12), stone, casters);
            Add("Contact cube", PrimitiveType.Cube, new Vector3(-2,.5f,0), Vector3.one, orange, casters);
            GameObject floating = Add("Floating sphere", PrimitiveType.Sphere,
                new Vector3(1,2,0), Vector3.one * 1.4f, pale, casters);
            GameObject ramp = Add("Slope test", PrimitiveType.Cube,
                new Vector3(2,.65f,2), new Vector3(3,.25f,2), stone, casters);
            ramp.transform.rotation = Quaternion.Euler(0,0,25);
            for (int i = 0; i < 7; i++)
                Add("Thin pole " + i, PrimitiveType.Cylinder, new Vector3(-3 + i*.8f,1,-2),
                    new Vector3(.07f,.95f,.07f), pale, casters);
            for (int i = 0; i < 4; i++)
                Add("Step " + i, PrimitiveType.Cube, new Vector3(-3 + i*.7f,.15f*(i+1),2.7f),
                    new Vector3(.65f,.3f*(i+1),1), orange, casters);
            var rig = new GameObject("Virtual spotlight (custom shader)").transform;
            rig.position = new Vector3(0,7,-5);
            rig.LookAt(new Vector3(0,.5f,0));
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.035f,.045f,.065f);
            camera.nearClipPlane = .1f; camera.farClipPlane = 100;
            camera.transform.position = new Vector3(10,8,-12);
            camera.transform.LookAt(new Vector3(0,.5f,0));
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            var controller = new GameObject("ShadowLab Controls").AddComponent<ShadowLabController>();
            controller.lightRig = rig;
            controller.movingObject = floating.transform;
            controller.displayCamera = camera;
            controller.depthMaterial = depthMaterial;
            controller.casters = casters.ToArray();
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, path);
            Selection.activeGameObject = controller.gameObject;
            EditorUtility.DisplayDialog("Demo created", "Open Game view and press Play. Use the panel to compare hard shadows and PCF. First-run Mac validation is still required.", "OK");
        }

        static Material MakeMaterial(Shader shader, string name, Color color)
        {
            var material = new Material(shader);
            material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, AssetDatabase.GenerateUniqueAssetPath(Root + "/" + name + ".mat"));
            return material;
        }
        static GameObject Add(string name, PrimitiveType primitive, Vector3 position,
            Vector3 scale, Material material, List<Renderer> casters)
        {
            var obj = GameObject.CreatePrimitive(primitive);
            obj.name = name;
            obj.transform.position = position;
            obj.transform.localScale = scale;
            var renderer = obj.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            casters.Add(renderer);
            return obj;
        }
    }
}
