using MalbersAnimations.HAP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HorseRacing.Race.Editor
{
    /// <summary>
    /// One skeleton, one Animator on JockeyVisual. Malbers riding clips retarget to Tripo humanoid.
    /// </summary>
    public static class TripoJockeyMirrorSetup
    {
        const string FbxPath = "Assets/TripoModels/jockey_3d_model/jockey_3d_model.fbx";
        const string ControllerPath = "Assets/Malbers Animations/Horse AnimSet Pro/2 - Animations/AC Human v5 Rider.controller";

        static readonly Vector3 SeatPosition = new(0.00449071f, -0.1047014f, -0.03018377f);
        static readonly Vector3 SeatEuler = new(0f, 180f, 0f);
        static readonly Vector3 SeatScale = new(2.080597f, 2.080597f, 2.080597f);

        [MenuItem("Horse Racing/Setup Tripo Jockey (Simple — One Rig)")]
        public static void SetupFromMenu()
        {
            var rider = FindRider();
            if (!rider)
            {
                Debug.LogError("[TripoJockey] Rider not found in open scene.");
                return;
            }

            if (Apply(rider))
                Debug.Log("[TripoJockey] Done. Tripo Animator on JockeyVisual drives riding clips. Tune JockeyVisual rotation in Scene if facing is off.");
        }

        [MenuItem("Horse Racing/Setup Tripo Mirror Jockey (Single Rig)")]
        public static void SetupLegacyMenu() => SetupFromMenu();

        public static bool Apply(GameObject rider)
        {
            RemoveHacks(rider);
            HideCowboyVisuals(rider);

            var jockeyVisual = EnsureJockeyVisual(rider);
            jockeyVisual.localPosition = SeatPosition;
            jockeyVisual.localRotation = Quaternion.Euler(SeatEuler);
            jockeyVisual.localScale = SeatScale;

            EnsureTripoAnimatorOnVisual(rider, jockeyVisual.gameObject);
            WireRiderHands(rider);
            RestoreImportedChildTransforms(jockeyVisual);

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

        static void RemoveHacks(GameObject rider)
        {
            var jv = rider.transform.Find("JockeyVisual") ?? rider.transform.Find("TripoJockeyVisual");
            if (jv)
            {
                var mirror = jv.GetComponent<TripoJockeyVisualMirror>();
                if (mirror) Object.DestroyImmediate(mirror);

                var mirrorMesh = jv.Find("tripo_mesh_mirror");
                if (mirrorMesh) Object.DestroyImmediate(mirrorMesh.gameObject);

                foreach (Transform child in jv)
                {
                    if (child.name.Contains("tripo_node"))
                    {
                        child.gameObject.SetActive(true);
                        var smr = child.GetComponent<SkinnedMeshRenderer>();
                        if (smr) smr.enabled = true;
                    }
                }
            }

            var riderSync = rider.GetComponent<RiderAnimatorSynchronizer>();
            if (riderSync) Object.DestroyImmediate(riderSync);
        }

        static void HideCowboyVisuals(GameObject rider)
        {
            var rcg = rider.transform.Find("R_CG");
            if (rcg) rcg.gameObject.SetActive(false);

            var meshGo = rider.transform.Find("Mesh/Mesh");
            if (!meshGo) return;

            meshGo.gameObject.SetActive(true);
            var parentSmr = meshGo.GetComponent<SkinnedMeshRenderer>();
            if (parentSmr) parentSmr.enabled = false;

            var cowboy = meshGo.Find("CowBoy");
            if (cowboy)
            {
                var cowboySmr = cowboy.GetComponent<SkinnedMeshRenderer>();
                if (cowboySmr) cowboySmr.enabled = false;
            }

            foreach (Transform child in meshGo)
            {
                if (child.name == "Bandana")
                    child.gameObject.SetActive(false);
            }
        }

        static void EnsureTripoAnimatorOnVisual(GameObject rider, GameObject jockeyVisual)
        {
            var riderAnimator = rider.GetComponent<Animator>();
            if (riderAnimator)
                riderAnimator.enabled = true;

            var animator = jockeyVisual.GetComponent<Animator>();
            if (!animator)
                animator = jockeyVisual.AddComponent<Animator>();

            Avatar tripAvatar = null;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(FbxPath))
            {
                if (asset is Avatar avatar)
                    tripAvatar = avatar;
            }

            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);

            if (tripAvatar)
                animator.avatar = tripAvatar;
            if (controller)
                animator.runtimeAnimatorController = controller;

            animator.applyRootMotion = false;
            animator.enabled = true;

            var synchronizer = jockeyVisual.GetComponent<RiderAnimatorSynchronizer>();
            if (!synchronizer)
                synchronizer = jockeyVisual.AddComponent<RiderAnimatorSynchronizer>();
            synchronizer.Configure(riderAnimator, animator, rider.GetComponent<MRider>());
            EditorUtility.SetDirty(synchronizer);

            var serializedAnimator = new SerializedObject(animator);
            if (tripAvatar)
                serializedAnimator.FindProperty("m_Avatar").objectReferenceValue = tripAvatar;
            if (controller)
                serializedAnimator.FindProperty("m_Controller").objectReferenceValue = controller;
            serializedAnimator.FindProperty("m_ApplyRootMotion").boolValue = false;
            serializedAnimator.ApplyModifiedPropertiesWithoutUndo();

            if (PrefabUtility.IsPartOfPrefabInstance(jockeyVisual))
                PrefabUtility.RecordPrefabInstancePropertyModifications(animator);

            if (riderAnimator)
            {
                var serializedRiderAnimator = new SerializedObject(riderAnimator);
                serializedRiderAnimator.FindProperty("m_Enabled").boolValue = true;
                serializedRiderAnimator.ApplyModifiedPropertiesWithoutUndo();
                if (PrefabUtility.IsPartOfPrefabInstance(rider))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(riderAnimator);
            }
        }

        static Transform EnsureJockeyVisual(GameObject rider)
        {
            var existing = rider.transform.Find("JockeyVisual") ?? rider.transform.Find("TripoJockeyVisual");
            if (existing)
            {
                existing.name = "JockeyVisual";
                return existing;
            }

            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(fbx, rider.transform);
            instance.name = "JockeyVisual";
            return instance.transform;
        }

        static void RestoreImportedChildTransforms(Transform jockeyVisual)
        {
            foreach (Transform child in jockeyVisual)
            {
                if (PrefabUtility.IsPartOfPrefabInstance(child.gameObject))
                    PrefabUtility.RevertObjectOverride(child, InteractionMode.AutomatedAction);
            }

            if (PrefabUtility.IsPartOfPrefabInstance(jockeyVisual.gameObject))
                PrefabUtility.RecordPrefabInstancePropertyModifications(jockeyVisual);
        }

        static void WireRiderHands(GameObject rider)
        {
            var malbersRider = rider.GetComponent<MRider>();
            if (!malbersRider) return;

            Transform left = null;
            Transform right = null;

            var jockeyVisual = rider.transform.Find("JockeyVisual");
            var animator = jockeyVisual ? jockeyVisual.GetComponent<Animator>() : null;

            if (animator && animator.avatar != null && animator.avatar.isValid && animator.avatar.isHuman)
            {
                left = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                right = animator.GetBoneTransform(HumanBodyBones.RightHand);
            }

            if ((!left || !right) && jockeyVisual)
            {
                foreach (var bone in jockeyVisual.GetComponentsInChildren<Transform>(true))
                {
                    if (bone.name == "L_Hand") left = bone;
                    if (bone.name == "R_Hand") right = bone;
                }
            }

            if (!left || !right)
            {
                Debug.LogWarning("[TripoJockey] Could not locate Tripo hand bones for MRider wiring.");
                return;
            }

            var serializedRider = new SerializedObject(malbersRider);
            serializedRider.FindProperty("LeftHand").objectReferenceValue = left;
            serializedRider.FindProperty("RightHand").objectReferenceValue = right;
            serializedRider.ApplyModifiedPropertiesWithoutUndo();

            if (PrefabUtility.IsPartOfPrefabInstance(rider))
                PrefabUtility.RecordPrefabInstancePropertyModifications(malbersRider);

            EditorUtility.SetDirty(malbersRider);
        }
    }
}
