using PurrNet;
using UnityEngine;
using ZZabmongus.Core;

namespace ZZabmongus.Networking
{
    public sealed partial class NetworkPlayer
    {
        // These are local owner-only fields, deliberately not SyncVars.
        private int privateRound;
        private bool receivedMatchInfo, mafia, originalMafia, submitted;
        private float nextSubmissionAt;
        public bool HasMatchInfo => isOwner && receivedMatchInfo && lobby && lobby.InSession && privateRound == lobby.Round;
        public bool IsMafia => HasMatchInfo && mafia;
        public bool IsOriginalMafia => HasMatchInfo && originalMafia;
        public bool HasSubmitted => HasMatchInfo && submitted;
        public string SubmissionMessage { get; private set; } = "";

        internal void SendMatchInfo(int revision, PlayerView view)
        {
            if (!isServer || !owner.HasValue) return;
            ReceiveMatchInfo(owner.Value, revision, view != null, view?.Faction == Faction.Mafia, view?.IsOriginalMafia ?? false, view?.HasSubmitted ?? false);
        }

        [TargetRpc]
        private void ReceiveMatchInfo(PlayerID recipient, int revision, bool active, bool isMafia, bool isOriginal, bool hasSubmitted)
        {
            if (!isOwner || revision < privateRound) return;
            if (revision != privateRound || !active) ClearDeviceState();
            if (revision != privateRound || !active) SubmissionMessage = "";
            privateRound = revision; receivedMatchInfo = active;
            mafia = active && isMafia; originalMafia = active && isOriginal; submitted = active && hasSubmitted;
        }

        public void SubmitFinalAnswer(int number, int suspectSlot)
        {
            if (!HasMatchInfo || !lobby || lobby.Phase != MatchPhase.Submission || HasSubmitted) return;
            RequestFinalAnswer(lobby.Round, number, suspectSlot);
        }

        [ServerRpc(requireOwnership: true)]
        private void RequestFinalAnswer(int expectedRound, int number, int suspectSlot)
        {
            if (!isServer || !lobby || Time.unscaledTime < nextSubmissionAt) return;
            nextSubmissionAt = Time.unscaledTime + 0.25f;
            // Ownership is checked by PurrNet. The player ID is derived from this object's owner.
            lobby.SubmitFinal(this, expectedRound, number, suspectSlot);
        }

        internal void SendSubmissionResponse(int revision, bool accepted, string message)
        {
            if (isServer && owner.HasValue) ReceiveSubmissionResponse(owner.Value, revision, accepted, message);
        }

        [TargetRpc]
        private void ReceiveSubmissionResponse(PlayerID recipient, int revision, bool accepted, string message)
        {
            if (!isOwner || revision != privateRound || !receivedMatchInfo) return;
            SubmissionMessage = message;
            if (accepted) submitted = true;
        }

        private void ClearMatchInfo()
        {
            ClearDeviceState();
            privateRound = 0; receivedMatchInfo = mafia = originalMafia = submitted = false;
            nextSubmissionAt = 0; SubmissionMessage = "";
        }
    }
}
