using UnityEngine;

namespace HorseRacing.Race
{
    /// <summary>
    /// Supplies conservative, frame-rate-independent travel for in-place gait clips.
    /// </summary>
    public sealed class GaitTravelSpeedModel
    {
        public const float MaximumRecommendedSprintSpeed = 10.5f;

        public float Speed { get; private set; }

        public static float ClampSprintSpeed(float gallopSpeed, float sprintSpeed)
        {
            var minimum = Mathf.Clamp(gallopSpeed, 0f, MaximumRecommendedSprintSpeed);
            return Mathf.Clamp(sprintSpeed, minimum, MaximumRecommendedSprintSpeed);
        }

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
            var target = gait switch
            {
                <= 0 => 0f,
                1 => walkSpeed,
                2 => trotSpeed,
                3 => canterSpeed,
                4 => gallopSpeed,
                _ => sprintSpeed
            };

            return StepToTarget(target, deltaTime, acceleration, deceleration);
        }

        /// <summary>
        /// Moves towards any target speed, not just one of the five gait speeds, and
        /// returns the ground covered this frame.
        /// </summary>
        public float StepToTarget(float targetSpeed, float deltaTime,
            float acceleration, float deceleration)
        {
            if (deltaTime <= 0f) return 0f;

            targetSpeed = Mathf.Max(0f, targetSpeed);
            var previous = Speed;
            var rate = targetSpeed > previous ? acceleration : deceleration;
            Speed = Mathf.MoveTowards(previous, targetSpeed,
                Mathf.Max(0.01f, rate) * deltaTime);
            if (Speed < 0.001f) Speed = 0f;
            return (previous + Speed) * 0.5f * deltaTime;
        }

        /// <summary>
        /// Track speed for a continuous effort reading. The gait speeds become corners of
        /// a ramp rather than five fixed values, so pushing harder inside one gait band
        /// still covers more ground. Snapping to the band's own speed is what made two
        /// runners at clearly different paces travel at exactly the same speed.
        /// </summary>
        public static float TargetSpeedForEffort(float effort,
            float walkAt, float trotAt, float canterAt, float gallopAt, float sprintAt,
            float walkSpeed, float trotSpeed, float canterSpeed,
            float gallopSpeed, float sprintSpeed)
        {
            effort = Mathf.Clamp01(effort);
            if (effort <= walkAt) return 0f;
            if (effort < trotAt) return Ramp(effort, walkAt, trotAt, walkSpeed, trotSpeed);
            if (effort < canterAt) return Ramp(effort, trotAt, canterAt, trotSpeed, canterSpeed);
            if (effort < gallopAt) return Ramp(effort, canterAt, gallopAt, canterSpeed, gallopSpeed);
            if (effort < sprintAt) return Ramp(effort, gallopAt, sprintAt, gallopSpeed, sprintSpeed);
            return sprintSpeed;
        }

        static float Ramp(float value, float from, float to, float fromSpeed, float toSpeed)
        {
            var span = to - from;
            return span <= 0.0001f
                ? toSpeed
                : Mathf.Lerp(fromSpeed, toSpeed, (value - from) / span);
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
