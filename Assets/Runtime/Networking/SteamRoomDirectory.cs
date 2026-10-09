using System;
using System.Collections.Generic;
using System.Linq;
using Steamworks;
using UnityEngine;
using ZZabmongus.Core.Rooms;

namespace ZZabmongus.Networking
{
    /// <summary>Steam is the directory; PurrNet's authenticator owns gameplay admission.</summary>
    [RequireComponent(typeof(MultiplayerConnection))]
    public sealed class SteamRoomDirectory : MonoBehaviour, IRoomDirectory
    {
        public const string GameKey = "mmg_game";
        public const string GameValue = "MultiMafiaGame";
        public const string ProtocolKey = "mmg_protocol";
        public const string CountryKey = "mmg_country";
        private const string ReadyKey = "mmg_ready";
        private enum Operation { None, Create, RefreshSelected, Join, Connect }
        private readonly List<RoomListing> rooms = new();
        private MultiplayerConnection connection;
        private NetworkLobby lobby;
        private CallResult<LobbyMatchList_t> searchResult;
        private CallResult<LobbyCreated_t> createResult;
        private CallResult<LobbyEnter_t> enterResult;
        private Callback<LobbyDataUpdate_t> dataUpdated;
        private Callback<LobbyChatUpdate_t> membersChanged;
        private Operation operation;
        private bool searching, ownsLobby;
        private string country = "KR", password, lastPublished;
        private RoomCreation creation;
        private RoomListing requested;
        private float operationDeadline, searchDeadline;
        public ulong CurrentLobby { get; private set; }
        public IReadOnlyList<RoomListing> Rooms => rooms;
        public bool IsBusy => searching || operation != Operation.None || (connection && connection.Busy);
        public string Status { get; private set; } = "서버를 선택하고 방 목록을 확인하세요.";
        public event Action Changed;
        public event Action<string> Failed;

