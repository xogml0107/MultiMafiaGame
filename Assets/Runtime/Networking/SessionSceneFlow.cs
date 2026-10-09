using System;
using System.Collections.Generic;
using System.Linq;
using PurrNet;
using PurrNet.Modules;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using ZZabmongus.Core;

namespace ZZabmongus.Networking
{
    /// <summary>Persistent transport lifetime; server-owned public scene changes.</summary>
    [DefaultExecutionOrder(-1100)]
    public sealed class SessionSceneFlow : MonoBehaviour
    {
        public const string LobbyScene = "LobbyScene", WaitingScene = "WaitingRoomScene", GameScene = "GameScene";
        public static SessionSceneFlow Instance { get; private set; }
        [SerializeField] private MultiplayerConnection connection;
        [SerializeField] private GameObject roomStatePrefab;
        public MultiplayerConnection Connection => connection;
        private NetworkLobby lobby;
        public NetworkLobby Lobby => lobby ? lobby : lobby = FindFirstObjectByType<NetworkLobby>();
        private SceneLoadBarrier barrier;
        private SceneID targetId;
        private string targetScene;
        private AsyncOperation sceneOperation, offlineOperation;
        private readonly List<AsyncOperation> networkOperations = new();
        private bool wasBusy, returnOffline, cancelGame;
        private GameObject loadingCanvas;
        private TMP_Text loadingText;
        public bool Transitioning => barrier != null || returnOffline || (sceneOperation != null && !sceneOperation.isDone);
        public bool CanMove => connection.Connected && Lobby && !Lobby.Loading && !Transitioning &&
                               (SceneManager.GetActiveScene().name == WaitingScene || SceneManager.GetActiveScene().name == GameScene);

        private void Awake()
        {
            Instance = this;
            // NetworkManager records the original scene, then moves this entire root to DDOL.
        }

        private void Start()
        {
            connection.Network.onPlayerLeft += PlayerLeft;
            BuildLoadingOverlay();
        }

        public bool BeginGame()
        {
            if (!connection.Hosting || Transitioning || SceneManager.GetActiveScene().name != WaitingScene) return false;
            return ChangeScene(GameScene);
        }

        public void ReturnToWaiting()
        {
            if (!connection.Hosting) return;
            if (barrier != null) { cancelGame = true; return; }
            ChangeScene(WaitingScene);
        }

        private bool ChangeScene(string name)
        {
            var state = Lobby;
            var network = connection.Network;
            if (!state || !state.isServer || !network.TryGetModule(out ScenesModule scenes, true)) return false;
            var ids = state.Players.Where(p => p.owner.HasValue).Select(p => p.owner.Value.ToString()).ToArray();
            if (ids.Length == 0) return false;
            state.SetLoading(true);
            try
            {
                sceneOperation = scenes.LoadSceneAsync(name, LoadSceneMode.Single);
                if (sceneOperation == null) throw new InvalidOperationException("Scene load did not start.");
                targetId = scenes.lastSceneId; targetScene = name; cancelGame = false;
                barrier = new SceneLoadBarrier(targetId.id, ids, Time.unscaledTimeAsDouble, 45);
                networkOperations.Add(sceneOperation);
                return true;
            }
            catch (Exception error)
            {
                Debug.LogError("Session scene transition failed: " + error.Message);
                connection.Leave();
                return false;
            }
        }

        // Snapshot async handles before the modules are disposed, then return offline after they finish.
        public void PrepareLeave()
        {
            RememberOperations(true); RememberOperations(false);
            barrier = null; returnOffline = true;
        }

        private void RememberOperations(bool asServer)
        {
            if (!connection.Network.TryGetModule(out ScenesModule scenes, asServer)) return;
            foreach (var pending in scenes.GetPendingOperations())
                if (pending.operation != null && !networkOperations.Contains(pending.operation)) networkOperations.Add(pending.operation);
        }

