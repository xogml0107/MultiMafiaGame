using UnityEngine;

namespace ZZabmongus.Networking
{
    public sealed class MultiplayerCamera : MonoBehaviour
    {
        private void LateUpdate()
        {
            var player = NetworkPlayer.Local;
            var target = player ? player.transform.position : Vector3.zero;
            transform.position = Vector3.Lerp(transform.position, target + new Vector3(0, 22, -10),
                1 - Mathf.Exp(-6 * Time.unscaledDeltaTime));
        }
    }
}
