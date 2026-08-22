using NUnit.Framework;
using UnityEngine;

namespace HorseRacing.Race.Tests
{
    public sealed class RootMotionDistanceAccumulatorTests
    {
        [Test]
        public void Consume_ReturnsHorizontalDistanceOnlyOnce()
        {
            var value = new RootMotionDistanceAccumulator();
            value.Add(new Vector3(0f, 5f, 3f));
            value.Add(new Vector3(4f, -2f, 0f));

            Assert.That(value.Consume(), Is.EqualTo(7f).Within(0.0001f));
            Assert.That(value.Consume(), Is.Zero);
        }
    }
}
