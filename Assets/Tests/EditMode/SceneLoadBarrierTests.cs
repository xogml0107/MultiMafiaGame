using System;
using NUnit.Framework;
using ZZabmongus.Core;

namespace ZZabmongus.Tests
{
    public sealed class SceneLoadBarrierTests
    {
        [Test]
        public void AllAuthenticatedParticipantsMustLoadTheCurrentScene()
        {
            var barrier = new SceneLoadBarrier(7, new[] { "host", "client" }, 10, 45);
            Assert.That(barrier.Acknowledge(6, "client"), Is.False);
            Assert.That(barrier.Acknowledge(7, "stranger"), Is.False);
            Assert.That(barrier.Acknowledge(7, "host"), Is.True);
            Assert.That(barrier.Acknowledge(7, "host"), Is.False);
            Assert.That(barrier.Complete, Is.False);
            Assert.That(barrier.Remaining, Is.EqualTo(1));
            barrier.Acknowledge(7, "client");
            Assert.That(barrier.Complete, Is.True);
            Assert.That(barrier.TimedOut(100), Is.False);
        }

        [Test]
        public void LeavingDuringLoadInvalidatesGameRosterAndAllowsRemainingPlayersToReturn()
        {
            var barrier = new SceneLoadBarrier(8, new[] { "host", "client" }, 10, 45);
            barrier.Acknowledge(8, "host"); barrier.Remove("client");
            Assert.That(barrier.RosterChanged, Is.True);
            Assert.That(barrier.Complete, Is.True);
            Assert.That(barrier.Acknowledge(8, "client"), Is.False);
        }

        [Test]
        public void RemovingLoadedPlayerDoesNotCountTheirAcknowledgementForAnotherPlayer()
        {
            var barrier = new SceneLoadBarrier(9, new[] { "host", "a", "b" }, 0, 45);
            barrier.Acknowledge(9, "a"); barrier.Remove("a"); barrier.Remove("unknown");
            Assert.That(barrier.Remaining, Is.EqualTo(2));
            Assert.That(barrier.Complete, Is.False);
            barrier.Acknowledge(9, "host"); barrier.Acknowledge(9, "b");
            Assert.That(barrier.Complete, Is.True);
        }

        [Test]
        public void DeadlineCannotStartMatchWithMissingPlayers()
        {
            var barrier = new SceneLoadBarrier(10, new[] { "host", "client" }, 10, 45);
            barrier.Acknowledge(10, "host");
            Assert.That(barrier.TimedOut(54.99), Is.False);
            Assert.That(barrier.TimedOut(55), Is.True);
            Assert.That(barrier.Complete, Is.False);
            barrier.Remove("host"); barrier.Remove("client");
            Assert.That(barrier.Complete, Is.False);
        }

        [TestCase(0)] [TestCase(-1)] [TestCase(double.NaN)] [TestCase(double.PositiveInfinity)]
        public void InvalidTimeoutIsRejected(double timeout) =>
            Assert.Throws<ArgumentOutOfRangeException>(() => new SceneLoadBarrier(1, new[] { "host" }, 0, timeout));

        [Test]
        public void RosterCannotBeEmptyOrContainDuplicateIdentities()
        {
            Assert.Throws<ArgumentException>(() => new SceneLoadBarrier(1, Array.Empty<string>(), 0, 45));
            Assert.Throws<ArgumentException>(() => new SceneLoadBarrier(1, new[] { "host", "host" }, 0, 45));
            Assert.Throws<ArgumentException>(() => new SceneLoadBarrier(1, new[] { "host", " " }, 0, 45));
        }
    }
}
