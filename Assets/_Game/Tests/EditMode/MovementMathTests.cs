using Greenhaven.Player;
using NUnit.Framework;
using UnityEngine;

namespace Greenhaven.Tests
{
    public class MovementMathTests
    {
        const float Tolerance = 1e-4f;

        [Test]
        public void CameraRelative_ForwardInput_WithZeroYaw_MovesAlongWorldForward()
        {
            Vector3 dir = MovementMath.CameraRelative(new Vector2(0f, 1f), 0f);
            Assert.That(Vector3.Distance(dir, Vector3.forward), Is.LessThan(Tolerance));
        }

        [Test]
        public void CameraRelative_ForwardInput_WithCameraFacingEast_MovesEast()
        {
            Vector3 dir = MovementMath.CameraRelative(new Vector2(0f, 1f), 90f);
            Assert.That(Vector3.Distance(dir, Vector3.right), Is.LessThan(Tolerance));
        }

        [Test]
        public void CameraRelative_ClampsDiagonalKeyboardInputToUnitLength()
        {
            Vector3 dir = MovementMath.CameraRelative(new Vector2(1f, 1f), 30f);
            Assert.That(dir.magnitude, Is.EqualTo(1f).Within(Tolerance));
            Assert.That(dir.y, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void CameraRelative_PreservesPartialStickMagnitude()
        {
            Vector3 dir = MovementMath.CameraRelative(new Vector2(0f, 0.4f), 0f);
            Assert.That(dir.magnitude, Is.EqualTo(0.4f).Within(Tolerance));
        }

        [Test]
        public void JumpVelocity_ReachesRequestedHeight()
        {
            const float height = 1.2f;
            const float gravity = -22f;
            float v = MovementMath.JumpVelocity(height, gravity);
            // Apex height = v² / (2|g|).
            Assert.That(v * v / (2f * -gravity), Is.EqualTo(height).Within(Tolerance));
        }

        [TestCase(0f, -20f)]
        [TestCase(-1f, -20f)]
        [TestCase(1f, 0f)]
        [TestCase(1f, 5f)]
        public void JumpVelocity_InvalidInputs_ReturnZero(float height, float gravity)
        {
            Assert.That(MovementMath.JumpVelocity(height, gravity), Is.EqualTo(0f));
        }
    }
}
