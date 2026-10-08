using System;

namespace ZZabmongus.Core
{
    /// <summary>Immutable, host-owned balance settings. Times are match seconds.</summary>
    public sealed class MatchConfig
    {
        public const int MinPlayers = 4;
        public const int MaxPlayers = 12;
        public double DiscussionAt { get; }
        public double SubmissionAt { get; }
        public double SubmissionDuration { get; }
        public double RecruitmentDuration { get; }
        public float InteractionDistance { get; }
        public double DeviceCooldown { get; }
        public double FreePointInterval { get; }
        public double FreeActionDuration { get; }
        public double MissionDuration { get; }
        public double DebriefDuration { get; }
        public bool SuspicionBonusEnabled { get; }

        public MatchConfig(double discussionAt = 1080, double submissionAt = 1200,
            double submissionDuration = 30, double recruitmentDuration = 3,
            float interactionDistance = 2.5f, double deviceCooldown = 30,
            double freePointInterval = 90, double freeActionDuration = 45,
            double missionDuration = 45, double debriefDuration = 25,
            bool suspicionBonusEnabled = true)
        {
            Positive(discussionAt, nameof(discussionAt));
            Positive(submissionAt, nameof(submissionAt));
            Positive(submissionDuration, nameof(submissionDuration));
            Positive(recruitmentDuration, nameof(recruitmentDuration));
            Positive(interactionDistance, nameof(interactionDistance));
            Positive(deviceCooldown, nameof(deviceCooldown));
            Positive(freePointInterval, nameof(freePointInterval));
            Positive(freeActionDuration, nameof(freeActionDuration));
            Positive(missionDuration, nameof(missionDuration));
            Positive(debriefDuration, nameof(debriefDuration));
            if (discussionAt <= 900 || submissionAt <= discussionAt)
                throw new ArgumentException("Discussion must follow all three rule tiers, then submission.");
            DiscussionAt = discussionAt;
            SubmissionAt = submissionAt;
            SubmissionDuration = submissionDuration;
            RecruitmentDuration = recruitmentDuration;
            InteractionDistance = interactionDistance;
            DeviceCooldown = deviceCooldown;
            FreePointInterval = freePointInterval;
            FreeActionDuration = freeActionDuration;
            MissionDuration = missionDuration;
            DebriefDuration = debriefDuration;
            SuspicionBonusEnabled = suspicionBonusEnabled;
        }

        public static int MafiaCap(int players)
        {
            if (players < MinPlayers || players > MaxPlayers)
                throw new ArgumentOutOfRangeException(nameof(players));
            return players < 7 ? 1 : players < 10 ? 2 : 3;
        }

        public static int RequiredCorrect(int civilians) => (2 * civilians + 2) / 3;

        private static void Positive(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
                throw new ArgumentOutOfRangeException(name);
        }
    }
}
