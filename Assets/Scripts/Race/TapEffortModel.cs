using System.Collections.Generic;
using UnityEngine;

namespace HorseRacing.Race
{
    public sealed class TapEffortModel
    {
        public const int MaxTapHistory = 64;
        public const float DefaultDriveCeiling = 1f;

        readonly Queue<float> _tapTimes = new Queue<float>(MaxTapHistory);
        float _effort;
        float _drive;
        float _tapsPerSecond;

        public float Effort => _effort;
        public int TapCount => _tapTimes.Count;

        /// <summary>
        /// Smoothed tap rate in taps per second. Unlike <see cref="Effort"/> this keeps
        /// climbing once a player is already fast enough to hold the top gait, so a
        /// faster runner on the platform can still be told apart from a slower one.
        /// </summary>
        public float TapsPerSecond => _tapsPerSecond;

        /// <summary>
        /// Effort without the 0..1 ceiling, capped by the drive ceiling passed to
        /// <see cref="Tick(float, float, float, float, float, float, float)"/>. A value of
        /// 2 means the player is tapping twice as fast as the top-gait requirement.
        /// </summary>
        public float Drive => _drive;

        public void RegisterTap(float timestamp)
        {
            if (float.IsNaN(timestamp) || float.IsInfinity(timestamp)) return;
            while (_tapTimes.Count >= MaxTapHistory) _tapTimes.Dequeue();
            _tapTimes.Enqueue(timestamp);
        }

        public float Tick(float now, float deltaTime, float tapWindow,
            float tapsPerSecondForMax, float accelTime, float coastTime)
        {
            return Tick(now, deltaTime, tapWindow, tapsPerSecondForMax,
                accelTime, coastTime, DefaultDriveCeiling);
        }

        public float Tick(float now, float deltaTime, float tapWindow,
            float tapsPerSecondForMax, float accelTime, float coastTime, float driveCeiling)
        {
            tapWindow = Mathf.Max(0.05f, tapWindow);
            tapsPerSecondForMax = Mathf.Max(0.1f, tapsPerSecondForMax);
            driveCeiling = Mathf.Max(1f, driveCeiling);
            var cutoff = now - tapWindow;
            while (_tapTimes.Count > 0 && _tapTimes.Peek() < cutoff) _tapTimes.Dequeue();

            var rawRatio = (_tapTimes.Count / tapWindow) / tapsPerSecondForMax;
            var target = Mathf.Clamp01(rawRatio);
            _effort = Smooth(_effort, target, deltaTime, accelTime, coastTime);
            _effort = Mathf.Clamp01(_effort);

            var driveTarget = Mathf.Clamp(rawRatio, 0f, driveCeiling);
            _drive = Smooth(_drive, driveTarget, deltaTime, accelTime, coastTime);
            _drive = Mathf.Clamp(_drive, 0f, driveCeiling);
            _tapsPerSecond = _drive * tapsPerSecondForMax;

            if (_tapTimes.Count == 0 && _effort < 0.01f)
            {
                _effort = 0f;
                _drive = 0f;
                _tapsPerSecond = 0f;
            }

            return _effort;
        }

        static float Smooth(float current, float target, float deltaTime,
            float accelTime, float coastTime)
        {
            var timeConstant = target > current ? accelTime : coastTime;
            if (timeConstant <= 0.0001f)
                return target;

            return Mathf.Lerp(current, target,
                1f - Mathf.Exp(-Mathf.Max(0f, deltaTime) / timeConstant));
        }

        public static int SelectGait(float effort, int gait, float walkAt,
            float trotAt, float canterAt, float gallopAt, float hysteresis)
        {
            return SelectGait(effort, gait, walkAt, trotAt, canterAt, gallopAt,
                float.PositiveInfinity, hysteresis);
        }

        public static int SelectGait(float effort, int gait, float walkAt,
            float trotAt, float canterAt, float gallopAt, float sprintAt, float hysteresis)
        {
            effort = Mathf.Clamp01(effort);
            gait = Mathf.Clamp(gait, 0, 5);
            hysteresis = Mathf.Max(0f, hysteresis);

            while (gait < 5 && effort >= Threshold(
                       gait + 1, walkAt, trotAt, canterAt, gallopAt, sprintAt))
                gait++;
            while (gait > 0 && effort <= Mathf.Max(0f,
                       Threshold(gait, walkAt, trotAt, canterAt, gallopAt, sprintAt) - hysteresis))
                gait--;
            return gait;
        }

        static float Threshold(int gait, float walkAt, float trotAt, float canterAt,
            float gallopAt, float sprintAt)
        {
            switch (gait)
            {
                case 1: return walkAt;
                case 2: return trotAt;
                case 3: return canterAt;
                case 4: return gallopAt;
                default: return sprintAt;
            }
        }

        public void Reset()
        {
            _tapTimes.Clear();
            _effort = 0f;
            _drive = 0f;
            _tapsPerSecond = 0f;
        }
    }
}
