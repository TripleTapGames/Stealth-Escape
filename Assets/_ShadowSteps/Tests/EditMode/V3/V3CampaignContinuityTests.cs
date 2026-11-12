using System.IO;
using NUnit.Framework;
using ShadowSteps.Data;
using ShadowSteps.V3;

namespace ShadowSteps.Tests.EditMode.V3
{
    /// <summary>Levels 26–30 (ids 31–35) extend the campaign: order, unlocking and the end of the campaign.</summary>
    public class V3CampaignContinuityTests
    {
        private static StagePack Pack() => StageJson.Parse(File.ReadAllText(Path.Combine(TestPaths.ProjectRoot, V3PackParityTests.PackRelative)));

        [Test]
        public void Campaign_Has50Stages_Main25ProceedsToMain26_AndMain45IsLast()
        {
            var catalog = new StageCatalog(Pack().Stages);
            Assert.AreEqual(50, catalog.Count);
            for (int i = 0; i < 50; i++) Assert.AreEqual(i + 1, catalog.ByIndex(i).Id, "ids stay sequential");
            Assert.AreEqual("LEVEL-25", catalog.ById(30).Code); Assert.AreEqual("LEVEL-26", catalog.NextAfter(30).Code);
            for (int n = 26; n < 45; n++) Assert.AreEqual("LEVEL-" + (n + 1), catalog.NextAfter(catalog.ByCode("LEVEL-" + n).Id).Code);
            Assert.AreEqual("LEVEL-30", catalog.ById(35).Code); Assert.AreEqual("LEVEL-31", catalog.NextAfter(35).Code, "D-078: Garden Gambit follows Main 30");
            Assert.AreEqual("LEVEL-32", catalog.ById(37).Code); Assert.AreEqual("LEVEL-33", catalog.NextAfter(37).Code, "D-081: Crossed Blades follows Main 32");
            Assert.AreEqual("LEVEL-45", catalog.ById(50).Code); Assert.IsNull(catalog.NextAfter(50), "no stage after Main 45 (the flow returns to level select)");
            for (int n = 26; n <= 45; n++) Assert.AreEqual("Main", catalog.ByCode("LEVEL-" + n).Group);
        }

        [Test]
        public void NewStages_UnlockSequentially_FromAnExistingSave_WithoutBeingMarked()
        {
            var catalog = new StageCatalog(Pack().Stages);
            StageProgression.DebugUnlockAllowed = false;
            try
            {
                var save = new SaveData(); // fresh player
                Assert.IsFalse(StageProgression.IsUnlocked(catalog, save, 31), "Main 26 locked until Main 25 is completed");
                Assert.IsFalse(save.IsCompleted(31));
                for (int id = 1; id <= 30; id++) save.Results[id] = new LevelResult { Completed = true, BestTurns = 1, BestStars = 1 }; // a player who finished the old campaign
                Assert.IsTrue(StageProgression.IsUnlocked(catalog, save, 31), "Main 26 opens after Main 25");
                Assert.IsFalse(StageProgression.IsUnlocked(catalog, save, 32), "Main 27 still needs Main 26");
                Assert.AreEqual("LEVEL-26", StageProgression.FirstUnfinished(catalog, save).Code);
                for (int id = 31; id <= 34; id++) { save.Results[id] = new LevelResult { Completed = true, BestTurns = 1, BestStars = 1 }; Assert.IsTrue(StageProgression.IsUnlocked(catalog, save, id + 1)); }
            }
            finally { StageProgression.DebugUnlockAllowed = true; }
        }
    }
}
