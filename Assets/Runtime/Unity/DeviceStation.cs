using UnityEngine;
using ZZabmongus.Core;

namespace ZZabmongus.Runtime
{
    public sealed class DeviceStation : MonoBehaviour
    {
        public bool missionRelay;
        public DeviceKind device;
        public string DisplayName => missionRelay ? "RELAY" : device.ToString().ToUpperInvariant();
    }
}
