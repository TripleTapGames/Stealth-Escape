using NUnit.Framework;
using ShadowSteps.Data;

namespace ShadowSteps.Tests.EditMode
{
    public class ProgressionTests
    {
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
            Assert.AreEqual(5, table.Count, "only levels solvable under the V2 detection rule carry a par (1, 4, 12, 14, 101)");
            Assert.AreEqual(8, table.ParFor(1));
            Assert.AreEqual(11, table.ParFor(14));
            Assert.AreEqual(0, table.ParFor(105), "unsolvable under beforeOrAfterGuardMove: no par");
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
