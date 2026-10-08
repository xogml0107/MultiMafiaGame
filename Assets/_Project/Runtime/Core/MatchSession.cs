using System;
using System.Collections.Generic;
using System.Linq;

namespace ZZabmongus.Core
{
    /// <summary>
    /// Authoritative in-memory match. Keep this instance on the host/server only.
    /// A future transport must authenticate the caller and validate movement/device proximity.
    /// No Unity objects, wall clock, transport, UI or client-supplied reward amounts are used here.
    /// </summary>
    public sealed class MatchSession
    {
        private sealed class Player
        {
            public string Id;
            public int Number;
            public Faction Faction;
            public bool Original;
            public double? RecruitedAt;
            public Position Position;
            public readonly List<Clue> Clues = new List<Clue>();
            public int? Guess;
            public string Suspect;
            public bool Submitted;
            public double DeviceReadyAt;
        }

        private sealed class Recruitment
        {
            public Player Actor, Target;
            public Position ActorStart, TargetStart;
            public double StartedAt;
        }

        private readonly MatchConfig config;
        private readonly Random random;
        private readonly Player[] players;
        private readonly Dictionary<string, Player> byId;
        private readonly List<MatchEvent> events = new List<MatchEvent>();
        private readonly List<RuleId> rules = new List<RuleId>();
        private readonly HashSet<string> contributors = new HashSet<string>();
        private Recruitment recruitment;
        private double nextActivityAt;
        private double nextFreePointAt;
        private int nextTier;
        private int recruitedCount;
        private MatchResult result;

        public MatchPhase Phase { get; private set; } = MatchPhase.Playing;
        public ActivityPhase Activity { get; private set; } = ActivityPhase.FreeAction;
        public double ElapsedSeconds { get; private set; }
        public double ActivityEndsAt => nextActivityAt;
        public int MissionPoints { get; private set; }
        public int NumberEpoch { get; private set; }
        public int MissionNumber { get; private set; }
        public int MissionContributions => contributors.Count;
        public int RequiredContributions => players.Length - MatchConfig.MafiaCap(players.Length);
        public IReadOnlyList<string> PlayerIds { get; }
        public IReadOnlyList<RuleId> ActiveRules => rules.AsReadOnly();
        public MatchConfig Config => config;
        public MatchResult Result => result;

        public MatchSession(IEnumerable<string> playerIds, int seed, MatchConfig config = null)
        {
            if (playerIds == null) throw new ArgumentNullException(nameof(playerIds));
            var ids = playerIds.ToArray();
            MatchConfig.MafiaCap(ids.Length);
            if (ids.Any(string.IsNullOrWhiteSpace) || ids.Distinct(StringComparer.Ordinal).Count() != ids.Length)
                throw new ArgumentException("Player IDs must be nonempty and unique.", nameof(playerIds));
            this.config = config ?? new MatchConfig();
            random = new Random(seed);
            var numbers = Enumerable.Range(1, ids.Length).ToArray();
            Shuffle(numbers);
            players = ids.Select((id, i) => new Player { Id = id, Number = numbers[i] }).ToArray();
            byId = players.ToDictionary(p => p.Id, StringComparer.Ordinal);
            PlayerIds = Array.AsReadOnly(ids);
            var original = players[random.Next(players.Length)];
            original.Faction = Faction.Mafia;
            original.Original = true;
            events.Add(new MatchEvent(0, original.Id + " selected as original mafia.", true));
            nextActivityAt = this.config.FreeActionDuration;
            nextFreePointAt = this.config.FreePointInterval;
        }

        public PlayerView GetPlayerView(string playerId)
        {
            var p = GetPlayer(playerId);
            return new PlayerView(p.Id, p.Faction, p.Original, p.Clues.ToArray(), p.Submitted, p.DeviceReadyAt);
        }

        public IReadOnlyList<MatchEvent> GetPublicEvents() => Array.AsReadOnly(events.Where(e => !e.HiddenUntilResults).ToArray());

        public void SetPosition(string playerId, Position position)
        {
            if (Phase == MatchPhase.Results) return;
            GetPlayer(playerId).Position = position;
            if (recruitment != null && !RecruitmentPositionsValid()) recruitment = null;
        }

