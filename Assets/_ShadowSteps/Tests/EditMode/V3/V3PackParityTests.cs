using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using ShadowSteps.Core;
using ShadowSteps.Data;
using ShadowSteps.V3;

namespace ShadowSteps.Tests.EditMode.V3
{
    /// <summary>
    /// M4 gate: the 30 browser levels import, validate, and the C# solver reproduces every browser par and Ghost flag;
    /// the shipped schema-3 pack and its validation traces agree with the solver.
    /// </summary>
    public class V3PackParityTests
    {
        public const string BrowserRelative = "reference/v3_handover_2026-10-29/levels_v3_playtest_reference.json";
        public const string BrowserReportRelative = "reference/v3_handover_2026-10-29/playtest_validation_report.json";
        public const string PackRelative = "Assets/StreamingAssets/ShadowSteps/levels_v3.json";
        public const string ValidationRelative = "Assets/StreamingAssets/ShadowSteps/validation_v3.json";

        private static string P(string rel) => Path.Combine(TestPaths.ProjectRoot, rel);
        private static List<BrowserLevelImporter.Imported> _imported;
        private static List<BrowserLevelImporter.Imported> Imported() => _imported ?? (_imported = BrowserLevelImporter.Import(File.ReadAllText(P(BrowserRelative))));
        private static StagePack _pack;
        private static StagePack Pack() => _pack ?? (_pack = StageJson.Parse(File.ReadAllText(P(PackRelative))));

        [Test]
        public void BrowserExport_Imports30Stages_AllStructurallyValid()
        {
            var list = Imported();
            Assert.AreEqual(30, list.Count);
            for (int i = 0; i < 5; i++) Assert.AreEqual("FTUE-" + (i + 1), list[i].Stage.Code);
            for (int i = 5; i < 30; i++) Assert.AreEqual("LEVEL-" + (i - 4), list[i].Stage.Code);
            foreach (var im in list)
            {
                var r = StageValidator.Validate(im.Stage, new StageValidationOptions { RunSolver = false });
                Assert.IsTrue(r.IsValid, im.Stage.Code + "\n" + r);
            }
        }

        [Test]
        public void Solver_ReproducesEveryBrowserPar_AndGhostOffer()
        {
            var lines = new List<string>();
            foreach (var im in Imported())
            {
                var m = StageMetrics.Analyze(im.Stage);
                lines.Add(im.Stage.Code + " par " + m.Par + " (browser " + im.BrowserPar + ") ghost " + m.GhostOffered + " (browser " + im.BrowserGhost + ")");
                Assert.AreEqual(im.BrowserPar, m.Par, "par of " + im.Stage.Code);
                Assert.AreEqual(im.BrowserGhost, m.GhostOffered, "ghost offer of " + im.Stage.Code);
                var replay = StageReplay.Run(im.Stage, m.Solution);
                Assert.IsTrue(replay.Won, im.Stage.Code + "\n" + replay.Trace());
                Assert.AreEqual(m.Par, replay.CountedActions, "counted actions of " + im.Stage.Code);
            }
            TestContext.WriteLine(string.Join("\n", lines));
        }

        [Test]
        public void BrowserReport_OptimalLengths_MatchTheCSharpSolver()
        {
            var report = JsonAccess.AsObject(MiniJson.Parse(File.ReadAllText(P(BrowserReportRelative))), "report");
            var byCode = new Dictionary<string, int>();
            foreach (JsonObject e in JsonAccess.AsArray(report["levels"], "levels")) byCode[(string)e["id"]] = JsonAccess.AsInt(e["optimal"], "optimal");
            foreach (var s in Pack().Stages) Assert.AreEqual(byCode[s.Code], s.Par, s.Code);
        }

        [Test]
        public void ShippedPack_Has30Stages_WithSolverScoring_AndNoWarnings()
        {
            var pack = Pack();
            Assert.AreEqual(30, pack.Stages.Count);
            Assert.AreEqual(0, pack.Warnings.Count, string.Join("\n", pack.Warnings));
            for (int i = 0; i < 30; i++) Assert.AreEqual(i + 1, pack.Stages[i].Id);
            var report = StageValidator.ValidatePack(pack.Stages);
            Assert.IsTrue(report.IsValid, report.ToString());
            foreach (var s in pack.Stages)
            {
                Assert.AreEqual(StageScoring.ThreeStarThreshold(s.Par), s.ThreeStarActions, s.Code);
                Assert.AreEqual(StageScoring.TwoStarThreshold(s.Par), s.TwoStarActions, s.Code);
                if (s.GhostOffered) Assert.IsTrue(s.Cameras.Count > 0, s.Code + " offers Ghost without a camera");
            }
        }

        [Test]
        public void ShippedValidation_ReplaysToWins_AndMatchesTheSolver()
        {
            var pack = Pack();
            var entries = JsonAccess.AsArray(MiniJson.Parse(File.ReadAllText(P(ValidationRelative))), "validation_v3");
            Assert.AreEqual(30, entries.Count);
            foreach (JsonObject e in entries)
            {
                var stage = pack.FindByCode((string)e["code"]);
                Assert.IsNotNull(stage, (string)e["code"]);
                var tokens = new List<string>(); foreach (var t in (List<object>)e["actions"]) tokens.Add((string)t);
                var actions = StageAction.ParseSequence(tokens);
                var replay = StageReplay.Run(stage, actions);
                Assert.IsTrue(replay.Won, stage.Code + "\n" + replay.Trace());
                Assert.AreEqual(JsonAccess.AsInt(e["par"], "par"), replay.CountedActions, stage.Code);
                Assert.AreEqual(stage.Par, JsonAccess.AsInt(e["par"], "par"), stage.Code);
                var solved = StageSolver.Solve(stage);
                Assert.AreEqual(string.Join(" ", tokens), solved.Tokens, "solver path drifted for " + stage.Code);
                Assert.AreEqual(JsonAccess.AsBool(e["ghost_offered"], "ghost_offered"), stage.GhostOffered, stage.Code);
            }
        }

        [Test]
        public void MandatoryGadgetLevels_AreProvenByConstrainedSolves()
        {
            var pack = Pack();
            var smokeRequired = new HashSet<string>(); var empRequired = new HashSet<string>(); var forcedAlarm = new HashSet<string>();
            foreach (var s in pack.Stages)
            {
                var m = StageMetrics.Analyze(s);
                if (m.SmokeRequired) smokeRequired.Add(s.Code);
                if (m.EmpRequired) empRequired.Add(s.Code);
                if (m.ForcedAlarm) forcedAlarm.Add(s.Code);
            }
            CollectionAssert.AreEquivalent(new[] { "LEVEL-21", "LEVEL-25" }, smokeRequired);
            CollectionAssert.AreEquivalent(new[] { "LEVEL-23", "LEVEL-25" }, empRequired);
            CollectionAssert.AreEquivalent(new[] { "LEVEL-17", "LEVEL-19", "LEVEL-23" }, forcedAlarm);
        }
    }
}
