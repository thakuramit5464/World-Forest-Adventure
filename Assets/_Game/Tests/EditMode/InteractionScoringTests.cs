using Greenhaven.Interaction;
using NUnit.Framework;
using UnityEngine;

namespace Greenhaven.Tests
{
    public class InteractionScoringTests
    {
        static readonly Vector3 Origin = Vector3.zero;
        static readonly Vector3 Forward = Vector3.forward;

        [Test]
        public void OutOfRange_IsInfinite()
        {
            float score = InteractionScoring.Score(Origin, Forward, new Vector3(0f, 0f, 3f), 2f);
            Assert.That(float.IsPositiveInfinity(score));
        }

        [Test]
        public void HeightDifference_DoesNotAffectRange()
        {
            // An NPC's interaction point is often at chest height; only planar distance counts.
            float score = InteractionScoring.Score(Origin, Forward, new Vector3(0f, 5f, 1.5f), 2f);
            Assert.That(float.IsPositiveInfinity(score), Is.False);
        }

        [Test]
        public void Closer_ScoresBetter_AtSameAngle()
        {
            float near = InteractionScoring.Score(Origin, Forward, new Vector3(0f, 0f, 0.8f), 2f);
            float far = InteractionScoring.Score(Origin, Forward, new Vector3(0f, 0f, 1.8f), 2f);
            Assert.That(near, Is.LessThan(far));
        }

        [Test]
        public void InFront_ScoresBetter_ThanBehind_AtSameDistance()
        {
            float front = InteractionScoring.Score(Origin, Forward, new Vector3(0f, 0f, 1.5f), 2f);
            float behind = InteractionScoring.Score(Origin, Forward, new Vector3(0f, 0f, -1.5f), 2f);
            Assert.That(front, Is.LessThan(behind));
        }

        [Test]
        public void Behind_IsStillSelectable_WhenInRange()
        {
            float behind = InteractionScoring.Score(Origin, Forward, new Vector3(0f, 0f, -1f), 2f);
            Assert.That(float.IsPositiveInfinity(behind), Is.False);
        }
    }
}
