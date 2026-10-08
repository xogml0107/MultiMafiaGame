using System.Collections.Generic;
using PurrNet;
using PurrNet.Modules;
using UnityEngine;

namespace ZZabmongus.Networking
{
    public sealed class LobbyPlayerSpawner : PurrMonoBehaviour
    {
        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private NetworkLobby lobby;
        private readonly Dictionary<PlayerID, NetworkPlayer> spawned = new();

        public override void Subscribe(NetworkManager network, bool asServer)
        {
            if (!asServer) return;
            spawned.Clear();
            network.onPlayerLeft += PlayerLeft;
            if (!network.TryGetModule(out ScenePlayersModule players, true)) return;
            players.onPlayerLoadedScene += PlayerLoaded;
            if (network.TryGetModule(out ScenesModule scenes, true) &&
                scenes.TryGetSceneID(gameObject.scene, out var scene) &&
                players.TryGetPlayersInScene(scene, out var existing))
                foreach (var player in existing) PlayerLoaded(player, scene, true);
        }

        public override void Unsubscribe(NetworkManager network, bool asServer)
        {
            if (!asServer) return;
            network.onPlayerLeft -= PlayerLeft;
            if (network.TryGetModule(out ScenePlayersModule players, true)) players.onPlayerLoadedScene -= PlayerLoaded;
            spawned.Clear();
        }

        private void PlayerLoaded(PlayerID player, SceneID scene, bool asServer)
        {
            if (!asServer || !manager || !manager.TryGetModule(out ScenesModule scenes, true) ||
                !scenes.TryGetSceneID(gameObject.scene, out var current) || current != scene || spawned.ContainsKey(player)) return;
            if (lobby.InSession || spawned.Count >= NetworkLobby.MaxPlayers)
            {
                manager.playerModule.KickPlayer(player);
                return;
            }
            var slot = 0;
            while (slot < NetworkLobby.MaxPlayers && IsSlotUsed(slot)) slot++;
            var position = new Vector3((slot % 4 - 1.5f) * 2, 0, (slot / 4 - 1) * 2);
            var instance = UnityProxy.Instantiate(playerPrefab, position, Quaternion.identity, gameObject.scene);
            var avatar = instance.GetComponent<NetworkPlayer>();
            avatar.InitializeSlot(slot);
            if (!avatar.IsSpawned(true)) manager.Spawn(instance);
            avatar.GiveOwnership(player);
            spawned.Add(player, avatar);
        }

        private bool IsSlotUsed(int slot)
        {
            foreach (var avatar in spawned.Values) if (avatar && avatar.Slot == slot) return true;
            return false;
        }

        private void PlayerLeft(PlayerID player, bool asServer)
        {
            if (!asServer || !spawned.Remove(player, out var avatar)) return;
            // PurrNet also despawns on owner disconnect; the guard handles either callback order.
            if (avatar && avatar.isSpawned) UnityProxy.Destroy(avatar.gameObject);
        }
    }
}
