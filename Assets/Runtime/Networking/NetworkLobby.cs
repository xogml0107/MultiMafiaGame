using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using PurrNet;
using UnityEngine;
using ZZabmongus.Core;

namespace ZZabmongus.Networking
{
    public sealed class NetworkLobby : NetworkBehaviour
    {
        public const int MinPlayers = 4;
        public const int MaxPlayers = 12;
        private readonly SyncVar<bool> inSession = new(false);
        private readonly SyncVar<bool> loading = new(false);
        private readonly SyncVar<int> capacity = new(MaxPlayers);
        private readonly SyncVar<int> round = new(0), phase = new(0), activity = new(0), elapsed = new(0), remaining = new(0), activityRemaining = new(0), points = new(0), rules = new(0), participants = new(0);
        private readonly SyncVar<string> notice = new(""), results = new("");
        private readonly HostedMatch hostedMatch = new();
        private readonly Dictionary<string, (string Name, int Slot)> roster = new();
        private float nextStateAt;
        public int Round => round.value;
        public MatchPhase Phase => (MatchPhase)phase.value;
        public ActivityPhase Activity => (ActivityPhase)activity.value;
        public int ElapsedSeconds => elapsed.value;
        public int RemainingSeconds => remaining.value;
        public int ActivityRemainingSeconds => activityRemaining.value;
        public int MissionPoints => points.value;
        public int RuleMask => rules.value;
        public int ParticipantCount => participants.value;
        public string Notice => notice.value;
        public string ResultSummary => results.value;
        public int Capacity => capacity.value;
        public bool InSession => isSpawned && inSession.value;
        public bool Loading => isSpawned && loading.value;
        public NetworkPlayer[] Players => FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None)
            .Where(p => p.isSpawned).OrderBy(p => p.Slot).ToArray();
        public bool CanStart => isServer && !InSession && !Loading && Players.Length >= MinPlayers &&
                                Players.Length <= Capacity && Players.All(p => p.Ready);

        protected override void OnSpawned(bool asServer)
        {
            if (!asServer) return;
            hostedMatch.End(); roster.Clear(); ResetPublicState();
            notice.value = "";
            inSession.value = false;
            loading.value = false;
            var connection = FindFirstObjectByType<MultiplayerConnection>();
            capacity.value = connection && connection.Authentication.Admission != null ? connection.Authentication.Admission.Capacity : MaxPlayers;
            networkManager.onPlayerLeft += ParticipantLeft;
        }

        protected override void OnDespawned(bool asServer)
        {
            if (!asServer) return;
            if (networkManager) networkManager.onPlayerLeft -= ParticipantLeft;
            hostedMatch.End(); roster.Clear();
        }

        // Called by local host UI only; clients cannot RPC this method.
        public void StartSession()
        {
            if (!CanStart) return;
            var flow = SessionSceneFlow.Instance;
            if (flow)
            {
                inSession.value = true;
                if (!flow.BeginGame()) inSession.value = false;
                return;
            }
            StartLoadedMatch();
        }

        internal void SetLoading(bool value) { if (isServer) loading.value = value; }

        internal void StartLoadedMatch()
        {
            if (!isServer || Loading || hostedMatch.Active) return;
            var players = Players;
            if (players.Length < MinPlayers || players.Length > Capacity || players.Any(p => !p.Ready))
            {
                EndSession("인원이 바뀌어 시작을 취소했습니다. 다시 준비해 주세요.");
                SessionSceneFlow.Instance?.ReturnToWaiting();
                return;
            }
            if (players.Any(p => !p.owner.HasValue) || players.Select(p => p.owner.Value).Distinct().Count() != players.Length)
            {
                EndSession("참가자의 연결 상태를 확인하지 못했습니다. 다시 준비해 주세요.");
                SessionSceneFlow.Instance?.ReturnToWaiting();
                return;
            }
            var seed = new byte[4];
            using (var random = RandomNumberGenerator.Create()) random.GetBytes(seed);
            hostedMatch.Start(players.Select(p => p.owner.Value.ToString()), BitConverter.ToInt32(seed, 0));
            roster.Clear();
            foreach (var player in players) roster.Add(player.owner.Value.ToString(), (player.DisplayName, player.Slot));
            notice.value = ""; results.value = "";
            inSession.value = true;
            PublishPublicState();
            foreach (var player in players) PublishOwnerState(player);
        }

