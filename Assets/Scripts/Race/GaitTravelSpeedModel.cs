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
            return Step(gait, deltaTime, walkSpeed, trotSpeed, canterSpeed,
                gallopSpeed, gallopSpeed, acceleration, acceleration);
        }

        public float Step(int gait, float deltaTime, float walkSpeed, float trotSpeed,
            float canterSpeed, float gallopSpeed, float sprintSpeed,
            float acceleration, float deceleration)
        {
            if (deltaTime <= 0f) return 0f;

            var target = gait switch
            {
                <= 0 => 0f,
                1 => walkSpeed,
                2 => trotSpeed,
                3 => canterSpeed,
                4 => gallopSpeed,
                _ => sprintSpeed
            };

            target = Mathf.Max(0f, target);
            var previous = Speed;
            var rate = target > previous ? acceleration : deceleration;
            Speed = Mathf.MoveTowards(previous, target,
                Mathf.Max(0.01f, rate) * deltaTime);
            if (Speed < 0.001f) Speed = 0f;
            return (previous + Speed) * 0.5f * deltaTime;
        }

        public static int SelectAnimationGait(int requestedGait, float speed,
            float walkSpeed, float trotSpeed, float canterSpeed,
            float gallopSpeed, float sprintSpeed)
        {
            requestedGait = Mathf.Clamp(requestedGait, 0, 5);
            if (speed <= 0.001f) return requestedGait;

            var speedGait = speed < (walkSpeed + trotSpeed) * 0.5f ? 1
                : speed < (trotSpeed + canterSpeed) * 0.5f ? 2
                : speed < (canterSpeed + gallopSpeed) * 0.5f ? 3
                : speed < (gallopSpeed + sprintSpeed) * 0.5f ? 4
                : 5;
            return Mathf.Max(requestedGait, speedGait);
        }

        public void FollowNative(float distance, float deltaTime)
        {
            if (deltaTime > 0f)
                Speed = Mathf.Max(0f, distance / deltaTime);
        }

        public void Reset() => Speed = 0f;
    }
}
