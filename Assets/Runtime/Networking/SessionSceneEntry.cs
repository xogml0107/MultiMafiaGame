using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZZabmongus.Networking
{
    public enum SessionScreen { Lobby, WaitingRoom, Game }

    /// <summary>Scene-local map entry. Only Lobby creates the persistent session.</summary>
    [DefaultExecutionOrder(-1200)]
    public sealed class SessionSceneEntry : MonoBehaviour
    {
        [SerializeField] private SessionScreen screen;
        [SerializeField] private GameObject sessionPrefab;
        [SerializeField] private Vector3 spawnOrigin;
        public SessionScreen Screen => screen;
        public GameObject SessionPrefab => sessionPrefab;
        public Vector3 SpawnPosition(int slot) => spawnOrigin + new Vector3((slot % 4 - 1.5f) * 2, 0, (slot / 4 - 1) * 2);

        private void Awake()
        {
            if (screen == SessionScreen.Lobby && !SessionSceneFlow.Instance && sessionPrefab)
                Instantiate(sessionPrefab);
        }

        private void Start()
        {
            if (screen != SessionScreen.Lobby && (!SessionSceneFlow.Instance || !SessionSceneFlow.Instance.Connection.Busy))
                SceneManager.LoadSceneAsync(SessionSceneFlow.LobbyScene);
        }
    }
}
