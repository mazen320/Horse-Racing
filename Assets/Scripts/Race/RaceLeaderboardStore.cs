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
    }
}
