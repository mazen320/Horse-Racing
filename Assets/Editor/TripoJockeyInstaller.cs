using System.Collections.Generic;
using System.Linq;
using MalbersAnimations.HAP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HorseRacing.Race.Editor
{
    /// <summary>
    /// Swaps the Malbers CowBoy SMR mesh for a Tripo mesh skinned to R_* bones.
    /// Run after Tools/blender_skin_tripo_malbers.py exports jockey_malbers_rigged.fbx.
    /// </summary>
    public static class TripoJockeyInstaller
    {
        const string RiggedFbxPath = "Assets/TripoModels/jockey_3d_model/jockey_malbers_rigged.fbx";
        const string JockeyMaterialPath = "Assets/TripoModels/jockey_3d_model/Materials/jockey_3d_model.mat";
        const string CowboyFbxPath = "Assets/Malbers Animations/Horse AnimSet Pro/7 - Models/CowBoy/Rider.FBX";
        const string BakedMeshPath = "Assets/TripoModels/jockey_3d_model/jockey_malbers_unity.mesh";

        [MenuItem("Horse Racing/Install Tripo Jockey On Malbers Rider")]
        public static void InstallFromMenu() => Install(FindSceneRider());

        public static bool Install(GameObject rider)
        {
            if (!rider)
            {
                Debug.LogError("[TripoJockeyInstaller] Rider not found in open scene.");
                return false;
            }

            var meshParent = rider.transform.Find("Mesh/Mesh");
            var cowboy = meshParent != null ? meshParent.Find("CowBoy") : null;
            if (!cowboy)
            {
                Debug.LogError("[TripoJockeyInstaller] Mesh/Mesh/CowBoy not found under Rider.");
                return false;
            }

            var smr = cowboy.GetComponent<SkinnedMeshRenderer>();
            if (!smr)
            {
                Debug.LogError("[TripoJockeyInstaller] CowBoy SkinnedMeshRenderer missing.");
                return false;
            }

            var sourceSmr = LoadSourceSkinnedMeshRenderer();
            if (!sourceSmr || !sourceSmr.sharedMesh)
            {
                Debug.LogError($"[TripoJockeyInstaller] No skinned mesh in {RiggedFbxPath}. Run Blender script first.");
                return false;
            }

            RemoveJockeyVisual(rider);

            var bakedMesh = RemapAndBakeMesh(smr, cowboy.transform, sourceSmr);
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(BakedMeshPath);
            if (existing)
                EditorUtility.CopySerialized(bakedMesh, existing);
            else
                AssetDatabase.CreateAsset(bakedMesh, BakedMeshPath);
            AssetDatabase.SaveAssets();

            smr.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(BakedMeshPath);
            var jockeyMat = AssetDatabase.LoadAssetAtPath<Material>(JockeyMaterialPath);
            if (jockeyMat)
                smr.sharedMaterial = jockeyMat;

            smr.enabled = true;
            cowboy.gameObject.SetActive(true);

            var parentSmr = meshParent.GetComponent<SkinnedMeshRenderer>();
            if (parentSmr)
                parentSmr.enabled = false;

            foreach (Transform child in meshParent)
            {
                if (child.name == "Bandana")
                    child.gameObject.SetActive(false);
            }

            var rcg = rider.transform.Find("R_CG");
            if (rcg)
                rcg.gameObject.SetActive(true);

            var meshRoot = rider.transform.Find("Mesh");
            if (meshRoot)
                meshRoot.gameObject.SetActive(true);

            RestoreRiderAvatar(rider);

            WireRiderHands(rider);

            EditorUtility.SetDirty(rider);
            EditorSceneManager.MarkSceneDirty(rider.scene);
            Debug.Log("[TripoJockeyInstaller] Tripo jockey installed on CowBoy SMR with remapped weights and bindposes.");
            return true;
        }

        static GameObject FindSceneRider()
        {
            var raceSetup = GameObject.Find("RaceSetup");
            if (raceSetup)
            {
                var rider = raceSetup.transform.Find("Rider");
                if (rider)
                    return rider.gameObject;
            }

            return GameObject.Find("Rider");
        }

        static SkinnedMeshRenderer LoadSourceSkinnedMeshRenderer()
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(RiggedFbxPath);
            if (!root)
                return null;

            return root.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .FirstOrDefault(renderer => renderer.sharedMesh && renderer.sharedMesh.name.Contains("tripo"));
        }

        static Mesh RemapAndBakeMesh(SkinnedMeshRenderer targetSmr, Transform meshTransform, SkinnedMeshRenderer sourceSmr)
        {
            var mesh = Object.Instantiate(sourceSmr.sharedMesh);
            mesh.name = "jockey_malbers_unity";

            var sourceBones = sourceSmr.bones;
            var targetBones = targetSmr.bones;
            var indexMap = BuildBoneIndexMap(sourceBones, targetBones);

            RemapBoneWeights(mesh, indexMap);
            mesh.bindposes = BakeBindposes(targetBones, meshTransform);
            return mesh;
        }

        static int[] BuildBoneIndexMap(Transform[] sourceBones, Transform[] targetBones)
        {
            var targetByName = new Dictionary<string, int>();
            for (var i = 0; i < targetBones.Length; i++)
            {
                if (targetBones[i])
                    targetByName[targetBones[i].name] = i;
            }

            var indexMap = new int[sourceBones.Length];
            for (var i = 0; i < sourceBones.Length; i++)
            {
                indexMap[i] = -1;
                if (!sourceBones[i])
                    continue;

                if (targetByName.TryGetValue(sourceBones[i].name, out var targetIndex))
                    indexMap[i] = targetIndex;
            }

            return indexMap;
        }

        static void RemapBoneWeights(Mesh mesh, int[] indexMap)
        {
            var weights = mesh.boneWeights;
            for (var i = 0; i < weights.Length; i++)
            {
                var weight = weights[i];
                weight.boneIndex0 = RemapBoneIndex(weight.boneIndex0, indexMap);
                weight.boneIndex1 = RemapBoneIndex(weight.boneIndex1, indexMap);
                weight.boneIndex2 = RemapBoneIndex(weight.boneIndex2, indexMap);
                weight.boneIndex3 = RemapBoneIndex(weight.boneIndex3, indexMap);
                weights[i] = weight;
            }

            mesh.boneWeights = weights;
        }

        static int RemapBoneIndex(int sourceIndex, int[] indexMap)
        {
            if (sourceIndex < 0 || sourceIndex >= indexMap.Length)
                return 0;

            return indexMap[sourceIndex] >= 0 ? indexMap[sourceIndex] : 0;
        }

        static Matrix4x4[] BakeBindposes(Transform[] bones, Transform meshTransform)
        {
            var bindposes = new Matrix4x4[bones.Length];
            for (var i = 0; i < bones.Length; i++)
            {
                bindposes[i] = bones[i]
                    ? bones[i].worldToLocalMatrix * meshTransform.localToWorldMatrix
                    : Matrix4x4.identity;
            }

            return bindposes;
        }

        static void RemoveJockeyVisual(GameObject rider)
        {
            var jockeyVisual = rider.transform.Find("JockeyVisual");
            if (jockeyVisual)
                Object.DestroyImmediate(jockeyVisual.gameObject);

            var legacy = rider.transform.Find("TripoJockeyVisual");
            if (legacy)
                Object.DestroyImmediate(legacy.gameObject);

            var synchronizer = rider.GetComponentInChildren<RiderAnimatorSynchronizer>(true);
            if (synchronizer)
                Object.DestroyImmediate(synchronizer);
        }

        static void RestoreRiderAvatar(GameObject rider)
        {
            var animator = rider.GetComponent<Animator>();
            if (!animator)
                return;

            var riderAvatar = AssetDatabase.LoadAllAssetsAtPath(CowboyFbxPath)
                .OfType<Avatar>()
                .FirstOrDefault(avatar => avatar.name == "RiderAvatar");

            if (riderAvatar)
                animator.avatar = riderAvatar;

            animator.applyRootMotion = false;
        }

        static void WireRiderHands(GameObject rider)
        {
            var malbersRider = rider.GetComponent<MRider>();
            var animator = rider.GetComponent<Animator>();
            if (!malbersRider || !animator)
                return;

            animator.Rebind();
            animator.Update(0f);

            var left = animator.isHuman
                ? animator.GetBoneTransform(HumanBodyBones.LeftHand)
                : null;
            var right = animator.isHuman
                ? animator.GetBoneTransform(HumanBodyBones.RightHand)
                : null;

            if (!left || !right)
            {
                foreach (var bone in rider.GetComponentsInChildren<Transform>(true))
                {
                    if (!left && bone.name == "R_L Hand") left = bone;
                    if (!right && bone.name == "R_R Hand") right = bone;
                }
            }

            if (!left || !right)
            {
                Debug.LogWarning("[TripoJockeyInstaller] Could not locate the original Malbers hand bones.");
                return;
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
