using System.Linq;
using PurrNet;
using UnityEngine;

namespace ZZabmongus.Networking
{
    public sealed class NetworkLobby : NetworkBehaviour
    {
        public const int MinPlayers = 4;
        public const int MaxPlayers = 12;
        private readonly SyncVar<bool> inSession = new(false);
        private readonly SyncVar<int> capacity = new(MaxPlayers);
        public int Capacity => capacity.value;
        public bool InSession => isSpawned && inSession.value;
        public NetworkPlayer[] Players => FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None)
            .Where(p => p.isSpawned).OrderBy(p => p.Slot).ToArray();
        public bool CanStart => isServer && !InSession && Players.Length >= MinPlayers &&
                                Players.Length <= Capacity && Players.All(p => p.Ready);

        protected override void OnSpawned(bool asServer)
        {
            if (!asServer) return;
            inSession.value = false;
            var connection = FindFirstObjectByType<MultiplayerConnection>();
            capacity.value = connection && connection.Authentication.Admission != null ? connection.Authentication.Admission.Capacity : MaxPlayers;
        }

        // Called by local host UI only; clients cannot RPC this method.
        public void StartSession()
        {
            if (CanStart) inSession.value = true;
        }

        public void ReturnToLobby()
        {
            if (!isServer || !InSession) return;
            inSession.value = false;
            foreach (var player in Players) player.ClearReady();
        }
    }
}
