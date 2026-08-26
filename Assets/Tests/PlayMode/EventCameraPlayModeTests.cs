using System.Collections;
using System.Linq;
using MalbersAnimations;
using MalbersAnimations.InputSystem;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HorseRacing.Race.Tests
{
    public sealed class EventCameraPlayModeTests
    {
        [UnityTest]
        public IEnumerator MainScene_UsesCenteredLockedBehindMountCamera()
        {
            var load = SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            while (!load.isDone) yield return null;
            yield return new WaitForEndOfFrame();

            var raceTargets = Object.FindObjectsByType<RaceCameraTarget>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.That(raceTargets.Length, Is.GreaterThanOrEqualTo(2),
                "Expected split-screen RaceCameraTarget for P1 and P2.");

            foreach (var raceTarget in raceTargets)
            {
                Assert.That(raceTarget.PositionAnchor, Is.Not.Null);
                Assert.That(raceTarget.HeadingSource, Is.Not.Null);
                Assert.That(raceTarget.SpeedSource, Is.Not.Null);
                Assert.That(raceTarget.MaxYawLagDegrees, Is.EqualTo(8f).Within(0.001f));
                Assert.That(raceTarget.BaseFieldOfView, Is.EqualTo(55f).Within(0.001f));
                Assert.That(raceTarget.SprintFieldOfView, Is.EqualTo(59f).Within(0.001f));
            }

            var cameras = Object.FindObjectsByType<CinemachineCamera>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(value => value.name.StartsWith("CM Third Person Mount") ||
                                value.name.StartsWith("CM Third Person Main"))
                .ToArray();
            Assert.That(cameras.Length, Is.EqualTo(4));

            var follows = Object.FindObjectsByType<CinemachineThirdPersonFollow>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            var mount = follows.Single(value => value.gameObject.name == "CM Third Person Mount");

            Assert.That(mount.Damping.x, Is.EqualTo(0.18f).Within(0.001f));
            Assert.That(mount.Damping.y, Is.EqualTo(0.24f).Within(0.001f));
            Assert.That(mount.Damping.z, Is.EqualTo(0.18f).Within(0.001f));
            Assert.That(mount.ShoulderOffset.x, Is.Zero.Within(0.001f));
            Assert.That(mount.ShoulderOffset.y, Is.Zero.Within(0.001f));
            Assert.That(mount.VerticalArmLength, Is.EqualTo(1.15f).Within(0.001f));
            Assert.That(mount.CameraDistance, Is.EqualTo(7.4f).Within(0.001f));
            Assert.That(mount.CameraSide, Is.EqualTo(0.5f).Within(0.001f));

            var brainP1 = GameObject.Find("CM Brain").GetComponent<Camera>();
            var brainP2 = GameObject.Find("CM Brain P2").GetComponent<Camera>();
            Assert.That(brainP1, Is.Not.Null);
            Assert.That(brainP2, Is.Not.Null);
            Assert.That(brainP1.rect, Is.EqualTo(new Rect(0f, 0f, 0.5f, 1f)));
            Assert.That(brainP2.rect, Is.EqualTo(new Rect(0.5f, 0f, 0.5f, 1f)));
            Assert.That(brainP1.nearClipPlane, Is.EqualTo(0.1f).Within(0.001f));
            Assert.That(brainP1.farClipPlane, Is.EqualTo(5000f).Within(0.001f));
            Assert.That(brainP2.GetComponent<AudioListener>().enabled, Is.False);

            var targets = Object.FindObjectsByType<ThirdPersonFollowTarget>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.That(targets, Is.Not.Empty);
            Assert.That(targets.All(value => !value.enabled), Is.True);

            var lookLinks = Object.FindObjectsByType<MInputLinkLook>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.That(lookLinks.All(value => !value.enabled), Is.True);

            var noises = Object.FindObjectsByType<CinemachineBasicMultiChannelPerlin>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.That(noises.All(value => value.AmplitudeGain == 0f), Is.True);

            var impulses = Object.FindObjectsByType<CinemachineExternalImpulseListener>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.That(impulses.All(value => !value.enabled || value.Gain == 0f), Is.True);

            Assert.That(QualitySettings.vSyncCount, Is.EqualTo(1));
        }
    }
}
