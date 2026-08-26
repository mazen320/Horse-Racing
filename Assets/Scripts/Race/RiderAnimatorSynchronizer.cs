using MalbersAnimations.HAP;
using UnityEngine;

namespace HorseRacing.Race
{
    /// <summary>
    /// Mirrors the hidden Malbers rider Animator onto a visual rider that must keep
    /// its own native humanoid root (for example, an FBX with a different axis setup).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class RiderAnimatorSynchronizer : MonoBehaviour
    {
        [SerializeField] Animator sourceAnimator;
        [SerializeField] Animator targetAnimator;
        [SerializeField] MRider rider;

        AnimatorControllerParameter[] parameters;
        bool initialized;

        public Animator SourceAnimator => sourceAnimator;
        public Animator TargetAnimator => targetAnimator;
        public MRider Rider => rider;

        public void Configure(Animator source, Animator target, MRider malbersRider)
        {
            sourceAnimator = source;
            targetAnimator = target;
            rider = malbersRider;
            initialized = false;
            Initialize();
        }

        void Awake() => Initialize();

        void OnEnable()
        {
            Initialize();
            Synchronize();
        }

        void Update() => Synchronize();

        void Initialize()
        {
            if (!targetAnimator)
                targetAnimator = GetComponent<Animator>();

            if (!sourceAnimator && transform.parent)
                sourceAnimator = transform.parent.GetComponentInParent<Animator>();

            if (!rider && transform.parent)
                rider = transform.parent.GetComponentInParent<MRider>();

            initialized = sourceAnimator && targetAnimator && sourceAnimator != targetAnimator;
            if (!initialized) return;

            parameters = sourceAnimator.parameters;
            targetAnimator.applyRootMotion = false;
            targetAnimator.updateMode = sourceAnimator.updateMode;
            targetAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            BindHumanoidReferences();
        }

        void Synchronize()
        {
            if (!initialized)
            {
                Initialize();
                if (!initialized) return;
            }

            targetAnimator.speed = sourceAnimator.speed;
            CopyParameters();

            var layerCount = Mathf.Min(sourceAnimator.layerCount, targetAnimator.layerCount);
            for (var layer = 0; layer < layerCount; layer++)
            {
                targetAnimator.SetLayerWeight(layer, sourceAnimator.GetLayerWeight(layer));
                SynchronizeLayerState(layer);
            }
        }

        void CopyParameters()
        {
            for (var i = 0; i < parameters.Length; i++)
            {
                var parameter = parameters[i];
                if (targetAnimator.IsParameterControlledByCurve(parameter.nameHash))
                    continue;

                switch (parameter.type)
                {
                    case AnimatorControllerParameterType.Float:
                        targetAnimator.SetFloat(parameter.nameHash, sourceAnimator.GetFloat(parameter.nameHash));
                        break;
                    case AnimatorControllerParameterType.Int:
                        targetAnimator.SetInteger(parameter.nameHash, sourceAnimator.GetInteger(parameter.nameHash));
                        break;
                    case AnimatorControllerParameterType.Bool:
                        targetAnimator.SetBool(parameter.nameHash, sourceAnimator.GetBool(parameter.nameHash));
                        break;
                }
            }
        }

        void SynchronizeLayerState(int layer)
        {
            if (sourceAnimator.IsInTransition(layer))
            {
                var sourceNext = sourceAnimator.GetNextAnimatorStateInfo(layer);
                if (sourceNext.fullPathHash == 0) return;

                var targetNextHash = targetAnimator.IsInTransition(layer)
                    ? targetAnimator.GetNextAnimatorStateInfo(layer).fullPathHash
                    : 0;
                if (targetNextHash == sourceNext.fullPathHash) return;

                var transition = sourceAnimator.GetAnimatorTransitionInfo(layer);
                targetAnimator.CrossFade(
                    sourceNext.fullPathHash,
                    Mathf.Max(0.02f, transition.duration),
                    layer,
                    Mathf.Repeat(sourceNext.normalizedTime, 1f),
                    transition.normalizedTime);
                return;
            }

            var sourceState = sourceAnimator.GetCurrentAnimatorStateInfo(layer);
            if (sourceState.fullPathHash == 0) return;

            var targetState = targetAnimator.GetCurrentAnimatorStateInfo(layer);
            if (targetState.fullPathHash != sourceState.fullPathHash)
            {
                targetAnimator.Play(
                    sourceState.fullPathHash,
                    layer,
                    Mathf.Repeat(sourceState.normalizedTime, 1f));
            }
        }

        void BindHumanoidReferences()
        {
            if (!rider || !targetAnimator.isHuman) return;

            rider.LeftHand = targetAnimator.GetBoneTransform(HumanBodyBones.LeftHand);
            rider.RightHand = targetAnimator.GetBoneTransform(HumanBodyBones.RightHand);
        }
    }
}
