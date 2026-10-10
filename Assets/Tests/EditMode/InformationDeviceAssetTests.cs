using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ZZabmongus.Networking;

namespace ZZabmongus.Tests
{
    public sealed class InformationDeviceAssetTests
    {
        [Test]
        public void GameContainsAnAuthoredParityStationAndLobbyAndWaitingDoNot()
        {
            Assert.That(EditorApplication.isPlaying, Is.False);
            foreach (var name in new[] { "GameScene", "LobbyScene", "WaitingRoomScene" })
            {
                var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/" + name + ".unity");
                try
                {
                    var stations = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<InformationDeviceStation>(true)).ToArray();
                    Assert.That(stations.Length, Is.EqualTo(name == "GameScene" ? 1 : 0));
                    if (stations.Length == 0) continue;
                    var station = stations.Single();
                    Assert.That(station.DeviceId, Is.GreaterThan(0));
                    Assert.That(station.Kind, Is.EqualTo(Core.DeviceKind.Parity));
                    Assert.That(station.GetComponentsInChildren<Collider>(), Is.Not.Empty);
                    Assert.That(station.transform.position.z, Is.InRange(-8,8));
                    var label = station.GetComponentInChildren<TMPro.TMP_Text>();
                    Assert.That(label.font, Is.Not.Null); Assert.That(label.text, Does.Contain("[E]"));
                }
                finally { EditorSceneManager.ClosePreviewScene(scene); }
            }
        }
    }
}
