using UnityEngine;

namespace HorseRacing.Race
{
    public static class RaceCameraHeadingModel
    {
        public static float StepYaw(float currentYaw, float targetYaw,
            ref float yawVelocity, float deltaTime, float smoothTime,
            float maxLagDegrees, float snapAngleDegrees, bool forceSnap = false)
        {
            smoothTime = Mathf.Max(0.0001f, smoothTime);
            maxLagDegrees = Mathf.Max(0f, maxLagDegrees);
            snapAngleDegrees = Mathf.Max(maxLagDegrees, snapAngleDegrees);
            var error = Mathf.DeltaAngle(currentYaw, targetYaw);

            if (forceSnap || deltaTime <= 0f || Mathf.Abs(error) >= snapAngleDegrees)
            {
                yawVelocity = 0f;
                return Normalize(targetYaw);
            }

            var smoothed = Mathf.SmoothDampAngle(currentYaw, targetYaw,
                ref yawVelocity, smoothTime, Mathf.Infinity, deltaTime);
            var lag = Mathf.DeltaAngle(targetYaw, smoothed);
            smoothed = targetYaw + Mathf.Clamp(lag, -maxLagDegrees, maxLagDegrees);
            return Normalize(smoothed);
        }

        static float Normalize(float angle) => Mathf.Repeat(angle, 360f);
    }
}
