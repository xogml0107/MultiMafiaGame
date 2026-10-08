using UnityEngine;
using ZZabmongus.Core;

namespace ZZabmongus.Runtime
{
    [CreateAssetMenu(menuName = "ZZabmongus/Sandbox Settings")]
    public sealed class SandboxSettings : ScriptableObject
    {
        [Range(4, 12)] public int playerCount = 8;
        [Tooltip("A fixed seed makes a test repeatable; change it to test another match.")]
        public int seed = 42;
        [Min(1)] public float movementSpeed = 5;
        [Min(1)] public float deviceCooldown = 30;
        [Min(1)] public float freePointInterval = 90;
        public bool suspicionBonus = true;

        public MatchConfig CreateConfig() => new MatchConfig(
            deviceCooldown: Mathf.Max(1, deviceCooldown), freePointInterval: Mathf.Max(1, freePointInterval),
            suspicionBonusEnabled: suspicionBonus);
    }
}
