using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using ZZabmongus.Networking;

namespace ZZabmongus.Editor
{
    public static class CountryLobbySetup
    {
        [MenuItem("ZZabmongus/Configure Country Room Lobby")]
        public static void Configure()
        {
            SessionSceneBuilder.CreateOrOpen();
        }

        public static void ConfigureOpenScene()
        {
            var font = CreateFont();
            var connection = UnityEngine.Object.FindFirstObjectByType<MultiplayerConnection>();
            var hud = UnityEngine.Object.FindFirstObjectByType<MultiplayerHud>();
            if (!connection || !hud) throw new InvalidOperationException("Open MultiplayerBase before configuring the country lobby.");
            var root = connection.gameObject;
            var authentication = root.GetComponent<RoomAuthenticator>() ?? root.AddComponent<RoomAuthenticator>();
            if (!root.GetComponent<SteamRoomDirectory>()) root.AddComponent<SteamRoomDirectory>();
            if (!hud.GetComponent<RoomBrowserHud>()) hud.gameObject.AddComponent<RoomBrowserHud>();
            var manager = root.GetComponent<PurrNet.NetworkManager>();
            var serialized = new SerializedObject(manager);
            serialized.FindProperty("_authenticator").objectReferenceValue = authentication;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var prefab = PrefabUtility.LoadPrefabContents("Assets/Prefabs/NetworkPlayer.prefab");
            try
            {
                foreach (var label in prefab.GetComponentsInChildren<TMP_Text>()) label.font = font;
                PrefabUtility.SaveAsPrefabAsset(prefab,"Assets/Prefabs/NetworkPlayer.prefab");
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            EditorSceneManager.MarkSceneDirty(connection.gameObject.scene);
            EditorSceneManager.SaveScene(connection.gameObject.scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Country lobby configured: Steam discovery + host admission + Korean UI.");
        }

        private static TMP_FontAsset CreateFont()
        {
            const string path = "Assets/Resources/UI/LobbyFont.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (existing) return existing;
            Directory.CreateDirectory("Assets/Resources/UI"); AssetDatabase.Refresh();
            var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/Fonts/NotoSansKR.ttf");
            if (!source) throw new InvalidOperationException("Import NotoSansKR.ttf first.");
            var font = TMP_FontAsset.CreateFontAsset(source,44,4,GlyphRenderMode.SDFAA,1024,1024,AtlasPopulationMode.Dynamic,true);
            font.name = "LobbyFont";
            var code = string.Concat(new[] { "Assets/Runtime/Core/Rooms/RoomModels.cs", "Assets/Runtime/Networking/RoomBrowserHud.cs",
                "Assets/Runtime/Networking/SteamRoomDirectory.cs", "Assets/Runtime/Networking/MultiplayerConnection.cs",
                "Assets/Runtime/Networking/MultiplayerHud.cs" }.Select(File.ReadAllText));
            var characters = new string(code.Concat(Enumerable.Range(32,95).Select(i => (char)i)).Where(c => !char.IsControl(c) && !char.IsSurrogate(c)).Distinct().ToArray());
            font.TryAddCharacters(characters,out var missing);
            if (missing.Any(c => c >= 0xAC00 && c <= 0xD7A3)) throw new InvalidOperationException("Korean UI glyphs could not be baked.");
            font.atlasPopulationMode = AtlasPopulationMode.Static;
            SaveFont(font,path,false);
            var fallback = TMP_FontAsset.CreateFontAsset(source,44,4,GlyphRenderMode.SDFAA,1024,1024,AtlasPopulationMode.Dynamic,true);
            fallback.name = "LobbyFontDynamic";
            SaveFont(fallback,"Assets/Resources/UI/LobbyFontDynamic.asset",true);
            font.fallbackFontAssetTable.Add(fallback);
            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssets();
            return font;
        }
        private static void SaveFont(TMP_FontAsset font,string path,bool clearOnBuild)
        {
            var settings = new SerializedObject(font); settings.FindProperty("m_ClearDynamicDataOnBuild").boolValue = clearOnBuild;
            settings.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(font,path);
            AssetDatabase.AddObjectToAsset(font.material,font);
            foreach (var atlas in font.atlasTextures) if (atlas) AssetDatabase.AddObjectToAsset(atlas,font);
            EditorUtility.SetDirty(font);
        }
    }
}
