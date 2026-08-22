using UnityEngine;

namespace HorseRacing.Race
{
    public sealed class RootMotionDistanceAccumulator
    {
        float _pending;

        public void Add(Vector3 delta)
        {
            delta.y = 0f;
            if (float.IsNaN(delta.x) || float.IsNaN(delta.z)) return;
            _pending += delta.magnitude;
        }

        public float Consume()
        {
            var value = _pending;
            _pending = 0f;
            return value;
        }

        public void Reset() => _pending = 0f;
    }
}
