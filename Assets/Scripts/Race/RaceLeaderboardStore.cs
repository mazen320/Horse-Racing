using System;
using System.IO;
using UnityEngine;

namespace HorseRacing.Race
{
    /// <summary>
    /// Persists the fastest times next to the registration log so the board survives
    /// between activation sessions. Failures never interrupt a race: the board simply
    /// falls back to whatever is in memory.
    /// </summary>
    public sealed class RaceLeaderboardStore
    {
        public const string DefaultFileName = "Leaderboard.json";

        readonly RaceLeaderboardModel _model;
        readonly string _path;

        public RaceLeaderboardStore(int capacity = RaceLeaderboardModel.DefaultCapacity,
            string fileName = DefaultFileName)
        {
            _model = new RaceLeaderboardModel(capacity);
            _path = Path.Combine(Application.persistentDataPath, fileName);
        }

        public RaceLeaderboardModel Model => _model;
        public string FilePath => _path;

        public void Load()
        {
            try
            {
                if (!File.Exists(_path))
                {
                    _model.Clear();
                    return;
                }

                var json = File.ReadAllText(_path);
                _model.Load(JsonUtility.FromJson<RaceLeaderboardData>(json));
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Leaderboard could not be read from {_path}: {exception.Message}");
                _model.Clear();
            }
        }

        public int Submit(string name, float seconds)
        {
            var position = _model.Submit(name, seconds, DateTime.UtcNow);
            if (position > 0)
                Save();
            return position;
        }

        public void Save()
        {
            try
            {
                var directory = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                File.WriteAllText(_path, JsonUtility.ToJson(_model.ToData(), true));
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Leaderboard could not be written to {_path}: {exception.Message}");
            }
        }

        /// <summary>
        /// Writes the current board to CSV, clears the live board, then saves empty JSON.
        /// Returns the backup path, or null when there was nothing to archive.
        /// </summary>
        public string ClearWithCsvBackup(DateTime? archivedAt = null)
        {
            if (_model.Count == 0)
            {
                _model.Clear();
                Save();
                return null;
            }

            var stamp = (archivedAt ?? DateTime.Now).ToString("yyyyMMdd_HHmmss");
            var directory = Path.GetDirectoryName(_path);
            if (string.IsNullOrEmpty(directory))
                directory = Application.persistentDataPath;

            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            var backupPath = Path.Combine(directory, $"Leaderboard_backup_{stamp}.csv");

            try
            {
                File.WriteAllText(backupPath, RaceLeaderboardModel.BuildCsv(_model.Entries));
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Leaderboard backup could not be written to {backupPath}: {exception.Message}");
                return null;
            }

            _model.Clear();
            Save();
            return backupPath;
        }
    }
}
