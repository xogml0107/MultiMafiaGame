#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using PurrNet;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using ZZabmongus.Networking;

namespace ZZabmongus.Tests
{
    public sealed class MultiplayerSmokeTests
    {
        [UnityTest]
        public IEnumerator HostSpawnsOwnedAvatar_Readies_Moves_Leaves_AndHostsAgain()
        {
            var load = EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/MultiplayerBase.unity", new LoadSceneParameters(LoadSceneMode.Single));
            while (!load.isDone) yield return null;
            yield return null;
            var browser = Object.FindFirstObjectByType<RoomBrowserHud>();
            if (browser) browser.enabled = false;
            yield return new WaitForSecondsRealtime(0.2f);
            var connection = Object.FindFirstObjectByType<MultiplayerConnection>();
            var lobby = Object.FindFirstObjectByType<NetworkLobby>();
            GameObject.Find("Port").GetComponent<TMP_InputField>().text = "15101";
            Assert.That(connection.Busy, Is.False, "Scene must not connect automatically");
            Assert.That(Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None).All(t => t.font), Is.True);
            Canvas.ForceUpdateCanvases();
            var panel = (RectTransform)GameObject.Find("RoomPanel").transform;
            foreach (var button in panel.GetComponentsInChildren<UnityEngine.UI.Button>())
            {
                var rect = (RectTransform)button.transform;
                Assert.That(rect.rect.height, Is.GreaterThan(0));
                var corners = new Vector3[4];
                rect.GetWorldCorners(corners);
                Assert.That(panel.InverseTransformPoint(corners[0]).y, Is.GreaterThanOrEqualTo(panel.rect.yMin));
                Assert.That(panel.InverseTransformPoint(corners[1]).y, Is.LessThanOrEqualTo(panel.rect.yMax));
            }
            GameObject.Find("CREATE ROOM").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            // Exercise the actual UI connection path, rather than starting PurrNet directly.
            yield return WaitFor(() => NetworkPlayer.Local && connection.Connected);
            Assert.That(lobby.Players.Length, Is.EqualTo(1));
            var player = NetworkPlayer.Local;
            Assert.That(player.GetComponent<NetworkTransform>().ownerAuth, Is.False);
            Assert.That(player.GetComponent<CharacterController>().enabled, Is.True);
            Assert.That(lobby.CanStart, Is.False);
            lobby.StartSession();
            Assert.That(lobby.InSession, Is.False, "One player cannot start a four-player session");
            GameObject.Find("READY").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return WaitFor(() => player.Ready);
            var initial = player.transform.position;
            player.ProbeInput = new Vector2(100, 0);
            yield return new WaitForSecondsRealtime(0.8f);
            var distance = Vector3.Distance(initial, player.transform.position);
            Assert.That(distance, Is.InRange(1.5f, 4.5f), "Server must clamp oversized input");
            player.ProbeInput = Vector2.zero;
            connection.Leave();
            yield return WaitFor(() => !connection.Busy && lobby.Players.Length == 0);
            connection.Host();
            yield return WaitFor(() => NetworkPlayer.Local && connection.Connected);
            Assert.That(lobby.Players.Length, Is.EqualTo(1));
            Assert.That(NetworkPlayer.Local.Ready, Is.False, "New room must reset readiness");
            connection.Leave();
            yield return WaitFor(() => !connection.Busy && lobby.Players.Length == 0);
            LogAssert.NoUnexpectedReceived();
        }

        private static IEnumerator WaitFor(System.Func<bool> predicate)
        {
            var deadline = Time.unscaledTime + 10;
            while (!predicate() && Time.unscaledTime < deadline) yield return null;
            Assert.That(predicate(), Is.True, "Network operation timed out");
        }
    }
}
#endif
