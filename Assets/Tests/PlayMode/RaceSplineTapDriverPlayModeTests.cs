using System.Collections;
using System.Linq;
using MalbersAnimations.HAP;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HorseRacing.Race.Tests
{
    public sealed class RaceSplineTapDriverPlayModeTests : InputTestFixture
    {
        [UnityTest]
        public IEnumerator RacePresentation_UsesRenderSynchronizedClocksAndTunedSprint()
        {
            LogAssert.ignoreFailingMessages = true;
            try
            {
                var load = SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
                while (!load.isDone) yield return null;
                yield return new WaitForEndOfFrame();

                var driver = Object.FindFirstObjectByType<RaceSplineTapDriver>();
                var rider = Object.FindFirstObjectByType<MRider>();
                var brain = Object.FindFirstObjectByType<CinemachineBrain>();

                Assert.That(driver, Is.Not.Null);
                Assert.That(rider, Is.Not.Null);
                Assert.That(brain, Is.Not.Null);
                Assert.That(driver.animator.updateMode, Is.EqualTo(AnimatorUpdateMode.Normal));
                Assert.That(rider.Anim.updateMode, Is.EqualTo(AnimatorUpdateMode.Normal));
                Assert.That(brain.UpdateMethod,
                    Is.EqualTo(CinemachineBrain.UpdateMethods.LateUpdate));
                Assert.That(driver.sprintMetersPerSecond,
                    Is.EqualTo(9.25f).Within(0.001f));
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
            }
        }

        [UnityTest]
        public IEnumerator HandsOff_RemainsIdleAndStationary()
        {
            LogAssert.ignoreFailingMessages = true;
            try
            {
                var load = SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
                while (!load.isDone) yield return null;

                var driver = Object.FindFirstObjectByType<RaceSplineTapDriver>();
                Assert.That(driver, Is.Not.Null);
                yield return new WaitForEndOfFrame();
                var startPosition = driver.transform.position;
                yield return new WaitForSecondsRealtime(1f);
                yield return new WaitForEndOfFrame();

                Assert.That(driver.Effort, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(Vector3.Distance(driver.transform.position, startPosition), Is.LessThan(0.0001f));
                Assert.That(driver.animal.ActiveState, Is.Not.Null);
                Assert.That(driver.animal.ActiveState.ID.ID, Is.EqualTo(0));
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
            }
        }

        [UnityTest]
        public IEnumerator ConfiguredKeyboardTap_CreatesRaceEffort()
        {
            LogAssert.ignoreFailingMessages = true;
            try
            {
                var load = SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
                while (!load.isDone) yield return null;

                var driver = Object.FindFirstObjectByType<RaceSplineTapDriver>();
                Assert.That(driver, Is.Not.Null);
                var keyboard = InputSystem.AddDevice<Keyboard>();
                Press(keyboard.spaceKey);
                yield return null;

                Assert.That(driver.Effort, Is.GreaterThan(0f));
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
            }
        }

        [UnityTest]
        public IEnumerator KeyboardSpam_MovesWithLocomotionAnimationThenStopsAtIdle()
        {
            LogAssert.ignoreFailingMessages = true;
            try
            {
                var load = SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
                while (!load.isDone) yield return null;

                var driver = Object.FindFirstObjectByType<RaceSplineTapDriver>();
                Assert.That(driver, Is.Not.Null);
                var keyboard = InputSystem.AddDevice<Keyboard>();
                yield return new WaitForEndOfFrame();
                var startPosition = driver.transform.position;

                var spamUntil = Time.realtimeSinceStartup + 2.25f;
                while (Time.realtimeSinceStartup < spamUntil)
                {
                    Press(keyboard.wKey);
                    yield return null;
                    Release(keyboard.wKey);
                    yield return null;
                }

                yield return new WaitForEndOfFrame();
                Assert.That(Vector3.Distance(driver.transform.position, startPosition), Is.GreaterThan(0.5f));
                Assert.That(driver.animal.LockForwardMovement, Is.False);
                Assert.That(driver.animal.VerticalSmooth, Is.GreaterThan(0.1f));
                Assert.That(driver.RequestedGait, Is.EqualTo(5));
                Assert.That(driver.AnimationGait, Is.EqualTo(5));
                Assert.That(driver.TravelSpeed, Is.GreaterThan(driver.gallopMetersPerSecond));
                Assert.That(driver.animal.CanSprint, Is.True);
                Assert.That(driver.animal.Sprint, Is.True);
                Assert.That(driver.animal.CurrentSpeedIndex,
                    Is.EqualTo(driver.animal.CurrentSpeedSet.SprintIndex));
                Assert.That(driver.animator.GetCurrentAnimatorClipInfo(0)
                    .Any(info => info.clip && !info.clip.name.Contains("Idle")), Is.True);

                var releasePosition = driver.transform.position;
                yield return null;
                yield return new WaitForEndOfFrame();
                Assert.That(driver.TravelSpeed, Is.GreaterThan(0f));
                Assert.That(driver.AnimationGait, Is.GreaterThan(0));
                Assert.That(Vector3.Distance(driver.transform.position, releasePosition), Is.GreaterThan(0f));

                var coastTimeout = Time.realtimeSinceStartup + 3f;
                while (driver.RequestedGait >= 5 && Time.realtimeSinceStartup < coastTimeout)
                    yield return null;
                var coastSpeed = driver.TravelSpeed;
                yield return new WaitForSecondsRealtime(0.1f);
                Assert.That(driver.TravelSpeed, Is.LessThan(coastSpeed));

                var releaseTimeout = Time.realtimeSinceStartup + 8f;
                while ((driver.Effort > 0f || driver.TravelSpeed > 0f) &&
                       Time.realtimeSinceStartup < releaseTimeout)
                    yield return null;

                yield return new WaitForEndOfFrame();
                Assert.That(driver.Effort, Is.Zero.Within(0.0001f));
                Assert.That(driver.TravelSpeed, Is.Zero.Within(0.0001f));
                Assert.That(driver.AnimationGait, Is.Zero);
                Assert.That(driver.animal.ActiveState.ID.ID, Is.EqualTo(0));
                var stoppedPosition = driver.transform.position;
                yield return new WaitForSecondsRealtime(0.25f);
                yield return new WaitForEndOfFrame();
                Assert.That(Vector3.Distance(driver.transform.position, stoppedPosition), Is.LessThan(0.0001f));
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
            }
        }

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
