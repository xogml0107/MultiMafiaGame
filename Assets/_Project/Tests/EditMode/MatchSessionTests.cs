using System;
using System.Linq;
using NUnit.Framework;
using ZZabmongus.Core;

namespace ZZabmongus.Tests
{
    public sealed class MatchSessionTests
    {
        private static MatchSession New(int count = 8, int seed = 42) => new MatchSession(Enumerable.Range(1, count).Select(i => "P" + i), seed);
        private static string Original(MatchSession match) => match.PlayerIds.Single(id => match.GetPlayerView(id).IsOriginalMafia);
        private static string Civilian(MatchSession match) => match.PlayerIds.First(id => match.GetPlayerView(id).Faction == Faction.Civilian);

        [TestCase(4, 1)] [TestCase(5, 1)] [TestCase(6, 1)] [TestCase(7, 2)] [TestCase(8, 2)]
        [TestCase(9, 2)] [TestCase(10, 3)] [TestCase(11, 3)] [TestCase(12, 3)]
        public void Roster_HasUniqueNumbers_AndOneOriginalMafia(int count, int cap)
        {
            for (var seed = 0; seed < 20; seed++)
            {
                var match = New(count, seed);
                Assert.That(match.PlayerIds.Count(id => match.GetPlayerView(id).Faction == Faction.Mafia), Is.EqualTo(1));
                Assert.That(MatchConfig.MafiaCap(count), Is.EqualTo(cap));
                match.Tick(1230);
                Assert.That(match.Result.Players.Select(p => p.Number).OrderBy(n => n), Is.EqualTo(Enumerable.Range(1, count)));
            }
        }

        [TestCase(3)] [TestCase(13)]
        public void InvalidRosterSizeRejected(int count) => Assert.Throws<ArgumentOutOfRangeException>(() => New(count));

        [Test]
        public void DuplicateIdsRejected() => Assert.Throws<ArgumentException>(() => new MatchSession(new[] { "a", "b", "a", "d" }, 1));

        [Test]
        public void NumberAndSecretEventsStayHiddenUntilResults()
        {
            var match = New();
            Assert.That(match.Result, Is.Null);
            Assert.That(match.GetPublicEvents(), Is.Empty);
            match.Tick(1200);
            Assert.That(match.Result, Is.Null);
            Assert.That(match.GetPublicEvents().Any(e => e.Text.Contains("original mafia")), Is.False);
            match.Tick(30);
            Assert.That(match.Result.Events.Any(e => e.Text.Contains("original mafia")), Is.True);
        }

        [Test]
        public void RecruitmentUnlocksAtFiveMinutes_AndRequiresFullHold()
        {
            var match = New(); var actor = Original(match); var target = Civilian(match);
            match.Tick(299.9);
            Assert.That(match.TryBeginRecruitment(actor, target, out _), Is.False);
            match.Tick(0.1);
            Assert.That(match.TryBeginRecruitment(actor, target, out _), Is.True);
            match.Tick(2.99);
            Assert.That(match.GetPlayerView(target).Faction, Is.EqualTo(Faction.Civilian));
            match.Tick(0.01);
            Assert.That(match.GetPlayerView(target).Faction, Is.EqualTo(Faction.Mafia));
            Assert.That(match.TryBeginRecruitment(actor, Civilian(match), out _), Is.False);
            Assert.That(match.GetPublicEvents().Any(e => e.Text.Contains("recruited by")), Is.False);
        }

        [TestCase(4)] [TestCase(5)] [TestCase(6)]
        public void SmallMatchesNeverAllowRecruitment(int count)
        {
            var match = New(count); match.Tick(900);
            Assert.That(match.TryBeginRecruitment(Original(match), Civilian(match), out _), Is.False);
        }

        [Test]
        public void SecondSlotRequiresTenPlayersAndTenMinutes()
        {
            var match = New(12); var actor = Original(match); match.Tick(300);
            Assert.That(match.TryBeginRecruitment(actor, Civilian(match), out _), Is.True); match.Tick(3);
            Assert.That(match.TryBeginRecruitment(actor, Civilian(match), out _), Is.False); match.Tick(297);
            Assert.That(match.TryBeginRecruitment(actor, Civilian(match), out _), Is.True); match.Tick(3);
            Assert.That(match.PlayerIds.Count(id => match.GetPlayerView(id).Faction == Faction.Mafia), Is.EqualTo(3));
            Assert.That(match.TryBeginRecruitment(actor, Civilian(match), out _), Is.False);
        }

