using System.Collections.Generic;
using System.Linq;
using MalbersAnimations.HAP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HorseRacing.Race.Editor
{
    /// <summary>
    /// Swaps the scene CowBoy SMR mesh with cowboy_no_hat.fbx exported from Blender.
    /// Keeps Malbers bindposes and remaps bone weights by bone name.
    /// </summary>
    public static class CowboyNoHatInstaller
    {
        const string EditedMeshFbxPath = "Assets/TripoModels/jockey_3d_model/cowboy_no_hat.fbx";
        const string BakedMeshPath = "Assets/TripoModels/jockey_3d_model/cowboy_no_hat_unity.mesh";
        const string MalbersRiderFbxPath =
            "Assets/Malbers Animations/Horse AnimSet Pro/7 - Models/CowBoy/Rider.FBX";
        const string CowboyMaterialPath =
            "Assets/Malbers Animations/Horse AnimSet Pro/5 - Materials & Textures/Cowboy/Cowboy.mat";

        [MenuItem("Horse Racing/Install Edited Cowboy (No Hat, from Blender)")]
        public static void InstallFromMenu()
        {
            var rider = FindRider();
            if (!rider)
            {
                Debug.LogError("[CowboyNoHatInstaller] Rider not found in open scene.");
                return;
            }

            if (Apply(rider))
                Debug.Log("[CowboyNoHatInstaller] No-hat cowboy installed. Riding animations unchanged.");
        }

        public static bool Apply(GameObject rider)
        {
            var meshParent = rider.transform.Find("Mesh/Mesh");
            var cowboy = meshParent != null ? meshParent.Find("CowBoy") : null;
            if (!cowboy)
            {
                Debug.LogError("[CowboyNoHatInstaller] Mesh/Mesh/CowBoy not found under Rider.");
                return false;
            }

            var smr = cowboy.GetComponent<SkinnedMeshRenderer>();
            if (!smr)
            {
                Debug.LogError("[CowboyNoHatInstaller] CowBoy SkinnedMeshRenderer missing.");
                return false;
            }

            var sourceSmr = LoadEditedSkinnedMeshRenderer();
            if (!sourceSmr || !sourceSmr.sharedMesh)
            {
                Debug.LogError($"[CowboyNoHatInstaller] No mesh in {EditedMeshFbxPath}. Export from Blender first.");
                return false;
            }

            var bakedMesh = BakeMesh(smr, sourceSmr);
            if (!bakedMesh)
                return false;

            if (AssetDatabase.LoadAssetAtPath<Mesh>(BakedMeshPath))
                AssetDatabase.DeleteAsset(BakedMeshPath);

            AssetDatabase.CreateAsset(bakedMesh, BakedMeshPath);
            AssetDatabase.SaveAssets();

            smr.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(BakedMeshPath);
            var cowboyMat = AssetDatabase.LoadAssetAtPath<Material>(CowboyMaterialPath);
            if (cowboyMat)
                smr.sharedMaterial = cowboyMat;

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

        static SkinnedMeshRenderer LoadEditedSkinnedMeshRenderer()
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(EditedMeshFbxPath);
            if (!root) return null;

            return root.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .FirstOrDefault(renderer => renderer.sharedMesh && renderer.name.Contains("CowBoy"));
        }

        static Mesh BakeMesh(SkinnedMeshRenderer targetSmr, SkinnedMeshRenderer sourceSmr)
        {
            var originalMesh = LoadOriginalCowboyMesh();
            if (!originalMesh)
            {
                Debug.LogError("[CowboyNoHatInstaller] Could not load original Malbers CowBoy mesh.");
                return null;
            }

            var sourceMesh = sourceSmr.sharedMesh;
            if (sourceMesh.vertexCount < 100)
            {
                Debug.LogError($"[CowboyNoHatInstaller] Source mesh looks empty ({sourceMesh.vertexCount} verts). Re-export from Blender.");
                return null;
            }

            var sourceBoneNames = GetBoneNames(sourceSmr);
            if (sourceBoneNames.Count == 0)
            {
                Debug.LogError("[CowboyNoHatInstaller] Could not read bone names from imported FBX.");
                return null;
            }

            var targetBoneIndex = BuildTargetBoneIndex(targetSmr.bones);
            var indexMap = BuildIndexMap(sourceBoneNames, targetBoneIndex);
            if (indexMap.Any(i => i < 0))
                Debug.LogWarning("[CowboyNoHatInstaller] Some source bones have no scene match; those weights go to root.");

            var mesh = Object.Instantiate(sourceMesh);
            mesh.name = "cowboy_no_hat_unity";
            RemapBoneWeights(mesh, indexMap);
            FitMeshToReference(mesh, originalMesh);
            mesh.bindposes = originalMesh.bindposes;
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            mesh.UploadMeshData(false);

            Debug.Log(
                $"[CowboyNoHatInstaller] Baked {mesh.vertexCount} verts, bounds {mesh.bounds.size}, " +
                $"ref {originalMesh.bounds.size}");

            return mesh;
        }

        /// <summary>
        /// Blender re-exports can arrive with wrong scale/orientation. Match Malbers CowBoy bounds per axis.
        /// </summary>
        static void FitMeshToReference(Mesh mesh, Mesh reference)
        {
            var src = mesh.bounds;
            var dst = reference.bounds;

            var srcSize = src.size;
            var dstSize = dst.size;
            if (srcSize.sqrMagnitude <= 1e-8f || dstSize.sqrMagnitude <= 1e-8f)
                return;

            var scale = new Vector3(
                dstSize.x / Mathf.Max(srcSize.x, 1e-5f),
                dstSize.y / Mathf.Max(srcSize.y, 1e-5f),
                dstSize.z / Mathf.Max(srcSize.z, 1e-5f));

            // Only correct when clearly wrong (Blender cm export, etc.).
            var maxRatio = Mathf.Max(scale.x, scale.y, scale.z);
            var minRatio = Mathf.Min(scale.x, scale.y, scale.z);
            if (maxRatio > 0.5f && minRatio < 2f)
                return;

            var verts = mesh.vertices;
            var srcCenter = src.center;
            var dstCenter = dst.center;
            for (var i = 0; i < verts.Length; i++)
            {
                var offset = verts[i] - srcCenter;
                verts[i] = new Vector3(
                    offset.x * scale.x,
                    offset.y * scale.y,
                    offset.z * scale.z) + dstCenter;
            }

            mesh.vertices = verts;
            mesh.RecalculateBounds();

            Debug.Log(
                $"[CowboyNoHatInstaller] Fitted mesh scale {scale} " +
                $"(src {src.size} -> {mesh.bounds.size}, ref {dst.size})");
        }

        static List<string> GetBoneNames(SkinnedMeshRenderer smr)
        {
            var names = new List<string>();
            if (smr.bones != null && smr.bones.Length > 0)
            {
                foreach (var bone in smr.bones)
                    names.Add(bone ? bone.name : string.Empty);
                return names;
            }

            var root = AssetDatabase.LoadAssetAtPath<GameObject>(EditedMeshFbxPath);
            if (!root) return names;

            var armature = root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => t.name == "Armature" || t.name == "R_CG");
            if (!armature) return names;

            foreach (var bone in armature.GetComponentsInChildren<Transform>(true))
                names.Add(bone.name);

            return names;
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

        static Dictionary<string, int> BuildTargetBoneIndex(Transform[] targetBones)
        {
            var map = new Dictionary<string, int>();
            for (var i = 0; i < targetBones.Length; i++)
            {
                if (targetBones[i])
                    map[targetBones[i].name] = i;
            }

            return map;
        }

        static int[] BuildIndexMap(List<string> sourceBoneNames, Dictionary<string, int> targetBoneIndex)
        {
            var indexMap = new int[sourceBoneNames.Count];
            for (var i = 0; i < sourceBoneNames.Count; i++)
            {
                indexMap[i] = -1;
                if (!string.IsNullOrEmpty(sourceBoneNames[i]) &&
                    targetBoneIndex.TryGetValue(sourceBoneNames[i], out var targetIndex))
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
