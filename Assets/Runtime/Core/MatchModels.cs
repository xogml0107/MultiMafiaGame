using System;
using System.Collections.Generic;

namespace ZZabmongus.Core
{
    public enum Faction { Civilian, Mafia }
    public enum MatchPhase { Playing, FinalDiscussion, Submission, Results }
    public enum ActivityPhase { FreeAction, Mission, Debrief }
    public enum DeviceKind { Parity, Compare, Range, Difference, Precision }
    public enum RuleId { PublicParity, PairedDevices, RangeUnavailable, PublicComparisons, NumberSwap, PrecisionUnavailable }

    public readonly struct Position
    {
        public float X { get; }
        public float Z { get; }
        public Position(float x, float z)
        {
            if (float.IsNaN(x) || float.IsNaN(z) || float.IsInfinity(x) || float.IsInfinity(z))
                throw new ArgumentException("Position must be finite.");
            X = x;
            Z = z;
        }
        public float DistanceSquared(Position other)
        {
            var x = X - other.X;
            var z = Z - other.Z;
            return x * x + z * z;
        }
    }

    public sealed class Clue
    {
        public string Text { get; }
        public double ObservedAt { get; }
        public int NumberEpoch { get; }
        public bool IsPublic { get; }
        internal Clue(string text, double time, int epoch, bool isPublic)
        { Text = text; ObservedAt = time; NumberEpoch = epoch; IsPublic = isPublic; }
    }

    /// <summary>Owner-only view. Never contains the hidden number or other players' factions.</summary>
    public sealed class PlayerView
    {
        public string Id { get; }
        public Faction Faction { get; }
        public bool IsOriginalMafia { get; }
        public IReadOnlyList<Clue> Clues { get; }
        public bool HasSubmitted { get; }
        public double DeviceReadyAt { get; }
        internal PlayerView(string id, Faction faction, bool original, Clue[] clues, bool submitted, double readyAt)
        { Id = id; Faction = faction; IsOriginalMafia = original; Clues = Array.AsReadOnly(clues); HasSubmitted = submitted; DeviceReadyAt = readyAt; }
    }

    public sealed class MatchEvent
    {
        public double Time { get; }
        public string Text { get; }
        internal bool HiddenUntilResults { get; }
        internal MatchEvent(double time, string text, bool hidden = false)
        { Time = time; Text = text; HiddenUntilResults = hidden; }
    }

    public sealed class PlayerResult
    {
        public string Id { get; }
        public int Number { get; }
        public int? SubmittedNumber { get; }
        public Faction Faction { get; }
        public bool IsOriginalMafia { get; }
        public double? RecruitedAt { get; }
        public bool Correct => Number == SubmittedNumber;
        internal PlayerResult(string id, int number, int? guess, Faction faction, bool original, double? recruitedAt)
        { Id = id; Number = number; SubmittedNumber = guess; Faction = faction; IsOriginalMafia = original; RecruitedAt = recruitedAt; }
    }

    public sealed class MatchResult
    {
        public IReadOnlyList<PlayerResult> Players { get; }
        public IReadOnlyList<MatchEvent> Events { get; }
        public int CorrectCivilians { get; }
        public int RequiredCorrect { get; }
        public int SuspicionBonus { get; }
        public bool CiviliansWin => CorrectCivilians + SuspicionBonus >= RequiredCorrect;
        internal MatchResult(PlayerResult[] players, MatchEvent[] events, int correct, int required, int bonus)
        { Players = Array.AsReadOnly(players); Events = Array.AsReadOnly(events); CorrectCivilians = correct; RequiredCorrect = required; SuspicionBonus = bonus; }
    }
}
