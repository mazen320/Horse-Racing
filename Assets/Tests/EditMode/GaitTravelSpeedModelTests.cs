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
        public void Sprint_UsesCappedSprintSpeed()
        {
            var model = new GaitTravelSpeedModel();
            model.Step(5, 1f, 1.6f, 3.2f, 5.2f, 7.2f, 8.5f, 20f, 3f);

            Assert.That(model.Speed, Is.EqualTo(8.5f).Within(0.0001f));
        }

        [Test]
        public void SprintSpeed_IsBoundedForEventPresentation()
        {
            Assert.That(GaitTravelSpeedModel.ClampSprintSpeed(7.2f, 99f),
                Is.EqualTo(10.5f));
            Assert.That(GaitTravelSpeedModel.ClampSprintSpeed(7.2f, 6f),
                Is.EqualTo(7.2f));
        }

        [Test]
        public void ReleasedInput_CoastsThenStopsExactly()
        {
            var model = new GaitTravelSpeedModel();
            model.Step(5, 1f, 1.6f, 3.2f, 5.2f, 7.2f, 8.5f, 20f, 3f);

            var firstCoastDistance = model.Step(
                0, 0.5f, 1.6f, 3.2f, 5.2f, 7.2f, 8.5f, 20f, 3f);

            Assert.That(firstCoastDistance, Is.GreaterThan(0f));
            Assert.That(model.Speed, Is.EqualTo(7f).Within(0.0001f));

            for (var i = 0; i < 10; i++)
                model.Step(0, 0.5f, 1.6f, 3.2f, 5.2f, 7.2f, 8.5f, 20f, 3f);

            Assert.That(model.Speed, Is.Zero);
        }

        [Test]
        public void CoastingSpeed_KeepsALocomotionGaitUntilExactStop()
        {
            Assert.That(GaitTravelSpeedModel.SelectAnimationGait(
                0, 1.2f, 1.6f, 3.2f, 5.2f, 7.2f, 8.5f), Is.EqualTo(1));

            Assert.That(GaitTravelSpeedModel.SelectAnimationGait(
                0, 0f, 1.6f, 3.2f, 5.2f, 7.2f, 8.5f), Is.Zero);
        }

        static float SimulateOneSecond(int frames)
        {
            var model = new GaitTravelSpeedModel();
            var deltaTime = 1f / frames;
            var distance = 0f;
            for (var frame = 0; frame < frames; frame++)
                distance += model.Step(
                    4, deltaTime, 1.6f, 3.2f, 5.2f, 7.2f, 8.5f, 4.5f, 3f);
            return distance;
        }
    }
}