        private void Awake()
        {
            connection = GetComponent<MultiplayerConnection>();
            lobby = FindFirstObjectByType<NetworkLobby>();
            connection.Failed += ConnectionFailed;
        }
        private bool Initialize()
        {
            if (!connection.EnsureSteam()) return false;
            if (searchResult != null) return true;
            searchResult = CallResult<LobbyMatchList_t>.Create((value, failed) => Safe(() => OnSearch(value, failed)));
            createResult = CallResult<LobbyCreated_t>.Create((value, failed) => Safe(() => OnCreated(value, failed)));
            enterResult = CallResult<LobbyEnter_t>.Create((value, failed) => Safe(() => OnEntered(value, failed)));
            dataUpdated = Callback<LobbyDataUpdate_t>.Create(value => Safe(() => OnData(value)));
            membersChanged = Callback<LobbyChatUpdate_t>.Create(value => Safe(() => OnMembers(value)));
            return true;
        }
        public void Refresh(string code) => Safe(() => RefreshInternal(code));
        private void RefreshInternal(string code)
        {
            if (IsBusy || !Countries.Contains(code)) return;
            country = code;
            rooms.Clear(); Changed?.Invoke();
            if (!Initialize()) return;
            searching = true; searchDeadline = Time.unscaledTime + 25;
            Status = "방 목록을 불러오는 중..."; Changed?.Invoke();
            SteamMatchmaking.AddRequestLobbyListStringFilter(GameKey, GameValue, ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListStringFilter(ProtocolKey, RoomAuthenticator.Protocol.ToString(), ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListStringFilter(CountryKey, country, ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListStringFilter(ReadyKey, "1", ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListDistanceFilter(ELobbyDistanceFilter.k_ELobbyDistanceFilterWorldwide);
            SteamMatchmaking.AddRequestLobbyListResultCountFilter(100);
            searchResult.Set(SteamMatchmaking.RequestLobbyList());
        }
        public void Create(RoomCreation request) => Safe(() => CreateInternal(request));
        private void CreateInternal(RoomCreation request)
        {
            if (IsBusy || !Initialize()) return;
            creation = request; country = request.Country;
            Begin(Operation.Create, "방을 생성하는 중...");
            // Steam hides physically full lobbies in searches. The directory has spare seats;
            // actual 4-12 player capacity is enforced atomically by the host authenticator.
            createResult.Set(SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, 250));
        }
        private void OnCreated(LobbyCreated_t result, bool failed)
        {
            if (operation != Operation.Create)
            {
                if (!failed && result.m_eResult == EResult.k_EResultOK) SteamMatchmaking.LeaveLobby(new CSteamID(result.m_ulSteamIDLobby));
                return;
            }
            if (failed || result.m_eResult != EResult.k_EResultOK) { Fail("방을 생성하지 못했습니다. 다시 시도해 주세요."); return; }
            CurrentLobby = result.m_ulSteamIDLobby; ownsLobby = true; lastPublished = "";
            var id = new CSteamID(CurrentLobby);
            var settings = creation;
            SetData(id, GameKey, GameValue); SetData(id, ProtocolKey, RoomAuthenticator.Protocol.ToString());
            SetData(id, CountryKey, settings.Country); SetData(id, "mmg_name", settings.Name);
            SetData(id, "mmg_locked", settings.IsPublic ? "0" : "1"); SetData(id, "mmg_capacity", settings.Capacity.ToString());
            SetData(id, "mmg_host", connection.SteamId); SetData(id, "mmg_port", connection.Port.ToString());
            SetData(id, "mmg_players", "0"); SetData(id, "mmg_started", "0"); SetData(id, ReadyKey, "0");
            SteamMatchmaking.SetLobbyJoinable(id, true);
            Begin(Operation.Connect, "방을 준비하는 중...");
            connection.Mode = ConnectionMode.Steam;
            connection.HostRoom(CurrentLobby.ToString(), settings);
            creation = null;
        }
        private void OnSearch(LobbyMatchList_t result, bool failed)
        {
            if (!searching) return;
            searching = false; rooms.Clear();
            if (failed) { Fail("방 목록을 불러오지 못했습니다. 다시 시도해 주세요."); return; }
            for (var i = 0; i < result.m_nLobbiesMatching; i++)
            {
                var room = Read(SteamMatchmaking.GetLobbyByIndex(i).m_SteamID);
                if (room != null && room.Country == country) rooms.Add(room);
            }
            rooms.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));
            Status = rooms.Count == 0 ? "현재 생성된 방이 없습니다." : "방을 더블 클릭하면 입장합니다.";
            Changed?.Invoke();
        }
        public void Join(RoomListing room, string suppliedPassword) => Safe(() => JoinInternal(room, suppliedPassword));
        private void JoinInternal(RoomListing room, string suppliedPassword)
        {
            if (IsBusy || !Initialize()) return;
            if (room.Country != country) { Fail(RoomMessages.For(RoomDenial.WrongRoom)); return; }
            if (room.IsFull) { Fail(RoomMessages.For(RoomDenial.Full)); return; }
            requested = room; password = suppliedPassword ?? "";
            Begin(Operation.RefreshSelected, "방 상태를 확인하는 중...");
            if (!ulong.TryParse(room.Id, out var id) || !SteamMatchmaking.RequestLobbyData(new CSteamID(id)))
                Fail(RoomMessages.For(RoomDenial.Closed));
        }
        private void OnData(LobbyDataUpdate_t data)
        {
            if (operation == Operation.RefreshSelected && requested != null && data.m_ulSteamIDLobby.ToString() == requested.Id)
            {
                if (data.m_bSuccess == 0) { Fail(RoomMessages.For(RoomDenial.Closed)); return; }
                var latest = Read(data.m_ulSteamIDLobby);
                if (!ValidateListing(latest)) return;
                requested = latest;
                Begin(Operation.Join, "방에 입장하는 중...");
                enterResult.Set(SteamMatchmaking.JoinLobby(new CSteamID(data.m_ulSteamIDLobby)));
                return;
            }
            if (CurrentLobby != 0 && data.m_ulSteamIDLobby == CurrentLobby && !ownsLobby)
            {
                var current = Read(CurrentLobby);
                if (current == null || SteamMatchmaking.GetLobbyOwner(new CSteamID(CurrentLobby)).m_SteamID != current.Host)
                    Fail("방장이 나가서 방이 종료되었습니다.");
            }
        }
        private bool ValidateListing(RoomListing room)
        {
            if (room == null || room.Country != country) { Fail(RoomMessages.For(RoomDenial.Closed)); return false; }
            if (room.IsFull) { Fail(RoomMessages.For(RoomDenial.Full)); return false; }
            if (room.InProgress) { Fail(RoomMessages.For(RoomDenial.InProgress)); return false; }
            return true;
        }
        private void OnEntered(LobbyEnter_t result, bool failed)
        {
            var id = new CSteamID(result.m_ulSteamIDLobby);
            var response = (EChatRoomEnterResponse)result.m_EChatRoomEnterResponse;
            if (operation != Operation.Join)
            {
                if (!failed && response == EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess) SteamMatchmaking.LeaveLobby(id);
                return;
            }
            if (failed || response != EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
            { Fail(RoomMessages.For(response == EChatRoomEnterResponse.k_EChatRoomEnterResponseFull ? RoomDenial.Full : RoomDenial.Closed)); return; }
            CurrentLobby = result.m_ulSteamIDLobby; ownsLobby = false;
            var latest = Read(CurrentLobby);
            if (!ValidateListing(latest)) return;
            if (SteamMatchmaking.GetLobbyOwner(id).m_SteamID != latest.Host) { Fail(RoomMessages.For(RoomDenial.Closed)); return; }
            Begin(Operation.Connect, "호스트에 연결하는 중...");
            connection.JoinRoom(latest, password);
            password = null;
        }
        private RoomListing Read(ulong value)
        {
            var id = new CSteamID(value);
            string Get(string key) => SteamMatchmaking.GetLobbyData(id, key);
            if (Get(GameKey) != GameValue || Get(ProtocolKey) != RoomAuthenticator.Protocol.ToString() || Get(ReadyKey) != "1") return null;
            var region = Get(CountryKey);
            if (!Countries.Contains(region) || !ulong.TryParse(Get("mmg_host"), out var host) || host == 0 ||
                !ushort.TryParse(Get("mmg_port"), out var port) || port == 0 ||
                !int.TryParse(Get("mmg_capacity"), out var capacity) || capacity < 4 || capacity > 12 ||
                !int.TryParse(Get("mmg_players"), out var count) || count < 0) return null;
            return new RoomListing(value.ToString(), Get("mmg_name"), region, host, port, Get("mmg_locked") == "1", count, capacity, Get("mmg_started") == "1");
        }
        private void Update() => Safe(Tick);
        private void Tick()
        {
            if (!lobby) lobby = FindFirstObjectByType<NetworkLobby>();
            if (searching && Time.unscaledTime > searchDeadline) { searching = false; searchResult.Cancel(); Fail("방 목록 요청 시간이 초과되었습니다."); }
            if (operation != Operation.None && Time.unscaledTime > operationDeadline) { Fail("방 연결 시간이 초과되었습니다."); return; }
            if (CurrentLobby == 0) return;
            if (operation == Operation.Connect && NetworkPlayer.Local && connection.Network.isLocalPlayerReady)
            {
                operation = Operation.None; Status = "방에 입장했습니다."; Changed?.Invoke();
            }
            if (operation == Operation.None && !connection.Busy) { CloseLobby(); Changed?.Invoke(); return; }
            if (!ownsLobby || !connection.Hosting) return;
            var admission = connection.Authentication.Admission;
            if (admission == null) return;
            var started = lobby && (lobby.InSession || lobby.Loading);
            var ready = NetworkPlayer.Local && connection.Network.isLocalPlayerReady;
            var signature = admission.Count + ":" + started + ":" + ready;
            if (signature == lastPublished) return;
            lastPublished = signature;
            var id = new CSteamID(CurrentLobby);
            SetData(id, "mmg_players", admission.Count.ToString()); SetData(id, "mmg_started", started ? "1" : "0");
            SetData(id, ReadyKey, ready ? "1" : "0");
        }
        private void OnMembers(LobbyChatUpdate_t data)
        {
            if (CurrentLobby == 0 || data.m_ulSteamIDLobby != CurrentLobby || ownsLobby) return;
            var host = SteamMatchmaking.GetLobbyData(new CSteamID(CurrentLobby), "mmg_host");
            if (SteamMatchmaking.GetLobbyOwner(new CSteamID(CurrentLobby)).ToString() != host) Fail("방장이 나가서 방이 종료되었습니다.");
        }
        private void Begin(Operation next, string status) { operation = next; operationDeadline = Time.unscaledTime + 25; Status = status; Changed?.Invoke(); }
        private void SetData(CSteamID id, string key, string value)
        {
            if (!SteamMatchmaking.SetLobbyData(id, key, value)) throw new InvalidOperationException("Steam lobby metadata could not be set: " + key);
        }
        private void ConnectionFailed(string message) => Fail(message);
        private void Safe(Action action)
        {
            try { action(); }
            catch (Exception) { Fail("Steam 연결을 처리하지 못했습니다. 다시 시도해 주세요."); }
        }
        private void Fail(string message)
        {
            if (searching) { searching = false; searchResult?.Cancel(); }
            operation = Operation.None; creation = null; requested = null; password = null;
            CloseLobby();
            if (connection.Busy) connection.Leave();
            Status = message; Changed?.Invoke(); Failed?.Invoke(message);
        }
        private void CloseLobby()
        {
            if (CurrentLobby != 0 && connection.SteamReady)
            {
                var id = new CSteamID(CurrentLobby);
                if (ownsLobby) { SteamMatchmaking.SetLobbyData(id, ReadyKey, "0"); SteamMatchmaking.SetLobbyJoinable(id, false); }
                SteamMatchmaking.LeaveLobby(id);
            }
            CurrentLobby = 0; ownsLobby = false; lastPublished = "";
        }
        public void CloseForShutdown() => CloseLobby();
        private void OnDestroy()
        {
            if (connection) { connection.Failed -= ConnectionFailed; CloseLobby(); }
            searchResult?.Dispose(); createResult?.Dispose(); enterResult?.Dispose(); dataUpdated?.Dispose(); membersChanged?.Dispose();
        }
    }
}
