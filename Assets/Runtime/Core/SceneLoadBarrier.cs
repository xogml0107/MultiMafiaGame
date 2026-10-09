using System;
using System.Collections.Generic;
using System.Linq;

namespace ZZabmongus.Core
{
    /// <summary>Server snapshot of participants required for one scene transition.</summary>
    public sealed class SceneLoadBarrier
    {
        private readonly HashSet<string> expected;
        private readonly HashSet<string> loaded = new(StringComparer.Ordinal);
        public int SceneToken { get; }
        public bool RosterChanged { get; private set; }
        public bool Complete => expected.Count > 0 && expected.SetEquals(loaded);
        public int Remaining => expected.Count - loaded.Count;
        public double Deadline { get; }

        public SceneLoadBarrier(int sceneToken, IEnumerable<string> participants, double now, double timeout)
        {
            if (timeout <= 0 || double.IsNaN(timeout) || double.IsInfinity(timeout)) throw new ArgumentOutOfRangeException(nameof(timeout));
            var ids = participants.ToArray();
            if (ids.Length == 0 || ids.Any(string.IsNullOrWhiteSpace) || ids.Distinct(StringComparer.Ordinal).Count() != ids.Length)
                throw new ArgumentException("Unique authenticated participants are required.", nameof(participants));
            expected = new HashSet<string>(ids, StringComparer.Ordinal);
            SceneToken = sceneToken; Deadline = now + timeout;
        }

        public bool Acknowledge(int sceneToken, string authenticatedPlayer)
        {
            return sceneToken == SceneToken && authenticatedPlayer != null && expected.Contains(authenticatedPlayer) && loaded.Add(authenticatedPlayer);
        }

        public void Remove(string authenticatedPlayer)
        {
            if (authenticatedPlayer == null || !expected.Remove(authenticatedPlayer)) return;
            loaded.Remove(authenticatedPlayer); RosterChanged = true;
        }

        public bool TimedOut(double now) => !Complete && now >= Deadline;
    }
}
