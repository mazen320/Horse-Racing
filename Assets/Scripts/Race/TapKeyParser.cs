using System;
using UnityEngine.InputSystem;

namespace HorseRacing.Race
{
    public static class TapKeyParser
    {
        public static bool TryParse(string text, out Key key)
        {
            key = Key.None;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            var trimmed = text.Trim();
            if (Enum.TryParse(trimmed, ignoreCase: true, out key) && key != Key.None)
                return true;

            switch (NormalizeAlias(trimmed))
            {
                case "space":
                case "spacebar":
                    key = Key.Space;
                    return true;
                case "enter":
                case "return":
                    key = Key.Enter;
                    return true;
                case "esc":
                case "escape":
                    key = Key.Escape;
                    return true;
                case "leftarrow":
                case "left":
                    key = Key.LeftArrow;
                    return true;
                case "rightarrow":
                case "right":
                    key = Key.RightArrow;
                    return true;
                case "uparrow":
                case "up":
                    key = Key.UpArrow;
                    return true;
                case "downarrow":
                case "down":
                    key = Key.DownArrow;
                    return true;
                default:
                    return false;
            }
        }

        public static Key[] ParseBindings(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return Array.Empty<Key>();

            var parts = text.Split(new[] { ',', ';', '|', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return Array.Empty<Key>();

            var keys = new Key[parts.Length];
            var count = 0;
            for (var i = 0; i < parts.Length; i++)
            {
                if (!TryParse(parts[i], out var key))
                    continue;

                keys[count++] = key;
            }

            if (count == 0)
                return Array.Empty<Key>();

            if (count == keys.Length)
                return keys;

            var trimmed = new Key[count];
            Array.Copy(keys, trimmed, count);
            return trimmed;
        }

        public static string Format(Key key) => key == Key.None ? string.Empty : key.ToString();

        static string NormalizeAlias(string text)
        {
            return text
                .Replace(" ", string.Empty)
                .Replace("_", string.Empty)
                .Replace("-", string.Empty)
                .ToLowerInvariant();
        }
    }
}
