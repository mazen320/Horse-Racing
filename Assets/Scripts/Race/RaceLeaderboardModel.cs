using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace HorseRacing.Race
{
    [Serializable]
    public sealed class RaceLeaderboardEntry
    {
        public string name;
        public float seconds;
        public string recordedUtc;

        public RaceLeaderboardEntry() { }

        public RaceLeaderboardEntry(string name, float seconds, DateTime recordedUtc)
        {
            this.name = name;
            this.seconds = seconds;
            this.recordedUtc = recordedUtc.ToString("o");
        }
    }

    [Serializable]
    public sealed class RaceLeaderboardData
    {
        public List<RaceLeaderboardEntry> entries = new List<RaceLeaderboardEntry>();
    }

    /// <summary>
    /// Keeps the fastest recorded run times, quickest first. Pure logic so the ordering
    /// and trimming rules can be tested without an editor or a file on disk.
    /// </summary>
    public sealed class RaceLeaderboardModel
    {
        public const int DefaultCapacity = 10;
        public const string FallbackName = "RIDER";

        readonly List<RaceLeaderboardEntry> _entries = new List<RaceLeaderboardEntry>();
        readonly int _capacity;

        public RaceLeaderboardModel(int capacity = DefaultCapacity)
        {
            _capacity = Mathf.Max(1, capacity);
        }

        public int Count => _entries.Count;
        public int Capacity => _capacity;
        public IReadOnlyList<RaceLeaderboardEntry> Entries => _entries;

        public RaceLeaderboardEntry Fastest => _entries.Count > 0 ? _entries[0] : null;

        /// <summary>
        /// Files a finishing time. Returns the leaderboard position (1 = fastest ever) or
        /// 0 when the run was too slow to make the board.
        /// </summary>
        public int Submit(string name, float seconds, DateTime recordedUtc)
        {
            if (seconds <= 0f || float.IsNaN(seconds) || float.IsInfinity(seconds))
                return 0;

            var entry = new RaceLeaderboardEntry(NormalizeName(name), seconds, recordedUtc);

            var index = _entries.Count;
            for (var i = 0; i < _entries.Count; i++)
            {
                if (seconds >= _entries[i].seconds) continue;
                index = i;
                break;
            }

            if (index >= _capacity)
                return 0;

            _entries.Insert(index, entry);
            if (_entries.Count > _capacity)
                _entries.RemoveRange(_capacity, _entries.Count - _capacity);

            return index + 1;
        }

        public void Load(RaceLeaderboardData data)
        {
            _entries.Clear();
            if (data?.entries == null) return;

            foreach (var entry in data.entries)
            {
                if (entry == null) continue;
                if (entry.seconds <= 0f || float.IsNaN(entry.seconds) || float.IsInfinity(entry.seconds))
                    continue;

                entry.name = NormalizeName(entry.name);
                _entries.Add(entry);
            }

            _entries.Sort((left, right) => left.seconds.CompareTo(right.seconds));
            if (_entries.Count > _capacity)
                _entries.RemoveRange(_capacity, _entries.Count - _capacity);
        }

        public RaceLeaderboardData ToData()
        {
            var data = new RaceLeaderboardData();
            data.entries.AddRange(_entries);
            return data;
        }

        public void Clear() => _entries.Clear();

        /// <summary>CSV header and rows for archiving before a clear.</summary>
        public static string BuildCsv(IReadOnlyList<RaceLeaderboardEntry> entries)
        {
            var builder = new StringBuilder();
            builder.AppendLine("Rank,Name,Seconds,Time,RecordedUtc");

            if (entries == null)
                return builder.ToString();

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null) continue;

                builder.Append(i + 1).Append(',')
                    .Append(EscapeCsv(entry.name)).Append(',')
                    .Append(entry.seconds.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                    .Append(EscapeCsv(FormatSeconds(entry.seconds))).Append(',')
                    .Append(EscapeCsv(entry.recordedUtc ?? string.Empty))
                    .AppendLine();
            }

            return builder.ToString();
        }

        static string EscapeCsv(string value)
        {
            value ??= string.Empty;
            if (value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0)
                return value;

            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        public static string NormalizeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return FallbackName;

            var trimmed = name.Trim();
            // Arabic has no case; uppercasing Latin-only keeps English nameplates consistent.
            return ContainsArabicScript(trimmed) ? trimmed : trimmed.ToUpperInvariant();
        }

        public static bool ContainsArabicScript(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            foreach (var ch in value)
            {
                if (ch is >= '\u0600' and <= '\u06FF' // Arabic
                    or >= '\u0750' and <= '\u077F' // Arabic Supplement
                    or >= '\u08A0' and <= '\u08FF' // Arabic Extended-A
                    or >= '\uFB50' and <= '\uFDFF' // Presentation Forms-A
                    or >= '\uFE70' and <= '\uFEFF') // Presentation Forms-B
                    return true;
            }

            return false;
        }

        /// <summary>Race clock format shared by the HUD timer and the board: 0:00.0</summary>
        public static string FormatSeconds(float seconds)
        {
            seconds = Mathf.Max(0f, seconds);
            var minutes = Mathf.FloorToInt(seconds / 60f);
            return $"{minutes:0}:{seconds % 60f:00.0}";
        }
    }
}
