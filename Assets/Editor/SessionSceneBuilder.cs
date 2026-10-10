using System;
using System.IO;
using System.Linq;
using PurrNet;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ZZabmongus.Networking;

namespace ZZabmongus.Editor
{
    public static class SessionSceneBuilder
    {
        public const string LobbyPath = "Assets/Scenes/LobbyScene.unity";
        public const string WaitingPath = "Assets/Scenes/WaitingRoomScene.unity";
        public const string GamePath = "Assets/Scenes/GameScene.unity";
        public const string SessionPrefabPath = "Assets/Prefabs/NetworkSession.prefab";
        public const string StatePrefabPath = "Assets/Prefabs/NetworkRoomState.prefab";
        public const string RulesPath = "Assets/Settings/SessionNetworkRules.asset";
        public const string PrefabsPath = "Assets/Settings/SessionNetworkPrefabs.asset";
        public static readonly string[] ScenePaths = { LobbyPath, WaitingPath, GamePath };

        [MenuItem("ZZabmongus/Create or Open Lobby")]
        public static void CreateOrOpen()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
            for (var i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save modified scenes first.");
            if (!File.Exists(MultiplayerSceneBuilder.ScenePath)) throw new InvalidOperationException("The existing multiplayer foundation scene is required.");
            EnsureSessionPrefab();
            var session = AssetDatabase.LoadAssetAtPath<GameObject>(SessionPrefabPath);
            if (!File.Exists(LobbyPath)) CreateScene(LobbyPath, SessionScreen.Lobby, session);
            if (!File.Exists(WaitingPath)) CreateScene(WaitingPath, SessionScreen.WaitingRoom, null);
            if (!File.Exists(GamePath)) CreateScene(GamePath, SessionScreen.Game, null);
            InformationDeviceSetup.ConfigureGameScene();
            var retained = EditorBuildSettings.scenes.Where(s => !ScenePaths.Contains(s.path));
            EditorBuildSettings.scenes = ScenePaths.Select(path => new EditorBuildSettingsScene(path, true)).Concat(retained).ToArray();
            EditorSceneManager.OpenScene(LobbyPath);
            AssetDatabase.SaveAssets();
            Debug.Log("Session scenes ready: LobbyScene -> WaitingRoomScene -> GameScene. No Play mode was entered.");
        }

