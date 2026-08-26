using NUnit.Framework;
using UnityEngine.InputSystem;

namespace HorseRacing.Race.Tests
{
    public sealed class TapKeyParserTests
    {
        [TestCase("a", Key.A)]
        [TestCase("A", Key.A)]
        [TestCase("space", Key.Space)]
        [TestCase("SPACE", Key.Space)]
        [TestCase("Spacebar", Key.Space)]
        [TestCase("left arrow", Key.LeftArrow)]
        [TestCase("Up", Key.UpArrow)]
        public void TryParse_IsCaseInsensitive(string text, Key expected)
        {
            Assert.That(TapKeyParser.TryParse(text, out var key), Is.True);
            Assert.That(key, Is.EqualTo(expected));
        }

        [Test]
        public void ParseBindings_SplitsCommaSeparatedKeys()
        {
            var keys = TapKeyParser.ParseBindings("a, space");
            Assert.That(keys, Is.EqualTo(new[] { Key.A, Key.Space }));
        }

        [Test]
        public void TryParse_RejectsEmptyText()
        {
            Assert.That(TapKeyParser.TryParse(string.Empty, out _), Is.False);
            Assert.That(TapKeyParser.TryParse("not-a-real-key", out _), Is.False);
        }
    }
}