        public void ReturnToLobby()
        {
            if (!isServer || !InSession) return;
            EndSession("");
            SessionSceneFlow.Instance?.ReturnToWaiting();
        }

        private void Update()
        {
            if (!isServer || !InSession || Loading || !hostedMatch.Active) return;
            hostedMatch.Tick(Time.unscaledDeltaTime);
            if (Time.unscaledTime >= nextStateAt)
            {
                nextStateAt = Time.unscaledTime + 0.2f;
                PublishPublicState();
            }
        }

        private void PublishPublicState()
        {
            var state = hostedMatch.PublicState;
            if (state == null) return;
            round.value = state.Round; phase.value = (int)state.Phase; activity.value = (int)state.Activity;
            elapsed.value = state.ElapsedSeconds; remaining.value = state.RemainingSeconds;
            activityRemaining.value = state.ActivityRemainingSeconds; points.value = state.MissionPoints; participants.value = state.PlayerCount;
            rules.value = state.Rules.Aggregate(0, (mask, rule) => mask | (1 << (int)rule));
            if (state.Phase == MatchPhase.Results && string.IsNullOrEmpty(results.value)) PublishResults();
        }

        private void PublishOwnerState(NetworkPlayer player)
        {
            if (player && player.owner.HasValue && hostedMatch.TryGetOwnerView(player.owner.Value.ToString(), out var view))
                player.SendMatchInfo(hostedMatch.Round, view);
        }

        internal void SubmitFinal(NetworkPlayer player, int expectedRound, int number, int suspectSlot)
        {
            if (!isServer || !player || !player.owner.HasValue || !Players.Contains(player)) return;
            var suspect = suspectSlot == -1 ? null : roster.FirstOrDefault(pair => pair.Value.Slot == suspectSlot).Key;
            if (suspectSlot != -1 && suspect == null)
            {
                player.SendSubmissionResponse(Round, false, "의심 대상을 다시 선택해 주세요."); return;
            }
            var accepted = hostedMatch.TrySubmit(player.owner.Value.ToString(), expectedRound, number, suspect, out var message);
            PublishOwnerState(player);
            player.SendSubmissionResponse(Round, accepted, message);
            PublishPublicState();
        }

        private void PublishResults()
        {
            var result = hostedMatch.Result;
            if (result == null) return;
            var text = new StringBuilder();
            text.AppendLine(result.CiviliansWin ? "시민 승리" : "마피아 승리");
            text.AppendLine($"시민 정답 {result.CorrectCivilians}명 / 필요 {result.RequiredCorrect}명 · 의심 보너스 {result.SuspicionBonus}");
            foreach (var player in result.Players)
            {
                var name = roster.TryGetValue(player.Id, out var entry) ? entry.Name : "참가자";
                var faction = player.Faction == Faction.Mafia ? "마피아" : "시민";
                text.AppendLine($"{name}  |  번호 {player.Number}  |  제출 {player.SubmittedNumber?.ToString() ?? "미제출"}  |  {faction}");
            }
            results.value = text.ToString();
        }

        private void ParticipantLeft(PlayerID player, bool asServer)
        {
            if (asServer && InSession && (Loading || hostedMatch.ParticipantLeft(player.ToString())))
            {
                EndSession("참가자가 나가서 판을 중단했습니다. 다시 준비해 주세요.", false);
                SessionSceneFlow.Instance?.ReturnToWaiting();
            }
        }

        private void EndSession(string message, bool endCore = true)
        {
            if (endCore) hostedMatch.End();
            inSession.value = false;
            ResetPublicState(); notice.value = message; roster.Clear();
            foreach (var player in Players) { player.ClearReady(); player.SendMatchInfo(hostedMatch.Round, null); }
        }

        private void ResetPublicState()
        {
            round.value = hostedMatch.Round; phase.value = activity.value = elapsed.value = remaining.value = activityRemaining.value = points.value = rules.value = participants.value = 0;
            results.value = "";
        }
    }
}
