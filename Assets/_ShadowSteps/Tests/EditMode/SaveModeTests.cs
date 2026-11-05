using NUnit.Framework;
using ShadowSteps.Data;
using ShadowSteps.V3;

namespace ShadowSteps.Tests.EditMode
{
    /// <summary>
    /// Per-mode records and the remembered mode (D-040): legacy saves load as Stealth without change, a Knockout win never
    /// overwrites a Stealth best, both tables and the selection round-trip, unlocks and completion are shared.
    /// </summary>
    public class SaveModeTests
    {
        private static StageCatalog Catalog() => new StageCatalog(StageJson.Parse(System.IO.File.ReadAllText(
            System.IO.Path.Combine(TestPaths.ProjectRoot, "Assets/StreamingAssets/ShadowSteps/levels_v4.json"))).Stages);

        [Test]
        public void LegacyVersion1Save_LoadsAsStealthResults_WithStealthSelected()
        {
            const string legacy = "{\"version\":1,\"highest_unlocked_level_id\":4,\"last_played_level_id\":4,\"results\":[{\"level_id\":1,\"completed\":true,\"best_turns\":3,\"best_stars\":3,\"ghost\":false},{\"level_id\":2,\"completed\":true,\"best_turns\":9,\"best_stars\":2,\"ghost\":true}]}";
            var d = SaveSerializer.FromJson(legacy);
            Assert.AreEqual(StageModeUtil.Default, d.SelectedMode, "no saved selection: the shipped default (Knockout, D-043)");
            Assert.AreEqual(4, d.HighestUnlockedLevelId);
            Assert.AreEqual(3, d.GetResult(1).BestTurns); Assert.AreEqual(9, d.GetResult(2, StageMode.Stealth).BestTurns); Assert.IsTrue(d.GetResult(2).Ghost);
            Assert.AreEqual(LevelResult.StealthRulesVersion, d.GetResult(1).RulesVersion);
            Assert.IsNull(d.GetResult(1, StageMode.Knockout)); Assert.AreEqual(0, d.KnockoutResults.Count, "nothing is ever relabelled as a Knockout result");
            Assert.IsTrue(d.IsCompleted(2)); Assert.IsFalse(d.IsCompleted(2, StageMode.Knockout));
            Assert.AreEqual(2, d.CompletedCount());
        }

        [Test]
        public void KnockoutWin_NeverOverwritesTheStealthBest_AndRoundTripsSeparately()
        {
            var d = new SaveData();
            Assert.IsTrue(d.RecordWin(5, 12, 2, false, 6, StageMode.Stealth));
            Assert.IsTrue(d.RecordWin(5, 8, 3, true, 6, StageMode.Knockout), "first knockout record is a new best in its own table");
            Assert.AreEqual(12, d.GetResult(5, StageMode.Stealth).BestTurns); Assert.AreEqual(2, d.GetResult(5).BestStars); Assert.IsFalse(d.GetResult(5).Ghost);
            Assert.AreEqual(8, d.GetResult(5, StageMode.Knockout).BestTurns); Assert.AreEqual(StageModeUtil.RulesVersion, d.GetResult(5, StageMode.Knockout).RulesVersion);
            Assert.IsFalse(d.RecordWin(5, 10, 3, false, 6, StageMode.Knockout), "10 > 8 is not a new knockout best");
            Assert.AreEqual(1, d.CompletedCount(), "one level, completed in both modes");
            d.SelectedMode = StageMode.Knockout;
            var loaded = SaveSerializer.FromJson(SaveSerializer.ToJson(d));
            Assert.AreEqual(StageMode.Knockout, loaded.SelectedMode);
            Assert.AreEqual(12, loaded.GetResult(5, StageMode.Stealth).BestTurns); Assert.AreEqual(8, loaded.GetResult(5, StageMode.Knockout).BestTurns);
            Assert.IsTrue(loaded.GetResult(5, StageMode.Knockout).Ghost); Assert.IsFalse(loaded.GetResult(5, StageMode.Stealth).Ghost, "Ghost is per mode");
            Assert.AreEqual(SaveData.CurrentVersion, loaded.Version);
        }

