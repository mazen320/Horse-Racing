using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace HorseRacing.Race
{
    public sealed class ConfigurableKeyboardTapInput
    {
        readonly HashSet<Key> _keys = new HashSet<Key>();
        public int BindingCount => _keys.Count;

        public void SetBindings(IEnumerable<Key> keys)
        {
            _keys.Clear();
            if (keys == null) return;
            foreach (var key in keys)
                if (key != Key.None) _keys.Add(key);
        }

        public bool WasPressedThisFrame(Keyboard keyboard)
        {
            if (keyboard == null) return false;
            foreach (var key in _keys)
            {
                var control = keyboard[key];
                if (control != null && control.wasPressedThisFrame) return true;
            }
            return false;
        }
    }
}
