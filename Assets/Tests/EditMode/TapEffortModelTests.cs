using NUnit.Framework;

namespace HorseRacing.Race.Tests
{
    public sealed class TapEffortModelTests
    {
        [Test]
        public void ExtremeSpam_IsBoundedAndClamped()
        {
            var model = new TapEffortModel();
            for (var i = 0; i < 100; i++) model.RegisterTap(i * 0.001f);

            var effort = model.Tick(0.1f, 0.02f, 1f, 4f, 0f, 0.5f);

            Assert.That(model.TapCount, Is.EqualTo(TapEffortModel.MaxTapHistory));
            Assert.That(effort, Is.EqualTo(1f));
        }

        [Test]
        public void ReleasedInput_ReachesExactIdle()
        {
            var model = new TapEffortModel();
            model.RegisterTap(0f);
            model.Tick(0f, 0.02f, 0.5f, 1f, 0f, 0.2f);

            Assert.That(model.Tick(2f, 1f, 0.5f, 1f, 0f, 0.2f), Is.Zero);
            Assert.That(model.TapCount, Is.Zero);
        }

        [Test]
        public void SelectGait_UsesLowerThresholdWhenDropping()
        {
            Assert.That(TapEffortModel.SelectGait(0.28f, 1, 0.08f, 0.28f, 0.52f, 0.78f, 0.92f, 0.05f), Is.EqualTo(2));
            Assert.That(TapEffortModel.SelectGait(0.25f, 2, 0.08f, 0.28f, 0.52f, 0.78f, 0.92f, 0.05f), Is.EqualTo(2));
            Assert.That(TapEffortModel.SelectGait(0.22f, 2, 0.08f, 0.28f, 0.52f, 0.78f, 0.92f, 0.05f), Is.EqualTo(1));
        }

        [Test]
        public void SelectGait_SprintUsesSeparateExitThreshold()
        {
            Assert.That(TapEffortModel.SelectGait(
                0.92f, 4, 0.08f, 0.28f, 0.52f, 0.78f, 0.92f, 0.06f), Is.EqualTo(5));
            Assert.That(TapEffortModel.SelectGait(
                0.87f, 5, 0.08f, 0.28f, 0.52f, 0.78f, 0.92f, 0.06f), Is.EqualTo(5));
            Assert.That(TapEffortModel.SelectGait(
                0.85f, 5, 0.08f, 0.28f, 0.52f, 0.78f, 0.92f, 0.06f), Is.EqualTo(4));
        }

        [Test]
        public void SelectGait_ZeroEffortAlwaysReturnsToIdle()
        {
            Assert.That(TapEffortModel.SelectGait(
                0f, 1, 0.06f, 0.2f, 0.4f, 0.65f, 0.85f, 0.06f), Is.Zero);
        }
    }
}
