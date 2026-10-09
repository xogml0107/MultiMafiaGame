using System.Linq;
using NUnit.Framework;
using PurrNet;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ZZabmongus.Networking;

namespace ZZabmongus.Tests
{
    public sealed class SessionSceneAssetTests
    {
        [TestCase("LobbyScene", SessionScreen.Lobby)]
        [TestCase("WaitingRoomScene", SessionScreen.WaitingRoom)]
        [TestCase("GameScene", SessionScreen.Game)]
        public void ScreensHaveSeparateMapsAndUi_AndNoSceneLocalNetworkManager(string name, SessionScreen screen)
        {
            Assert.That(EditorApplication.isPlaying, Is.False);
            var preview = EditorSceneManager.OpenPreviewScene("Assets/Scenes/" + name + ".unity");
            try
            {
                var roots = preview.GetRootGameObjects();
                var entry = roots.SelectMany(r => r.GetComponentsInChildren<SessionSceneEntry>(true)).Single();
                Assert.That(entry.Screen, Is.EqualTo(screen));
                Assert.That(roots.SelectMany(r => r.GetComponentsInChildren<NetworkManager>(true)), Is.Empty);
                Assert.That(roots.SelectMany(r => r.GetComponentsInChildren<NetworkPlayer>(true)), Is.Empty);
                Assert.That(roots.SelectMany(r => r.GetComponentsInChildren<RoomBrowserHud>(true)).Count(), Is.EqualTo(screen == SessionScreen.Lobby ? 1 : 0));
                Assert.That(roots.SelectMany(r => r.GetComponentsInChildren<MultiplayerHud>(true)).Count(), Is.EqualTo(screen == SessionScreen.Lobby ? 0 : 1));
                Assert.That(entry.SessionPrefab != null, Is.EqualTo(screen == SessionScreen.Lobby));
                if (screen != SessionScreen.Lobby)
                {
                    var names = roots.Select(r => r.name).ToArray();
                    Assert.That(names, Does.Contain(screen == SessionScreen.WaitingRoom ? "WaitingRoomMap" : "GameMap"));
                    Assert.That(names, Does.Not.Contain(screen == SessionScreen.WaitingRoom ? "GameMap" : "WaitingRoomMap"));
                    for (var slot = 0; slot < 12; slot++) Assert.That(entry.SpawnPosition(slot).y, Is.Zero);
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }

        [Test]
        public void SessionOwnsTransportAndRoomPrefab_AndAllThreeScenesAreIncludedInOrder()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/NetworkSession.prefab");
            Assert.That(prefab, Is.Not.Null);
            var manager = prefab.GetComponent<NetworkManager>();
            var connection = prefab.GetComponent<MultiplayerConnection>();
            Assert.That(new SerializedObject(manager).FindProperty("_dontDestroyOnLoad").boolValue, Is.True);
            Assert.That(manager.startServerFlags, Is.EqualTo(StartFlags.None));
            Assert.That(manager.startClientFlags, Is.EqualTo(StartFlags.None));
            Assert.That(connection.Network, Is.EqualTo(manager));
            Assert.That(prefab.GetComponent<SteamRoomDirectory>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<LobbyPlayerSpawner>(), Is.Not.Null);
            Assert.That(prefab.GetComponentsInChildren<NetworkLobby>(), Is.Empty);
            var flow = prefab.GetComponent<SessionSceneFlow>();
            var state = new SerializedObject(flow).FindProperty("roomStatePrefab").objectReferenceValue as GameObject;
            Assert.That(state.GetComponent<NetworkLobby>(), Is.Not.Null);
            var provider = AssetDatabase.LoadAssetAtPath<NetworkPrefabs>("Assets/Settings/SessionNetworkPrefabs.asset");
            Assert.That(provider.prefabs.Any(p => p.prefab == state), Is.True);
            var rules = AssetDatabase.LoadAssetAtPath<NetworkRules>("Assets/Settings/SessionNetworkRules.asset");
            Assert.That(rules.ShouldAlwaysIncludeDontDestroyOnLoadScene(), Is.True);
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Take(3).Select(s => s.path).ToArray();
            Assert.That(scenes, Is.EqualTo(new[] { "Assets/Scenes/LobbyScene.unity", "Assets/Scenes/WaitingRoomScene.unity", "Assets/Scenes/GameScene.unity" }));
        }
    }
}
