using System;
using PurrNet;
using PurrNet.Steam;
using PurrNet.Transports;
using Steamworks;
using UnityEngine;
using ZZabmongus.Core.Rooms;

namespace ZZabmongus.Networking
{
    public enum ConnectionMode { Lan, Steam }

    /// <summary>Explicit connection controls. Steam is initialized only when selected.</summary>
    [DefaultExecutionOrder(-50)]
    public sealed class MultiplayerConnection : MonoBehaviour
    {
        [SerializeField] private NetworkManager network;
        [SerializeField] private UDPTransport udp;
        [SerializeField] private SteamTransport steam;
        public NetworkManager Network => network;
        public RoomAuthenticator Authentication { get; private set; }
        public bool SteamReady => steamInitialized;
        public event Action<string> Failed;
        public string DisplayName { get; set; } = "Player";
        public ConnectionMode Mode { get; set; }
        public string Address { get; set; } = "127.0.0.1";
        public ushort Port { get; set; } = 5000;
        public string Message { get; private set; } = "방을 만들거나 목록에서 입장하세요.";
        public string SteamId => steamInitialized ? SteamUser.GetSteamID().ToString() : "";
        public bool Busy => network.serverState != ConnectionState.Disconnected || network.clientState != ConnectionState.Disconnected;
        public bool Connected => network.clientState == ConnectionState.Connected;
        public bool Hosting => network.serverState == ConnectionState.Connected;
        private bool steamInitialized, requestedHost, connecting, denied;
        private RoomDenial denialReason;
        private float deadline;
        private ITransport admissionTransport;

        private void Awake()
        {
            Authentication = GetComponent<RoomAuthenticator>();
            if (!Authentication) Authentication = gameObject.AddComponent<RoomAuthenticator>();
            network.authenticator = Authentication;
            Authentication.Rejected += OnRejected;
            network.onServerConnectionState += ServerState;
            network.onClientConnectionState += ClientState;
            Application.runInBackground = true;
        }

        public void Host()
        {
            if (Busy) return;
            Authentication.ConfigureHost("direct", new RoomCreation("Local Room", "KR", true, ""));
            Connect(true);
        }
        public void Join()
        {
            if (Busy) return;
            Authentication.ConfigureClient("direct", "");
            Connect(false);
        }
        public void HostRoom(string roomId, RoomCreation settings)
        {
            if (Busy) return;
            Authentication.ConfigureHost(roomId, settings);
            Connect(true);
        }
        public void JoinRoom(RoomListing room, string password)
        {
            if (Busy) return;
            Authentication.ConfigureClient(room.Id, password);
            Mode = ConnectionMode.Steam; Address = room.Host.ToString(); Port = room.Port;
            Connect(false);
        }

        private void Connect(bool host)
        {
            if (Busy) return;
            denied = false;
            if (Port == 0) { ReportFailure("포트를 1~65535 사이로 설정해 주세요."); return; }
            if (!host && string.IsNullOrWhiteSpace(Address)) { ReportFailure("호스트 주소를 입력해 주세요."); return; }
            try
            {
                if (Mode == ConnectionMode.Steam)
                {
                    if (!host && (!ulong.TryParse(Address.Trim(), out var target) || target == 0))
                    { ReportFailure("호스트 Steam ID가 올바르지 않습니다."); return; }
                    if (!EnsureSteam()) return;
                    steam.peerToPeer = true;
                    steam.dedicatedServer = false;
                    steam.serverPort = Port;
                    steam.address = host ? SteamId : Address.Trim();
                    network.transport = steam;
                }
                else
                {
                    udp.address = host ? "127.0.0.1" : Address.Trim();
                    udp.serverPort = Port;
                    // Keep an extra admission lane so full rooms can return a typed Full denial.
                    udp.maxConnections = 32;
                    network.transport = udp;
                }
                if (admissionTransport != null) admissionTransport.onDisconnected -= ReleaseAdmission;
                admissionTransport = network.transport.transport;
                admissionTransport.onDisconnected += ReleaseAdmission;
                requestedHost = host;
                connecting = true;
                deadline = Time.unscaledTime + 12;
                Message = host ? "방을 준비하는 중..." : "연결하는 중...";
                if (host) network.StartHost(); else network.StartClient();
            }
            catch (Exception error)
            {
                Leave();
                ReportFailure("연결에 실패했습니다: " + error.Message);
            }
        }

        public void Leave()
        {
            requestedHost = connecting = false;
            if (Authentication.Admission != null) Authentication.Admission.Closed = true;
            Authentication.ClearClientPassword();
            network.StopClient();
            network.StopServer();
            Message = "방에서 나왔습니다.";
        }

        public bool EnsureSteam()
        {
            if (steamInitialized) return true;
            try
            {
                var result = SteamAPI.InitEx(out var reason);
                if (result != ESteamAPIInitResult.k_ESteamAPIInitResult_OK)
                {
                    ReportFailure("Steam에 연결할 수 없습니다. Steam 실행과 App ID 설정을 확인해 주세요.");
                    return false;
                }
                steamInitialized = true;
                return true;
            }
            catch (Exception error)
            {
                ReportFailure("Steam 초기화에 실패했습니다. Steam 실행과 App ID 설정을 확인해 주세요.");
                return false;
            }
        }

        private void Update()
        {
            if (steamInitialized) SteamAPI.RunCallbacks();
            if (denied)
            {
                denied = false;
                var reason = denialReason;
                Leave();
                ReportFailure(RoomMessages.For(reason));
                return;
            }
            if (!connecting || Time.unscaledTime < deadline) return;
            Leave();
            ReportFailure("연결 시간이 초과되었습니다. 방 목록을 새로고침해 주세요.");
        }

        private void ClientState(ConnectionState state)
        {
            if (state == ConnectionState.Connected)
            {
                Message = requestedHost ? "방을 생성했습니다. 참가자를 기다리는 중입니다." : "방에 입장했습니다.";
            }
            else if (state == ConnectionState.Disconnected && !connecting)
            {
                Message = "방 연결이 종료되었습니다.";
                if (requestedHost) Leave();
            }
        }

        private void ServerState(ConnectionState state)
        {
            if (state == ConnectionState.Disconnected && requestedHost)
            {
                Leave();
                Message = "방장이 나가서 방이 종료되었습니다.";
            }
        }

        private void OnRejected(RoomDenial reason) { denialReason = reason; denied = true; }
        private void ReleaseAdmission(Connection conn, DisconnectReason reason, bool asServer)
        {
            if (asServer) Authentication.Admission?.Release(conn.connectionId);
        }
        private void ReportFailure(string message) { Message = message; Failed?.Invoke(message); }
        private void LateUpdate()
        {
            if (connecting && network.isLocalPlayerReady && NetworkPlayer.Local)
            {
                connecting = false;
                Authentication.ClearClientPassword();
            }
        }

        private void OnDestroy()
        {
            if (admissionTransport != null) admissionTransport.onDisconnected -= ReleaseAdmission;
            if (Authentication) Authentication.Rejected -= OnRejected;
            if (network)
            {
                network.onServerConnectionState -= ServerState;
                network.onClientConnectionState -= ClientState;
            }
            if (steamInitialized)
            {
                var directory = GetComponent<SteamRoomDirectory>();
                if (directory) directory.CloseForShutdown();
                steamInitialized = false;
                SteamAPI.Shutdown();
            }
        }
    }
}
