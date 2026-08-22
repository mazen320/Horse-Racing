using NUnit.Framework;
using UnityEngine.InputSystem;

namespace HorseRacing.Race.Tests
{
    public sealed class ConfigurableKeyboardTapInputTests
    {
        [Test]
        public void SetBindings_RemovesNoneAndDuplicates()
        {
            var input = new ConfigurableKeyboardTapInput();
            input.SetBindings(new[] { Key.Space, Key.None, Key.Space, Key.A });
            Assert.That(input.BindingCount, Is.EqualTo(2));
        }
    }
}
