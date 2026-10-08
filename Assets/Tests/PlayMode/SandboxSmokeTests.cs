#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using ZZabmongus.Core;
using ZZabmongus.Runtime;

namespace ZZabmongus.Tests
{
    public sealed class SandboxSmokeTests
    {
        [UnityTest]
        public IEnumerator SandboxStarts_SwitchesPrivateViews_AndShowsResults()
        {
            var operation = EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/_Project/Scenes/FoundationSandbox.unity", new LoadSceneParameters(LoadSceneMode.Single));
            while (!operation.isDone) yield return null;
            yield return null;
            yield return null;
            var sandbox = Object.FindFirstObjectByType<LocalSandbox>();
            Assert.That(sandbox, Is.Not.Null);
            Assert.That(sandbox.Session.PlayerIds.Count, Is.EqualTo(8));
            Assert.That(Object.FindObjectsByType<CharacterController>(FindObjectsSortMode.None).Length, Is.EqualTo(8));
            Assert.That(Object.FindObjectsByType<DeviceStation>(FindObjectsSortMode.None).Length, Is.EqualTo(6));
            Assert.That(Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(Object.FindFirstObjectByType<UnityEngine.UI.GraphicRaycaster>(), Is.Not.Null);
            var first = sandbox.SelectedId;
            GameObject.Find("Next player [Tab]").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Assert.That(sandbox.SelectedId, Is.Not.EqualTo(first));
            yield return new WaitForSecondsRealtime(0.2f);
            var notebook = GameObject.Find("PrivateNotebook").GetComponentsInChildren<TMP_Text>();
            Assert.That(notebook.Any(t => t.text.Contains("Your number: ???")), Is.True);
            Assert.That(notebook.All(t => t.font != null), Is.True);
            sandbox.Session.Tick(1200 - sandbox.Session.ElapsedSeconds);
            Assert.That(sandbox.Session.Phase, Is.EqualTo(MatchPhase.Submission));
            sandbox.Submit();
            Assert.That(sandbox.Session.GetPlayerView(sandbox.SelectedId).HasSubmitted, Is.True);
            sandbox.Session.Tick(30);
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.That(GameObject.Find("Results"), Is.Not.Null);
            Assert.That(GameObject.Find("Results").GetComponentInChildren<TMP_Text>().text, Does.Contain("NUMBER REVEAL"));
            sandbox.Restart();
            yield return null;
            Assert.That(sandbox.Session.Phase, Is.EqualTo(MatchPhase.Playing));
            Assert.That(Object.FindObjectsByType<CharacterController>(FindObjectsSortMode.None).Length, Is.EqualTo(8));
            LogAssert.NoUnexpectedReceived();
        }
    }
}
#endif
