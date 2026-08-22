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

            var follows = Object.FindObjectsByType<CinemachineThirdPersonFollow>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            var mount = follows.Single(value => value.gameObject.name == "CM Third Person Mount");

            Assert.That(mount.ShoulderOffset.x, Is.Zero.Within(0.001f));
            Assert.That(mount.ShoulderOffset.y, Is.EqualTo(0.2f).Within(0.001f));
            Assert.That(mount.VerticalArmLength, Is.EqualTo(0.8f).Within(0.001f));
            Assert.That(mount.CameraDistance, Is.EqualTo(6.75f).Within(0.001f));
            Assert.That(mount.CameraSide, Is.EqualTo(0.5f).Within(0.001f));

            var targets = Object.FindObjectsByType<ThirdPersonFollowTarget>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.That(targets, Is.Not.Empty);
            Assert.That(targets.All(value => !value.AllowCameraRotation.Value), Is.True);

            var lookLinks = Object.FindObjectsByType<MInputLinkLook>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.That(lookLinks.All(value => !value.enabled), Is.True);

            var noise = mount.GetComponent<CinemachineBasicMultiChannelPerlin>();
            Assert.That(noise == null || noise.AmplitudeGain == 0f, Is.True);
        }
    }
}
