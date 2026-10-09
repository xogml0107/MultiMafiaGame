using System;
using System.IO;
using PurrNet;
using PurrNet.Steam;
using PurrNet.Transports;
using TMPro;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ZZabmongus.Networking;

namespace ZZabmongus.Editor
{
    public static class MultiplayerSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/MultiplayerBase.unity";
        private const string PrefabPath = "Assets/Prefabs/NetworkPlayer.prefab";

        [MenuItem("ZZabmongus/Create or Open Multiplayer Base")]
        public static void CreateOrOpen() => SessionSceneBuilder.CreateOrOpen();

        [MenuItem("ZZabmongus/Development/Create or Open Legacy Multiplayer Base")]
        public static void CreateOrOpenLegacy()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode first.");
            for (var i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save modified scenes first.");
            if (!TMP_Settings.defaultFontAsset) throw new InvalidOperationException("Import TMP Essential Resources first.");
            if (File.Exists(ScenePath)) { EditorSceneManager.OpenScene(ScenePath); CountryLobbySetup.ConfigureOpenScene(); return; }
            Directory.CreateDirectory("Assets/Prefabs");
            Directory.CreateDirectory("Assets/Settings");
            Directory.CreateDirectory("Assets/Art/Materials");
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(MultiplayerCamera)).GetComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0, 22, -10);
            camera.transform.LookAt(Vector3.zero);
            camera.orthographic = true; camera.orthographicSize = 10;
            camera.rect = new Rect(0.30f, 0, 0.70f, 1);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.05f, 0.08f);
            var light = new GameObject("Key Light", typeof(Light)).GetComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.4f;
            light.transform.rotation = Quaternion.Euler(55, -25, 0);
            RenderSettings.ambientLight = new Color(0.5f, 0.56f, 0.64f);
            var floor = Material("MultiplayerFloor", new Color(0.12f, 0.18f, 0.23f));
            var walls = Material("MultiplayerWalls", new Color(0.22f, 0.33f, 0.41f));
            Box("Floor", new Vector3(0,-0.25f,0), new Vector3(22,0.5f,18), floor);
            Box("NorthWall", new Vector3(0,0.75f,9), new Vector3(22,1.5f,0.5f), walls);
            Box("SouthWall", new Vector3(0,0.75f,-9), new Vector3(22,1.5f,0.5f), walls);
            Box("WestWall", new Vector3(-11,0.75f,0), new Vector3(0.5f,1.5f,18), walls);
            Box("EastWall", new Vector3(11,0.75f,0), new Vector3(0.5f,1.5f,18), walls);
            Box("Cargo A", new Vector3(-6,0.6f,4), new Vector3(2,1.2f,2), walls);
            Box("Cargo B", new Vector3(6,0.6f,-4), new Vector3(2,1.2f,2), walls);
            var playerPrefab = CreatePlayerPrefab();
            var prefabs = ScriptableObject.CreateInstance<NetworkPrefabs>();
            prefabs.autoGenerate = false; prefabs.searchAllIfNoFolder = false;
            prefabs.prefabs.Add(new NetworkPrefabs.UserPrefabData { prefab = playerPrefab,
                guid = AssetDatabase.AssetPathToGUID(PrefabPath), pooled = false });
            AssetDatabase.CreateAsset(prefabs, "Assets/Settings/MultiplayerPrefabs.asset");
            prefabs.Refresh();
            var rules = ScriptableObject.CreateInstance<NetworkRules>();
            Set(rules, "_hostMigrationRules.migrateAsHost", false);
            Set(rules, "_defaultSpawnRules.despawnAuth", (int)ActionAuth.Server);
            Set(rules, "_defaultSpawnRules.defaultOwner", (int)DefaultOwner.None);
            Set(rules, "_defaultSpawnRules.autoSpawnOnInstantiate", false);
            Set(rules, "_defaultSpawnRules.autoSpawnOnInstantiateAsync", false);
            Set(rules, "_defaultOwnershipRules.transferAuth", (int)ActionAuth.Server);
            Set(rules, "_defaultOwnershipRules.removeAuth", (int)ActionAuth.Server);
            Set(rules, "_defaultTransformRules.changeParentAuth", (int)ActionAuth.Server);
            Set(rules, "_defaultSceneRules.removePlayerFromSceneOnDisconnect", true);
            Set(rules, "_defaultSceneRules.sceneCleanupModeOnDisconnect", (int)SceneCleanupMode.Off);
            AssetDatabase.CreateAsset(rules, "Assets/Settings/MultiplayerRules.asset");
            var networkRoot = new GameObject("NetworkSession");
            var network = networkRoot.AddComponent<NetworkManager>();
            var udp = networkRoot.AddComponent<UDPTransport>();
            var steam = networkRoot.AddComponent<SteamTransport>();
            var connection = networkRoot.AddComponent<MultiplayerConnection>();
            var spawner = networkRoot.AddComponent<LobbyPlayerSpawner>();
            network.startServerFlags = StartFlags.None; network.startClientFlags = StartFlags.None;
            network.transport = udp;
            network.SetNetworkRules(rules);
            network.SetPrefabProvider(prefabs);
            Set(network, "_networkPrefabs", prefabs);
            Set(network, "_dontDestroyOnLoad", false);
            Set(network, "_tickRate", 30);
            udp.serverPort = 5000; udp.maxConnections = NetworkLobby.MaxPlayers;
            var lobby = new GameObject("RoomState").AddComponent<NetworkLobby>();
            Set(connection, "network", network); Set(connection, "udp", udp); Set(connection, "steam", steam);
            Set(spawner, "playerPrefab", playerPrefab); Set(spawner, "lobby", lobby);
            var hud = new GameObject("RoomUI").AddComponent<MultiplayerHud>();
            Set(hud, "connection", connection); Set(hud, "lobby", lobby);
            new GameObject("DevelopmentNetworkProbe").AddComponent<MultiplayerProbe>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            CountryLobbySetup.ConfigureOpenScene();
            Debug.Log("MultiplayerBase ready. Create a room in one instance and join from another.");
        }

        private static GameObject CreatePlayerPrefab()
        {
            if (File.Exists(PrefabPath)) return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var root = new GameObject("NetworkPlayer");
            var controller = root.AddComponent<CharacterController>();
            controller.height = 1.4f; controller.radius = 0.4f;
            controller.center = new Vector3(0,0.7f,0); controller.stepOffset = 0.1f; controller.minMoveDistance = 0;
            var networkTransform = root.AddComponent<NetworkTransform>();
            Set(networkTransform, "_ownerAuth", false);
            Set(networkTransform, "_syncPosition", (int)SyncMode.World);
            Set(networkTransform, "_syncRotation", (int)SyncMode.No);
            Set(networkTransform, "_syncScale", false); Set(networkTransform, "_syncParent", false);
            Set(networkTransform, "_interpolateSettings", (int)TransformSyncMode.Position);
            var player = root.AddComponent<NetworkPlayer>();
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body"; body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0,0.7f,0); body.transform.localScale = new Vector3(0.8f,0.7f,0.8f);
            UnityEngine.Object.DestroyImmediate(body.GetComponent<Collider>());
            body.GetComponent<Renderer>().sharedMaterial = Material("PlayerBody", Color.white);
            var visor = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visor.name = "Visor"; visor.transform.SetParent(root.transform, false);
            visor.transform.localPosition = new Vector3(0,1,-0.34f); visor.transform.localScale = new Vector3(0.6f,0.32f,0.24f);
            UnityEngine.Object.DestroyImmediate(visor.GetComponent<Collider>());
            visor.GetComponent<Renderer>().sharedMaterial = Material("PlayerVisor", new Color(0.6f,0.9f,1));
            var label = new GameObject("Name").AddComponent<TextMeshPro>();
            label.transform.SetParent(root.transform, false); label.transform.localPosition = new Vector3(0,1.85f,0);
            label.font = TMP_Settings.defaultFontAsset; label.fontSize = 2.2f;
            label.alignment = TextAlignmentOptions.Center; label.rectTransform.sizeDelta = new Vector2(4,0.7f);
            Set(player, "body", body.GetComponent<Renderer>()); Set(player, "nameLabel", label);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        [MenuItem("ZZabmongus/Build Multiplayer Windows Test Player")]
        public static void BuildTestPlayer()
        {
            CreateOrOpen();
            Directory.CreateDirectory("Build/MultiplayerBase");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = SessionSceneBuilder.ScenePaths,
                locationPathName = "Build/MultiplayerBase/ZZabmongus.exe", target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development });
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Multiplayer test player build failed: " + report.summary.result);
            Debug.Log("Multiplayer Windows test player built.");
        }

        private static void Set(UnityEngine.Object target, string field, object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field) ?? throw new InvalidOperationException("Missing serialized field: " + field);
            if (value is bool flag) property.boolValue = flag;
            else if (value is int number) property.intValue = number;
            else property.objectReferenceValue = (UnityEngine.Object)value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        private static Material Material(string name, Color color)
        {
            var path = "Assets/Art/Materials/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing) return existing;
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name, color = color };
            AssetDatabase.CreateAsset(material, path); return material;
        }
        private static void Box(string name, Vector3 position, Vector3 scale, Material material)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube); box.name = name;
            box.transform.position = position; box.transform.localScale = scale;
            box.GetComponent<Renderer>().sharedMaterial = material;
        }
    }
}
