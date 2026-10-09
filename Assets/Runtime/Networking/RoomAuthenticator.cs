using System;
using System.Threading.Tasks;
using PurrNet;
using PurrNet.Authentication;
using PurrNet.Packing;
using PurrNet.Transports;
using UnityEngine;
using ZZabmongus.Core.Rooms;

namespace ZZabmongus.Networking
{
    public struct RoomJoinPayload : IPackedAuto
    {
        public int protocol;
        public string roomId;
        public string password;
    }

    public sealed class RoomAuthenticator : AuthenticationBehaviour<RoomJoinPayload, RoomDenial>
    {
        public const int Protocol = 3;
        private RoomJoinPayload clientPayload;
        private RoomAdmission admission;
        private NetworkLobby lobby;
        public RoomAdmission Admission => admission;
        public event Action<RoomDenial> Rejected;

        public void ConfigureHost(string roomId, RoomCreation settings)
        {
            admission = new RoomAdmission(roomId, settings);
            ConfigureClient(roomId, settings.Password);
        }
        public void ConfigureClient(string roomId, string password)
        {
            clientPayload = new RoomJoinPayload { protocol = Protocol, roomId = roomId, password = password ?? "" };
        }
        public void ClearClientPassword() => clientPayload.password = "";
        protected override Task<AuthenticationRequest<RoomJoinPayload>> GetClientPayload() =>
            Task.FromResult(new AuthenticationRequest<RoomJoinPayload>(clientPayload));
        protected override Task<AuthenticationResponse<RoomDenial>> ValidateClientPayload(Connection conn, RoomJoinPayload payload)
        {
            if (!lobby) lobby = FindFirstObjectByType<NetworkLobby>();
            if (admission != null) admission.InProgress = (lobby && (lobby.InSession || lobby.Loading)) ||
                (SessionSceneFlow.Instance && SessionSceneFlow.Instance.Transitioning);
            var result = payload.protocol != Protocol ? RoomDenial.InvalidRequest :
                admission == null ? RoomDenial.Closed : admission.TryAdmit(conn.connectionId, payload.roomId, payload.password);
            return Task.FromResult(result == RoomDenial.None ? AuthenticationResponse<RoomDenial>.Accept() : AuthenticationResponse<RoomDenial>.Deny(result));
        }
        protected override void UnAuthenticateClient(Connection conn) => admission?.Release(conn.connectionId);
        protected override void OnDeniedByServer(DenialKind kind, RoomDenial? reason) => Rejected?.Invoke(reason ?? RoomDenial.Closed);
    }
}
