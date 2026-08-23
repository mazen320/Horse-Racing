using NUnit.Framework;
using UnityEngine;

namespace HorseRacing.Race.Tests
{
    public sealed class RaceCameraHeadingModelTests
    {
        [Test]
        public void StepYaw_NeverExceedsConfiguredLag()
        {
            var velocity = 0f;
            var result = RaceCameraHeadingModel.StepYaw(
                0f, 30f, ref velocity, 1f / 60f, 0.08f, 8f, 45f);

            Assert.That(Mathf.Abs(Mathf.DeltaAngle(result, 30f)),
                Is.LessThanOrEqualTo(8.001f));
        }

        [Test]
        public void StepYaw_SnapsAcrossLargeDiscontinuity()
        {
            var velocity = 90f;
            var result = RaceCameraHeadingModel.StepYaw(
                0f, 100f, ref velocity, 1f / 60f, 0.08f, 8f, 45f);

            Assert.That(Mathf.DeltaAngle(result, 100f), Is.Zero.Within(0.001f));
            Assert.That(velocity, Is.Zero);
        }

        [Test]
        public void StepYaw_ForceSnapPlacesCameraBehindImmediately()
        {
            var velocity = 20f;
            var result = RaceCameraHeadingModel.StepYaw(
                270f, 15f, ref velocity, 0f, 0.08f, 8f, 45f, true);

            Assert.That(Mathf.DeltaAngle(result, 15f), Is.Zero.Within(0.001f));
            Assert.That(velocity, Is.Zero);
        }
    }
}
