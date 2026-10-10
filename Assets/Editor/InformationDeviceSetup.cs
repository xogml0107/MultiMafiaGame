using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ZZabmongus.Networking;

namespace ZZabmongus.Editor
{
    public static class InformationDeviceSetup
    {
        public const string PrefabPath = "Assets/Prefabs/ParityInformationDevice.prefab";

        [MenuItem("ZZabmongus/Configure Game Information Device")]
        public static void ConfigureGameScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
            for (var i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save modified scenes first.");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scene = SceneManager.GetSceneByPath(SessionSceneBuilder.GamePath);
                if (!scene.IsValid() || !scene.isLoaded) scene = EditorSceneManager.OpenScene(SessionSceneBuilder.GamePath, OpenSceneMode.Additive);
                ConfigureScene(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save the game scene.");
                AssetDatabase.SaveAssets();
                Debug.Log("GameScene information device configured. E uses parity; no Play mode entered.");
            }
            finally
            {
                if (setup.Any(s => s.isLoaded && s.isActive)) EditorSceneManager.RestoreSceneManagerSetup(setup);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        public static void ConfigureScene(Scene scene)
        {
            var stations = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<InformationDeviceStation>(true)).ToArray();
            if (stations.Length > 0) return; // Preserve authored stations and their positions.
            var root = new GameObject("InformationDevices"); SceneManager.MoveGameObjectToScene(root, scene);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(EnsurePrefab(), scene);
            instance.name = "ParityInformationDevice"; instance.transform.SetParent(root.transform, false);
            instance.transform.position = new Vector3(0,0,5);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static GameObject EnsurePrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing) return existing;
            var root = new GameObject("ParityInformationDevice", typeof(InformationDeviceStation));
            try
            {
                var materialPath = "Assets/Art/Materials/InformationDevice.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (!material)
                {
                    material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "InformationDevice", color = new Color(0.12f,0.65f,0.68f) };
                    AssetDatabase.CreateAsset(material, materialPath);
                }
                var pedestal = GameObject.CreatePrimitive(PrimitiveType.Cube); pedestal.name = "Console";
                pedestal.transform.SetParent(root.transform, false); pedestal.transform.localPosition = new Vector3(0,0.6f,0);
                pedestal.transform.localScale = new Vector3(1.4f,1.2f,1.4f); pedestal.GetComponent<Renderer>().sharedMaterial = material;
                var label = new GameObject("DeviceLabel", typeof(TextMeshPro)).GetComponent<TextMeshPro>();
                label.transform.SetParent(root.transform, false); label.transform.localPosition = new Vector3(0,2.1f,0);
                label.transform.rotation = Quaternion.Euler(65,0,0); label.rectTransform.sizeDelta = new Vector2(5,1.1f);
                label.font = Resources.Load<TMP_FontAsset>("UI/LobbyFont") ?? TMP_Settings.defaultFontAsset;
                label.text = "홀짝 검사\n[E] 1 포인트"; label.fontSize = 2.3f; label.alignment = TextAlignmentOptions.Center; label.color = Color.white;
                return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
