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
        const string JockeyModelPath = "Assets/TripoModels/jockey_3d_model/jockey_3d_model.fbx";

        [Test]
        public void MainScene_RiderUsesSingleTripoHumanoidWithMalbersController()
        {
            var scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
            var raceSetup = scene.GetRootGameObjects().Single(root => root.name == "RaceSetup");
            var rider = raceSetup.transform.Find("Rider");

            Assert.That(rider, Is.Not.Null, "RaceSetup/Rider is required by the mounted race setup.");

            var jockeyAsset = AssetDatabase.LoadAssetAtPath<GameObject>(JockeyModelPath);
            var jockeyAvatar = jockeyAsset != null ? jockeyAsset.GetComponent<Animator>()?.avatar : null;
            var riderAnimator = rider.GetComponent<Animator>();
            var jockeyVisual = rider.Find("JockeyVisual");
            var jockeyAnimator = jockeyVisual != null ? jockeyVisual.GetComponent<Animator>() : null;

            Assert.That(jockeyAvatar, Is.Not.Null);
            Assert.That(jockeyAvatar.isValid, Is.True);
            Assert.That(jockeyAvatar.isHuman, Is.True);
            Assert.That(jockeyVisual, Is.Not.Null, "The Tripo model must be installed as Rider/JockeyVisual.");
            Assert.That(jockeyAnimator, Is.Not.Null, "Tripo Animator must live on JockeyVisual.");
            Assert.That(jockeyAnimator.avatar, Is.SameAs(jockeyAvatar));
            Assert.That(jockeyAnimator.runtimeAnimatorController, Is.Not.Null);
            Assert.That(jockeyAnimator.applyRootMotion, Is.False);
            Assert.That(riderAnimator == null || !riderAnimator.enabled,
                "Rider root Animator must not compete with the Tripo visual rig.");
            Assert.That(jockeyVisual.Find("tripo_mesh_mirror"), Is.Null);
            Assert.That(jockeyVisual.GetComponent<RiderAnimatorSynchronizer>(), Is.Null);
            Assert.That(jockeyVisual.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Any(renderer => renderer.enabled && renderer.gameObject.activeInHierarchy), Is.True);

            var visibleCowboy = rider.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Any(renderer => renderer.gameObject.name == "CowBoy" && renderer.enabled && renderer.gameObject.activeInHierarchy);
            Assert.That(visibleCowboy, Is.False, "The replaced cowboy must not remain visible.");

            var malbersRider = rider.GetComponent<MRider>();
            Assert.That(malbersRider, Is.Not.Null);

            var riderSo = new SerializedObject(malbersRider);
            var leftProp = riderSo.FindProperty("LeftHand");
            var rightProp = riderSo.FindProperty("RightHand");
            Assert.That(leftProp.objectReferenceValue, Is.Not.Null, "MRider.LeftHand must be wired to Tripo L_Hand.");
            Assert.That(rightProp.objectReferenceValue, Is.Not.Null, "MRider.RightHand must be wired to Tripo R_Hand.");
            Assert.That(leftProp.objectReferenceValue, Is.SameAs(jockeyAnimator.GetBoneTransform(HumanBodyBones.LeftHand)));
            Assert.That(rightProp.objectReferenceValue, Is.SameAs(jockeyAnimator.GetBoneTransform(HumanBodyBones.RightHand)));
        }
    }
}