        [TestCase(true)] [TestCase(false)]
        public void MovementOfEitherParticipantCancelsRecruitment(bool moveActor)
        {
            var match = New(); var actor = Original(match); var target = Civilian(match); match.Tick(300);
            match.TryBeginRecruitment(actor, target, out _); match.Tick(2);
            match.SetPosition(moveActor ? actor : target, new Position(0.2f, 0)); match.Tick(2);
            Assert.That(match.GetPlayerView(target).Faction, Is.EqualTo(Faction.Civilian));
        }

        [Test]
        public void ReleaseAndDistancePreventRecruitment()
        {
            var match = New(); var actor = Original(match); var target = Civilian(match); match.Tick(300);
            match.SetPosition(target, new Position(10, 0));
            Assert.That(match.TryBeginRecruitment(actor, target, out _), Is.False);
            match.SetPosition(target, new Position(0, 0));
            Assert.That(match.TryBeginRecruitment(actor, target, out _), Is.True);
            match.CancelRecruitment(actor); match.Tick(4);
            Assert.That(match.GetPlayerView(target).Faction, Is.EqualTo(Faction.Civilian));
        }

        [Test]
        public void RecruitedMafiaCannotRecruit()
        {
            var match = New(12); var target = Civilian(match); match.Tick(600);
            match.TryBeginRecruitment(Original(match), target, out _); match.Tick(3);
            Assert.That(match.TryBeginRecruitment(target, Civilian(match), out _), Is.False);
        }

        [Test]
        public void LargeTickCrossesAllBoundaries_AndMatchesSmallTicks()
        {
            var large = New(); var small = New();
            large.Tick(1080);
            for (var i = 0; i < 1080; i++) small.Tick(1);
            Assert.That(large.Phase, Is.EqualTo(MatchPhase.FinalDiscussion));
            Assert.That(large.ActiveRules.Count, Is.EqualTo(3));
            Assert.That(large.ActiveRules, Is.EqualTo(small.ActiveRules));
            Assert.That(large.MissionPoints, Is.EqualTo(small.MissionPoints));
            Assert.That(large.MissionNumber, Is.EqualTo(small.MissionNumber));
            Assert.That(large.GetPublicEvents().Select(e => e.Text), Is.EqualTo(small.GetPublicEvents().Select(e => e.Text)));
        }

        [Test]
        public void FinalDiscussionClosesDevicesMissionsRecruitment()
        {
            var match = New(); match.Tick(1080);
            Assert.That(match.TryInspect("P1", DeviceKind.Parity, "P2", out _), Is.False);
            Assert.That(match.TryContribute("P1", out _), Is.False);
            Assert.That(match.TryBeginRecruitment(Original(match), Civilian(match), out _), Is.False);
            Assert.That(match.TrySubmit("P1", 1, null, out _), Is.False);
        }

        [Test]
        public void MissionSucceedsWithoutMafiaAndOnlyAwardsOnce()
        {
            var match = New(6); match.Tick(45);
            foreach (var id in match.PlayerIds.Where(id => match.GetPlayerView(id).Faction == Faction.Civilian))
                Assert.That(match.TryContribute(id, out _), Is.True);
            Assert.That(match.Activity, Is.EqualTo(ActivityPhase.Debrief));
            Assert.That(match.MissionPoints, Is.EqualTo(2));
            Assert.That(match.TryContribute(Original(match), out _), Is.False);
            Assert.That(match.MissionPoints, Is.EqualTo(2));
        }

        [Test]
        public void DuplicateContributionsAreNotCounted()
        {
            var match = New(); match.Tick(45);
            Assert.That(match.TryContribute("P1", out _), Is.True);
            Assert.That(match.TryContribute("P1", out _), Is.False);
            Assert.That(match.MissionContributions, Is.EqualTo(1));
        }

        [Test]
        public void FailedMissionsStillAllowMinimumInformation()
        {
            var match = New(); match.Tick(90);
            Assert.That(match.MissionPoints, Is.EqualTo(1));
            Assert.That(match.TryInspect("P1", DeviceKind.Parity, null, out _), Is.True);
            Assert.That(match.MissionPoints, Is.Zero);
            Assert.That(match.GetPlayerView("P1").Clues.Count, Is.EqualTo(1));
            Assert.That(match.GetPlayerView("P2").Clues.Count, Is.Zero);
        }

        [Test]
        public void InvalidDeviceRequestsDoNotSpendPoints()
        {
            var match = New(); match.Tick(180); var points = match.MissionPoints;
            Assert.That(match.TryInspect("P1", DeviceKind.Compare, "P1", out _), Is.False);
            Assert.That(match.TryInspect("P1", DeviceKind.Compare, null, out _), Is.False);
            Assert.That(match.TryInspect("P1", DeviceKind.Precision, null, out _), Is.False);
            Assert.That(match.MissionPoints, Is.EqualTo(points));
        }

