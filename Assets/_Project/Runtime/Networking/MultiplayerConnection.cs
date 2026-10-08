using System;
using PurrNet;
using PurrNet.Steam;
using PurrNet.Transports;
using Steamworks;
using UnityEngine;

namespace ZZabmongus.Networking
{
    public enum ConnectionMode { Lan, Steam }

    /// <summary>Explicit connection controls. Steam is initialized only when selected.</summary>
    public sealed class MultiplayerConnection : MonoBehaviour
    {
        [SerializeField] private NetworkManager network;
        [SerializeField] private UDPTransport udp;
        [SerializeField] private SteamTransport steam;
        public NetworkManager Network => network;
        public string DisplayName { get; set; } = "Player";
        public ConnectionMode Mode { get; set; }
        public string Address { get; set; } = "127.0.0.1";
        public ushort Port { get; set; } = 5000;
        public string Message { get; private set; } = "Create a room or join a host.";
        public string SteamId => steamInitialized ? SteamUser.GetSteamID().ToString() : "";
        public bool Busy => network.serverState != ConnectionState.Disconnected || network.clientState != ConnectionState.Disconnected;
        public bool Connected => network.clientState == ConnectionState.Connected;
        public bool Hosting => network.serverState == ConnectionState.Connected;
        private bool steamInitialized, requestedHost, connecting;
        private float deadline;

        private void Awake()
        {
            network.onServerConnectionState += ServerState;
            network.onClientConnectionState += ClientState;
            Application.runInBackground = true;
        }

        public void Host() => Connect(true);
        public void Join() => Connect(false);

        private void Connect(bool host)
        {
            if (Busy) return;
            if (Port == 0) { Message = "Choose a port between 1 and 65535."; return; }
            if (!host && string.IsNullOrWhiteSpace(Address)) { Message = "Enter a host address."; return; }
            try
            {
                if (Mode == ConnectionMode.Steam)
                {
                    if (!host && (!ulong.TryParse(Address.Trim(), out var target) || target == 0))
                    { Message = "Enter the host's Steam ID."; return; }
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
                    udp.maxConnections = NetworkLobby.MaxPlayers;
                    network.transport = udp;
                }
                requestedHost = host;
                connecting = true;
                deadline = Time.unscaledTime + 12;
                Message = host ? "Creating room..." : "Connecting...";
                if (host) network.StartHost(); else network.StartClient();
            }
            catch (Exception error)
            {
                Leave();
                Message = "Connection failed: " + error.Message;
                Debug.LogException(error);
            }
        }

        public void Leave()
        {
            requestedHost = connecting = false;
            network.StopClient();
            network.StopServer();
            Message = "Disconnected. Create or join a room.";
        }

        private bool EnsureSteam()
        {
            if (steamInitialized) return true;
            try
            {
                var result = SteamAPI.InitEx(out var reason);
                if (result != ESteamAPIInitResult.k_ESteamAPIInitResult_OK)
                {
                    Message = "Steam unavailable. Run Steam and configure your game's App ID. " + reason;
                    return false;
                }
                steamInitialized = true;
                return true;
            }
            catch (Exception error)
            {
                Message = "Steam unavailable: " + error.Message;
                return false;
            }
        }

        private void Update()
        {
            if (steamInitialized) SteamAPI.RunCallbacks();
            if (!connecting || Time.unscaledTime < deadline) return;
            Leave();
            Message = "Connection timed out. Check the host address and port.";
        }

        private void ClientState(ConnectionState state)
        {
            if (state == ConnectionState.Connected)
            {
                connecting = false;
                Message = requestedHost ? "Room created. Waiting for players." : "Joined room.";
            }
            else if (state == ConnectionState.Disconnected && !connecting)
            {
                Message = "Room connection closed.";
                if (requestedHost) Leave();
            }
        }

        private void ServerState(ConnectionState state)
        {
            if (state == ConnectionState.Disconnected && requestedHost)
            {
                Leave();
                Message = "Host stopped. Room closed.";
            }
        }

        private void OnDestroy()
        {
            if (network)
            {
                network.onServerConnectionState -= ServerState;
                network.onClientConnectionState -= ClientState;
            }
            if (steamInitialized) SteamAPI.Shutdown();
        }
    }
}
