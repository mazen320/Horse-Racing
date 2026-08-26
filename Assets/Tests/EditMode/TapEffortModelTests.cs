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

        [Test]
        public void Drive_KeepsSeparatingPacesAfterEffortIsCapped()
        {
            var steady = new TapEffortModel();
            var fast = new TapEffortModel();

            // 4 taps in the window is already double the 2 taps/second requirement, and
            // 8 taps is quadruple. Effort saturates for both; drive must not.
            for (var i = 0; i < 4; i++) steady.RegisterTap(i * 0.25f);
            for (var i = 0; i < 8; i++) fast.RegisterTap(i * 0.12f);

            var steadyEffort = steady.Tick(1f, 1f, 1f, 2f, 0f, 0f, 4f);
            var fastEffort = fast.Tick(1f, 1f, 1f, 2f, 0f, 0f, 4f);

            Assert.That(steadyEffort, Is.EqualTo(1f));
            Assert.That(fastEffort, Is.EqualTo(1f));
            Assert.That(fast.Drive, Is.GreaterThan(steady.Drive));
            Assert.That(fast.TapsPerSecond, Is.GreaterThan(steady.TapsPerSecond));
        }

        [Test]
        public void Drive_RespectsCeiling()
        {
            var model = new TapEffortModel();
            for (var i = 0; i < 40; i++) model.RegisterTap(i * 0.02f);

            model.Tick(1f, 1f, 1f, 2f, 0f, 0f, 3f);

            Assert.That(model.Drive, Is.EqualTo(3f).Within(0.0001f));
        }

        [Test]
        public void Drive_DefaultCeilingMatchesLegacyEffort()
        {
            var model = new TapEffortModel();
            for (var i = 0; i < 20; i++) model.RegisterTap(i * 0.05f);

            var effort = model.Tick(1f, 1f, 1f, 2f, 0f, 0f);

            Assert.That(effort, Is.EqualTo(1f));
            Assert.That(model.Drive, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void Reset_ClearsDrive()
        {
            var model = new TapEffortModel();
            for (var i = 0; i < 10; i++) model.RegisterTap(i * 0.05f);
            model.Tick(1f, 1f, 1f, 2f, 0f, 0f, 4f);

            model.Reset();

            Assert.That(model.Drive, Is.Zero);
            Assert.That(model.TapsPerSecond, Is.Zero);
            Assert.That(model.Effort, Is.Zero);
        }
    }
}
