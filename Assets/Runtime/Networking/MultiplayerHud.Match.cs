using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using ZZabmongus.Core;

namespace ZZabmongus.Networking
{
    public sealed partial class MultiplayerHud
    {
        private GameObject matchPanel, answerControls, resultPanel;
        private TMP_Text matchRole, matchClock, matchActivity, matchRules, answerMessage, resultText;
        private TMP_Dropdown finalNumber, finalSuspect;
        private UnityEngine.UI.Button submitAnswer, matchBack;
        private readonly List<int> suspectSlots = new();
        private int optionsRound = -1;

        private void BuildMatchHud(Transform canvas)
        {
            matchPanel = MatchPanel("MatchPanel", canvas, new Vector2(0.012f,0.025f), new Vector2(0.29f,0.975f));
            Text(matchPanel.transform, "번호를 추리하세요", 27, 40);
            matchRole = Text(matchPanel.transform, "진영 정보를 받는 중...", 22, 72);
            matchClock = Text(matchPanel.transform, "", 22, 60);
            matchActivity = Text(matchPanel.transform, "", 18, 70);
            matchRules = Text(matchPanel.transform, "", 17, 110);
            answerControls = new GameObject("FinalAnswerControls", typeof(RectTransform), typeof(UnityEngine.UI.VerticalLayoutGroup), typeof(UnityEngine.UI.LayoutElement));
            answerControls.transform.SetParent(matchPanel.transform, false);
            var answerLayout = answerControls.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
            answerLayout.spacing = 6; answerLayout.childControlWidth = answerLayout.childControlHeight = true; answerLayout.childForceExpandHeight = false;
            answerControls.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 222;
            Text(answerControls.transform, "최종 번호", 19, 28);
            finalNumber = MatchDropdown(answerControls.transform, "FinalNumber");
            Text(answerControls.transform, "의심 대상 (선택)", 19, 28);
            finalSuspect = MatchDropdown(answerControls.transform, "FinalSuspect");
            submitAnswer = Button(answerControls.transform, "최종 답안 확정", () =>
            {
                var local = NetworkPlayer.Local;
                if (local && finalSuspect.value < suspectSlots.Count)
                    local.SubmitFinalAnswer(finalNumber.value + 1, suspectSlots[finalSuspect.value]);
            });
            submitAnswer.name = "SubmitFinalAnswer"; submitAnswer.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 44;
            answerMessage = Text(matchPanel.transform, "", 18, 60);
            matchBack = Button(matchPanel.transform, "대기실로 돌아가기", () => lobby.ReturnToLobby());
            matchBack.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 40;
            Button(matchPanel.transform, "방 나가기", () => connection.Leave()).GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 40;
            resultPanel = MatchPanel("MatchResultsPanel", canvas, new Vector2(0.32f,0.10f), new Vector2(0.975f,0.90f));
            Text(resultPanel.transform, "최종 결과", 30, 56);
            resultText = Text(resultPanel.transform, "", 22, 650);
            resultText.richText = false;
            BuildDeviceHud(canvas);
            matchPanel.SetActive(false); resultPanel.SetActive(false); answerControls.SetActive(false);
        }