        private void Update()
        {
            if (connection.Busy)
            {
                RememberOperations(true); RememberOperations(false);
                // The public room object is spawned in DDOL, never attached to a disposable map.
                if (connection.Hosting && !Lobby && connection.Network.TryGetModule(out ScenesModule scenes, true) &&
                    scenes.TryGetSceneID(gameObject.scene, out _))
                {
                    var state = UnityProxy.Instantiate(roomStatePrefab, Vector3.zero, Quaternion.identity, gameObject.scene);
                    connection.Network.Spawn(state);
                }
            }
            if (wasBusy && !connection.Busy) { barrier = null; returnOffline = true; }
            wasBusy = connection.Busy;
            if (returnOffline && !connection.Busy && networkOperations.All(op => op.isDone))
            {
                if (offlineOperation == null && SceneManager.GetActiveScene().name != LobbyScene)
                    offlineOperation = SceneManager.LoadSceneAsync(LobbyScene);
                if (offlineOperation == null || offlineOperation.isDone)
                {
                    returnOffline = false; offlineOperation = null; sceneOperation = null; networkOperations.Clear();
                }
            }
            if (!returnOffline && connection.Hosting && barrier == null && Lobby && Lobby.isSpawned &&
                NetworkPlayer.Local && connection.Network.isLocalPlayerReady && SceneManager.GetActiveScene().name == LobbyScene)
                ChangeScene(WaitingScene);
            PollBarrier();
            var loading = Transitioning || (Lobby && Lobby.Loading) || (connection.Busy && !NetworkPlayer.Local);
            if (loadingCanvas)
            {
                loadingCanvas.SetActive(loading);
                loadingText.text = returnOffline ? "로비로 돌아가는 중..." : "씬을 불러오는 중...\n참가자들의 준비를 기다리고 있습니다.";
            }
        }

        private void PollBarrier()
        {
            if (barrier == null || !connection.Hosting) return;
            if (connection.Network.TryGetModule(out ScenePlayersModule players, true) && players.TryGetPlayersInScene(targetId, out var loaded))
                foreach (var player in loaded) barrier.Acknowledge(targetId.id, player.ToString());
            if (barrier.TimedOut(Time.unscaledTimeAsDouble)) { connection.Leave(); return; }
            if (!barrier.Complete || sceneOperation == null || !sceneOperation.isDone) return;
            var changed = cancelGame || barrier.RosterChanged;
            var arrived = targetScene;
            barrier = null; sceneOperation = null;
            var entry = FindFirstObjectByType<SessionSceneEntry>();
            if (!entry || entry.gameObject.scene.name != arrived) { connection.Leave(); return; }
            foreach (var player in Lobby.Players) player.PlaceAt(entry.SpawnPosition(player.Slot));
            if (arrived == GameScene && changed) { ChangeScene(WaitingScene); return; }
            Lobby.SetLoading(false);
            if (arrived == GameScene) Lobby.StartLoadedMatch();
        }

        private void PlayerLeft(PlayerID player, bool asServer)
        {
            if (asServer) barrier?.Remove(player.ToString());
        }

        private void BuildLoadingOverlay()
        {
            loadingCanvas = new GameObject("SessionLoadingCanvas", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            loadingCanvas.transform.SetParent(transform, false);
            var canvas = loadingCanvas.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 600;
            var scaler = loadingCanvas.GetComponent<UnityEngine.UI.CanvasScaler>(); scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600,1000);
            var background = new GameObject("LoadingOverlay", typeof(RectTransform), typeof(UnityEngine.UI.Image)); background.transform.SetParent(loadingCanvas.transform, false);
            var rect = (RectTransform)background.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            background.GetComponent<UnityEngine.UI.Image>().color = new Color(0.025f,0.035f,0.065f,0.97f);
            var label = new GameObject("LoadingMessage", typeof(RectTransform), typeof(TextMeshProUGUI)); label.transform.SetParent(background.transform, false);
            rect = (RectTransform)label.transform; rect.anchorMin = new Vector2(0.1f,0.3f); rect.anchorMax = new Vector2(0.9f,0.7f); rect.offsetMin = rect.offsetMax = Vector2.zero;
            loadingText = label.GetComponent<TextMeshProUGUI>(); loadingText.font = Resources.Load<TMP_FontAsset>("UI/LobbyFont") ?? TMP_Settings.defaultFontAsset;
            loadingText.fontSize = 30; loadingText.alignment = TextAlignmentOptions.Center; loadingText.raycastTarget = false;
            loadingCanvas.SetActive(false);
        }

        private void OnDestroy()
        {
            if (connection && connection.Network) connection.Network.onPlayerLeft -= PlayerLeft;
            if (Instance == this) Instance = null;
        }
    }
}
