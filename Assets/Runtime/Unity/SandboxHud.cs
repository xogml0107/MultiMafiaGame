using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using ZZabmongus.Core;

namespace ZZabmongus.Runtime
{
    /// <summary>Explicit developer UI. Never include this harness in a multiplayer gameplay scene.</summary>
    public sealed class SandboxHud : MonoBehaviour
    {
        private LocalSandbox sandbox;
        private TMP_Text status, info, footer, result, guessLabel, suspectLabel, speedLabel;
        private TMP_Text[] candidateLabels;
        private UnityEngine.UI.Button submit;
        private GameObject resultPanel, canvasObject;
        private float nextRefresh;
        private bool showResults = true;

        public void Initialize(LocalSandbox value)
        {
            sandbox = value;
            canvasObject = new GameObject("SandboxHUD", typeof(RectTransform), typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = 0.5f;
            if (EventSystem.current == null)
            {
                var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform, false);
            }
            var left = Panel("SessionPanel", canvas.transform, new Vector2(0.015f, 0.23f), new Vector2(0.23f, 0.98f));
            var layout = left.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 14, 14);
            layout.spacing = 8;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            Text(left, "ZZABMONGUS", 27, 40);
            Text(left, "LOCAL DEVELOPMENT SANDBOX", 13, 24).color = new Color(0.4f, 0.85f, 0.95f);
            status = Text(left, "", 17, 140);
            Button(left, "Next player [Tab]", sandbox.NextPlayer);
            speedLabel = Button(left, "Speed: 1x", sandbox.ToggleSpeed).GetComponentInChildren<TMP_Text>();
            Button(left, "+60 sec (developer)", sandbox.AdvanceMinute);
            guessLabel = Button(left, "Guess: 1", sandbox.CycleGuess).GetComponentInChildren<TMP_Text>();
            suspectLabel = Button(left, "Suspect: abstain", sandbox.CycleSuspect).GetComponentInChildren<TMP_Text>();
            submit = Button(left, "LOCK FINAL ANSWER", sandbox.Submit);
            Button(left, "Restart same seed", () => { sandbox.Restart(); showResults = true; });
            Button(left, "Show / hide results", () => showResults = !showResults);

            var right = Panel("PrivateNotebook", canvas.transform, new Vector2(0.755f, 0.23f), new Vector2(0.985f, 0.98f));
            var rightLayout = right.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            rightLayout.padding = new RectOffset(16, 16, 14, 14);
            rightLayout.spacing = 8;
            rightLayout.childControlWidth = true;
            rightLayout.childControlHeight = true;
            rightLayout.childForceExpandHeight = false;
            Text(right, "PRIVATE NOTEBOOK", 22, 40);
            info = Text(right, "", 17, 430);
            Text(right, "Candidate notes (tap to mark)", 15, 25);
            var grid = new GameObject("Candidates", typeof(RectTransform), typeof(UnityEngine.UI.GridLayoutGroup), typeof(UnityEngine.UI.LayoutElement));
            grid.transform.SetParent(right, false);
            grid.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 90;
            var gridLayout = grid.GetComponent<UnityEngine.UI.GridLayoutGroup>();
            gridLayout.cellSize = new Vector2(43, 35);
            gridLayout.spacing = new Vector2(6, 6);
            gridLayout.constraint = UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = 6;
            candidateLabels = new TMP_Text[sandbox.Session.PlayerIds.Count];
            for (var i = 0; i < candidateLabels.Length; i++)
            {
                var number = i + 1;
                candidateLabels[i] = Button(grid.transform, number.ToString(), () => sandbox.ToggleCandidate(number)).GetComponentInChildren<TMP_Text>();
            }

            var bottom = Panel("InteractionHint", canvas.transform, new Vector2(0.015f, 0.015f), new Vector2(0.985f, 0.19f));
            footer = Text(bottom, "", 20, 0);
            Stretch(footer.rectTransform, 18);
            var resultRoot = Panel("Results", canvas.transform, new Vector2(0.245f, 0.23f), new Vector2(0.745f, 0.98f));
            resultPanel = resultRoot.gameObject;
            result = Text(resultRoot, "", 17, 0);
            Stretch(result.rectTransform, 20);
            Refresh();
        }