        /// <summary>Advance using server-owned elapsed time. Processes every crossed boundary in order.</summary>
        public void Tick(double deltaSeconds)
        {
            if (double.IsNaN(deltaSeconds) || double.IsInfinity(deltaSeconds) || deltaSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            if (Phase == MatchPhase.Results) return;
            var end = Math.Min(ElapsedSeconds + deltaSeconds, config.SubmissionAt + config.SubmissionDuration);
            while (ElapsedSeconds < end && Phase != MatchPhase.Results)
            {
                var boundary = Phase == MatchPhase.Playing ? config.DiscussionAt :
                    Phase == MatchPhase.FinalDiscussion ? config.SubmissionAt : config.SubmissionAt + config.SubmissionDuration;
                if (Phase == MatchPhase.Playing)
                {
                    boundary = Math.Min(boundary, Math.Min(nextActivityAt, nextFreePointAt));
                    if (nextTier < 3) boundary = Math.Min(boundary, (nextTier + 1) * 300);
                    if (recruitment != null) boundary = Math.Min(boundary, recruitment.StartedAt + config.RecruitmentDuration);
                }
                ElapsedSeconds = Math.Min(end, boundary);
                ProcessBoundaries();
            }
        }

        private void ProcessBoundaries()
        {
            if (Phase == MatchPhase.Playing && ElapsedSeconds >= config.DiscussionAt)
            {
                Phase = MatchPhase.FinalDiscussion;
                recruitment = null;
                events.Add(new MatchEvent(ElapsedSeconds, "Final discussion. Missions, devices and recruitment closed."));
            }
            if (Phase == MatchPhase.FinalDiscussion && ElapsedSeconds >= config.SubmissionAt)
            {
                Phase = MatchPhase.Submission;
                events.Add(new MatchEvent(ElapsedSeconds, "Final submission opened."));
            }
            if (Phase == MatchPhase.Submission && ElapsedSeconds >= config.SubmissionAt + config.SubmissionDuration)
                Finish();
            if (Phase != MatchPhase.Playing) return;
            if (nextTier < 3 && ElapsedSeconds >= (nextTier + 1) * 300)
            {
                var pool = nextTier == 0 ? new[] { RuleId.PublicParity, RuleId.PairedDevices } :
                    nextTier == 1 ? new[] { RuleId.RangeUnavailable, RuleId.PublicComparisons } :
                    new[] { RuleId.NumberSwap, RuleId.PrecisionUnavailable };
                ApplyRule(pool[random.Next(pool.Length)]);
                nextTier++;
            }
            if (ElapsedSeconds >= nextFreePointAt)
            {
                MissionPoints++;
                nextFreePointAt += config.FreePointInterval;
            }
            if (ElapsedSeconds >= nextActivityAt) AdvanceActivity();
            if (recruitment != null && ElapsedSeconds >= recruitment.StartedAt + config.RecruitmentDuration)
            {
                if (RecruitmentPositionsValid())
                {
                    recruitment.Target.Faction = Faction.Mafia;
                    recruitment.Target.RecruitedAt = ElapsedSeconds;
                    recruitedCount++;
                    events.Add(new MatchEvent(ElapsedSeconds, recruitment.Target.Id + " recruited by " + recruitment.Actor.Id + ".", true));
                }
                recruitment = null;
            }
        }

        private void AdvanceActivity()
        {
            if (Activity == ActivityPhase.FreeAction)
            {
                Activity = ActivityPhase.Mission;
                MissionNumber++;
                contributors.Clear();
                nextActivityAt += config.MissionDuration;
                events.Add(new MatchEvent(ElapsedSeconds, "Relay mission " + MissionNumber + " started."));
            }
            else if (Activity == ActivityPhase.Mission)
            {
                Activity = ActivityPhase.Debrief;
                nextActivityAt += config.DebriefDuration;
                events.Add(new MatchEvent(ElapsedSeconds, "Mission failed."));
            }
            else
            {
                Activity = ActivityPhase.FreeAction;
                nextActivityAt += config.FreeActionDuration;
            }
        }

        public bool TryContribute(string playerId, out string message)
        {
            GetPlayer(playerId);
            if (Phase != MatchPhase.Playing || Activity != ActivityPhase.Mission)
                return Fail("No active mission.", out message);
            if (!contributors.Add(playerId)) return Fail("Already contributed to this mission.", out message);
            message = "Relay contribution registered.";
            if (contributors.Count >= RequiredContributions)
            {
                MissionPoints += 2;
                Activity = ActivityPhase.Debrief;
                nextActivityAt = ElapsedSeconds + config.DebriefDuration;
                message = "Mission success. +2 team points.";
                events.Add(new MatchEvent(ElapsedSeconds, message));
            }
            return true;
        }

        public bool TryBeginRecruitment(string actorId, string targetId, out string message)
        {
            var actor = GetPlayer(actorId);
            var target = GetPlayer(targetId);
            if (Phase != MatchPhase.Playing) return Fail("Recruitment is closed.", out message);
            if (!actor.Original || actor.Faction != Faction.Mafia) return Fail("Only the original mafia can recruit.", out message);
            var unlocked = players.Length < 7 || ElapsedSeconds < 300 ? 0 : players.Length < 10 || ElapsedSeconds < 600 ? 1 : 2;
            if (recruitedCount >= unlocked) return Fail("No recruitment slot available.", out message);
            if (target.Faction == Faction.Mafia) return Fail("Target must be a civilian.", out message);
            if (recruitment != null) return Fail("A recruitment is already in progress.", out message);
            if (!Nearby(actor, target)) return Fail("Move closer to the target.", out message);
            recruitment = new Recruitment { Actor = actor, Target = target, ActorStart = actor.Position, TargetStart = target.Position, StartedAt = ElapsedSeconds };
            message = "Hold still to recruit.";
            return true;
        }

        public void CancelRecruitment(string actorId)
        {
            if (recruitment?.Actor.Id == actorId) recruitment = null;
        }

        public double GetRecruitmentProgress(string actorId) => recruitment?.Actor.Id == actorId ?
            Math.Min(1, (ElapsedSeconds - recruitment.StartedAt) / config.RecruitmentDuration) : 0;

        private bool RecruitmentPositionsValid() => Nearby(recruitment.Actor, recruitment.Target) &&
            recruitment.Actor.Position.DistanceSquared(recruitment.ActorStart) <= 0.0025f &&
            recruitment.Target.Position.DistanceSquared(recruitment.TargetStart) <= 0.0025f;

        public static int DeviceCost(DeviceKind kind)
        {
            switch (kind)
            {
                case DeviceKind.Parity: return 1;
                case DeviceKind.Compare: return 1;
                case DeviceKind.Range: return 2;
                case DeviceKind.Difference: return 2;
                case DeviceKind.Precision: return 3;
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        /// <summary>Call only after the authority validates proximity to an actual device.</summary>
        public bool TryInspect(string playerId, DeviceKind kind, string partnerId, out string message)
        {
            var p = GetPlayer(playerId);
            var cost = DeviceCost(kind);
            if (Phase != MatchPhase.Playing) return Fail("Information devices are closed.", out message);
            if (ElapsedSeconds < p.DeviceReadyAt) return Fail("Device cooldown is active.", out message);
            if ((kind == DeviceKind.Range && rules.Contains(RuleId.RangeUnavailable)) ||
                (kind == DeviceKind.Precision && rules.Contains(RuleId.PrecisionUnavailable)))
                return Fail("This device is blocked by a rule.", out message);
            var paired = kind == DeviceKind.Compare || kind == DeviceKind.Difference || rules.Contains(RuleId.PairedDevices);
            Player partner = null;
            if (paired && (partnerId == null || !byId.TryGetValue(partnerId, out partner) || partner == p || !Nearby(p, partner)))
                return Fail("A second player must be nearby.", out message);
            if (MissionPoints < cost) return Fail("Not enough team points.", out message);
            string clue;
            switch (kind)
            {
                case DeviceKind.Parity:
                    clue = p.Id + " is " + (p.Number % 2 == 0 ? "even." : "odd."); break;
                case DeviceKind.Compare:
                    clue = p.Id + (p.Number > partner.Number ? " is higher than " : " is lower than ") + partner.Id + "."; break;
                case DeviceKind.Range:
                    var low = Math.Max(1, Math.Min(p.Number - 1, players.Length - 3));
                    clue = p.Id + " is in [" + low + ", " + Math.Min(players.Length, low + 3) + "]."; break;
                case DeviceKind.Difference:
                    clue = "The difference between " + p.Id + " and " + partner.Id + " is " +
                        (Math.Abs(p.Number - partner.Number) >= 3 ? "at least 3." : "less than 3."); break;
                default:
                    var candidates = Enumerable.Range(1, players.Length).Where(n => n != p.Number).ToArray();
                    Shuffle(candidates);
                    clue = p.Id + " is one of: " + string.Join(" / ", new[] { p.Number, candidates[0], candidates[1] }.OrderBy(n => n)) + "."; break;
            }
            var isPublic = (kind == DeviceKind.Parity && rules.Contains(RuleId.PublicParity)) ||
                (kind == DeviceKind.Compare && rules.Contains(RuleId.PublicComparisons));
            var record = new Clue(clue, ElapsedSeconds, NumberEpoch, isPublic);
            MissionPoints -= cost;
            p.DeviceReadyAt = ElapsedSeconds + config.DeviceCooldown;
            if (isPublic) foreach (var player in players) player.Clues.Add(record);
            else p.Clues.Add(record);
            message = clue;
            return true;
        }

        private void ApplyRule(RuleId rule)
        {
            rules.Add(rule);
            events.Add(new MatchEvent(ElapsedSeconds, "Rule applied: " + rule));
            if (rule != RuleId.NumberSwap) return;
            var first = random.Next(players.Length);
            var second = random.Next(players.Length - 1);
            if (second >= first) second++;
            var number = players[first].Number;
            players[first].Number = players[second].Number;
            players[second].Number = number;
            NumberEpoch++;
            events.Add(new MatchEvent(ElapsedSeconds, "Two players' numbers changed. Older clues describe the previous state."));
            events.Add(new MatchEvent(ElapsedSeconds, players[first].Id + " and " + players[second].Id + " swapped numbers.", true));
        }

        public bool TrySubmit(string playerId, int number, string suspectId, out string message)
        {
            var p = GetPlayer(playerId);
            if (Phase != MatchPhase.Submission) return Fail("Submission is not open.", out message);
            if (p.Submitted) return Fail("Submission already locked.", out message);
            if (number < 1 || number > players.Length) return Fail("Number is out of range.", out message);
            if (suspectId != null && (!byId.ContainsKey(suspectId) || suspectId == playerId))
                return Fail("Select another player or abstain.", out message);
            p.Guess = number;
            p.Suspect = suspectId;
            p.Submitted = true;
            message = "Submission locked.";
            if (players.All(player => player.Submitted)) Finish();
            return true;
        }

        private void Finish()
        {
            Phase = MatchPhase.Results;
            var civilians = players.Where(p => p.Faction == Faction.Civilian).ToArray();
            var correct = civilians.Count(p => p.Guess == p.Number);
            var majority = civilians.Where(p => p.Suspect != null).GroupBy(p => p.Suspect)
                .FirstOrDefault(group => group.Count() > civilians.Length / 2);
            var bonus = config.SuspicionBonusEnabled && majority != null && byId[majority.Key].Faction == Faction.Mafia ? 1 : 0;
            events.Add(new MatchEvent(ElapsedSeconds, "Match finished."));
            result = new MatchResult(players.Select(p => new PlayerResult(p.Id, p.Number, p.Guess, p.Faction, p.Original, p.RecruitedAt)).ToArray(),
                events.ToArray(), correct, MatchConfig.RequiredCorrect(civilians.Length), bonus);
        }

        private bool Nearby(Player a, Player b) => a.Position.DistanceSquared(b.Position) <= config.InteractionDistance * config.InteractionDistance;
        private Player GetPlayer(string id)
        {
            if (id == null || !byId.TryGetValue(id, out var p)) throw new ArgumentException("Unknown player.", nameof(id));
            return p;
        }
        private static bool Fail(string reason, out string message) { message = reason; return false; }
        private void Shuffle(int[] array)
        {
            for (var i = array.Length - 1; i > 0; i--)
            { var j = random.Next(i + 1); var value = array[i]; array[i] = array[j]; array[j] = value; }
        }
    }
}
