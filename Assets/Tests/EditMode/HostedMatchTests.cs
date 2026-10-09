using System;
using System.Linq;
using NUnit.Framework;
using ZZabmongus.Core;

namespace ZZabmongus.Tests
{
    public sealed class HostedMatchTests
    {
        private static readonly string[] Ids = { "a", "b", "c", "d" };
        private static HostedMatch Start()
        {
            var host = new HostedMatch(); host.Start(Ids, 1001); return host;
        }

        [Test]
        public void StartingKeepsNumbersAndSeedOffPublicState_AndOneMafiaInOwnerViews()
        {
            var host = Start(); var state = host.PublicState;
            Assert.That(state.PlayerCount, Is.EqualTo(4)); Assert.That(state.RemainingSeconds, Is.EqualTo(1080));
            Assert.That(host.Result, Is.Null);
            var publicProperties = typeof(PublicMatchState).GetProperties().Select(p => p.Name).ToArray();
            Assert.That(publicProperties, Does.Not.Contain("Seed")); Assert.That(publicProperties, Does.Not.Contain("Number"));
            Assert.That(publicProperties, Does.Not.Contain("Faction"));
            var mafia = Ids.Count(id => host.TryGetOwnerView(id, out var view) && view.Faction == Faction.Mafia && view.IsOriginalMafia);
            Assert.That(mafia, Is.EqualTo(1)); Assert.That(typeof(PlayerView).GetProperty("Number"), Is.Null);
            Assert.That(host.TryGetOwnerView("outsider", out var denied), Is.False); Assert.That(denied, Is.Null);
        }

        [TestCase(3)] [TestCase(13)]
        public void InvalidRosterCannotCreateActiveRound(int count)
        {
            var host = new HostedMatch();
            Assert.Throws<ArgumentOutOfRangeException>(() => host.Start(Enumerable.Range(0,count).Select(i => i.ToString()), 1));
            Assert.That(host.Active, Is.False); Assert.That(host.Round, Is.Zero);
        }

        [Test]
        public void ActiveMatchCannotBeReplaced_AndDuplicateOwnersAreRejected()
        {
            var host = Start(); Assert.Throws<InvalidOperationException>(() => host.Start(Ids,2));
            var fresh = new HostedMatch(); Assert.Throws<ArgumentException>(() => fresh.Start(new[] { "a", "a", "b", "c" },1));
            Assert.That(fresh.Active, Is.False);
        }

        [Test]
        public void OnlyServerTimeAdvancesPhases_AndResultsRemainHiddenUntilDeadline()
        {
            var host = Start(); host.Tick(90);
            Assert.That(host.PublicState.MissionPoints, Is.EqualTo(1));
            host.Tick(990); Assert.That(host.PublicState.Phase, Is.EqualTo(MatchPhase.FinalDiscussion));
            Assert.That(host.PublicState.RemainingSeconds, Is.EqualTo(120)); Assert.That(host.Result, Is.Null);
            host.Tick(120); Assert.That(host.PublicState.Phase, Is.EqualTo(MatchPhase.Submission)); Assert.That(host.Result, Is.Null);
            host.Tick(30); Assert.That(host.PublicState.Phase, Is.EqualTo(MatchPhase.Results));
            Assert.That(host.Result.Players.Select(p => p.Number).OrderBy(n => n), Is.EqualTo(new[] { 1,2,3,4 }));
        }

        [Test]
        public void UnauthenticatedOrStaleSubmissionsCannotLockAnswers()
        {
            var host = Start(); host.Tick(1200);
            Assert.That(host.TrySubmit("outsider",host.Round,1,null,out _), Is.False);
            Assert.That(host.TrySubmit("a",host.Round - 1,1,null,out _), Is.False);
            Assert.That(host.TrySubmit("a",host.Round,1,"outsider",out _), Is.False);
            Assert.That(host.TryGetOwnerView("a",out var view), Is.True); Assert.That(view.HasSubmitted, Is.False);
        }

        [Test]
        public void SubmissionIsPhaseBoundedAndImmutable_AndAllAnswersFinishEarly()
        {
            var host = Start();
            Assert.That(host.TrySubmit("a",host.Round,1,null,out _), Is.False);
            host.Tick(1200);
            Assert.That(host.TrySubmit("a",host.Round,0,null,out _), Is.False);
            Assert.That(host.TrySubmit("a",host.Round,5,null,out _), Is.False);
            Assert.That(host.TrySubmit("a",host.Round,1,"a",out _), Is.False);
            Assert.That(host.TrySubmit("a",host.Round,1,null,out _), Is.True);
            Assert.That(host.TrySubmit("a",host.Round,2,null,out _), Is.False);
            foreach (var id in Ids.Skip(1)) Assert.That(host.TrySubmit(id,host.Round,1,null,out _), Is.True);
            Assert.That(host.PublicState.Phase, Is.EqualTo(MatchPhase.Results));
            Assert.That(host.Result.Players.Single(p => p.Id == "a").SubmittedNumber, Is.EqualTo(1));
        }

        [Test]
        public void ParticipantDisconnectAbortsWithoutRevealingSecrets_AndInvalidatesOldRound()
        {
            var host = Start(); var firstRound = host.Round;
            Assert.That(host.ParticipantLeft("outsider"), Is.False); Assert.That(host.Active, Is.True);
            Assert.That(host.ParticipantLeft("a"), Is.True); Assert.That(host.Active, Is.False);
            Assert.That(host.PublicState, Is.Null); Assert.That(host.Result, Is.Null); Assert.That(host.TryGetOwnerView("b",out _), Is.False);
            host.Start(Ids,2); host.Tick(1200);
            Assert.That(host.Round, Is.GreaterThan(firstRound)); Assert.That(host.TrySubmit("a",firstRound,1,null,out _), Is.False);
        }

        [Test]
        public void ReturningToLobbyClearsSecretsResultsAndSubmissionState()
        {
            var host = Start(); host.Tick(1230); Assert.That(host.Result, Is.Not.Null);
            Assert.That(host.ParticipantLeft("a"), Is.False, "Results can remain visible to remaining participants");
            host.End(); Assert.That(host.Result, Is.Null); Assert.That(host.PublicState, Is.Null);
            host.Start(Ids,3); host.TryGetOwnerView("a",out var view); Assert.That(view.HasSubmitted, Is.False);
            Assert.That(host.PublicState.ElapsedSeconds, Is.Zero);
        }
    }
}
