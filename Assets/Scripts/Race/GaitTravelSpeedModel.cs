using UnityEngine;

namespace HorseRacing.Race
{
    /// <summary>
    /// Supplies conservative, frame-rate-independent travel for in-place gait clips.
    /// </summary>
    public sealed class GaitTravelSpeedModel
    {
        public float Speed { get; private set; }

        public float Step(int gait, float deltaTime, float walkSpeed, float trotSpeed,
            float canterSpeed, float gallopSpeed, float acceleration)
        {
            if (gait <= 0 || deltaTime <= 0f)
            {
                Speed = 0f;
                return 0f;
            }

            var target = gait switch
            {
                1 => walkSpeed,
                2 => trotSpeed,
                3 => canterSpeed,
                _ => gallopSpeed
            };

            var previous = Speed;
            Speed = Mathf.MoveTowards(previous, Mathf.Max(0f, target),
                Mathf.Max(0.01f, acceleration) * deltaTime);
            return (previous + Speed) * 0.5f * deltaTime;
        }

        public void FollowNative(float distance, float deltaTime)
        {
            if (deltaTime > 0f)
                Speed = Mathf.Max(0f, distance / deltaTime);
        }

        public void Reset() => Speed = 0f;
    }
}
