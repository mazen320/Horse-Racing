using NUnit.Framework;

namespace HorseRacing.Race.Tests
{
    public sealed class EventRaceProgressModelTests
    {
        [Test]
        public void Advance_StopsExactlyAtEventFinish()
        {
            var model = new EventRaceProgressModel();

            Assert.That(model.Advance(80f, 125f), Is.EqualTo(80f));
            Assert.That(model.Advance(60f, 125f), Is.EqualTo(45f));
            Assert.That(model.DistanceTravelled, Is.EqualTo(125f));
            Assert.That(model.Progress(125f), Is.EqualTo(1f));
            Assert.That(model.IsFinished, Is.True);
        }

        [Test]
        public void FinishedRace_RejectsMoreTravelUntilReset()
        {
            var model = new EventRaceProgressModel();
            model.Advance(10f, 10f);

            Assert.That(model.Advance(5f, 10f), Is.Zero);

            model.Reset();
            Assert.That(model.IsFinished, Is.False);
            Assert.That(model.DistanceTravelled, Is.Zero);
            Assert.That(model.Progress(10f), Is.Zero);
        }
    }
}
