using System;
using System.IO;
using NUnit.Framework;

namespace HorseRacing.Race.Tests
{
    public sealed class RaceLeaderboardModelTests
    {
        static readonly DateTime Recorded = new DateTime(2026, 8, 26, 12, 0, 0, DateTimeKind.Utc);

        [Test]
        public void Submit_OrdersFastestFirstAndReportsPosition()
        {
            var model = new RaceLeaderboardModel(5);

            Assert.That(model.Submit("Sara", 42.5f, Recorded), Is.EqualTo(1));
            Assert.That(model.Submit("Omar", 39.1f, Recorded), Is.EqualTo(1));
            Assert.That(model.Submit("Lena", 51.4f, Recorded), Is.EqualTo(3));

            Assert.That(model.Entries[0].name, Is.EqualTo("OMAR"));
            Assert.That(model.Entries[1].name, Is.EqualTo("SARA"));
            Assert.That(model.Entries[2].name, Is.EqualTo("LENA"));
            Assert.That(model.Fastest.seconds, Is.EqualTo(39.1f));
        }

        [Test]
        public void Submit_TrimsToCapacityAndRejectsSlowRuns()
        {
            var model = new RaceLeaderboardModel(2);
            model.Submit("A", 10f, Recorded);
            model.Submit("B", 20f, Recorded);

            Assert.That(model.Submit("C", 30f, Recorded), Is.Zero);
            Assert.That(model.Count, Is.EqualTo(2));

            Assert.That(model.Submit("D", 15f, Recorded), Is.EqualTo(2));
            Assert.That(model.Count, Is.EqualTo(2));
            Assert.That(model.Entries[1].name, Is.EqualTo("D"));
        }

        [Test]
        public void Submit_IgnoresInvalidTimes()
        {
            var model = new RaceLeaderboardModel();

            Assert.That(model.Submit("A", 0f, Recorded), Is.Zero);
            Assert.That(model.Submit("A", -4f, Recorded), Is.Zero);
            Assert.That(model.Submit("A", float.NaN, Recorded), Is.Zero);
            Assert.That(model.Submit("A", float.PositiveInfinity, Recorded), Is.Zero);
            Assert.That(model.Count, Is.Zero);
        }

        [Test]
        public void Submit_FallsBackWhenNameIsBlank()
        {
            var model = new RaceLeaderboardModel();
            model.Submit("   ", 20f, Recorded);

            Assert.That(model.Entries[0].name, Is.EqualTo(RaceLeaderboardModel.FallbackName));
        }

        [Test]
        public void LoadThenToData_SortsAndSurvivesRoundTrip()
        {
            var source = new RaceLeaderboardModel(3);
            source.Submit("A", 30f, Recorded);
            source.Submit("B", 12f, Recorded);

            var restored = new RaceLeaderboardModel(3);
            restored.Load(source.ToData());

            Assert.That(restored.Count, Is.EqualTo(2));
            Assert.That(restored.Entries[0].name, Is.EqualTo("B"));
            Assert.That(restored.Entries[1].name, Is.EqualTo("A"));
        }

        [Test]
        public void Load_DropsCorruptRowsAndRespectsCapacity()
        {
            var data = new RaceLeaderboardData();
            data.entries.Add(new RaceLeaderboardEntry("A", 30f, Recorded));
            data.entries.Add(null);
            data.entries.Add(new RaceLeaderboardEntry("B", float.NaN, Recorded));
            data.entries.Add(new RaceLeaderboardEntry("C", 10f, Recorded));
            data.entries.Add(new RaceLeaderboardEntry("D", 20f, Recorded));

            var model = new RaceLeaderboardModel(2);
            model.Load(data);

            Assert.That(model.Count, Is.EqualTo(2));
            Assert.That(model.Entries[0].name, Is.EqualTo("C"));
            Assert.That(model.Entries[1].name, Is.EqualTo("D"));
        }

        [Test]
        public void FormatSeconds_MatchesRaceClock()
        {
            Assert.That(RaceLeaderboardModel.FormatSeconds(0f), Is.EqualTo("0:00.0"));
            Assert.That(RaceLeaderboardModel.FormatSeconds(9.44f), Is.EqualTo("0:09.4"));
            Assert.That(RaceLeaderboardModel.FormatSeconds(75.24f), Is.EqualTo("1:15.2"));
            Assert.That(RaceLeaderboardModel.FormatSeconds(119.96f), Is.EqualTo("1:60.0"));
            Assert.That(RaceLeaderboardModel.FormatSeconds(-3f), Is.EqualTo("0:00.0"));
        }

        [Test]
        public void BuildCsv_ListsRankNameSecondsTimeAndRecordedUtc()
        {
            var model = new RaceLeaderboardModel(3);
            model.Submit("Omar", 39.1f, Recorded);
            model.Submit("Sara", 42.5f, Recorded.AddMinutes(1));

            var csv = RaceLeaderboardModel.BuildCsv(model.Entries);

            Assert.That(csv, Does.StartWith("Rank,Name,Seconds,Time,RecordedUtc"));
            StringAssert.Contains("1,OMAR,39.100,0:39.1", csv);
            StringAssert.Contains("2,SARA,42.500,0:42.5", csv);
        }

        [Test]
        public void ClearWithCsvBackup_WritesCsvThenClearsJson()
        {
            var path = Path.Combine(Path.GetTempPath(), $"leaderboard_test_{Guid.NewGuid():N}.json");
            var store = new RaceLeaderboardStore(5, Path.GetFileName(path));
            var storeType = typeof(RaceLeaderboardStore);
            var pathField = storeType.GetField("_path", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            pathField.SetValue(store, path);

            store.Submit("Alex", 12.3f);
            var backupPath = store.ClearWithCsvBackup(new DateTime(2026, 8, 26, 19, 30, 0));

            Assert.That(backupPath, Does.EndWith("Leaderboard_backup_20260826_193000.csv"));
            Assert.That(File.ReadAllText(backupPath), Does.Contain("ALEX"));
            Assert.That(store.Model.Count, Is.Zero);
            Assert.That(File.ReadAllText(path), Does.Contain("\"entries\": []").Or.Contains("\"entries\":[]"));

            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(backupPath)) File.Delete(backupPath);
        }
    }
}
