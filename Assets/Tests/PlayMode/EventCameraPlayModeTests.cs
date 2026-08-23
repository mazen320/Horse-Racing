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

            var raceTarget = Object.FindFirstObjectByType<RaceCameraTarget>();
            Assert.That(raceTarget, Is.Not.Null);
            Assert.That(raceTarget.PositionAnchor, Is.Not.Null);
            Assert.That(raceTarget.HeadingSource, Is.Not.Null);
            Assert.That(raceTarget.SpeedSource, Is.Not.Null);
            Assert.That(raceTarget.MaxYawLagDegrees, Is.EqualTo(8f).Within(0.001f));
            Assert.That(raceTarget.BaseFieldOfView, Is.EqualTo(42.5f).Within(0.001f));
            Assert.That(raceTarget.SprintFieldOfView, Is.EqualTo(44.5f).Within(0.001f));

            var cameras = Object.FindObjectsByType<CinemachineCamera>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(value => value.name == "CM Third Person Mount" ||
                                value.name == "CM Third Person Main")
                .ToArray();
            Assert.That(cameras, Has.Length.EqualTo(2));
            Assert.That(cameras.All(value =>
                value.Target.TrackingTarget == raceTarget.transform), Is.True);
            Assert.That(cameras.All(value =>
                value.Lens.NearClipPlane == 0.1f && value.Lens.FarClipPlane == 5000f), Is.True);

            var follows = Object.FindObjectsByType<CinemachineThirdPersonFollow>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            var mount = follows.Single(value => value.gameObject.name == "CM Third Person Mount");

            Assert.That(mount.Damping.x, Is.EqualTo(0.18f).Within(0.001f));
            Assert.That(mount.Damping.y, Is.EqualTo(0.24f).Within(0.001f));
            Assert.That(mount.Damping.z, Is.EqualTo(0.18f).Within(0.001f));
            Assert.That(mount.ShoulderOffset.x, Is.Zero.Within(0.001f));
            Assert.That(mount.ShoulderOffset.y, Is.Zero.Within(0.001f));
            Assert.That(mount.VerticalArmLength, Is.EqualTo(0.25f).Within(0.001f));
            Assert.That(mount.CameraDistance, Is.EqualTo(6.5f).Within(0.001f));
            Assert.That(mount.CameraSide, Is.EqualTo(0.5f).Within(0.001f));

            var mountCamera = cameras.Single(value => value.name == "CM Third Person Mount");
            Assert.That(mountCamera.Lens.FieldOfView, Is.EqualTo(42.5f).Within(0.001f));

            var outputCamera = GameObject.Find("CM Brain").GetComponent<Camera>();
            Assert.That(outputCamera.nearClipPlane, Is.EqualTo(0.1f).Within(0.001f));
            Assert.That(outputCamera.farClipPlane, Is.EqualTo(5000f).Within(0.001f));

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
