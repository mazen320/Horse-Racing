using MalbersAnimations.HAP;
using UnityEngine;

namespace HorseRacing.Race
{
    /// <summary>
    /// Single-rig Tripo jockey visual: bones animate once via the Rider Animator.
    /// The hidden source mesh keeps the working bind; the mirrored mesh is a visual-only flip.
    /// Tune mount + mirror offsets in the Inspector — never sync a second humanoid avatar.
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class TripoJockeyVisualMirror : MonoBehaviour
    {
        [Header("Seat (whole jockey visual)")]
        [SerializeField] Vector3 mountLocalPosition = new(0f, 0.77f, 0.05f);
        [SerializeField] Vector3 mountLocalEuler = Vector3.zero;
        [SerializeField] Vector3 mountLocalScale = new(1.89f, 1.89f, 1.89f);

        [Header("Meshes (visual only — bones unchanged)")]
        [SerializeField] Transform meshOriginal;
        [SerializeField] Transform meshMirror;
        [SerializeField] Vector3 hiddenMeshLocalEuler = new(270f, 0f, 0f);
        [SerializeField] Vector3 mirrorMeshLocalEuler = new(270f, 0f, 0f);
        [SerializeField] Vector3 mirrorMeshLocalScale = new(1f, -1f, 1f);

        [Tooltip("When off, use the Scene rotation gizmo on JockeyVisual / mesh children — script won't overwrite.")]
        [SerializeField] bool driveTransformsFromInspector = true;

        [Tooltip("Show the mirrored mesh copy (tripo_mesh_mirror).")]
        [SerializeField] bool showMirrorMesh = true;

        [Tooltip("Show the original FBX mesh (tripo_node) for A/B comparison.")]
        [SerializeField] bool showOriginalMesh;

        Transform armature;

        public Transform MeshOriginal => meshOriginal;
        public Transform MeshMirror => meshMirror;
        public bool DriveTransformsFromInspector => driveTransformsFromInspector;

        public void Configure(Transform original, Transform mirror, Transform bonesRoot)
        {
            meshOriginal = original;
            meshMirror = mirror;
            armature = bonesRoot;
            ApplyVisualOffsets();
        }

        void OnEnable() => ApplyVisualOffsets();

#if UNITY_EDITOR
        void OnValidate() => ApplyVisualOffsets();
#endif

        [ContextMenu("Apply Offsets Now")]
        public void ApplyVisualOffsets()
        {
            if (driveTransformsFromInspector)
            {
                transform.localPosition = mountLocalPosition;
                transform.localRotation = Quaternion.Euler(mountLocalEuler);
                transform.localScale = mountLocalScale;

                if (meshOriginal)
                {
                    meshOriginal.localRotation = Quaternion.Euler(hiddenMeshLocalEuler);
                    meshOriginal.localScale = Vector3.one;
                }

                if (meshMirror)
                {
                    if (meshOriginal)
                        meshMirror.localPosition = meshOriginal.localPosition;

                    meshMirror.localRotation = Quaternion.Euler(mirrorMeshLocalEuler);
                    meshMirror.localScale = mirrorMeshLocalScale;
                }
            }

            if (meshOriginal)
            {
                var smr = meshOriginal.GetComponent<SkinnedMeshRenderer>();
                if (smr) smr.enabled = showOriginalMesh;
            }

            if (meshMirror)
            {
                var smr = meshMirror.GetComponent<SkinnedMeshRenderer>();
                if (smr) smr.enabled = showMirrorMesh;
            }
        }

        [ContextMenu("Capture Scene Transforms Into Inspector")]
        public void CaptureSceneTransforms()
        {
            mountLocalPosition = transform.localPosition;
            mountLocalEuler = transform.localEulerAngles;

            var s = transform.localScale;
            mountLocalScale = new Vector3(
                Mathf.Approximately(s.x, 0f) ? 1f : s.x,
                Mathf.Approximately(s.y, 0f) ? 1f : s.y,
                Mathf.Approximately(s.z, 0f) ? 1f : s.z);

            if (meshOriginal)
                hiddenMeshLocalEuler = meshOriginal.localEulerAngles;

            if (meshMirror)
            {
                mirrorMeshLocalEuler = meshMirror.localEulerAngles;
                mirrorMeshLocalScale = meshMirror.localScale;
            }

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        public static TripoJockeyVisualMirror EnsureSetup(Transform jockeyVisualRoot, MRider rider)
        {
            if (!jockeyVisualRoot) return null;

            var mirror = jockeyVisualRoot.GetComponent<TripoJockeyVisualMirror>();
            if (!mirror) mirror = jockeyVisualRoot.gameObject.AddComponent<TripoJockeyVisualMirror>();

            Transform original = null;
            Transform bones = null;
            foreach (var t in jockeyVisualRoot.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.Contains("tripo_node") && !t.name.Contains("mirror"))
                    original = t;
                if (t.name == "Armature")
                    bones = t;
            }

            if (!original) return mirror;

            var mirrorTransform = jockeyVisualRoot.Find("tripo_mesh_mirror");
            if (!mirrorTransform)
            {
                mirrorTransform = Instantiate(original.gameObject, jockeyVisualRoot).transform;
                mirrorTransform.name = "tripo_mesh_mirror";
            }

            mirror.Configure(original, mirrorTransform, bones);

            var animator = rider ? rider.GetComponent<Animator>() : jockeyVisualRoot.root.GetComponent<Animator>();
            if (rider && animator && animator.isHuman)
            {
                rider.LeftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                rider.RightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            }

            return mirror;
        }
    }
}
