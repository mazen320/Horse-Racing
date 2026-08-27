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
        public override void Setup()
        {
            // Set before the base fixture runs: resetting the Input System is itself one of
            // the things that throws from third-party scene furniture.
            IgnoreThirdPartySceneLogs();
            base.Setup();
        }

        public override void TearDown()
        {
            IgnoreThirdPartySceneLogs();
            base.TearDown();
        }

        /// <summary>
        /// Scene furniture in this project logs exceptions the race code has no say over:
        /// the Input System's unpaired-device counter throws when rider prefabs are
        /// disabled, and UUtility's tab system tweens a RectTransform that reloading the
        /// scene has already destroyed. Race behaviour is asserted directly instead. The
        /// runner clears this per test, so every test sets it again as it starts.
        /// </summary>
        static void IgnoreThirdPartySceneLogs() => LogAssert.ignoreFailingMessages = true;

        /// <summary>
        /// Binds W and opens the input gate. The scene owns both of these: RaceTapKeySettings
        /// applies the per-player keys from the UTool panel, and the HUD parks every horse at
        /// the grid until the countdown clears. Either can land after the test has started, so
        /// this is called again on every frame that taps.
        /// </summary>
        static void ArmDriverForTapping(RaceSplineTapDriver driver)
        {
            driver.SetPrimaryTapKey("W");
            driver.SetRaceInputEnabled(true);
        }

        static IEnumerator LoadMainScene()
        {
            IgnoreThirdPartySceneLogs();
            var load = SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            while (!load.isDone) yield return null;
        }

        [UnityTest]
        public IEnumerator RacePresentation_UsesRenderSynchronizedClocksAndTunedSprint()
        {
            yield return LoadMainScene();
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
            // RaceFinishLine derives the race distance from the winning post, so the event
            // runs to that line rather than a whole lap of the spline.
            Assert.That(driver.raceFullSpline, Is.False);
            Assert.That(driver.ActiveRaceDistance, Is.InRange(560f, 575f));
            Assert.That(driver.courseSpeedMultiplier,
                Is.EqualTo(1.35f).Within(0.001f));
            Assert.That(driver.tapsPerSecondForMax,
                Is.EqualTo(2.2f).Within(0.001f));
            Assert.That(driver.EstimatedBestTimeSeconds, Is.InRange(24f, 30f));
        }

        [UnityTest]
        public IEnumerator HandsOff_RemainsIdleAndStationary()
        {
            yield return LoadMainScene();

            var driver = Object.FindFirstObjectByType<RaceSplineTapDriver>();
            Assert.That(driver, Is.Not.Null);
            yield return new WaitForEndOfFrame();
            var startPosition = driver.transform.position;
            yield return new WaitForSecondsRealtime(1f);
            yield return new WaitForEndOfFrame();

            Assert.That(driver.Effort, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(Vector3.Distance(driver.transform.position, startPosition),
                Is.LessThan(0.0001f));
            Assert.That(driver.animal.ActiveState, Is.Not.Null);
            Assert.That(driver.animal.ActiveState.ID.ID, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator ConfiguredKeyboardTap_CreatesRaceEffort()
        {
            yield return LoadMainScene();

            var driver = Object.FindFirstObjectByType<RaceSplineTapDriver>();
            Assert.That(driver, Is.Not.Null);
            var keyboard = InputSystem.AddDevice<Keyboard>();
            ArmDriverForTapping(driver);
            Press(keyboard.wKey);
            yield return null;

            Assert.That(driver.Effort, Is.GreaterThan(0f));
        }

        [UnityTest]
        public IEnumerator KeyboardSpam_MovesWithLocomotionAnimationThenStopsAtIdle()
        {
            yield return LoadMainScene();

            var driver = Object.FindFirstObjectByType<RaceSplineTapDriver>();
            Assert.That(driver, Is.Not.Null);
            var keyboard = InputSystem.AddDevice<Keyboard>();
            ArmDriverForTapping(driver);
            yield return new WaitForEndOfFrame();
            var startPosition = driver.transform.position;

            var spamUntil = Time.realtimeSinceStartup + 2.25f;
            while (Time.realtimeSinceStartup < spamUntil)
            {
                ArmDriverForTapping(driver);
                Press(keyboard.wKey);
                yield return null;
                Release(keyboard.wKey);
                yield return null;
            }

            yield return new WaitForEndOfFrame();
            Assert.That(Vector3.Distance(driver.transform.position, startPosition),
                Is.GreaterThan(0.5f));
            Assert.That(driver.animal.LockForwardMovement, Is.False);
            Assert.That(driver.animal.VerticalSmooth, Is.GreaterThan(0.1f));
            Assert.That(driver.RequestedGait, Is.EqualTo(5));
            Assert.That(driver.AnimationGait, Is.EqualTo(5));
            Assert.That(driver.TravelSpeed, Is.GreaterThan(driver.gallopMetersPerSecond));
            // Visual sprint may use Malbers' modifier or its stable fastest speed slot.
            // Either configuration must keep gait 5 on the fastest locomotion index.
            Assert.That(driver.animal.CurrentSpeedIndex, Is.EqualTo(4));
            Assert.That(!driver.animal.Sprint || driver.animal.CanSprint, Is.True);
            Assert.That(driver.animator.GetCurrentAnimatorClipInfo(0)
                .Any(info => info.clip && !info.clip.name.Contains("Idle")), Is.True);

            var releasePosition = driver.transform.position;
            yield return null;
            yield return new WaitForEndOfFrame();
            Assert.That(driver.TravelSpeed, Is.GreaterThan(0f));
            Assert.That(driver.AnimationGait, Is.GreaterThan(0));
            Assert.That(Vector3.Distance(driver.transform.position, releasePosition),
                Is.GreaterThan(0f));

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
            Assert.That(Vector3.Distance(driver.transform.position, stoppedPosition),
                Is.LessThan(0.0001f));
        }

        [UnityTest]
        public IEnumerator FasterTapping_CoversMoreGroundThanASteadyPace()
        {
            yield return LoadMainScene();

            var driver = Object.FindFirstObjectByType<RaceSplineTapDriver>();
            Assert.That(driver, Is.Not.Null);

            var steady = 0f;
            yield return MeasureTravel(driver, 0.34f, result => steady = result);

            var quick = 0f;
            yield return MeasureTravel(driver, 0.18f, result => quick = result);

            // Both paces sit above the top gait's tap requirement, where the game used to
            // hand out exactly the same speed however much harder someone was running.
            Assert.That(steady, Is.GreaterThan(0f));
            Assert.That(quick, Is.GreaterThan(steady * 1.05f));
        }

        /// <summary>Taps at a fixed interval for two seconds and reports metres covered.</summary>
        static IEnumerator MeasureTravel(RaceSplineTapDriver driver, float tapInterval,
            System.Action<float> report)
        {
            driver.RestartRace();
            ArmDriverForTapping(driver);
            yield return new WaitForEndOfFrame();

            var startPosition = driver.transform.position;
            var runUntil = Time.realtimeSinceStartup + 2f;
            var nextTap = 0f;
            while (Time.realtimeSinceStartup < runUntil)
            {
                ArmDriverForTapping(driver);
                if (Time.realtimeSinceStartup >= nextTap)
                {
                    driver.RegisterTap();
                    nextTap = Time.realtimeSinceStartup + tapInterval;
                }

                yield return null;
            }

            yield return new WaitForEndOfFrame();
            report(Vector3.Distance(driver.transform.position, startPosition));
        }

        [UnityTest]
        public IEnumerator ShortEventRace_FinishesOnceStopsAndCanRestart()
        {
            yield return LoadMainScene();

            var driver = Object.FindFirstObjectByType<RaceSplineTapDriver>();
            Assert.That(driver, Is.Not.Null);
            driver.raceFullSpline = false;
            driver.raceDistanceMeters = 0.5f;
            driver.courseSpeedMultiplier = 1f;
            var finishCount = 0;
            driver.onRaceFinished.AddListener(() => finishCount++);

            var keyboard = InputSystem.AddDevice<Keyboard>();
            var timeout = Time.realtimeSinceStartup + 3f;
            while (!driver.IsFinished && Time.realtimeSinceStartup < timeout)
            {
                ArmDriverForTapping(driver);
                Press(keyboard.wKey);
                yield return null;
                Release(keyboard.wKey);
                yield return null;
            }

            Assert.That(driver.IsFinished, Is.True);
            Assert.That(driver.DistanceTravelled, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(driver.RaceProgress, Is.EqualTo(1f));
            Assert.That(finishCount, Is.EqualTo(1));

            // A horse carrying pace over the line runs out past it before pulling up.
            var runOutTimeout = Time.realtimeSinceStartup + 8f;
            while (driver.IsRunningOut && Time.realtimeSinceStartup < runOutTimeout)
                yield return null;

            yield return new WaitForEndOfFrame();
            Assert.That(driver.TravelSpeed, Is.Zero);
            Assert.That(driver.RequestedGait, Is.Zero);
            Assert.That(driver.DistanceTravelled, Is.EqualTo(0.5f).Within(0.0001f));

            var finishPosition = driver.transform.position;
            ArmDriverForTapping(driver);
            Press(keyboard.wKey);
            yield return null;
            Release(keyboard.wKey);
            yield return new WaitForSecondsRealtime(0.1f);
            yield return new WaitForEndOfFrame();
            Assert.That(Vector3.Distance(driver.transform.position, finishPosition),
                Is.LessThan(0.0001f));
            Assert.That(finishCount, Is.EqualTo(1));

            driver.RestartRace();
            Assert.That(driver.IsFinished, Is.False);
            Assert.That(driver.DistanceTravelled, Is.Zero);
            Assert.That(driver.RaceProgress, Is.Zero);
        }

        [UnityTest]
        public IEnumerator PullUp_KeepsCoastingThenStopsWithoutFinishing()
        {
            yield return LoadMainScene();

            var driver = Object.FindFirstObjectByType<RaceSplineTapDriver>();
            Assert.That(driver, Is.Not.Null);
            var keyboard = InputSystem.AddDevice<Keyboard>();

            var spamUntil = Time.realtimeSinceStartup + 1.5f;
            while (Time.realtimeSinceStartup < spamUntil)
            {
                ArmDriverForTapping(driver);
                Press(keyboard.wKey);
                yield return null;
                Release(keyboard.wKey);
                yield return null;
            }

            Assert.That(driver.TravelSpeed, Is.GreaterThan(0f));

            // The rider behind loses the moment the winner crosses, and easing down beats
            // freezing mid-stride.
            driver.PullUpAndStopRacing();
            Assert.That(driver.IsRunningOut, Is.True);
            Assert.That(driver.IsFinished, Is.False);

            var pullUpPosition = driver.transform.position;
            yield return null;
            yield return new WaitForEndOfFrame();
            Assert.That(Vector3.Distance(driver.transform.position, pullUpPosition),
                Is.GreaterThan(0f));

            var timeout = Time.realtimeSinceStartup + 8f;
            while (driver.IsRunningOut && Time.realtimeSinceStartup < timeout)
                yield return null;

            yield return new WaitForEndOfFrame();
            Assert.That(driver.IsRunningOut, Is.False);
            Assert.That(driver.AnimationGait, Is.Zero);
            Assert.That(driver.IsFinished, Is.False);

            var stoppedPosition = driver.transform.position;
            Press(keyboard.wKey);
            yield return null;
            Release(keyboard.wKey);
            yield return new WaitForSecondsRealtime(0.2f);
            yield return new WaitForEndOfFrame();
            Assert.That(Vector3.Distance(driver.transform.position, stoppedPosition),
                Is.LessThan(0.0001f));
        }

        [UnityTest]
        public IEnumerator MouseClick_DoesNotCreateRaceEffort()
        {
            yield return LoadMainScene();

            var driver = Object.FindFirstObjectByType<RaceSplineTapDriver>();
            Assert.That(driver, Is.Not.Null);
            var mouse = InputSystem.AddDevice<Mouse>();
            Press(mouse.leftButton);
            yield return null;

            Assert.That(driver.Effort, Is.EqualTo(0f).Within(0.0001f));
        }
    }
}
