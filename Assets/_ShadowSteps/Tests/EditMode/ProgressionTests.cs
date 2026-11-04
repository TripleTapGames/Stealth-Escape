using NUnit.Framework;
using ShadowSteps.Data;

namespace ShadowSteps.Tests.EditMode
{
    public class ProgressionTests
    {
        private static StageCatalog RuntimeCatalog() => new StageCatalog(StageJson.Parse(System.IO.File.ReadAllText(
            System.IO.Path.Combine(TestPaths.ProjectRoot, "Assets/StreamingAssets/ShadowSteps/levels_v4.json"))).Stages);

        [Test]
        public void Resume_NewInstall_StartsAtFirstFtue()
        {
            var catalog = RuntimeCatalog();
            Assert.AreEqual("FTUE-1", StageProgression.ResumeStage(catalog, new SaveData()).Code);
            Assert.AreEqual("FTUE-1", StageProgression.ResumeStage(catalog, null).Code);
        }

        [Test]
        public void Resume_RoundTrippedSave_UsesLastUnfinishedUnlockedStage()
        {
            var catalog = RuntimeCatalog(); var save = new SaveData();
            for (int i = 0; i < 7; i++) StageProgression.RecordWin(catalog, save, catalog.ByIndex(i), 10, false);
            save.LastPlayedLevelId = catalog.ByIndex(7).Id;
            var loaded = SaveSerializer.FromJson(SaveSerializer.ToJson(save));
            Assert.AreEqual(catalog.ByIndex(7).Id, StageProgression.ResumeStage(catalog, loaded).Id);
        }

        [Test]
        public void Resume_WinSavesNextStageBeforeResultsAreDismissed()
        {
            var catalog = RuntimeCatalog(); var save = new SaveData();
            StageProgression.RecordWin(catalog, save, catalog.ByIndex(0), 10, false);
            Assert.AreEqual(catalog.ByIndex(1).Id, save.LastPlayedLevelId);
            Assert.AreEqual(catalog.ByIndex(1).Id, StageProgression.ResumeStage(catalog, save).Id);
        }

        [Test]
        public void Resume_StaleLockedOrCompletedCheckpoint_FallsBackToProgress()
        {
            var catalog = RuntimeCatalog(); var save = new SaveData();
            save.LastPlayedLevelId = 9999;
            Assert.AreEqual("FTUE-1", StageProgression.ResumeStage(catalog, save).Code);
            save.LastPlayedLevelId = catalog.ByIndex(5).Id;
            Assert.AreEqual("FTUE-1", StageProgression.ResumeStage(catalog, save).Code);
            StageProgression.RecordWin(catalog, save, catalog.ByIndex(0), 10, false);
            save.LastPlayedLevelId = catalog.ByIndex(0).Id;
            Assert.AreEqual(catalog.ByIndex(1).Id, StageProgression.ResumeStage(catalog, save).Id);
        }

        [Test]
        public void Resume_DevelopmentUnlockAll_DoesNotSkipFtueForTestCheckpoint()
        {
            var catalog = RuntimeCatalog();
            var save = new SaveData { LastPlayedLevelId = catalog.ByCode("LEVEL-23").Id, DebugUnlockAll = true };
            bool previous = StageProgression.DebugUnlockAllowed;
            try
            {
                StageProgression.DebugUnlockAllowed = true;
                Assert.AreEqual("FTUE-1", StageProgression.ResumeStage(catalog, save).Code);
                save.RecordWin(catalog.ByCode("LEVEL-22").Id, 10, save.LastPlayedLevelId);
                Assert.AreEqual("FTUE-1", StageProgression.ResumeStage(catalog, save).Code, "clearing a debug-selected level does not complete skipped tutorials");
                Assert.IsTrue(StageProgression.IsUnlocked(catalog, save, save.LastPlayedLevelId), "explicit developer level selection stays available");
            }
            finally { StageProgression.DebugUnlockAllowed = previous; }
        }

        [Test]
        public void Resume_AllStagesComplete_UsesLastStage()
        {
            var catalog = RuntimeCatalog(); var save = new SaveData();
            foreach (var stage in catalog.Stages) StageProgression.RecordWin(catalog, save, stage, 10, false);
            Assert.AreEqual(catalog.ByIndex(catalog.Count - 1).Id, StageProgression.ResumeStage(catalog, save).Id);
        }

        [Test]
        public void Stars_FollowParRule()
        {
            Assert.AreEqual(3, ShadowSteps.Data.Progression.Stars(8, 8));
            Assert.AreEqual(2, ShadowSteps.Data.Progression.Stars(10, 8));
            Assert.AreEqual(1, ShadowSteps.Data.Progression.Stars(11, 8));
            Assert.AreEqual(1, ShadowSteps.Data.Progression.Stars(5, 0), "unknown par gives one star");
        }

        [Test]
        public void ParTable_ReadsBothValidationFiles()
        {
            var table = new ShadowSteps.Data.ParTable();
            table.AddFromValidationJson(System.IO.File.ReadAllText(TestPaths.PackagedValidationPath));
            table.AddFromValidationJson(System.IO.File.ReadAllText(System.IO.Path.Combine(TestPaths.ProjectRoot, "Assets/StreamingAssets/ShadowSteps/validation_tutorial_v2.json")));
            Assert.AreEqual(25, table.Count);
            Assert.AreEqual(8, table.ParFor(1));
            Assert.AreEqual(12, table.ParFor(14));
            Assert.AreEqual(12, table.ParFor(105));
            Assert.AreEqual(0, table.ParFor(999));
        }

        [Test]
        public void Unlocking_FollowsCatalogOrder_NotIds()
        {
            var tutorial = LevelPackLoader.Parse(System.IO.File.ReadAllText(System.IO.Path.Combine(TestPaths.ProjectRoot, "Assets/StreamingAssets/ShadowSteps/levels_tutorial_v2.json")));
            var merged = new System.Collections.Generic.List<ShadowSteps.Core.LevelDefinition>(tutorial.Levels);
            merged.AddRange(TestPaths.LoadPackagedPack().Levels);
            var catalog = new ShadowSteps.Data.LevelCatalog(new LevelPack(2, "", merged, new System.Collections.Generic.List<string>()));
            var save = new SaveData();
            Assert.IsTrue(ShadowSteps.Data.Progression.IsUnlocked(catalog, save, 101), "first catalog entry is open");
            Assert.IsFalse(ShadowSteps.Data.Progression.IsUnlocked(catalog, save, 102));
            Assert.IsFalse(ShadowSteps.Data.Progression.IsUnlocked(catalog, save, 1), "legacy level 1 comes after the tutorials");
            Assert.AreEqual(101, ShadowSteps.Data.Progression.FirstUnfinished(catalog, save).Id);
            Assert.IsTrue(ShadowSteps.Data.Progression.RecordWin(catalog, save, 101, 9));
            Assert.IsTrue(ShadowSteps.Data.Progression.IsUnlocked(catalog, save, 102));
            for (int id = 102; id <= 105; id++) ShadowSteps.Data.Progression.RecordWin(catalog, save, id, 20);
            Assert.IsTrue(ShadowSteps.Data.Progression.IsUnlocked(catalog, save, 1));
            Assert.AreEqual(1, ShadowSteps.Data.Progression.FirstUnfinished(catalog, save).Id);
            Assert.IsFalse(ShadowSteps.Data.Progression.RecordWin(catalog, save, 101, 12), "worse turns are not a new best");
            Assert.AreEqual(9, save.GetResult(101).BestTurns);
        }
    }
}