        private static void EnsureSessionPrefab()
        {
            if (File.Exists(SessionPrefabPath)) return;
            EditorSceneManager.OpenScene(MultiplayerSceneBuilder.ScenePath);
            var source = UnityEngine.Object.FindFirstObjectByType<MultiplayerConnection>();
            if (!source) throw new InvalidOperationException("The original connection object is missing.");
            var stateObject = new GameObject("NetworkRoomState", typeof(NetworkLobby));
            var statePrefab = PrefabUtility.SaveAsPrefabAsset(stateObject, StatePrefabPath);
            UnityEngine.Object.DestroyImmediate(stateObject);
            var rules = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<NetworkRules>("Assets/Settings/MultiplayerRules.asset"));
            rules.name = "SessionNetworkRules";
            Set(rules, "_defaultSceneRules.alwaysIncludeDontDestroyOnLoadScene", true);
            Set(rules, "_defaultSpawnRules.includeInstantiatedSceneObjects", true);
            AssetDatabase.CreateAsset(rules, RulesPath);
            var prefabs = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<NetworkPrefabs>("Assets/Settings/MultiplayerPrefabs.asset"));
            prefabs.name = "SessionNetworkPrefabs";
            prefabs.prefabs.Add(new NetworkPrefabs.UserPrefabData { prefab = statePrefab, guid = AssetDatabase.AssetPathToGUID(StatePrefabPath), pooled = false });
            AssetDatabase.CreateAsset(prefabs, PrefabsPath); prefabs.Refresh();
            var root = UnityEngine.Object.Instantiate(source.gameObject); root.name = "NetworkSession";
            var manager = root.GetComponent<NetworkManager>();
            manager.startServerFlags = manager.startClientFlags = StartFlags.None;
            manager.SetNetworkRules(rules); manager.SetPrefabProvider(prefabs);
            Set(manager, "_networkPrefabs", prefabs); Set(manager, "_dontDestroyOnLoad", true); Set(manager, "_stopPlayingOnDisconnect", false);
            Set(root.GetComponent<LobbyPlayerSpawner>(), "lobby", null);
            var flow = root.AddComponent<SessionSceneFlow>();
            Set(flow, "connection", root.GetComponent<MultiplayerConnection>()); Set(flow, "roomStatePrefab", statePrefab);
            PrefabUtility.SaveAsPrefabAsset(root, SessionPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            // The source scene is only read; return to an empty scene without saving it.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        private static void CreateScene(string path, SessionScreen screen, GameObject sessionPrefab)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var entry = new GameObject("SceneEntry").AddComponent<SessionSceneEntry>();
            Set(entry, "screen", (int)screen); Set(entry, "sessionPrefab", sessionPrefab);
            var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            camera.tag = "MainCamera"; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.025f,0.035f,0.065f);
            if (screen == SessionScreen.Lobby)
            {
                new GameObject("LobbyUI", typeof(RoomBrowserHud));
            }
            else
            {
                camera.gameObject.AddComponent<MultiplayerCamera>();
                camera.transform.position = new Vector3(0,22,-10); camera.transform.LookAt(Vector3.zero);
                camera.orthographic = true; camera.orthographicSize = screen == SessionScreen.WaitingRoom ? 8 : 10;
                camera.rect = new Rect(0.30f,0,0.70f,1);
                var light = new GameObject("Key Light", typeof(Light)).GetComponent<Light>();
                light.type = LightType.Directional; light.intensity = 1.4f; light.transform.rotation = Quaternion.Euler(55,-25,0);
                RenderSettings.ambientLight = new Color(0.5f,0.56f,0.64f);
                var map = new GameObject(screen == SessionScreen.WaitingRoom ? "WaitingRoomMap" : "GameMap");
                var width = screen == SessionScreen.WaitingRoom ? 14f : 22f;
                var depth = screen == SessionScreen.WaitingRoom ? 12f : 18f;
                Box(map.transform, "Floor", new Vector3(0,-0.25f,0), new Vector3(width,0.5f,depth), "MultiplayerFloor");
                Box(map.transform, "NorthWall", new Vector3(0,0.75f,depth/2), new Vector3(width,1.5f,0.5f), "MultiplayerWalls");
                Box(map.transform, "SouthWall", new Vector3(0,0.75f,-depth/2), new Vector3(width,1.5f,0.5f), "MultiplayerWalls");
                Box(map.transform, "WestWall", new Vector3(-width/2,0.75f,0), new Vector3(0.5f,1.5f,depth), "MultiplayerWalls");
                Box(map.transform, "EastWall", new Vector3(width/2,0.75f,0), new Vector3(0.5f,1.5f,depth), "MultiplayerWalls");
                if (screen == SessionScreen.Game)
                {
                    Box(map.transform, "Cargo A", new Vector3(-6,0.6f,4), new Vector3(2,1.2f,2), "MultiplayerWalls");
                    Box(map.transform, "Cargo B", new Vector3(6,0.6f,-4), new Vector3(2,1.2f,2), "MultiplayerWalls");
                }
                new GameObject(screen == SessionScreen.WaitingRoom ? "WaitingRoomUI" : "GameUI", typeof(MultiplayerHud));
                if (screen == SessionScreen.Game) InformationDeviceSetup.ConfigureScene(scene);
            }
            if (!EditorSceneManager.SaveScene(scene, path)) throw new InvalidOperationException("Could not save " + path);
        }

        private static void Box(Transform parent, string name, Vector3 position, Vector3 scale, string material)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube); box.name = name; box.transform.SetParent(parent, false);
            box.transform.position = position; box.transform.localScale = scale;
            box.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/" + material + ".mat");
        }

        private static void Set(UnityEngine.Object target, string field, object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field) ?? throw new InvalidOperationException("Missing field " + field);
            if (value is bool flag) property.boolValue = flag;
            else if (value is int number) property.intValue = number;
            else property.objectReferenceValue = (UnityEngine.Object)value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
