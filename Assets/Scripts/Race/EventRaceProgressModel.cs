using UnityEngine;

namespace HorseRacing.Race
{
    /// <summary>
    /// Tracks one short event race independently from the full presentation spline.
    /// </summary>
    public sealed class EventRaceProgressModel
    {
        const float MinimumRaceDistance = 0.01f;

        public float DistanceTravelled { get; private set; }
        public bool IsFinished { get; private set; }

        public float Progress(float raceDistance)
        {
            raceDistance = Mathf.Max(MinimumRaceDistance, raceDistance);
            return Mathf.Clamp01(DistanceTravelled / raceDistance);
        }

        public float Advance(float requestedDistance, float raceDistance)
        {
            if (IsFinished || float.IsNaN(requestedDistance) ||
                float.IsInfinity(requestedDistance) || requestedDistance <= 0f)
                return 0f;

            raceDistance = Mathf.Max(MinimumRaceDistance, raceDistance);
            var acceptedDistance = Mathf.Min(requestedDistance,
                Mathf.Max(0f, raceDistance - DistanceTravelled));
            DistanceTravelled += acceptedDistance;

            if (DistanceTravelled >= raceDistance - 0.0001f)
            {
                DistanceTravelled = raceDistance;
                IsFinished = true;
            }

            return acceptedDistance;
        }

        public void Reset()
        {
            DistanceTravelled = 0f;
            IsFinished = false;
        }
    }
}
