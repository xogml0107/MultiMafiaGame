using System;
using System.Linq;
using NUnit.Framework;
using ZZabmongus.Core;

namespace ZZabmongus.Tests
{
    public sealed class HostedDeviceTests
    {
        private static readonly string[] Ids = { "a", "b", "c", "d" };
        private static HostedMatch Create(int seed = 101)
        {
            var host = new HostedMatch(); host.Start(Ids, seed);
            host.RegisterDevices(new[] { new HostedDevice(1, DeviceKind.Parity, new Position(0,0)), new HostedDevice(2, DeviceKind.Parity, new Position(5,0)) });
            return host;
        }

        [Test]
        public void UnauthenticatedStaleUnknownOrUnpositionedRequestsCannotSpendPoints()
        {
            var host = Create(); host.Tick(180);
            Assert.That(host.TryInspect("stranger", host.Round, 1, out _), Is.False);
            Assert.That(host.TryInspect("a", host.Round - 1, 1, out _), Is.False);
            Assert.That(host.TryInspect("a", host.Round, 999, out _), Is.False);
            Assert.That(host.TryInspect("a", host.Round, 1, out _), Is.False);
            Assert.That(host.SetPlayerPosition("stranger", new Position(0,0)), Is.False);
            Assert.That(host.PublicState.MissionPoints, Is.EqualTo(2));
            host.TryGetOwnerView("a", out var view); Assert.That(view.Clues, Is.Empty);
        }

        [Test]
        public void ServerDistanceBoundaryIsEnforcedAndParityStaysOwnerOnly()
        {
            var host = Create(); host.Tick(90);
            host.SetPlayerPosition("a", new Position(2.51f,0));
            Assert.That(host.TryInspect("a", host.Round, 1, out _), Is.False);
            host.SetPlayerPosition("a", new Position(2.5f,0));
            Assert.That(host.TryInspect("a", host.Round, 1, out _), Is.True);
            Assert.That(host.PublicState.MissionPoints, Is.Zero);
            host.TryGetOwnerView("a", out var actor);
            Assert.That(actor.Clues.Single().IsPublic, Is.False);
            Assert.That(actor.Clues.Single().IsEven.HasValue, Is.True);
            Assert.That(actor.Clues.Single().SubjectId, Is.EqualTo("a"));
            foreach (var id in Ids.Skip(1)) { host.TryGetOwnerView(id, out var other); Assert.That(other.Clues, Is.Empty); }
            Assert.That(typeof(Clue).GetProperty("Number"), Is.Null);
            Assert.That(typeof(PlayerView).GetProperty("Number"), Is.Null);
        }

        [Test]
        public void SharedCostAndPersonalCooldownApplyAcrossMultipleStations()
        {
            var host = Create(); host.Tick(180); host.SetPlayerPosition("a", new Position(0,0));
            Assert.That(host.TryInspect("a", host.Round, 1, out _), Is.True);
            host.SetPlayerPosition("a", new Position(5,0));
            Assert.That(host.TryInspect("a", host.Round, 2, out _), Is.False);
            Assert.That(host.CooldownRemaining("a"), Is.EqualTo(30));
            Assert.That(host.PublicState.MissionPoints, Is.EqualTo(1));
            host.Tick(29.99); Assert.That(host.TryInspect("a", host.Round, 2, out _), Is.False);
            host.Tick(0.01); Assert.That(host.TryInspect("a", host.Round, 2, out _), Is.True);
            Assert.That(host.PublicState.MissionPoints, Is.Zero);
            host.TryGetOwnerView("a", out var view); Assert.That(view.Clues.Count, Is.EqualTo(2));
        }

        [Test]
        public void InsufficientPointsDoesNotStartCooldownOrCreateClue()
        {
            var host = Create(); host.SetPlayerPosition("a", new Position(0,0));
            Assert.That(host.TryInspect("a", host.Round, 1, out var message), Is.False);
            Assert.That(message, Does.Contain("포인트")); Assert.That(host.CooldownRemaining("a"), Is.Zero);
            host.TryGetOwnerView("a", out var view); Assert.That(view.Clues, Is.Empty);
        }

