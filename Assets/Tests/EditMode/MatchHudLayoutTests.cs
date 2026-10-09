#if UNITY_EDITOR
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using ZZabmongus.Networking;

namespace ZZabmongus.Tests
{
    public sealed class MatchHudLayoutTests
    {
        [Test]
        public void MatchAndResultPanelsFit_AndAnswerDropdownsHaveValidTemplates_WithoutPlayMode()
        {
            Assert.That(EditorApplication.isPlaying, Is.False);
            var preview = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("MatchHudPreview", typeof(RectTransform), typeof(Canvas), typeof(MultiplayerHud));
            SceneManager.MoveGameObjectToScene(root, preview);
            try
            {
                root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                ((RectTransform)root.transform).sizeDelta = new Vector2(1600,1000);
                typeof(MultiplayerHud).GetMethod("BuildMatchHud", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(root.GetComponent<MultiplayerHud>(), new object[] { root.transform });
                var match = (RectTransform)root.transform.Find("MatchPanel");
                var answers = match.Find("FinalAnswerControls");
                var result = (RectTransform)root.transform.Find("MatchResultsPanel");
                match.gameObject.SetActive(true); answers.gameObject.SetActive(true); result.gameObject.SetActive(true);
                Canvas.ForceUpdateCanvases();
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(match);
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(result);
                var corners = new Vector3[4];
                foreach (var button in match.GetComponentsInChildren<UnityEngine.UI.Button>())
                {
                    var rect = (RectTransform)button.transform; rect.GetWorldCorners(corners);
                    Assert.That(rect.rect.height, Is.GreaterThan(0), button.name);
                    Assert.That(match.InverseTransformPoint(corners[0]).y, Is.GreaterThanOrEqualTo(match.rect.yMin), button.name);
                    Assert.That(match.InverseTransformPoint(corners[1]).y, Is.LessThanOrEqualTo(match.rect.yMax), button.name);
                }
                foreach (var dropdown in answers.GetComponentsInChildren<TMP_Dropdown>())
                {
                    Assert.That(dropdown.captionText, Is.Not.Null);
                    Assert.That(dropdown.template.gameObject.activeSelf, Is.False);
                    var scroll = dropdown.template.GetComponent<UnityEngine.UI.ScrollRect>();
                    Assert.That(scroll.viewport.GetComponent<UnityEngine.UI.Mask>(), Is.Not.Null);
                    Assert.That(scroll.content.GetComponentInChildren<UnityEngine.UI.Toggle>(true), Is.Not.Null);
                    Assert.That(dropdown.itemText.transform.IsChildOf(scroll.content), Is.True);
                }
                var text = result.GetComponentsInChildren<TMP_Text>()[1]; text.rectTransform.GetWorldCorners(corners);
                Assert.That(result.InverseTransformPoint(corners[0]).y, Is.GreaterThanOrEqualTo(result.rect.yMin));
                Assert.That(text.richText, Is.False);
                LogAssert.NoUnexpectedReceived();
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }
    }
}
#endif
