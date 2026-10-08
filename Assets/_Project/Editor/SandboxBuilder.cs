using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ZZabmongus.Core;
using ZZabmongus.Runtime;

namespace ZZabmongus.Editor
{
    public static class SandboxBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/FoundationSandbox.unity";
        private static double fontDeadline;
        private static bool waitingForFonts;

        [MenuItem("ZZabmongus/Create or Open Foundation Sandbox")]
        public static void CreateOrOpen()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode first.");
            for (var i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save your modified scenes before opening the sandbox.");
            if (waitingForFonts) return;
            if (Resources.Load<TMP_Settings>("TMP Settings") == null)
            {
                waitingForFonts = true;
                fontDeadline = EditorApplication.timeSinceStartup + 120;
                EditorApplication.update += WaitForFonts;
                TMP_PackageResourceImporter.ImportResources(true, false, false);
                return;
            }
            BuildOrOpenScene();
        }

        private static void WaitForFonts()
        {
            if (Resources.Load<TMP_Settings>("TMP Settings") == null)
            {
                if (EditorApplication.timeSinceStartup < fontDeadline) return;
                EditorApplication.update -= WaitForFonts;
                waitingForFonts = false;
                Debug.LogError("TMP resource import did not complete. Retry sandbox creation after resolving package errors.");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }
            EditorApplication.update -= WaitForFonts;
            waitingForFonts = false;
            EditorApplication.delayCall += BuildOrOpenScene;
        }

        private static void BuildOrOpenScene()
        {
            if (File.Exists(ScenePath))
            {
                var existing = EditorSceneManager.OpenScene(ScenePath);
                foreach (var text in UnityEngine.Object.FindObjectsByType<TextMeshPro>(FindObjectsSortMode.None))
                    if (text.font == null) text.font = TMP_Settings.defaultFontAsset;
                EditorSceneManager.SaveScene(existing);
                AssetDatabase.SaveAssets();
                if (Application.isBatchMode) EditorApplication.Exit(0);
                return;
            }
            Directory.CreateDirectory("Assets/_Project/Scenes");
            Directory.CreateDirectory("Assets/_Project/Settings");
            Directory.CreateDirectory("Assets/_Project/Art/Materials");
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0, 24, -15);
            camera.transform.LookAt(new Vector3(0, 0, 0));
            camera.orthographic = true;
            camera.orthographicSize = 14;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.055f, 0.075f, 0.11f);
            var light = new GameObject("Key Light", typeof(Light)).GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.5f;
            light.transform.rotation = Quaternion.Euler(50, -30, 0);
            RenderSettings.ambientLight = new Color(0.5f, 0.55f, 0.65f);
            var floor = Material("Floor", new Color(0.13f, 0.18f, 0.24f));
            var wall = Material("Walls", new Color(0.23f, 0.3f, 0.37f));
            Box("Arena", new Vector3(0, -0.25f, 0), new Vector3(22, 0.5f, 18), floor);
            Box("NorthWall", new Vector3(0, 0.5f, 9), new Vector3(22, 1, 0.4f), wall);
            Box("SouthWall", new Vector3(0, 0.5f, -9), new Vector3(22, 1, 0.4f), wall);
            Box("WestWall", new Vector3(-11, 0.5f, 0), new Vector3(0.4f, 1, 18), wall);
            Box("EastWall", new Vector3(11, 0.5f, 0), new Vector3(0.4f, 1, 18), wall);
            Station(DeviceKind.Parity, new Vector3(-8, 0.5f, 5), new Color(0.25f, 0.85f, 0.9f), camera);
            Station(DeviceKind.Compare, new Vector3(8, 0.5f, 5), new Color(0.4f, 0.65f, 1), camera);
            Station(DeviceKind.Range, new Vector3(-8, 0.5f, -5), new Color(0.75f, 0.55f, 1), camera);
            Station(DeviceKind.Difference, new Vector3(0, 0.5f, -5), new Color(1, 0.65f, 0.3f), camera);
            Station(DeviceKind.Precision, new Vector3(8, 0.5f, -5), new Color(0.95f, 0.45f, 0.65f), camera);
            var relay = Station(DeviceKind.Parity, new Vector3(0, 0.5f, 6), new Color(0.5f, 0.9f, 0.45f), camera, true);
            relay.gameObject.name = "CooperativeRelay";
            var settings = AssetDatabase.LoadAssetAtPath<SandboxSettings>("Assets/_Project/Settings/SandboxSettings.asset");
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<SandboxSettings>();
                AssetDatabase.CreateAsset(settings, "Assets/_Project/Settings/SandboxSettings.asset");
            }
            var bootstrap = new GameObject("LocalSandbox").AddComponent<LocalSandbox>();
            bootstrap.Configure(settings);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("Foundation sandbox ready. Press Play. This is a local development scene.");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        private static Material Material(string name, Color color)
        {
            var path = "Assets/_Project/Art/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name, color = color };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static GameObject Box(string name, Vector3 position, Vector3 scale, Material material)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.position = position;
            box.transform.localScale = scale;
            box.GetComponent<Renderer>().sharedMaterial = material;
            return box;
        }

        private static DeviceStation Station(DeviceKind kind, Vector3 position, Color color, Camera camera, bool relay = false)
        {
            var name = relay ? "RELAY" : kind.ToString().ToUpperInvariant();
            var box = Box(name, position, new Vector3(1.3f, 1, 1.3f), Material(name, color));
            var station = box.AddComponent<DeviceStation>();
            station.device = kind;
            station.missionRelay = relay;
            var label = new GameObject(name + " Label").AddComponent<TextMeshPro>();
            label.transform.position = position + new Vector3(0, 1.7f, 0);
            label.transform.rotation = camera.transform.rotation;
            label.text = name;
            label.fontSize = 3;
            label.alignment = TextAlignmentOptions.Center;
            label.rectTransform.sizeDelta = new Vector2(4, 1);
            return station;
        }
    }
}
