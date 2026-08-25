using MalbersAnimations.HAP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HorseRacing.Race.Editor
{
    public static class TripoJockeyReset
    {
        const string MalbersRiderFbxPath =
            "Assets/Malbers Animations/Horse AnimSet Pro/7 - Models/CowBoy/Rider.FBX";
        const string ControllerPath =
            "Assets/Malbers Animations/Horse AnimSet Pro/2 - Animations/AC Human v5 Rider.controller";

        [MenuItem("Horse Racing/Reset Rider to Malbers Cowboy (Original)")]
        public static void ResetFromMenu()
        {
            var rider = FindRider();
            if (!rider)
            {
                Debug.LogError("[TripoJockeyReset] Rider not found in open scene.");
                return;
            }

            if (Reset(rider))
                Debug.Log("[TripoJockeyReset] Rider restored to original Malbers cowboy. Tripo JockeyVisual removed.");
        }

        public static bool Reset(GameObject rider)
        {
            RemoveTripoVisual(rider);
            RestoreCowboyVisuals(rider);
            RestoreMalbersAnimator(rider);
            WireMalbersHands(rider);

            EditorUtility.SetDirty(rider);
            EditorSceneManager.MarkSceneDirty(rider.scene);
            EditorSceneManager.SaveOpenScenes();
            return true;
        }

        static GameObject FindRider()
        {
            var raceSetup = GameObject.Find("RaceSetup");
            if (raceSetup)
            {
                var child = raceSetup.transform.Find("Rider");
                if (child) return child.gameObject;
            }

            return GameObject.Find("Rider");
        }

        static void RemoveTripoVisual(GameObject rider)
        {
            var jv = rider.transform.Find("JockeyVisual") ?? rider.transform.Find("TripoJockeyVisual");
            if (jv)
                Object.DestroyImmediate(jv.gameObject);

            Object.DestroyImmediate(rider.GetComponent<RiderAnimatorSynchronizer>());
        }

        static void RestoreCowboyVisuals(GameObject rider)
        {
            var rcg = rider.transform.Find("R_CG");
            if (rcg) rcg.gameObject.SetActive(true);

            var meshGo = rider.transform.Find("Mesh/Mesh");
            if (!meshGo) return;

            meshGo.gameObject.SetActive(true);

            var parentSmr = meshGo.GetComponent<SkinnedMeshRenderer>();
            if (parentSmr) parentSmr.enabled = true;

            var cowboy = meshGo.Find("CowBoy");
            if (cowboy)
            {
                cowboy.gameObject.SetActive(true);
                var cowboySmr = cowboy.GetComponent<SkinnedMeshRenderer>();
                if (cowboySmr)
                {
                    cowboySmr.enabled = true;
                    var originalMesh = LoadOriginalCowboyMesh();
                    if (originalMesh)
                        cowboySmr.sharedMesh = originalMesh;
                }
            }

            foreach (Transform child in meshGo)
            {
                if (child.name == "Bandana")
                    child.gameObject.SetActive(true);
            }
        }

        static Mesh LoadOriginalCowboyMesh()
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(MalbersRiderFbxPath))
            {
                if (asset is Mesh mesh && mesh.name == "CowBoy")
                    return mesh;
            }

            return null;
        }

        static void RestoreMalbersAnimator(GameObject rider)
        {
            var animator = rider.GetComponent<Animator>();
            if (!animator) return;

            Avatar malbersAvatar = null;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(MalbersRiderFbxPath))
            {
                if (asset is Avatar avatar)
                    malbersAvatar = avatar;
            }

            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            if (malbersAvatar)
                animator.avatar = malbersAvatar;
            if (controller)
                animator.runtimeAnimatorController = controller;

            animator.applyRootMotion = false;
            animator.enabled = true;

            var serializedAnimator = new SerializedObject(animator);
            if (malbersAvatar)
                serializedAnimator.FindProperty("m_Avatar").objectReferenceValue = malbersAvatar;
            if (controller)
                serializedAnimator.FindProperty("m_Controller").objectReferenceValue = controller;
            serializedAnimator.FindProperty("m_ApplyRootMotion").boolValue = false;
            serializedAnimator.FindProperty("m_Enabled").boolValue = true;
            serializedAnimator.ApplyModifiedPropertiesWithoutUndo();

            if (PrefabUtility.IsPartOfPrefabInstance(rider))
                PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
        }

        static void WireMalbersHands(GameObject rider)
        {
            var malbersRider = rider.GetComponent<MRider>();
            var animator = rider.GetComponent<Animator>();
            if (!malbersRider || !animator) return;

            animator.Rebind();
            animator.Update(0f);

            Transform left = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            Transform right = animator.GetBoneTransform(HumanBodyBones.RightHand);

            if (!left || !right)
            {
                foreach (var bone in rider.GetComponentsInChildren<Transform>(true))
                {
                    if (bone.name == "R_L Hand") left = bone;
                    if (bone.name == "R_R Hand") right = bone;
                }
            }

            var serializedRider = new SerializedObject(malbersRider);
            serializedRider.FindProperty("LeftHand").objectReferenceValue = left;
            serializedRider.FindProperty("RightHand").objectReferenceValue = right;
            serializedRider.ApplyModifiedPropertiesWithoutUndo();

            if (PrefabUtility.IsPartOfPrefabInstance(rider))
                PrefabUtility.RecordPrefabInstancePropertyModifications(malbersRider);
        }
    }
}