        [Test]
        public void PublicParityRuleSharesOnlyTheParityObservation()
        {
            var host = FindRule(RuleId.PublicParity, 300); host.SetPlayerPosition("a", new Position(0,0));
            Assert.That(host.TryInspect("a", host.Round, 1, out _), Is.True);
            foreach (var id in Ids)
            {
                host.TryGetOwnerView(id, out var view); var clue = view.Clues.Single();
                Assert.That(clue.IsPublic, Is.True); Assert.That(clue.SubjectId, Is.EqualTo("a"));
                Assert.That(clue.IsEven.HasValue, Is.True);
            }
        }

        [Test]
        public void PairedRuleRequiresAnotherAuthenticatedPositionNearActorAndStation()
        {
            var host = FindRule(RuleId.PairedDevices, 300); host.SetPlayerPosition("a", new Position(0,0));
            var initial = host.PublicState.MissionPoints;
            Assert.That(host.TryInspect("a", host.Round, 1, out _), Is.False);
            host.SetPlayerPosition("b", new Position(10,0));
            Assert.That(host.TryInspect("a", host.Round, 1, out _), Is.False);
            Assert.That(host.PublicState.MissionPoints, Is.EqualTo(initial));
            host.SetPlayerPosition("b", new Position(1,0));
            Assert.That(host.TryInspect("a", host.Round, 1, out _), Is.True);
            host.TryGetOwnerView("b", out var partner); Assert.That(partner.Clues, Is.Empty);
        }

        [Test]
        public void NumberSwapPreservesHistoricalClueEpochAndClosesAtDiscussion()
        {
            var seed = Enumerable.Range(0,100).First(value => { var candidate = Create(value); candidate.Tick(900); return candidate.PublicState.Rules.Contains(RuleId.NumberSwap); });
            var host = Create(seed); host.Tick(90); host.SetPlayerPosition("a", new Position(0,0));
            host.TryInspect("a", host.Round, 1, out _); host.Tick(810);
            host.TryGetOwnerView("a", out var view);
            Assert.That(host.PublicState.NumberEpoch, Is.EqualTo(1)); Assert.That(view.Clues.Single().NumberEpoch, Is.Zero);
            host.Tick(180); var initial = host.PublicState.MissionPoints;
            Assert.That(host.TryInspect("a", host.Round, 1, out _), Is.False);
            Assert.That(host.PublicState.MissionPoints, Is.EqualTo(initial));
        }

        [Test]
        public void EndingAndStartingAnotherRoundClearsDevicesPositionsCluesAndCooldown()
        {
            var host = Create(); host.Tick(90); host.SetPlayerPosition("a", new Position(0,0)); host.TryInspect("a", host.Round, 1, out _);
            var oldRound = host.Round; host.End(); host.Start(Ids, 101); host.Tick(90);
            Assert.That(host.TryInspect("a", oldRound, 1, out _), Is.False);
            Assert.That(host.TryInspect("a", host.Round, 1, out _), Is.False);
            host.RegisterDevices(new[] { new HostedDevice(1, DeviceKind.Parity, new Position(0,0)) });
            Assert.That(host.TryInspect("a", host.Round, 1, out _), Is.False);
            Assert.That(host.CooldownRemaining("a"), Is.Zero);
            host.TryGetOwnerView("a", out var view); Assert.That(view.Clues, Is.Empty);
        }

        [Test]
        public void DuplicateMapDeviceIdsAreRejectedWithoutReplacingValidMap()
        {
            var host = Create();
            Assert.Throws<ArgumentException>(() => host.RegisterDevices(new[] { new HostedDevice(3,DeviceKind.Parity,new Position(0,0)), new HostedDevice(3,DeviceKind.Parity,new Position(1,0)) }));
            host.Tick(90); host.SetPlayerPosition("a", new Position(0,0));
            Assert.That(host.TryInspect("a", host.Round, 1, out _), Is.True);
        }

        private static HostedMatch FindRule(RuleId rule, double time)
        {
            foreach (var seed in Enumerable.Range(0,100)) { var host = Create(seed); host.Tick(time); if (host.PublicState.Rules.Contains(rule)) return host; }
            throw new InvalidOperationException("Could not select a seeded rule fixture.");
        }
    }
}
