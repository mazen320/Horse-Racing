using System;
using System.Collections.Generic;
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

        public static string NormalizeName(string name)
        {
            return string.IsNullOrWhiteSpace(name)
                ? FallbackName
                : name.Trim().ToUpperInvariant();
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
