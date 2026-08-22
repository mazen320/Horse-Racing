using NUnit.Framework;

namespace HorseRacing.Race.Tests
{
    public sealed class GaitTravelSpeedModelTests
    {
        [Test]
        public void InPlaceGallop_IsFrameRateIndependent()
        {
            var atThirty = SimulateOneSecond(30);
            var atOneTwenty = SimulateOneSecond(120);

            Assert.That(atThirty, Is.EqualTo(atOneTwenty).Within(0.0001f));
            Assert.That(atThirty, Is.EqualTo(2.25f).Within(0.0001f));
        }

        [Test]
        public void Idle_StopsExactlyWithoutSelfMovement()
        {
            var model = new GaitTravelSpeedModel();
            model.Step(4, 0.5f, 1.6f, 3.2f, 5.2f, 7.2f, 4.5f);

            Assert.That(model.Step(0, 0.25f, 1.6f, 3.2f, 5.2f, 7.2f, 4.5f), Is.Zero);
            Assert.That(model.Speed, Is.Zero);
        }

        static float SimulateOneSecond(int frames)
        {
            var model = new GaitTravelSpeedModel();
            var deltaTime = 1f / frames;
            var distance = 0f;
            for (var frame = 0; frame < frames; frame++)
                distance += model.Step(4, deltaTime, 1.6f, 3.2f, 5.2f, 7.2f, 4.5f);
            return distance;
        }
    }
}
