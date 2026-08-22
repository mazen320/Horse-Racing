using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HorseRacing.Race.Tests
{
    public sealed class RaceSplineTapDriverPlayModeTests : InputTestFixture
    {
        [UnityTest]
        public IEnumerator MouseClick_DoesNotCreateRaceEffort()
        {
            LogAssert.ignoreFailingMessages = true;
            try
            {
                var load = SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
                while (!load.isDone) yield return null;

                var driver = Object.FindFirstObjectByType<RaceSplineTapDriver>();
                Assert.That(driver, Is.Not.Null);
                var mouse = InputSystem.AddDevice<Mouse>();
                Press(mouse.leftButton);
                yield return null;

                Assert.That(driver.Effort, Is.EqualTo(0f).Within(0.0001f));
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
            }
        }
    }
}