        private void Update()
        {
            if (sandbox == null || Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.15f;
            Refresh();
        }

        private void Refresh()
        {
            var session = sandbox.Session;
            var view = session.GetPlayerView(sandbox.SelectedId);
            var time = System.TimeSpan.FromSeconds(session.ElapsedSeconds);
            status.text = $"{time.Minutes:00}:{time.Seconds:00}  |  {session.Phase}\nTeam points: {session.MissionPoints}\n{session.Activity}  #{session.MissionNumber}\nRelay: {session.MissionContributions}/{session.RequiredContributions}\nRules: {string.Join(", ", session.ActiveRules)}";
            info.text = $"Controlling {view.Id}\nFaction: {view.Faction}{(view.IsOriginalMafia ? " (original)" : "")}\nYour number: ???\nDevice ready in: {System.Math.Max(0, view.DeviceReadyAt - session.ElapsedSeconds):0}s\nRecruit hold: {session.GetRecruitmentProgress(view.Id):P0}\n\nVERIFIED OBSERVATIONS\n" +
                string.Join("\n\n", view.Clues.Reverse().Take(5).Select(c => $"[{c.ObservedAt / 60:0.0}m] {(c.NumberEpoch < session.NumberEpoch ? "[OLD] " : "")}{(c.IsPublic ? "[PUBLIC] " : "")}{c.Text}"));
            footer.text = sandbox.NearbyHint() + "\n" + sandbox.Feedback + "\nA single computer controls all players here. Online rooms are not connected yet.";
            guessLabel.text = "Guess: " + sandbox.Guess;
            suspectLabel.text = "Suspect: " + (sandbox.Suspect ?? "abstain");
            speedLabel.text = "Speed: " + sandbox.SimulationSpeed + "x";
            submit.interactable = session.Phase == MatchPhase.Submission && !view.HasSubmitted;
            for (var i = 0; i < candidateLabels.Length; i++) candidateLabels[i].color = sandbox.IsCandidate(i + 1) ? Color.cyan : Color.white;
            resultPanel.SetActive(session.Result != null && showResults);
            if (session.Result == null) return;
            var outcome = session.Result;
            result.text = (outcome.CiviliansWin ? "CIVILIANS WIN" : "MAFIA WIN") +
                $"\nCorrect civilians: {outcome.CorrectCivilians} + bonus {outcome.SuspicionBonus} / {outcome.RequiredCorrect}\n\nNUMBER REVEAL\n" +
                string.Join("\n", outcome.Players.Select(p => $"{p.Id}: {p.SubmittedNumber?.ToString() ?? "-"} -> {p.Number}  {(p.Correct ? "OK" : "MISS")}")) +
                "\n\nFACTION REVEAL\n" + string.Join("\n", outcome.Players.Where(p => p.Faction == Faction.Mafia).Select(p =>
                    p.Id + (p.IsOriginalMafia ? " - original mafia" : $" - recruited at {p.RecruitedAt / 60:0.00}m"))) +
                "\n\nRECENT EVENTS\n" + string.Join("\n", outcome.Events.Skip(System.Math.Max(0, outcome.Events.Count - 5)).Select(e => $"{e.Time / 60:0.00}m  {e.Text}"));
        }

        private static RectTransform Panel(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
            root.transform.SetParent(parent, false);
            root.GetComponent<UnityEngine.UI.Image>().color = new Color(0.045f, 0.065f, 0.10f, 0.96f);
            var rect = (RectTransform)root.transform;
            rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static TMP_Text Text(Transform parent, string text, int size, float height)
        {
            var root = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(UnityEngine.UI.LayoutElement));
            root.transform.SetParent(parent, false);
            var label = root.GetComponent<TextMeshProUGUI>();
            label.text = text; label.fontSize = size; label.color = Color.white;
            label.enableAutoSizing = false; label.raycastTarget = false;
            root.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = height;
            return label;
        }

        private static UnityEngine.UI.Button Button(Transform parent, string caption, UnityAction action)
        {
            var root = new GameObject(caption, typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button), typeof(UnityEngine.UI.LayoutElement));
            root.transform.SetParent(parent, false);
            root.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 34;
            root.GetComponent<UnityEngine.UI.LayoutElement>().minHeight = 28;
            var background = root.GetComponent<UnityEngine.UI.Image>();
            background.color = new Color(0.13f, 0.23f, 0.32f);
            var button = root.GetComponent<UnityEngine.UI.Button>();
            button.targetGraphic = background;
            button.onClick.AddListener(action);
            button.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
            var label = Text(root.transform, caption, 16, 0);
            label.alignment = TextAlignmentOptions.Center;
            Stretch(label.rectTransform, 3);
            return button;
        }

        private static void Stretch(RectTransform rect, float margin)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.one * margin; rect.offsetMax = Vector2.one * -margin;
        }
    }
}