        private static GameObject MatchPanel(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.VerticalLayoutGroup));
            panel.transform.SetParent(parent, false);
            var rect = (RectTransform)panel.transform; rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
            panel.GetComponent<UnityEngine.UI.Image>().color = new Color(0.045f,0.065f,0.10f,0.98f);
            var layout = panel.GetComponent<UnityEngine.UI.VerticalLayoutGroup>(); layout.padding = new RectOffset(18,18,18,18); layout.spacing = 8;
            layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandHeight = false;
            return panel;
        }

        private static TMP_Dropdown MatchDropdown(Transform parent, string name)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(TMP_Dropdown), typeof(UnityEngine.UI.LayoutElement));
            root.transform.SetParent(parent, false); root.GetComponent<UnityEngine.UI.Image>().color = new Color(0.12f,0.26f,0.37f);
            root.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 42;
            var caption = Text(root.transform, "", 19, 0); Stretch(caption.rectTransform, 8);
            var template = new GameObject("Template", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.ScrollRect));
            template.transform.SetParent(root.transform, false);
            var rect = (RectTransform)template.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.right; rect.pivot = new Vector2(0.5f,1);
            rect.sizeDelta = new Vector2(0,220); rect.anchoredPosition = new Vector2(0,-2);
            template.GetComponent<UnityEngine.UI.Image>().color = new Color(0.05f,0.09f,0.15f);
            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Mask));
            viewport.transform.SetParent(template.transform, false); Stretch((RectTransform)viewport.transform, 2);
            viewport.GetComponent<UnityEngine.UI.Mask>().showMaskGraphic = false;
            var content = new GameObject("Content", typeof(RectTransform)); content.transform.SetParent(viewport.transform, false);
            var contentRect = (RectTransform)content.transform; contentRect.anchorMin = new Vector2(0,1); contentRect.anchorMax = Vector2.one; contentRect.pivot = new Vector2(0.5f,1); contentRect.sizeDelta = new Vector2(0,42);
            var item = new GameObject("Item", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Toggle));
            item.transform.SetParent(content.transform, false); Stretch((RectTransform)item.transform, 0);
            var itemText = Text(item.transform, "", 19, 0); Stretch(itemText.rectTransform, 8);
            item.GetComponent<UnityEngine.UI.Image>().color = new Color(0.12f,0.26f,0.37f);
            item.GetComponent<UnityEngine.UI.Toggle>().targetGraphic = item.GetComponent<UnityEngine.UI.Image>();
            var scroll = template.GetComponent<UnityEngine.UI.ScrollRect>(); scroll.content = contentRect; scroll.viewport = (RectTransform)viewport.transform; scroll.horizontal = false;
            scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 35;
            var dropdown = root.GetComponent<TMP_Dropdown>(); dropdown.captionText = caption; dropdown.itemText = itemText; dropdown.template = rect;
            dropdown.ClearOptions(); template.SetActive(false); return dropdown;
        }

        private void RefreshMatchHud()
        {
            var visible = lobby.InSession && !lobby.Loading && connection.Connected;
            matchPanel.SetActive(visible); resultPanel.SetActive(visible && lobby.Phase == MatchPhase.Results);
            RefreshDeviceHud(visible);
            if (!visible) { optionsRound = -1; return; }
            var local = NetworkPlayer.Local;
            matchRole.text = local && local.HasMatchInfo ?
                (local.IsOriginalMafia ? "내 진영: 원조 마피아" : local.IsMafia ? "내 진영: 마피아" : "내 진영: 시민") + "\n자기 번호는 숨겨져 있습니다." : "진영 정보를 받는 중...";
            matchRole.color = local && local.IsMafia ? new Color(1,0.5f,0.45f) : new Color(0.4f,0.9f,0.95f);
            var timing = lobby.Phase == MatchPhase.Playing ? "정보 수집 종료까지" : lobby.Phase == MatchPhase.FinalDiscussion ? "최종 제출까지" : lobby.Phase == MatchPhase.Submission ? "제출 마감까지" : "판이 종료되었습니다.";
            matchClock.text = timing + (lobby.Phase == MatchPhase.Results ? "" : " " + Clock(lobby.RemainingSeconds)) + "\n경과 " + Clock(lobby.ElapsedSeconds);
            var activityName = lobby.Activity == ActivityPhase.FreeAction ? "자유 행동" : lobby.Activity == ActivityPhase.Mission ? "협동 미션" : "미션 결과 대기";
            matchActivity.text = $"참가자 {lobby.ParticipantCount}명 · 팀 포인트 {lobby.MissionPoints}\n" +
                (lobby.Phase == MatchPhase.Playing ? activityName + " " + Clock(lobby.ActivityRemainingSeconds) : lobby.Phase == MatchPhase.FinalDiscussion ? "최종 토론 시간" : "");
            var descriptions = new[] { "홀짝 결과 공개", "정보 장치 2인 동행", "범위 장치 봉쇄", "대소 비교 결과 공개", "번호 교환: 과거 단서 재검토", "정밀 장치 봉쇄" };
            var active = descriptions.Where((_, index) => (lobby.RuleMask & (1 << index)) != 0).ToArray();
            matchRules.text = "적용 규칙\n" + (active.Length == 0 ? "아직 적용된 규칙이 없습니다." : string.Join("\n", active));
            if (local && local.HasMatchInfo && optionsRound != lobby.Round)
            {
                optionsRound = lobby.Round;
                finalNumber.ClearOptions(); finalNumber.AddOptions(Enumerable.Range(1,lobby.ParticipantCount).Select(i => i.ToString()).ToList());
                var others = lobby.Players.Where(p => p != local).ToArray();
                suspectSlots.Clear(); suspectSlots.Add(-1); suspectSlots.AddRange(others.Select(p => p.Slot));
                finalSuspect.ClearOptions(); finalSuspect.AddOptions(new[] { "기권" }.Concat(others.Select(p => p.DisplayName)).ToList());
                finalNumber.SetValueWithoutNotify(0); finalSuspect.SetValueWithoutNotify(0);
            }
            answerControls.SetActive(lobby.Phase == MatchPhase.Submission);
            submitAnswer.interactable = finalNumber.interactable = finalSuspect.interactable = local && local.HasMatchInfo && !local.HasSubmitted && lobby.Phase == MatchPhase.Submission;
            answerMessage.text = local && local.SubmissionMessage.Length > 0 ? local.SubmissionMessage :
                lobby.Phase == MatchPhase.Submission ? "확정 후 답안을 바꿀 수 없습니다." : lobby.Phase == MatchPhase.Results ? "방장이 대기실로 돌아가면 다시 준비할 수 있습니다." : "20분에 최종 답안을 제출합니다.";
            matchBack.interactable = connection.Hosting;
            resultText.text = lobby.ResultSummary;
        }

        private static string Clock(int seconds) => (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00");
    }
}
