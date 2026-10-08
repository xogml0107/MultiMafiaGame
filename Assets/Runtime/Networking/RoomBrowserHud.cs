using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using ZZabmongus.Core.Rooms;

namespace ZZabmongus.Networking
{
    public sealed class RoomBrowserHud : MonoBehaviour
    {
        private MultiplayerConnection connection;
        private IRoomDirectory directory;
        private RoomBrowserController controller;
        private GameObject canvasObject, modal, createForm, passwordForm, errorForm;
        private Transform roomContent;
        private TMP_Text status, errorText, selectedName;
        private TMP_InputField playerName, roomName, createPassword, capacity, joinPassword;
        private UnityEngine.UI.Toggle isPublic;
        private UnityEngine.UI.Button refresh, create;
        private readonly Dictionary<string, RoomRow> rows = new();
        private readonly List<UnityEngine.UI.Button> countryButtons = new();
        private TMP_FontAsset font;
        private float nextRefresh;
        private RoomDialog previousDialog;
        public RoomBrowserController Controller => controller;
        public bool InRoom => connection && connection.Connected && NetworkPlayer.Local;

        public void Bind(IRoomDirectory value)
        {
            if (controller != null) { controller.Changed -= Render; controller.Dispose(); }
            directory = value; controller = new RoomBrowserController(value); controller.Changed += Render;
            nextRefresh = value is SteamRoomDirectory ? Time.unscaledTime + 0.6f : float.PositiveInfinity;
            if (canvasObject) Render();
        }
        private void Start()
        {
            connection = FindFirstObjectByType<MultiplayerConnection>();
            font = Resources.Load<TMP_FontAsset>("UI/LobbyFont") ?? TMP_Settings.defaultFontAsset;
            if (controller == null) Bind(FindFirstObjectByType<SteamRoomDirectory>());
            Build(); Render();
        }
        private void Update()
        {
            if (!canvasObject) return;
            canvasObject.SetActive(!InRoom);
            if (InRoom) return;
            var busy = controller.Busy;
            refresh.interactable = create.interactable = playerName.interactable = !busy;
            foreach (var button in countryButtons) button.interactable = !busy;
            if (Time.unscaledTime >= nextRefresh && !busy && controller.Dialog == RoomDialog.None)
            {
                nextRefresh = float.PositiveInfinity;
                controller.Refresh();
                if (directory is SteamRoomDirectory steam && connection.SteamReady) nextRefresh = Time.unscaledTime + 5;
            }
        }
        private void OnDisable() { if (canvasObject) canvasObject.SetActive(false); }
        private void OnDestroy() { if (controller != null) { controller.Changed -= Render; controller.Dispose(); } }

