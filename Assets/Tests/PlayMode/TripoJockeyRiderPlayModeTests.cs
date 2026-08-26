using System.Collections;
using System.Linq;
using MalbersAnimations.HAP;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HorseRacing.Race.Tests
{
    public sealed class TripoJockeyRiderPlayModeTests
    {
        [UnityTest]
        public IEnumerator MainScene_TripoMeshAnimatesOnOriginalMountedRig()
        {
            LogAssert.ignoreFailingMessages = true;
            try
            {
                var load = SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
                while (!load.isDone) yield return null;
                yield return new WaitForSecondsRealtime(2.5f);
                yield return new WaitForEndOfFrame();

                var rider = GameObject.Find("Rider");
                Assert.That(rider, Is.Not.Null);
                Assert.That(rider.transform.Find("JockeyVisual"), Is.Null,
                    "A second humanoid rig must not be present.");

                var animator = rider.GetComponent<Animator>();
                var malbersRider = rider.GetComponent<MRider>();
                Assert.That(animator, Is.Not.Null);
                Assert.That(animator.enabled, Is.True);
                Assert.That(animator.isHuman, Is.True);
                Assert.That(animator.runtimeAnimatorController, Is.Not.Null);
                Assert.That(malbersRider, Is.Not.Null);
                Assert.That(malbersRider.IsRiding, Is.True);

                var renderer = rider.transform.Find("Mesh/Mesh/CowBoy")
                    ?.GetComponent<SkinnedMeshRenderer>();
                Assert.That(renderer, Is.Not.Null);
                Assert.That(renderer.enabled, Is.True);
                Assert.That(renderer.sharedMesh.name, Is.EqualTo("jockey_malbers_unity"));

                var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                var head = animator.GetBoneTransform(HumanBodyBones.Head);
                var leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                var rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                Assert.That(hips, Is.Not.Null);
                Assert.That(head, Is.Not.Null);
                Assert.That(leftHand, Is.Not.Null);
                Assert.That(rightHand, Is.Not.Null);
                Assert.That(Vector3.Distance(head.position, hips.position), Is.GreaterThan(0.4f));
                Assert.That(Vector3.Distance(leftHand.position, rightHand.position), Is.GreaterThan(0.1f));
                Assert.That(animator.GetCurrentAnimatorClipInfo(1).Length, Is.GreaterThan(0),
                    "The original Malbers mounted layer must actively pose the Tripo mesh.");

                var driver = Object.FindAnyObjectByType<RaceSplineTapDriver>();
                Assert.That(driver, Is.Not.Null);
                driver.SetRaceInputEnabled(true);
                for (var i = 0; i < 12; i++) driver.RegisterTap();
                yield return new WaitForSecondsRealtime(0.65f);
                for (var i = 0; i < 12; i++) driver.RegisterTap();
                yield return new WaitForSecondsRealtime(0.85f);
                yield return new WaitForEndOfFrame();

                var mountedClips = animator.GetCurrentAnimatorClipInfo(1)
                    .Select(info => info.clip.name)
                    .ToArray();
                var torso = head.position - hips.position;
                Assert.That(driver.AnimationGait, Is.EqualTo(5));
                Assert.That(mountedClips, Has.Some.Contains("Rider_"));
                Assert.That(Vector3.Dot(torso.normalized, driver.transform.forward), Is.GreaterThan(0.1f),
                    "The sprinting jockey must lean with the horse, not backward from a reversed retarget rig.");
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
            }
        }
    }
}