        [Test]
        public void CooldownPreventsRepeatedSpending()
        {
            var match = New(); match.Tick(180);
            Assert.That(match.TryInspect("P1", DeviceKind.Parity, null, out _), Is.True);
            Assert.That(match.TryInspect("P1", DeviceKind.Parity, null, out _), Is.False);
            Assert.That(match.MissionPoints, Is.EqualTo(1));
        }

        [Test]
        public void SwapPreservesHistoricCluesAndDoesNotRevealTargets()
        {
            var found = false;
            for (var seed = 0; seed < 50 && !found; seed++)
            {
                var match = New(8, seed); match.Tick(90);
                match.TryInspect("P1", DeviceKind.Parity, null, out _);
                var record = match.GetPlayerView("P1").Clues.Single();
                match.Tick(810);
                if (!match.ActiveRules.Contains(RuleId.NumberSwap)) continue;
                found = true;
                Assert.That(record.NumberEpoch, Is.LessThan(match.NumberEpoch));
                Assert.That(match.GetPlayerView("P1").Clues.Single(), Is.SameAs(record));
                Assert.That(match.GetPublicEvents().Any(e => e.Text.Contains("swapped numbers")), Is.False);
            }
            Assert.That(found, Is.True);
        }

        [TestCase(4, 2)] [TestCase(5, 3)] [TestCase(6, 4)] [TestCase(7, 4)] [TestCase(8, 4)]
        [TestCase(9, 5)] [TestCase(10, 5)] [TestCase(11, 6)] [TestCase(12, 6)]
        public void FullRecruitmentThresholdMatchesDesignTable(int total, int expected) =>
            Assert.That(MatchConfig.RequiredCorrect(total - MatchConfig.MafiaCap(total)), Is.EqualTo(expected));

        [Test]
        public void UnfilledRecruitmentSlotsUseActualCivilianCount()
        {
            var match = New(12); match.Tick(1230);
            Assert.That(match.Result.RequiredCorrect, Is.EqualTo(8));
        }

        [Test]
        public void WrongMafiaAnswerCannotDefeatCorrectCivilians()
        {
            var reference = New(); reference.Tick(1230);
            var match = New(); match.Tick(1200);
            foreach (var player in reference.Result.Players)
            {
                var guess = player.Faction == Faction.Mafia ? player.Number % 8 + 1 : player.Number;
                Assert.That(match.TrySubmit(player.Id, guess, null, out _), Is.True);
            }
            Assert.That(match.Result.CiviliansWin, Is.True);
            Assert.That(match.Result.CorrectCivilians, Is.EqualTo(7));
        }

        [Test]
        public void CivilianMajorityBonusCanCoverOneIncorrectAnswer()
        {
            var reference = New(4); reference.Tick(1230);
            var match = New(4); match.Tick(1200);
            var actor = Original(match); var right = reference.Result.Players.First(p => p.Faction == Faction.Civilian).Id;
            foreach (var p in reference.Result.Players)
                match.TrySubmit(p.Id, p.Id == right ? p.Number : p.Number % 4 + 1, p.Id == actor ? null : actor, out _);
            Assert.That(match.Result.CorrectCivilians, Is.EqualTo(1));
            Assert.That(match.Result.SuspicionBonus, Is.EqualTo(1));
            Assert.That(match.Result.CiviliansWin, Is.True);
        }

        [Test]
        public void SubmissionDeadlineHandlesMissingPlayersAndLocksResult()
        {
            var match = New(); match.Tick(1200);
            Assert.That(match.TrySubmit("P1", 9, null, out _), Is.False);
            Assert.That(match.TrySubmit("P1", 1, "P1", out _), Is.False);
            Assert.That(match.TrySubmit("P1", 1, null, out _), Is.True);
            Assert.That(match.TrySubmit("P1", 2, null, out _), Is.False);
            match.Tick(30); var result = match.Result;
            Assert.That(result.Players.Count(p => p.SubmittedNumber == null), Is.EqualTo(7));
            match.Tick(300);
            Assert.That(match.Result, Is.SameAs(result));
        }

        [TestCase(double.NaN)] [TestCase(double.PositiveInfinity)] [TestCase(-1)]
        public void InvalidTimeRejected(double value) => Assert.Throws<ArgumentOutOfRangeException>(() => New().Tick(value));
    }
}
