using System.Collections.Generic;
using UnityEngine;

namespace HorseRacing.Race
{
    public sealed class TapEffortModel
    {
        public const int MaxTapHistory = 64;
        readonly Queue<float> _tapTimes = new Queue<float>(MaxTapHistory);
        float _effort;

        public float Effort => _effort;
        public int TapCount => _tapTimes.Count;

        public void RegisterTap(float timestamp)
        {
            if (float.IsNaN(timestamp) || float.IsInfinity(timestamp)) return;
            while (_tapTimes.Count >= MaxTapHistory) _tapTimes.Dequeue();
            _tapTimes.Enqueue(timestamp);
        }

        public float Tick(float now, float deltaTime, float tapWindow,
            float tapsPerSecondForMax, float accelTime, float coastTime)
        {
            tapWindow = Mathf.Max(0.05f, tapWindow);
            tapsPerSecondForMax = Mathf.Max(0.1f, tapsPerSecondForMax);
            var cutoff = now - tapWindow;
            while (_tapTimes.Count > 0 && _tapTimes.Peek() < cutoff) _tapTimes.Dequeue();

            var target = Mathf.Clamp01((_tapTimes.Count / tapWindow) / tapsPerSecondForMax);
            var timeConstant = target > _effort ? accelTime : coastTime;
            if (timeConstant <= 0.0001f)
                _effort = target;
            else
                _effort = Mathf.Clamp01(Mathf.Lerp(
                    _effort, target,
                    1f - Mathf.Exp(-Mathf.Max(0f, deltaTime) / timeConstant)));

            if (_tapTimes.Count == 0 && _effort < 0.01f) _effort = 0f;
            return _effort;
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
            while (gait > 0 && effort < Mathf.Max(0f,
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
        }
    }
}
