using NUnit.Framework;
using UnityEngine.InputSystem;

namespace HorseRacing.Race.Tests
{
    public sealed class ConfigurableKeyboardTapInputPlayModeTests : InputTestFixture
    {
        [Test]
        public void Polling_AcceptsOnlyConfiguredKey()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var input = new ConfigurableKeyboardTapInput();
            input.SetBindings(new[] { Key.A });

            Press(keyboard.bKey);
            Assert.That(input.WasPressedThisFrame(keyboard), Is.False);
            Release(keyboard.bKey);
            Press(keyboard.aKey);
            Assert.That(input.WasPressedThisFrame(keyboard), Is.True);
        }
    }
}
