using System.IO;
using NUnit.Framework;
using ShadowSteps.Data;

namespace ShadowSteps.Tests.EditMode
{
    public class SaveRepositoryTests
    {
        private string _dir;
        private string _path;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "shadowsteps_tests_" + Path.GetRandomFileName());
            Directory.CreateDirectory(_dir);
            _path = Path.Combine(_dir, "save.json");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        [Test]
        public void FreshInstall_CreatesDefaults()
        {
            var repo = new SaveRepository(_path);
            var d = repo.Load();
            Assert.AreEqual(SaveLoadStatus.CreatedNew, repo.LastLoadStatus);
            Assert.AreEqual(1, d.HighestUnlockedLevelId);
            Assert.IsTrue(d.IsUnlocked(1));
            Assert.IsFalse(d.IsUnlocked(2));
            Assert.IsTrue(d.Settings.SfxOn);
        }

        [Test]
        public void SaveThenLoad_RoundTrips()
        {
            var repo = new SaveRepository(_path);
            var d = repo.Load();
            Assert.IsTrue(d.RecordWin(1, 9, 2));
            Assert.IsFalse(d.RecordWin(1, 12, 2), "worse result must not replace best");
            Assert.IsTrue(d.RecordWin(1, 8, 2));
            d.Settings.MusicOn = false;
            d.Settings.ReducedMotion = true;
            d.MarkTutorialSeen("smoke");
            d.LastPlayedLevelId = 2;
            Assert.IsTrue(repo.Save(d), repo.LastMessage);

            var repo2 = new SaveRepository(_path);
            var loaded = repo2.Load();
            Assert.AreEqual(SaveLoadStatus.Loaded, repo2.LastLoadStatus);
            Assert.AreEqual(2, loaded.HighestUnlockedLevelId);
            Assert.AreEqual(8, loaded.GetResult(1).BestTurns);
            Assert.IsTrue(loaded.IsCompleted(1));
            Assert.IsFalse(loaded.Settings.MusicOn);
            Assert.IsTrue(loaded.Settings.ReducedMotion);
            Assert.IsTrue(loaded.HasSeenTutorial("smoke"));
            Assert.AreEqual(2, loaded.LastPlayedLevelId);
        }

        [Test]
        public void SecondSave_KeepsBackupOfPrevious()
        {
            var repo = new SaveRepository(_path);
            var d = repo.Load();
            d.RecordWin(1, 8, 2);
            repo.Save(d);
            d.RecordWin(2, 9, 3);
            repo.Save(d);
            Assert.IsTrue(File.Exists(repo.BackupPath));
            Assert.IsFalse(File.Exists(repo.TempPath));
            var backup = SaveSerializer.FromJson(File.ReadAllText(repo.BackupPath));
            Assert.AreEqual(2, backup.HighestUnlockedLevelId);
        }

        [Test]
        public void CorruptMain_RestoresFromBackup()
        {
            var repo = new SaveRepository(_path);
            var d = repo.Load();
            d.RecordWin(1, 8, 2); repo.Save(d);
            d.RecordWin(2, 9, 3); repo.Save(d);
            File.WriteAllText(_path, "{ this is not json");
            var repo2 = new SaveRepository(_path);
            var loaded = repo2.Load();
            Assert.AreEqual(SaveLoadStatus.RestoredFromBackup, repo2.LastLoadStatus);
            Assert.AreEqual(2, loaded.HighestUnlockedLevelId);
        }

        [Test]
        public void CorruptMainAndBackup_ResetsAndKeepsCorruptCopy()
        {
            File.WriteAllText(_path, "garbage");
            File.WriteAllText(_path + ".bak", "more garbage");
            var repo = new SaveRepository(_path);
            var loaded = repo.Load();
            Assert.AreEqual(SaveLoadStatus.ResetAfterCorruption, repo.LastLoadStatus);
            Assert.AreEqual(1, loaded.HighestUnlockedLevelId);
            Assert.IsTrue(File.Exists(_path + ".corrupt"));
        }

        [Test]
        public void OlderSaveWithMissingFields_LoadsWithDefaults()
        {
            var d = SaveSerializer.FromJson("{\"version\":1,\"highest_unlocked_level_id\":5}");
            Assert.AreEqual(5, d.HighestUnlockedLevelId);
            Assert.IsTrue(d.Settings.HapticsOn);
            Assert.AreEqual(0, d.Results.Count);
        }

        [Test]
        public void NewerSaveVersion_IsRejected()
        {
            Assert.Throws<LevelDataException>(() => SaveSerializer.FromJson("{\"version\":" + (SaveData.CurrentVersion + 1) + "}"));
            Assert.AreEqual(SaveData.CurrentVersion, SaveSerializer.FromJson("{\"version\":1}").Version, "version-1 files still load");
        }
    }
}