        private void Build()
        {
            canvasObject = new GameObject("RoomBrowserCanvas", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 200;
            var scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 1000); scaler.matchWidthOrHeight = 1;
            if (!EventSystem.current) new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule)).transform.SetParent(transform, false);
            var background = Panel("BrowserBackground", canvasObject.transform, Vector2.zero, Vector2.one, new Color(0.025f,0.035f,0.065f,1));
            var frame = Panel("BrowserFrame", background, new Vector2(0.045f,0.065f), new Vector2(0.955f,0.935f), new Color(0.065f,0.09f,0.145f,1));
            var title = Text(frame, "국가별 서버 로비", 36, 0); Anchor(title.rectTransform, new Vector2(0.03f,0.90f), new Vector2(0.97f,0.98f));
            var countries = Panel("CountryServers", frame, new Vector2(0.02f,0.18f), new Vector2(0.19f,0.88f), new Color(0.04f,0.06f,0.10f));
            Stack(countries, 16, 12);
            Text(countries, "서버 선택", 23, 45);
            for (var i = 0; i < Countries.Codes.Length; i++)
            {
                var code = Countries.Codes[i];
                countryButtons.Add(Button(countries, "Server_" + code, Countries.Names[i], () => controller.SelectCountry(code), 52));
            }
            status = Text(frame, "", 21, 0); Anchor(status.rectTransform, new Vector2(0.215f,0.805f), new Vector2(0.975f,0.875f));
            roomContent = Scroll(frame);
            var bar = Panel("BrowserActions", frame, new Vector2(0.02f,0.035f), new Vector2(0.98f,0.145f), new Color(0.04f,0.06f,0.10f));
            var layout = bar.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            layout.padding = new RectOffset(18,18,18,18); layout.spacing = 20;
            layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandWidth = false; layout.childForceExpandHeight = false;
            var nameLabel = Text(bar, "이름", 22, 45); nameLabel.GetComponent<UnityEngine.UI.LayoutElement>().preferredWidth = 65;
            playerName = Input(bar, "BrowserPlayerName", "플레이어 이름", 16);
            playerName.text = connection.DisplayName;
            playerName.GetComponent<UnityEngine.UI.LayoutElement>().preferredWidth = 300;
            playerName.onEndEdit.AddListener(value => connection.DisplayName = NetworkPlayer.SanitizeName(value));
            refresh = Button(bar, "RefreshRooms", "새로고침", () => controller.Refresh(), 48); refresh.GetComponent<UnityEngine.UI.LayoutElement>().preferredWidth = 175;
            create = Button(bar, "OpenCreateRoom", "방 생성", () => controller.OpenCreate(), 48); create.GetComponent<UnityEngine.UI.LayoutElement>().preferredWidth = 175;
            var hint = Text(bar, "방을 더블 클릭하여 입장", 18, 48); hint.GetComponent<UnityEngine.UI.LayoutElement>().flexibleWidth = 1;
            BuildDialogs(canvasObject.transform);
        }
        private Transform Scroll(Transform parent)
        {
            var root = Panel("RoomListScroll", parent, new Vector2(0.21f,0.18f), new Vector2(0.98f,0.80f), new Color(0.035f,0.055f,0.09f));
            var scroll = root.gameObject.AddComponent<UnityEngine.UI.ScrollRect>(); scroll.horizontal = false;
            scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 40;
            var viewport = Panel("Viewport", root, Vector2.zero, Vector2.one, Color.white);
            viewport.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic = false;
            var content = new GameObject("RoomListContent", typeof(RectTransform), typeof(UnityEngine.UI.VerticalLayoutGroup), typeof(UnityEngine.UI.ContentSizeFitter));
            content.transform.SetParent(viewport, false);
            var rect = (RectTransform)content.transform;
            rect.anchorMin = new Vector2(0,1); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(0.5f,1); rect.sizeDelta = Vector2.zero;
            var layout = content.GetComponent<UnityEngine.UI.VerticalLayoutGroup>(); layout.padding = new RectOffset(12,12,12,12); layout.spacing = 8;
            layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandHeight = false;
            content.GetComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = rect; scroll.viewport = viewport;
            return content.transform;
        }
        private void BuildDialogs(Transform parent)
        {
            var overlay = Panel("RoomModalOverlay", parent, Vector2.zero, Vector2.one, new Color(0,0,0,0.7f));
            modal = overlay.gameObject;
            var createPanel = Panel("CreateRoomDialog", overlay, new Vector2(0.30f,0.15f), new Vector2(0.70f,0.85f), new Color(0.075f,0.11f,0.17f));
            createForm = createPanel.gameObject; Stack(createPanel,28,10);
            Text(createPanel, "방 생성", 30,50);
            Text(createPanel, "방 이름", 21,30); roomName = Input(createPanel, "CreateRoomName", "방 이름을 입력하세요",40);
            isPublic = Toggle(createPanel);
            Text(createPanel, "비밀번호",21,30); createPassword = Input(createPanel,"CreateRoomPassword","비공개 방 비밀번호",32);
            createPassword.contentType = TMP_InputField.ContentType.Password;
            isPublic.onValueChanged.AddListener(value => { createPassword.interactable = !value; if (value) createPassword.text = ""; });
            isPublic.isOn = true; createPassword.interactable = false;
            Text(createPanel,"최대 인원 (4~12명)",21,30); capacity = Input(createPanel,"CreateRoomCapacity","12",2);
            capacity.contentType = TMP_InputField.ContentType.IntegerNumber; capacity.text = "12";
            Button(createPanel,"ConfirmCreateRoom","생성", () =>
            {
                connection.DisplayName = NetworkPlayer.SanitizeName(playerName.text);
                controller.Create(roomName.text, isPublic.isOn, createPassword.text, int.TryParse(capacity.text,out var number) ? number : 0);
            },48);
            Button(createPanel,"CancelCreateRoom","취소",() => controller.CloseDialog(),48);
            var passwordPanel = Panel("PasswordRoomDialog",overlay,new Vector2(0.30f,0.32f),new Vector2(0.70f,0.68f),new Color(0.075f,0.11f,0.17f));
            passwordForm = passwordPanel.gameObject; Stack(passwordPanel,28,12);
            Text(passwordPanel,"비공개 방 입장",28,45); selectedName = Text(passwordPanel,"",21,35);
            joinPassword = Input(passwordPanel,"JoinRoomPassword","비밀번호를 입력하세요",32); joinPassword.contentType = TMP_InputField.ContentType.Password;
            Button(passwordPanel,"ConfirmJoinPassword","입장", () => { connection.DisplayName = NetworkPlayer.SanitizeName(playerName.text); controller.SubmitPassword(joinPassword.text); },48);
            Button(passwordPanel,"CancelJoinPassword","취소",() => controller.CloseDialog(),48);
            var errorPanel = Panel("RoomErrorDialog",overlay,new Vector2(0.28f,0.36f),new Vector2(0.72f,0.64f),new Color(0.075f,0.11f,0.17f));
            errorForm = errorPanel.gameObject; Stack(errorPanel,28,14);
            Text(errorPanel,"알림",28,42); errorText = Text(errorPanel,"",22,90);
            Button(errorPanel,"DismissRoomError","확인",() => controller.CloseDialog(),48);
        }
        private void Render()
        {
            if (!canvasObject) return;
            status.text = Countries.Name(controller.Country) + " 서버  |  " + (directory is SteamRoomDirectory steam ? steam.Status : "방을 더블 클릭하면 입장합니다.");
            var listed = controller.Rooms;
            var ids = new HashSet<string>(listed.Select(r => r.Id));
            foreach (var stale in rows.Keys.Where(key => !ids.Contains(key)).ToArray()) { Destroy(rows[stale].gameObject); rows.Remove(stale); }
            for (var i = 0; i < listed.Count; i++)
            {
                var room = listed[i];
                if (!rows.TryGetValue(room.Id,out var row)) { row = NewRow(room.Id); rows.Add(room.Id,row); }
                row.Set(room); row.transform.SetSiblingIndex(i);
            }
            for (var i = 0; i < countryButtons.Count; i++)
                countryButtons[i].GetComponent<UnityEngine.UI.Image>().color = Countries.Codes[i] == controller.Country ? new Color(0.15f,0.42f,0.53f) : new Color(0.12f,0.19f,0.29f);
            var dialog = controller.Dialog;
            if (dialog != previousDialog)
            {
                joinPassword.text = "";
                if (dialog != RoomDialog.Create) createPassword.text = "";
                if (dialog == RoomDialog.Create) { roomName.text = ""; isPublic.isOn = true; createPassword.interactable = false; capacity.text = "12"; }
                previousDialog = dialog;
            }
            modal.SetActive(dialog != RoomDialog.None);
            createForm.SetActive(dialog == RoomDialog.Create); passwordForm.SetActive(dialog == RoomDialog.Password); errorForm.SetActive(dialog == RoomDialog.Error);
            selectedName.text = controller.SelectedRoom?.Name ?? ""; errorText.text = controller.Error;
        }
        private RoomRow NewRow(string id)
        {
            var root = new GameObject("Room_" + id,typeof(RectTransform),typeof(UnityEngine.UI.Image),typeof(UnityEngine.UI.LayoutElement),typeof(RoomRow));
            root.transform.SetParent(roomContent,false); root.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 70;
            root.GetComponent<UnityEngine.UI.Image>().color = new Color(0.10f,0.16f,0.24f);
            var name = Text(root.transform,"",23,0); Anchor(name.rectTransform,new Vector2(0.10f,0.08f),new Vector2(0.73f,0.92f)); name.textWrappingMode = TextWrappingModes.NoWrap; name.overflowMode = TextOverflowModes.Ellipsis;
            var population = Text(root.transform,"",20,0); Anchor(population.rectTransform,new Vector2(0.76f,0.08f),new Vector2(0.98f,0.92f)); population.alignment = TextAlignmentOptions.MidlineRight;
            var padlock = Panel("LockIcon",root.transform,new Vector2(0.025f,0.24f),new Vector2(0.072f,0.76f),Color.clear);
            var gold = new Color(1,0.78f,0.3f);
            Panel("ShackleTop",padlock,new Vector2(0.2f,0.82f),new Vector2(0.8f,0.96f),gold).GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            Panel("ShackleLeft",padlock,new Vector2(0.2f,0.45f),new Vector2(0.33f,0.88f),gold).GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            Panel("ShackleRight",padlock,new Vector2(0.67f,0.45f),new Vector2(0.8f,0.88f),gold).GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            Panel("LockBody",padlock,new Vector2(0.05f,0.05f),new Vector2(0.95f,0.58f),gold).GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            padlock.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            var row = root.GetComponent<RoomRow>(); row.Initialize(name,population,padlock.gameObject,(room,count) => controller.ClickRoom(room,count));
            return row;
        }
        private RectTransform Panel(string name,Transform parent,Vector2 min,Vector2 max,Color color)
        {
            var root = new GameObject(name,typeof(RectTransform),typeof(UnityEngine.UI.Image)); root.transform.SetParent(parent,false);
            root.GetComponent<UnityEngine.UI.Image>().color = color; var rect = (RectTransform)root.transform; Anchor(rect,min,max); return rect;
        }
        private TMP_Text Text(Transform parent,string caption,int size,float height)
        {
            var root = new GameObject("Label",typeof(RectTransform),typeof(TextMeshProUGUI),typeof(UnityEngine.UI.LayoutElement)); root.transform.SetParent(parent,false);
            var text = root.GetComponent<TextMeshProUGUI>(); text.font = font; text.fontSize = size; text.text = caption; text.color = Color.white; text.raycastTarget = false;
            text.enableAutoSizing = false;
            root.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = height; return text;
        }
        private UnityEngine.UI.Button Button(Transform parent,string name,string caption,UnityAction action,float height)
        {
            var root = new GameObject(name,typeof(RectTransform),typeof(UnityEngine.UI.Image),typeof(UnityEngine.UI.Button),typeof(UnityEngine.UI.LayoutElement)); root.transform.SetParent(parent,false);
            root.GetComponent<UnityEngine.UI.Image>().color = new Color(0.12f,0.26f,0.37f); root.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = height;
            var label = Text(root.transform,caption,21,0); Anchor(label.rectTransform,Vector2.zero,Vector2.one,6); label.alignment = TextAlignmentOptions.Center;
            var button = root.GetComponent<UnityEngine.UI.Button>(); button.onClick.AddListener(action); return button;
        }
        private TMP_InputField Input(Transform parent,string name,string placeholder,int limit)
        {
            var root = new GameObject(name,typeof(RectTransform),typeof(UnityEngine.UI.Image),typeof(TMP_InputField),typeof(UnityEngine.UI.LayoutElement)); root.transform.SetParent(parent,false);
            root.GetComponent<UnityEngine.UI.Image>().color = new Color(0.035f,0.055f,0.09f); root.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 46;
            var viewport = new GameObject("TextArea",typeof(RectTransform),typeof(UnityEngine.UI.RectMask2D)); viewport.transform.SetParent(root.transform,false); Anchor((RectTransform)viewport.transform,Vector2.zero,Vector2.one,8);
            var label = Text(viewport.transform,"",22,0); Anchor(label.rectTransform,Vector2.zero,Vector2.one);
            var hint = Text(viewport.transform,placeholder,20,0); hint.color = new Color(0.5f,0.6f,0.7f); Anchor(hint.rectTransform,Vector2.zero,Vector2.one);
            var field = root.GetComponent<TMP_InputField>(); field.textViewport = (RectTransform)viewport.transform; field.textComponent = (TextMeshProUGUI)label; field.placeholder = hint;
            field.lineType = TMP_InputField.LineType.SingleLine; field.characterLimit = limit; return field;
        }
        private UnityEngine.UI.Toggle Toggle(Transform parent)
        {
            var root = new GameObject("PublicRoomToggle",typeof(RectTransform),typeof(UnityEngine.UI.Toggle),typeof(UnityEngine.UI.LayoutElement)); root.transform.SetParent(parent,false);
            root.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 44;
            var box = Panel("Checkbox",root.transform,new Vector2(0,0.12f),new Vector2(0.075f,0.88f),new Color(0.2f,0.32f,0.44f));
            var check = Panel("Checkmark",box,new Vector2(0.23f,0.23f),new Vector2(0.77f,0.77f),Color.white);
            var label = Text(root.transform,"공개 방",22,0); Anchor(label.rectTransform,new Vector2(0.10f,0),Vector2.one);
            var toggle = root.GetComponent<UnityEngine.UI.Toggle>(); toggle.targetGraphic = box.GetComponent<UnityEngine.UI.Image>(); toggle.graphic = check.GetComponent<UnityEngine.UI.Image>(); return toggle;
        }
        private static void Stack(RectTransform root,int padding,int spacing)
        {
            var layout = root.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>(); layout.padding = new RectOffset(padding,padding,padding,padding); layout.spacing = spacing;
            layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandHeight = false;
        }
        private static void Anchor(RectTransform root,Vector2 min,Vector2 max,float padding = 0)
        {
            root.anchorMin = min; root.anchorMax = max; root.offsetMin = new Vector2(padding,padding); root.offsetMax = new Vector2(-padding,-padding);
        }
    }

    public sealed class RoomRow : MonoBehaviour, IPointerClickHandler
    {
        private TMP_Text roomName, population;
        private GameObject padlock;
        private Action<string,int> clicked;
        public RoomListing Listing { get; private set; }
        public void Initialize(TMP_Text name,TMP_Text count,GameObject icon,Action<string,int> callback)
        { roomName = name; population = count; padlock = icon; clicked = callback; }
        public void Set(RoomListing room)
        {
            Listing = room; roomName.text = room.Name; padlock.SetActive(room.IsPrivate);
            population.text = room.Players + " / " + room.Capacity + (room.IsFull ? "\n가득 참" : room.InProgress ? "\n게임 중" : "");
            population.color = room.IsFull ? new Color(1,0.55f,0.4f) : Color.white;
        }
        public void OnPointerClick(PointerEventData data)
        { if (data.button == PointerEventData.InputButton.Left && Listing != null) clicked?.Invoke(Listing.Id,data.clickCount); }
    }
}
