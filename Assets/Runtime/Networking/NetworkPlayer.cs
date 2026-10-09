using System;
using System.Text;
using PurrNet;
using PurrNet.Transports;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace ZZabmongus.Networking
{
    [RequireComponent(typeof(CharacterController), typeof(NetworkTransform))]
    public sealed partial class NetworkPlayer : NetworkBehaviour
    {
        public static readonly Color[] Palette = 
        {
            new(0.92f,0.22f,0.28f), new(0.22f,0.52f,0.95f), new(0.28f,0.8f,0.5f), new(1,0.8f,0.22f),
            new(0.73f,0.38f,0.93f), new(1,0.48f,0.72f), new(1,0.55f,0.2f), new(0.3f,0.88f,0.88f),
            new(0.88f,0.9f,0.94f), new(0.35f,0.38f,0.46f), new(0.57f,0.37f,0.25f), new(0.65f,0.87f,0.23f)
        };
        [SerializeField] private Renderer body;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private float speed = 4.5f;
        private readonly SyncVar<int> slot = new(0);
        private readonly SyncVar<int> color = new(0);
        private readonly SyncVar<string> displayName = new("Player");
        private readonly SyncVar<bool> ready = new(false);
        public static NetworkPlayer Local { get; private set; }
        public int Slot => slot.value;
        public int ColorIndex => color.value;
        public string DisplayName => displayName.value;
        public bool Ready => ready.value;
        private CharacterController controller;
        private NetworkLobby lobby;
        private MaterialPropertyBlock properties;
        private Vector2 serverInput;
        private float nextSend, lastInputAt, nextProfileAt, nextReadyAt;
        private uint inputSequence, lastSequence;
        private bool receivedInput, profileSent;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public Vector2? ProbeInput { get; set; }
#endif

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            properties = new MaterialPropertyBlock();
            controller.enabled = false;
        }

        public void InitializeSlot(int value)
        {
            slot.value = value;
            color.value = value;
            displayName.value = "Player " + (value + 1);
        }

        protected override void OnSpawned()
        {
            controller.enabled = isServer;
            lobby = FindFirstObjectByType<NetworkLobby>();
        }

        protected override void OnDespawned()
        {
            ClearMatchInfo();
            if (Local == this) Local = null;
            controller.enabled = false;
            profileSent = false;
            serverInput = Vector2.zero;
            receivedInput = false;
        }

        private void Update()
        {
            if (!isSpawned) return;
            if (!lobby) lobby = FindFirstObjectByType<NetworkLobby>();
            if (isOwner)
            {
                Local = this;
                if (!profileSent && lobby && !lobby.InSession && !lobby.Loading)
                {
                    profileSent = true;
                    var connection = FindFirstObjectByType<MultiplayerConnection>();
                    SetProfile(connection ? connection.DisplayName : "Player", ColorIndex);
                }
                if (Time.unscaledTime >= nextSend)
                {
                    nextSend = Time.unscaledTime + 0.05f;
                    var input = SessionSceneFlow.Instance && !SessionSceneFlow.Instance.CanMove ? Vector2.zero : ReadInput();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    if (ProbeInput.HasValue) input = ProbeInput.Value;
#endif
                    SendInput(input, ++inputSequence);
                }
            }
            if (body)
            {
                properties.SetColor("_BaseColor", Palette[Mathf.Clamp(ColorIndex, 0, Palette.Length - 1)]);
                body.SetPropertyBlock(properties);
            }
            if (nameLabel)
            {
                nameLabel.text = DisplayName + (Ready ? " *" : "");
                if (Camera.main) nameLabel.transform.rotation = Camera.main.transform.rotation;
            }
        }

        private static Vector2 ReadInput()
        {
            var selected = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
            if (selected && selected.activeInHierarchy &&
                (selected.GetComponent<TMP_InputField>() || selected.GetComponentInParent<TMP_Dropdown>())) return Vector2.zero;
            var keyboard = Keyboard.current;
            if (keyboard == null) return Vector2.zero;

            return Vector2.ClampMagnitude(new Vector2(
                (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1 : 0),
                (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1 : 0)), 1);
        }

        [ServerRpc(channel: Channel.Unreliable)]
        private void SendInput(Vector2 input, uint sequence)
        {
            if (float.IsNaN(input.x) || float.IsNaN(input.y) || float.IsInfinity(input.x) || float.IsInfinity(input.y)) return;
            // Ignore old/out-of-order UDP input, including across uint wraparound.
            if (receivedInput && unchecked((int)(sequence - lastSequence)) <= 0) return;
            lastSequence = sequence;
            receivedInput = true;
            serverInput = Vector2.ClampMagnitude(input, 1);
            lastInputAt = Time.unscaledTime;
        }

        private void FixedUpdate()
        {
            if (!isServer || !controller.enabled) return;
            if (SessionSceneFlow.Instance && !SessionSceneFlow.Instance.CanMove) return;
            var input = Time.unscaledTime - lastInputAt < 0.25f ? serverInput : Vector2.zero;
            controller.Move(new Vector3(input.x * speed, -2, input.y * speed) * Time.fixedDeltaTime);
        }

        internal void PlaceAt(Vector3 position)
        {
            if (!isServer) return;
            controller.enabled = false;
            serverInput = Vector2.zero; lastInputAt = -1;
            transform.position = position;
            var sync = GetComponent<NetworkTransform>();
            sync.ClearInterpolation(position, null, null); sync.ForceSync();
            controller.enabled = true;
        }

        public void SetProfile(string name, int requestedColor)
        {
            if (isOwner) RequestProfile(name, requestedColor);
        }

        [ServerRpc]
        private void RequestProfile(string name, int requestedColor)
        {
            if ((lobby && (lobby.InSession || lobby.Loading)) || Time.unscaledTime < nextProfileAt) return;
            nextProfileAt = Time.unscaledTime + 0.3f;
            displayName.value = SanitizeName(name);
            requestedColor = Mathf.Clamp(requestedColor, 0, Palette.Length - 1);
            if (lobby)
                foreach (var other in lobby.Players)
                    if (other != this && other.ColorIndex == requestedColor) return;
            color.value = requestedColor;
        }

        public static string SanitizeName(string value)
        {
            var clean = new StringBuilder();
            foreach (var c in value ?? "")
            {
                if (clean.Length >= 16) break;
                if (!char.IsControl(c) && c != '<' && c != '>' && !char.IsSurrogate(c)) clean.Append(c);
            }
            var result = clean.ToString().Trim();
            return string.IsNullOrEmpty(result) ? "Player" : result;
        }

        public void ToggleReady() { if (isOwner) RequestReady(!Ready); }
        [ServerRpc]
        private void RequestReady(bool value)
        {
            if ((lobby && (lobby.InSession || lobby.Loading)) || Time.unscaledTime < nextReadyAt) return;
            nextReadyAt = Time.unscaledTime + 0.2f;
            ready.value = value;
        }
        public void ClearReady() { if (isServer) ready.value = false; }
    }
}