        [Test]
        public void OldKnockoutRulesRecord_IsKeptAsLegacy_NeverComparedToNewPars()
        {
            const string json = "{\"version\":2,\"results\":[{\"level_id\":31,\"mode\":\"knockout\",\"rules_version\":\"knockout-move-v2\",\"completed\":true,\"best_turns\":5,\"best_stars\":3},{\"level_id\":31,\"mode\":\"stealth\",\"completed\":true,\"best_turns\":20}]}";
            var d = SaveSerializer.FromJson(json);
            Assert.IsNull(d.GetResult(31, StageMode.Knockout), "a record earned under the WAIT-era rules is not a current-rules best");
            Assert.IsTrue(d.KnockoutLegacyResults.ContainsKey(31), "…but it is preserved, labelled with its own rules revision"); Assert.AreEqual("knockout-move-v2", d.KnockoutLegacyResults[31].RulesVersion);
            Assert.IsTrue(d.IsCompleted(31), "progress is never lost"); Assert.AreEqual(5, d.DisplayResult(31).BestTurns, "shown as the best while no current-rules record exists");
            Assert.AreEqual(20, d.GetResult(31, StageMode.Stealth).BestTurns);
            // A new current-rules win is a fresh best (not compared with the 5), the legacy record stays.
            Assert.IsTrue(d.RecordWin(31, 9, 3, false, 32, StageMode.Knockout));
            Assert.AreEqual(9, d.GetResult(31, StageMode.Knockout).BestTurns); Assert.AreEqual(StageModeUtil.RulesVersion, d.GetResult(31, StageMode.Knockout).RulesVersion);
            Assert.AreEqual(5, d.KnockoutLegacyResults[31].BestTurns); Assert.AreEqual(9, d.DisplayResult(31).BestTurns);
            var loaded = SaveSerializer.FromJson(SaveSerializer.ToJson(d));
            Assert.AreEqual(9, loaded.GetResult(31, StageMode.Knockout).BestTurns); Assert.AreEqual(5, loaded.KnockoutLegacyResults[31].BestTurns); Assert.AreEqual(1, loaded.CompletedCount());
            // An in-memory record with a stale rules version is archived the moment a current-rules win lands.
            var e = new SaveData(); e.KnockoutResults[7] = new LevelResult(true, 4, 3, false) { RulesVersion = "knockout-move-v2" };
            Assert.IsTrue(e.RecordWin(7, 6, 2, false, 8, StageMode.Knockout)); Assert.AreEqual(6, e.GetResult(7, StageMode.Knockout).BestTurns); Assert.AreEqual(4, e.KnockoutLegacyResults[7].BestTurns);
        }

        [Test]
        public void Progression_UnlocksAndResumeAreSharedAcrossModes_StarsUseTheModePar()
        {
            var catalog = Catalog(); var save = new SaveData(); var first = catalog.ByIndex(0); var second = catalog.ByIndex(1);
            Assert.IsTrue(StageProgression.RecordWin(catalog, save, first, first.Par, false, StageMode.Knockout));
            Assert.IsTrue(StageProgression.IsUnlocked(catalog, save, second.Id), "a Knockout win unlocks the next level like a Stealth win");
            Assert.AreEqual(second.Id, StageProgression.ResumeStage(catalog, save).Id);
            Assert.IsNull(save.GetResult(first.Id, StageMode.Stealth)); Assert.IsTrue(save.IsCompleted(first.Id));
            Assert.AreEqual(3, save.GetResult(first.Id, StageMode.Knockout).BestStars);
            // Stars: Knockout uses its own par when stored, otherwise the Stealth par (which every Knockout run can also reach).
            var withKo = new StageBuilder { Id = 999, Code = "T", Par = 10, ThreeStarActions = 11, TwoStarActions = 13, KnockoutPar = 6, KnockoutThreeStarActions = 7, KnockoutTwoStarActions = 8 }.FromRows(new[] { "...." }); withKo.Start = new ShadowSteps.Core.GridCoord(0, 0); withKo.Exit = new ShadowSteps.Core.GridCoord(3, 0);
            var stage = withKo.Build();
            Assert.AreEqual(3, StageProgression.Stars(stage, 10, StageMode.Stealth)); Assert.AreEqual(1, StageProgression.Stars(stage, 10, StageMode.Knockout));
            Assert.AreEqual(6, stage.ParFor(StageMode.Knockout)); Assert.AreEqual(10, stage.ParFor(StageMode.Stealth));
            var without = new StageBuilder { Id = 998, Code = "U", Par = 10, ThreeStarActions = 11, TwoStarActions = 13 }.FromRows(new[] { "...." }); without.Start = new ShadowSteps.Core.GridCoord(0, 0); without.Exit = new ShadowSteps.Core.GridCoord(3, 0);
            Assert.AreEqual(10, without.Build().ParFor(StageMode.Knockout), "fallback to the Stealth par");
        }

        [Test]
        public void KnockoutScoring_RoundTripsThroughTheStagePack()
        {
            var b = new StageBuilder { Id = 7, Code = "RT", Par = 9, ThreeStarActions = 10, TwoStarActions = 12, KnockoutPar = 7, KnockoutThreeStarActions = 8, KnockoutTwoStarActions = 10 }.FromRows(new[] { "....", "...." });
            b.Start = new ShadowSteps.Core.GridCoord(0, 0); b.Exit = new ShadowSteps.Core.GridCoord(3, 1);
            var pack = StageJson.Parse(StageJson.ToJson(new[] { b.Build() }));
            Assert.AreEqual(0, pack.Warnings.Count, string.Join("; ", pack.Warnings));
            var s = pack.Stages[0];
            Assert.IsTrue(s.HasKnockoutScoring); Assert.AreEqual(7, s.KnockoutPar); Assert.AreEqual(8, s.KnockoutThreeStarActions); Assert.AreEqual(10, s.KnockoutTwoStarActions);
            var plain = StageJson.Parse(StageJson.ToJson(new[] { new StageBuilder { Id = 8, Code = "P", Par = 3 }.FromRows(new[] { "...." }).Build() })).Stages[0];
            Assert.IsFalse(plain.HasKnockoutScoring, "absent fields stay -1");
        }
    }
}
