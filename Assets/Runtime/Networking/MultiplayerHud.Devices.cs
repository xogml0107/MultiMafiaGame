using System.Linq;
using TMPro;
using UnityEngine;
using ZZabmongus.Core;

namespace ZZabmongus.Networking
{
    public sealed partial class MultiplayerHud
    {
        private GameObject notebookPanel;
        private TMP_Text deviceHint, deviceFeedback, notebookText;

        private void BuildDeviceHud(Transform canvas)
        {
            notebookPanel = MatchPanel("PrivateClueNotebook", canvas, new Vector2(0.72f,0.53f), new Vector2(0.975f,0.975f));
            Text(notebookPanel.transform, "개인 단서 노트", 25, 38);
            deviceHint = Text(notebookPanel.transform, "홀짝 검사 장치를 찾아보세요.", 17, 72);
            deviceFeedback = Text(notebookPanel.transform, "", 16, 44);
            var scrollRoot = new GameObject("ClueScroll", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.ScrollRect), typeof(UnityEngine.UI.LayoutElement));
            scrollRoot.transform.SetParent(notebookPanel.transform, false);
            scrollRoot.GetComponent<UnityEngine.UI.Image>().color = new Color(0.035f,0.055f,0.09f);
            var element = scrollRoot.GetComponent<UnityEngine.UI.LayoutElement>(); element.minHeight = 120; element.flexibleHeight = 1;
            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Mask));
            viewport.transform.SetParent(scrollRoot.transform, false); Stretch((RectTransform)viewport.transform, 3);
            viewport.GetComponent<UnityEngine.UI.Mask>().showMaskGraphic = false;
            var content = new GameObject("Content", typeof(RectTransform), typeof(UnityEngine.UI.VerticalLayoutGroup), typeof(UnityEngine.UI.ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            var rect = (RectTransform)content.transform; rect.anchorMin = new Vector2(0,1); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(0.5f,1); rect.sizeDelta = Vector2.zero;
            var layout = content.GetComponent<UnityEngine.UI.VerticalLayoutGroup>(); layout.padding = new RectOffset(8,8,8,8);
            layout.childControlHeight = layout.childControlWidth = true; layout.childForceExpandHeight = false;
            content.GetComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            notebookText = Text(content.transform, "아직 기록된 단서가 없습니다.", 17, 0);
            // TMP preferred height grows with wrapped lines; do not pin this row to height zero.
            notebookText.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = -1;
            notebookText.richText = false; notebookText.raycastTarget = false;
            var scroll = scrollRoot.GetComponent<UnityEngine.UI.ScrollRect>(); scroll.content = rect; scroll.viewport = (RectTransform)viewport.transform;
            scroll.horizontal = false; scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 35;
            notebookPanel.SetActive(false);
        }

        private void RefreshDeviceHud(bool matchVisible)
        {
            if (!notebookPanel) return;
            var visible = matchVisible && lobby.Phase != MatchPhase.Results;
            notebookPanel.SetActive(visible);
            if (!visible) return;
            var local = NetworkPlayer.Local;
            if (!local || !local.HasMatchInfo)
            { deviceHint.text = "개인 정보를 받는 중..."; notebookText.text = ""; deviceFeedback.text = ""; return; }
            var station = local.NearbyDevice;
            deviceHint.text = lobby.Phase != MatchPhase.Playing ? "정보 수집이 종료되었습니다." :
                (station ? "[E] " + station.DisplayName : "홀짝 검사 장치를 찾아보세요.") + "\n팀 포인트 1점 · 재사용 30초\n" +
                (local.DeviceCooldownSeconds > 0 ? "남은 대기 " + local.DeviceCooldownSeconds + "초" : "최소 포인트: 90초마다 +1");
            deviceFeedback.text = local.DeviceMessage;
            notebookText.text = local.ClueNotes.Count == 0 ? "아직 기록된 단서가 없습니다." : string.Join("\n\n", local.ClueNotes.Select(note =>
                (note.numberEpoch < local.NotebookEpoch ? "[과거] " : "") + (note.isPublic ? "[공개] " : "[개인] ") + Clock(note.observedSeconds) + "\n" + note.text));
        }
    }
}
