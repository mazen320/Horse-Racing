using MalbersAnimations.Conditions;
using UnityEngine;


namespace MalbersAnimations.HAP
{
    [System.Serializable, AddTypeMenu("Riding/Rider is Riding")]

    public class C2_Rider : ConditionCore
    {
        public MRider Target;

        protected override bool _Evaluate()
        {
            if (Target)
            {
                Debugging($"Is Riding? {Target.IsRiding}", Target.IsRiding, Target);
                return Target.IsRiding;
            }
            return false;
        }

        protected override void _SetTarget(Object target) => VerifyComponent(target, ref Target);
    }
}
