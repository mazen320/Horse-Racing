using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using UTool;
using UTool.TabSystem;
using UTool.Utility;
using UTool.PageSystem;

using DG.Tweening;
using Newtonsoft.Json;

namespace UTool.Leaderboard
{
    [HasTabField]
    public class Leaderboard : MonoBehaviour
    {
        public static Leaderboard _instance;

        public static void Open() => _instance.page.Open();
        public static void EnterEntry(EntrieData entrieData) => _instance.Enter(entrieData);
        public static void EnterEntries(List<EntrieData> dataEntries) => _instance.Enter(dataEntries);

        [EditorButton(nameof(AddRandomEntry), activityType: ButtonActivityType.OnPlayMode)]
        [EditorButton(nameof(ClearLeaderboard), activityType: ButtonActivityType.OnPlayMode)]
        [EditorButton(nameof(UpdateEntires), activityType: ButtonActivityType.OnPlayMode)]
        [SerializeField] public Page page;
        [SerializeField] public Transform entrieHolder;
        [SpaceArea]
        [SerializeField] private ParticleSystem p1;
        [SerializeField] private ParticleSystem p2;
        [SpaceArea]
        [SerializeField] private int restartDelay = 6;
        [SpaceArea]
        [SerializeField] private bool sortDescending = true;
        [SpaceArea]
        [EditorButton(nameof(GetChildEntries))]
        [SerializeField][ReorderableList] private List<Entrie> entries = new List<Entrie>();
        [SerializeField][Disable][ReorderableList] private List<EntrieData> dataEntries = new List<EntrieData>();

        private string savePath => $@"{UT.dataPath}\Leaderboard";
        private string fileName => $"entries.json";

        string filePath => $@"{savePath}\{fileName}";

        Tween tween;

        private void GetChildEntries()
        {
            entries = entrieHolder.GetChilds<Entrie>();
            entries.RemoveAt(0);
            this.RecordPrefabChanges();
        }

        private void Awake()
        {
            _instance = this;
        }

        private void Start()
        {
            UpdateLeaderboard();
        }

        private void Update()
        {
            if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.L))
                ClearLeaderboard();
        }

        public void UpdateLeaderboard()
        {
            LoadUsersScore();
            UpdateEntires();
        }

        public void OnOpen()
        {
            if (restartDelay != 0)
                DOVirtual.DelayedCall(restartDelay, () => UT.RestartLevel());
        }

        private void AddRandomEntry()
        {
            EntrieData entrieData = new EntrieData();
            entrieData.playerName = Guid.NewGuid()
                                        .ToString()
                                        .Replace('-', ' ')
                                        .Substring(0, UnityEngine.Random.Range(3, 15))
                                        .Trim();
            entrieData.score = UnityEngine.Random.Range(0, 99);
            Enter(entrieData);
        }

        public void Enter(EntrieData _dataEntries) => Enter(new List<EntrieData> { _dataEntries });
        public void Enter(List<EntrieData> _dataEntries)
        {
            dataEntries.AddRange(_dataEntries);

            if (sortDescending)
                dataEntries = dataEntries.OrderByDescending(x => x.score).ToList();
            else
                dataEntries = dataEntries.OrderBy(x => x.score).ToList();

            SaveUsersScore();
            UpdateEntires();

            foreach (EntrieData entrieData in dataEntries)
                PlayParticle(entrieData);
        }

        private void PlayParticle(EntrieData entrieData)
        {
            if (dataEntries.IndexOf(entrieData) == -1)
                return;

            if (dataEntries.IndexOf(entrieData) >= 10)
                return;

            tween.KillTween();
            tween = DOVirtual.DelayedCall(1f, () =>
            {
                if (dataEntries[0] == entrieData)
                {
                    if (p1)
                        p1.Play();
                }
                else
                {
                    if (p2)
                        p2.Play();
                }
            });
        }

        private void UpdateEntires()
        {
            List<(Entrie, int)> updateEntrie = new List<(Entrie, int)>();

            for (int i = 0; i < entries.Count; i++)
                if (dataEntries.HasIndex(i))
                    if (entries[i].entrieData != dataEntries[i])
                        updateEntrie.Add((entries[i], i));

            int index = updateEntrie.Count;
            bool first = true;
            foreach((Entrie, int) entrie in updateEntrie)
            {
                entrie.Item1.UpdateInfo(dataEntries[entrie.Item2], first, index);
                first = false;
                index--;
            }
        }

        private void SaveUsersScore()
        {
            UUtility.CheckAndCreateDirectory(savePath);

            string scoreData = JsonConvert.SerializeObject(dataEntries, Formatting.Indented);
            UUtility.WriteAllText(filePath, scoreData);
        }

        private void LoadUsersScore()
        {
            UUtility.CheckAndCreateDirectory(savePath);

            if (!UUtility.FileExists(filePath))
            {
                UUtility.CreateFile(filePath).Close();
                SaveUsersScore();
            }
            else
            {
                try
                {
                    string data = UUtility.ReadAllText(filePath);
                    dataEntries = JsonConvert.DeserializeObject<List<EntrieData>>(data);
                }
                catch
                {
                    SaveUsersScore();
                }
            }
        }

        [TabButton]
        public void ClearLeaderboard()
        {
            string oldData = UUtility.ReadAllText(filePath);

            string timeData = DateTime.Now.ToddMMyyyyhhmmss();

            string bpFilePath = savePath + $@"\backupEntries_{timeData}.json";
            UUtility.CreateFile(bpFilePath).Close();
            UUtility.WriteAllText(bpFilePath, oldData);

            dataEntries = new List<EntrieData>();
            SaveUsersScore();

            foreach (Entrie item in entries)
                item.Reset();
        }
    }

    [System.Serializable]
    public class EntrieData
    {
        [BeginGroup] public string playerName;
        [SpaceArea]
        [EndGroup] public float score;
    }
}