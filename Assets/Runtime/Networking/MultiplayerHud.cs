using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace ZZabmongus.Networking
{
    public sealed class MultiplayerHud : MonoBehaviour
    {
        [SerializeField] private MultiplayerConnection connection;
        [SerializeField] private NetworkLobby lobby;
        private TMP_InputField nameInput, addressInput, portInput;
        private TMP_Text status, roster, transportLabel;
        private UnityEngine.UI.Button host, join, leave, ready, color, start, back, mode;
        private float nextRefresh;
        private void Start()
        {
            var canvasObject = new GameObject("MultiplayerHUD", typeof(RectTransform), typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 1000);
            scaler.matchWidthOrHeight = 1;
            if (!EventSystem.current)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule)).transform.SetParent(transform, false);
            var panel = new GameObject("RoomPanel", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.VerticalLayoutGroup));
            panel.transform.SetParent(canvasObject.transform, false);
            var rect = (RectTransform)panel.transform;
            rect.anchorMin = new Vector2(0.012f, 0.025f); rect.anchorMax = new Vector2(0.29f, 0.975f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            panel.GetComponent<UnityEngine.UI.Image>().color = new Color(0.045f, 0.065f, 0.10f, 0.98f);
            var layout = panel.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.padding = new RectOffset(18, 18, 16, 16); layout.spacing = 3;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            Text(panel.transform, "ZZABMONGUS", 29, 36);
            Text(panel.transform, "MULTIPLAYER BASE  |  4-12 PLAYERS", 14, 23).color = new Color(0.35f, 0.85f, 0.9f);
            nameInput = Input(panel.transform, "Name", connection.DisplayName, 16);
            mode = Button(panel.transform, "Connection: LAN / UDP", () =>
            {
                connection.Mode = connection.Mode == ConnectionMode.Lan ? ConnectionMode.Steam : ConnectionMode.Lan;
                addressInput.text = connection.Mode == ConnectionMode.Lan ? "127.0.0.1" : "";
            });
            transportLabel = mode.GetComponentInChildren<TMP_Text>();
            addressInput = Input(panel.transform, "Host IP or Steam ID", connection.Address, 128);
            portInput = Input(panel.transform, "Port", connection.Port.ToString(), 5);
            portInput.contentType = TMP_InputField.ContentType.IntegerNumber;
            host = Button(panel.transform, "CREATE ROOM", () => { ApplyFields(); connection.Host(); });
            join = Button(panel.transform, "JOIN ROOM", () => { ApplyFields(); connection.Join(); });
            leave = Button(panel.transform, "LEAVE ROOM", connection.Leave);
            status = Text(panel.transform, "", 16, 64);
            roster = Text(panel.transform, "", 14, 250);
            color = Button(panel.transform, "Change color", () =>
            {
                var player = NetworkPlayer.Local;
                if (!player) return;
                var candidate = (player.ColorIndex + 1) % NetworkPlayer.Palette.Length;
                for (var i = 0; i < NetworkPlayer.Palette.Length; i++)
                {
                    if (!lobby.Players.Any(p => p != player && p.ColorIndex == candidate)) break;
                    candidate = (candidate + 1) % NetworkPlayer.Palette.Length;
                }
                player.SetProfile(nameInput.text, candidate);
            });
            ready = Button(panel.transform, "READY", () => { if (NetworkPlayer.Local) NetworkPlayer.Local.ToggleReady(); });
            start = Button(panel.transform, "START SESSION", lobby.StartSession);
            back = Button(panel.transform, "RETURN TO LOBBY", lobby.ReturnToLobby);
            Text(panel.transform, "WASD / arrows: move\nEveryone must be ready to start.", 15, 42);
            nameInput.onEndEdit.AddListener(value =>
            {
                connection.DisplayName = NetworkPlayer.SanitizeName(value);
                if (NetworkPlayer.Local) NetworkPlayer.Local.SetProfile(value, NetworkPlayer.Local.ColorIndex);
            });
            Refresh();
        }

        private void ApplyFields()
        {
            connection.DisplayName = NetworkPlayer.SanitizeName(nameInput.text);
            connection.Address = addressInput.text.Trim();
            connection.Port = ushort.TryParse(portInput.text, out var port) ? port : (ushort)0;
        }
        private void Update()
        {
            if (!status || Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.15f;
            Refresh();
        }
        private void Refresh()
        {
            var players = lobby.Players;
            var local = NetworkPlayer.Local;
            var busy = connection.Busy;
            host.interactable = join.interactable = mode.interactable = addressInput.interactable = portInput.interactable = !busy;
            leave.interactable = busy;
            nameInput.interactable = !lobby.InSession;
            ready.interactable = color.interactable = local && !lobby.InSession;
            start.interactable = lobby.CanStart;
            back.interactable = connection.Hosting && lobby.InSession;
            ready.GetComponentInChildren<TMP_Text>().text = local && local.Ready ? "NOT READY" : "READY";
            transportLabel.text = connection.Mode == ConnectionMode.Lan ? "Connection: LAN / UDP" : "Connection: Steam P2P";
            status.text = connection.Message + (connection.Mode == ConnectionMode.Steam && connection.SteamId.Length > 0 ? "\nYour Steam ID: " + connection.SteamId : "");
            roster.text = (lobby.InSession ? "SESSION RUNNING" : "WAITING ROOM") + $"  {players.Length}/{NetworkLobby.MaxPlayers}\n\n" +
                string.Join("\n", players.Select(p => $"<color=#{ColorUtility.ToHtmlStringRGB(NetworkPlayer.Palette[p.ColorIndex])}>●</color> {p.DisplayName}{(p.isOwner ? " (you)" : "")}   {(p.Ready ? "READY" : "...")}"));
        }

        private static TMP_Text Text(Transform parent, string caption, int size, float height)
        {
            var root = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(UnityEngine.UI.LayoutElement));
            root.transform.SetParent(parent, false);
            var label = root.GetComponent<TextMeshProUGUI>();
            label.font = TMP_Settings.defaultFontAsset;
            label.text = caption; label.fontSize = size; label.color = Color.white; label.raycastTarget = false;
            root.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = height;
            return label;
        }
        private static UnityEngine.UI.Button Button(Transform parent, string caption, UnityAction action)
        {
            var root = new GameObject(caption, typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button), typeof(UnityEngine.UI.LayoutElement));
            root.transform.SetParent(parent, false);
            root.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 30;
            root.GetComponent<UnityEngine.UI.Image>().color = new Color(0.15f, 0.27f, 0.36f);
            var button = root.GetComponent<UnityEngine.UI.Button>(); button.onClick.AddListener(action);
            var label = Text(root.transform, caption, 16, 0);
            Stretch(label.rectTransform, 6); label.alignment = TextAlignmentOptions.Center;
            return button;
        }
        private static TMP_InputField Input(Transform parent, string hint, string value, int limit)
        {
            var root = new GameObject(hint, typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(TMP_InputField), typeof(UnityEngine.UI.LayoutElement));
            root.transform.SetParent(parent, false);
            root.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 32;
            root.GetComponent<UnityEngine.UI.Image>().color = new Color(0.09f, 0.14f, 0.20f);
            var viewport = new GameObject("Text Area", typeof(RectTransform), typeof(UnityEngine.UI.RectMask2D));
            viewport.transform.SetParent(root.transform, false); Stretch((RectTransform)viewport.transform, 8);
            var text = Text(viewport.transform, "", 17, 0); Stretch(text.rectTransform, 0);
            var placeholder = Text(viewport.transform, hint, 17, 0); placeholder.color = Color.gray; Stretch(placeholder.rectTransform, 0);
            var field = root.GetComponent<TMP_InputField>();
            field.textViewport = (RectTransform)viewport.transform;
            field.textComponent = (TextMeshProUGUI)text;
            field.placeholder = placeholder;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.characterLimit = limit; field.text = value;
            return field;
        }
        private static void Stretch(RectTransform rect, float padding)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(padding, padding); rect.offsetMax = new Vector2(-padding, -padding);
        }
    }
}
