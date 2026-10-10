using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using ZZabmongus.Core;

namespace ZZabmongus.Networking
{
    /// <summary>Identical map metadata on each peer; use is authorized by the server.</summary>
    public sealed class InformationDeviceStation : MonoBehaviour
    {
        [SerializeField] private int deviceId = 1;
        public int DeviceId => deviceId;
        public DeviceKind Kind => DeviceKind.Parity;
        public const float UseDistance = 2.5f;
        public string DisplayName => "홀짝 검사";
        public HostedDevice ServerDefinition => new(DeviceId, Kind, new Position(transform.position.x, transform.position.z));

        public static InformationDeviceStation[] ActiveStations() => FindObjectsByType<InformationDeviceStation>(FindObjectsSortMode.None)
            .Where(s => s.isActiveAndEnabled && s.gameObject.scene == SceneManager.GetActiveScene() && s.gameObject.scene.name == SessionSceneFlow.GameScene).ToArray();

        public static InformationDeviceStation Nearest(Vector3 playerPosition) => ActiveStations()
            .Where(s => (s.transform.position - playerPosition).sqrMagnitude <= UseDistance * UseDistance)
            .OrderBy(s => (s.transform.position - playerPosition).sqrMagnitude).ThenBy(s => s.DeviceId).FirstOrDefault();

        public bool HasClearPath(NetworkPlayer player)
        {
            var start = player.transform.position + Vector3.up * 0.8f;
            var end = transform.position + Vector3.up * 0.8f;
            var delta = end - start;
            if (delta.sqrMagnitude < 0.0001f) return true;
            foreach (var hit in Physics.RaycastAll(start, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.collider.GetComponentInParent<NetworkPlayer>() && !hit.transform.IsChildOf(transform)) return false;
            return true;
        }
    }
}
