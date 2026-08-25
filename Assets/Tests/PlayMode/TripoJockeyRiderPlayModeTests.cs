using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HorseRacing.Race.Tests
{
    public sealed class TripoJockeyRiderPlayModeTests
    {
        [UnityTest]
        public IEnumerator MainScene_TripoJockeyAnimatesOnSingleRigWhenMounted()
        {
            LogAssert.ignoreFailingMessages = true;
            try
            {
                var load = SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
                while (!load.isDone) yield return null;
                yield return new WaitForSecondsRealtime(1.5f);
                yield return new WaitForEndOfFrame();

                var rider = GameObject.Find("Rider");
                Assert.That(rider, Is.Not.Null);

                var jockeyVisual = rider.transform.Find("JockeyVisual");
                var jockeyAnimator = jockeyVisual != null ? jockeyVisual.GetComponent<Animator>() : null;
                var riderAnimator = rider.GetComponent<Animator>();

                Assert.That(jockeyVisual, Is.Not.Null);
                Assert.That(jockeyAnimator, Is.Not.Null, "Tripo Animator must live on JockeyVisual.");
                Assert.That(riderAnimator == null || !riderAnimator.enabled,
                    "Rider root Animator must not compete with the Tripo visual rig.");
                Assert.That(jockeyAnimator.avatar, Is.Not.Null);
                Assert.That(jockeyAnimator.avatar.isHuman, Is.True);
                Assert.That(jockeyAnimator.runtimeAnimatorController, Is.Not.Null);
                Assert.That(jockeyAnimator.applyRootMotion, Is.False);

                var hips = jockeyAnimator.GetBoneTransform(HumanBodyBones.Hips);
                var head = jockeyAnimator.GetBoneTransform(HumanBodyBones.Head);
                Assert.That(hips, Is.Not.Null);
                Assert.That(head, Is.Not.Null);
                Assert.That(Vector3.Distance(head.position, hips.position), Is.GreaterThan(0.4f),
                    "Head and hips should be separated — jockey skeleton is present and posed.");

                var leftHand = jockeyAnimator.GetBoneTransform(HumanBodyBones.LeftHand);
                var rightHand = jockeyAnimator.GetBoneTransform(HumanBodyBones.RightHand);
                Assert.That(leftHand, Is.Not.Null);
                Assert.That(rightHand, Is.Not.Null);
                Assert.That(Vector3.Distance(leftHand.position, rightHand.position), Is.GreaterThan(0.1f),
                    "Hands should be separated — riding pose is active.");

                var visibleTripoMesh = jockeyVisual.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .Any(renderer => renderer.enabled && renderer.gameObject.activeInHierarchy);
                Assert.That(visibleTripoMesh, Is.True, "Tripo mesh must be visible.");

                var horse = Object.FindAnyObjectByType<RaceSplineTapDriver>();
                Assert.That(horse, Is.Not.Null);

                var stateInfo = jockeyAnimator.GetCurrentAnimatorStateInfo(0);
                Assert.That(stateInfo.fullPathHash, Is.Not.EqualTo(0),
                    "Tripo Animator should be in an active Malbers riding state.");
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
            }
        }
    }
}
