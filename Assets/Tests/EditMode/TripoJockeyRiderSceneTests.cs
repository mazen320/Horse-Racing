using System.Linq;
using MalbersAnimations.HAP;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HorseRacing.Race.Tests
{
    public class TripoJockeyRiderSceneTests
    {
        const string MainScenePath = "Assets/Scenes/Main.unity";

        [Test]
        public void MainScene_TripoMeshUsesOriginalMalbersRiderRig()
        {
            var scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
            var raceSetup = scene.GetRootGameObjects().Single(root => root.name == "RaceSetup");
            var rider = raceSetup.transform.Find("Rider");

            Assert.That(rider, Is.Not.Null);
            Assert.That(rider.Find("JockeyVisual"), Is.Null,
                "The final fake must not add a second skeleton or Animator.");

            var animator = rider.GetComponent<Animator>();
            Assert.That(animator, Is.Not.Null);
            Assert.That(animator.enabled, Is.True);
            Assert.That(animator.avatar, Is.Not.Null);
            Assert.That(animator.avatar.isHuman, Is.True);
            Assert.That(animator.runtimeAnimatorController, Is.Not.Null);

            var cowboy = rider.Find("Mesh/Mesh/CowBoy");
            var renderer = cowboy != null ? cowboy.GetComponent<SkinnedMeshRenderer>() : null;
            Assert.That(renderer, Is.Not.Null);
            Assert.That(renderer.enabled, Is.True);
            Assert.That(renderer.gameObject.activeInHierarchy, Is.True);
            Assert.That(renderer.sharedMesh, Is.Not.Null);
            Assert.That(renderer.sharedMesh.name, Is.EqualTo("cowboy_no_hat_unity"),
                "The original Malbers renderer must display the edited no-hat cowboy mesh.");

            var originalRig = rider.Find("R_CG");
            Assert.That(originalRig, Is.Not.Null);
            Assert.That(originalRig.gameObject.activeSelf, Is.True,
                "The original Malbers skeleton must stay intact and active.");

            var malbersRider = rider.GetComponent<MRider>();
            Assert.That(malbersRider, Is.Not.Null);
            var riderProperties = new SerializedObject(malbersRider);
            Assert.That(riderProperties.FindProperty("LeftHand").objectReferenceValue,
                Is.SameAs(animator.GetBoneTransform(HumanBodyBones.LeftHand)));
            Assert.That(riderProperties.FindProperty("RightHand").objectReferenceValue,
                Is.SameAs(animator.GetBoneTransform(HumanBodyBones.RightHand)));
        }
    }
}
