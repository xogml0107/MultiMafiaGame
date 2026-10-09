using System;
using System.Collections.Generic;
using System.Linq;

namespace ZZabmongus.Core
{
    /// <summary>Public projection. Contains no seed, numbers, submissions, or factions.</summary>
    public sealed class PublicMatchState
    {
        public int Round { get; }
        public int PlayerCount { get; }
        public MatchPhase Phase { get; }
        public ActivityPhase Activity { get; }
        public int ElapsedSeconds { get; }
        public int RemainingSeconds { get; }
        public int ActivityRemainingSeconds { get; }
        public int MissionPoints { get; }
        public IReadOnlyList<RuleId> Rules { get; }

        internal PublicMatchState(int round, MatchSession match)
        {
            Round = round; PlayerCount = match.PlayerIds.Count;
            Phase = match.Phase; Activity = match.Activity;
            ElapsedSeconds = (int)Math.Floor(match.ElapsedSeconds);
            var boundary = Phase == MatchPhase.Playing ? match.Config.DiscussionAt :
                Phase == MatchPhase.FinalDiscussion ? match.Config.SubmissionAt :
                Phase == MatchPhase.Submission ? match.Config.SubmissionAt + match.Config.SubmissionDuration : match.ElapsedSeconds;
            RemainingSeconds = (int)Math.Ceiling(Math.Max(0, boundary - match.ElapsedSeconds));
            ActivityRemainingSeconds = Phase == MatchPhase.Playing ?
                (int)Math.Ceiling(Math.Max(0, match.ActivityEndsAt - match.ElapsedSeconds)) : 0;
            MissionPoints = match.MissionPoints;
            Rules = Array.AsReadOnly(match.ActiveRules.ToArray());
        }
    }

    /// <summary>Server-only lifecycle and command gate around the existing rules.</summary>
    public sealed class HostedMatch
    {
        private MatchSession match;
        private HashSet<string> participants;
        public int Round { get; private set; }
        public bool Active => match != null;
        public PublicMatchState PublicState => match == null ? null : new PublicMatchState(Round, match);
        // Hidden data becomes available for broadcast only after the rules finish the match.
        public MatchResult Result => match?.Phase == MatchPhase.Results ? match.Result : null;

        public void Start(IEnumerable<string> playerIds, int seed)
        {
            if (Active) throw new InvalidOperationException("A match is already active.");
            var next = new MatchSession(playerIds, seed);
            match = next;
            participants = new HashSet<string>(next.PlayerIds, StringComparer.Ordinal);
            Round++;
        }

        public void Tick(double serverDeltaSeconds) => match?.Tick(serverDeltaSeconds);

        /// <remarks>The transport must derive this ID from the authenticated owner, never a request field.</remarks>
        public bool TryGetOwnerView(string authenticatedId, out PlayerView view)
        {
            view = null;
            if (match == null || authenticatedId == null || !participants.Contains(authenticatedId)) return false;
            view = match.GetPlayerView(authenticatedId);
            return true;
        }

        public bool TrySubmit(string authenticatedId, int expectedRound, int number, string suspectId, out string message)
        {
            if (!Active || expectedRound != Round) return Fail("현재 판의 입장 정보가 아닙니다.", out message);
            if (authenticatedId == null || !participants.Contains(authenticatedId)) return Fail("참가자만 제출할 수 있습니다.", out message);
            if (match.Phase != MatchPhase.Submission) return Fail("아직 최종 제출 시간이 아닙니다.", out message);
            if (match.GetPlayerView(authenticatedId).HasSubmitted) return Fail("이미 답안을 확정했습니다.", out message);
            if (number < 1 || number > participants.Count) return Fail("현재 인원수 안의 번호를 선택해 주세요.", out message);
            if (suspectId != null && (suspectId == authenticatedId || !participants.Contains(suspectId)))
                return Fail("다른 참가자를 선택하거나 기권해 주세요.", out message);
            var accepted = match.TrySubmit(authenticatedId, number, suspectId, out _);
            message = accepted ? "최종 답안을 확정했습니다." : "답안을 제출할 수 없습니다.";
            return accepted;
        }

        public bool ParticipantLeft(string authenticatedId)
        {
            if (!Active || match.Phase == MatchPhase.Results || authenticatedId == null || !participants.Contains(authenticatedId)) return false;
            End();
            return true;
        }

        public void End()
        {
            match = null; participants = null;
            // Invalidate delayed commands and owner-only messages from the previous round.
            Round++;
        }

        private static bool Fail(string reason, out string message) { message = reason; return false; }
    }
}
