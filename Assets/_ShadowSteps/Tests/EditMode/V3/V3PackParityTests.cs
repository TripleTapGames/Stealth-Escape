using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using ShadowSteps.Core;
using ShadowSteps.Data;
using ShadowSteps.V3;

namespace ShadowSteps.Tests.EditMode.V3
{
    /// <summary>
    /// M4/V4 gate: the 30 browser levels (V4 blocked-cover geometry) import, validate, and the C# solver reproduces every
    /// browser par and Ghost flag; the shipped schema-3 pack and its validation traces agree with the solver.
    /// </summary>
    public class V3PackParityTests
    {
        public const string BrowserRelative = "reference/v4_handover_2026-10-30/shadow_steps_levels_v4.json";
        public const string BrowserReportRelative = "reference/v4_handover_2026-10-30/v4_validation_report.json";
        public const string PackRelative = "Assets/StreamingAssets/ShadowSteps/levels_v4.json";
        public const string ValidationRelative = "Assets/StreamingAssets/ShadowSteps/validation_v4.json";

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

        /// <summary>Camera stages left browser parity on 2026-11-03 (the camera footprint now starts at the node in front of the lens; LEVEL-18/19 were redesigned), so the browser is the authority only for stages without cameras.</summary>
        [Test]
        public void Solver_ReproducesEveryBrowserPar_AndGhostOffer_ForStagesWithoutCameras()
        {
            var lines = new List<string>(); int checkedCount = 0;
            foreach (var im in Imported())
            {
                if (im.Stage.Cameras.Count > 0) continue;
                var m = StageMetrics.Analyze(im.Stage);
                lines.Add(im.Stage.Code + " par " + m.Par + " (browser " + im.BrowserPar + ") ghost " + m.GhostOffered + " (browser " + im.BrowserGhost + ")");
                Assert.AreEqual(im.BrowserPar, m.Par, "par of " + im.Stage.Code);
                Assert.AreEqual(im.BrowserGhost, m.GhostOffered, "ghost offer of " + im.Stage.Code);
                var replay = StageReplay.Run(im.Stage, m.Solution);
                Assert.IsTrue(replay.Won, im.Stage.Code + "\n" + replay.Trace());
                Assert.AreEqual(m.Par, replay.CountedActions, "counted actions of " + im.Stage.Code);
                checkedCount++;
            }
            Assert.AreEqual(22, checkedCount, "22 stages without cameras");
            TestContext.WriteLine(string.Join("\n", lines));
        }

        [Test]
        public void BrowserReport_OptimalLengths_MatchTheCSharpSolver()
        {
            var report = JsonAccess.AsObject(MiniJson.Parse(File.ReadAllText(P(BrowserReportRelative))), "report");
            var byLabel = new Dictionary<string, int>();
            foreach (JsonObject e in JsonAccess.AsArray(report["levels"], "levels")) byLabel[(string)e["label"]] = JsonAccess.AsInt(e["optimalActions"], "optimalActions");
            foreach (var s in Pack().Stages)
            {
                if (s.Cameras.Count > 0) continue; // camera stages: C# solver authority (new footprint), see validation_v4.json
                string label = s.Code.Replace("FTUE-", "FTUE ").Replace("LEVEL-", "Level ");
                if (!byLabel.ContainsKey(label)) continue; // Levels 26-30 are not in the browser report (ChatGPT kit, C# solver authority)
                Assert.AreEqual(byLabel[label], s.Par, s.Code);
            }
        }

        [Test]
        public void ShippedPack_Has35Stages_WithSolverScoring_AndNoWarnings()
        {
            var pack = Pack();
            Assert.AreEqual(35, pack.Stages.Count, "5 FTUE + 25 main + Levels 26-30 (ids 31-35)");
            Assert.AreEqual(0, pack.Warnings.Count, string.Join("\n", pack.Warnings));
            for (int i = 0; i < 35; i++) Assert.AreEqual(i + 1, pack.Stages[i].Id);
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
            var entries = JsonAccess.AsArray(MiniJson.Parse(File.ReadAllText(P(ValidationRelative))), "validation_v4");
            Assert.AreEqual(35, entries.Count);
            foreach (JsonObject e in entries)
            {
                var stage = pack.FindByCode((string)e["code"]);
                Assert.IsNotNull(stage, (string)e["code"]);
                var tokens = new List<string>(); foreach (var t in (List<object>)e["actions"]) tokens.Add((string)t);
                var actions = StageAction.ParseSequence(tokens);
                // The shipped validation is exported under the shipped rules (D-043/D-044: Knockout, no WAIT); the entry says which mode it was solved in.
                var mode = StageModeUtil.TryParse(JsonAccess.OptionalString(e, "mode", "stealth", "validation_v4"), out var m) ? m : StageMode.Stealth;
                Assert.AreEqual(StageModeUtil.Default, mode, stage.Code + ": validation exported for the shipped mode");
                Assert.AreEqual(0, stage.WaitLimit, stage.Code + ": waitLimit 0 (D-044)");
                foreach (var a in actions) Assert.AreNotEqual(StageActionKind.Wait, a.Kind, stage.Code + ": no WAIT in the shipped solution");
                var replay = StageReplay.Run(stage, actions, mode);
                Assert.IsTrue(replay.Won, stage.Code + "\n" + replay.Trace());
                Assert.AreEqual(JsonAccess.AsInt(e["par"], "par"), replay.CountedActions, stage.Code);
                Assert.AreEqual(stage.ParFor(mode), JsonAccess.AsInt(e["par"], "par"), stage.Code + ": stored par for the shipped mode");
                Assert.AreEqual(StageScoring.ThreeStarThreshold(stage.ParFor(mode)), stage.ThreeStarFor(mode), stage.Code); Assert.AreEqual(StageScoring.TwoStarThreshold(stage.ParFor(mode)), stage.TwoStarFor(mode), stage.Code);
                var solved = StageSolver.Solve(stage, new SolveOptions { Mode = mode, MaxWaits = 0 });
                Assert.IsTrue(solved.Found && !solved.HitLimit, stage.Code + ": solvable within limits");
                Assert.AreEqual(string.Join(" ", tokens), solved.Tokens, "solver path drifted for " + stage.Code);
                Assert.AreEqual(JsonAccess.AsBool(e["ghost_offered"], "ghost_offered"), stage.GhostOffered, stage.Code);
                if (stage.GhostOffered) { var ghost = StageSolver.Solve(stage, new SolveOptions { Mode = mode, MaxWaits = 0, AlarmFreeOnly = true }); Assert.IsTrue(ghost.Found, stage.Code + ": offered Ghost has an alarm-free route"); var gr = StageReplay.Run(stage, ghost.Actions, mode); Assert.IsTrue(gr.Won && !gr.Final.AlarmEver, stage.Code + ": Ghost route replays alarm-free"); }
            }
        }

        [Test]
        public void Level23_EMPTraining_IsSolvableAndStillRequiresEmp()
        {
            var stage = Pack().FindByCode("LEVEL-23");
            var metrics = StageMetrics.Analyze(stage);
            Assert.IsTrue(metrics.Solvable, "EMP training must have a winning route");
            Assert.IsTrue(metrics.EmpRequired, "Increasing alarm time must not allow skipping EMP");
            Assert.IsTrue(metrics.ForcedAlarm, "The player must still race the alarm after EMP expires");
            Assert.AreEqual(5, metrics.Par);
            var replay = StageReplay.Run(stage, StageAction.ParseSequence(new[] { "E", "EMP#0", "E", "E", "E", "E" }));
            Assert.IsTrue(replay.Won, replay.Trace());
            Assert.AreEqual(stage.Par, replay.CountedActions);
        }

        [Test]
        public void MandatoryGadgetLevels_AreProvenByConstrainedSolves()
        {
            var pack = Pack();
            var smokeRequired = new HashSet<string>(); var empRequired = new HashSet<string>(); var forcedAlarm = new HashSet<string>();
            foreach (var s in pack.Stages)
            {
                var m = StageMetrics.Analyze(s, new SolveOptions { Mode = StageModeUtil.Default, MaxWaits = 0 }); // shipped rules: Knockout, no WAIT (D-044)
                if (m.SmokeRequired) smokeRequired.Add(s.Code);
                if (m.EmpRequired) empRequired.Add(s.Code);
                if (m.ForcedAlarm) forcedAlarm.Add(s.Code);
            }
            // Under Knockout / no WAIT (validation_v4.json export, 2026-11-04): LEVEL-30 now needs its EMP as well (no WAIT to time the camera).
            CollectionAssert.AreEquivalent(new[] { "LEVEL-21", "LEVEL-25", "LEVEL-27" }, smokeRequired);
            CollectionAssert.AreEquivalent(new[] { "LEVEL-23", "LEVEL-25", "LEVEL-30" }, empRequired);
            CollectionAssert.AreEquivalent(new[] { "LEVEL-16", "LEVEL-17", "LEVEL-20", "LEVEL-23" }, forcedAlarm);
        }

        [Test]
        public void V4_ImportsExactly55BlockedNodesAcross25Stages_AndShipsThem()
        {
            int blocked = 0, stagesWithCover = 0;
            foreach (var im in Imported()) { blocked += im.Stage.BlockedCount; if (im.Stage.BlockedCount > 0) stagesWithCover++; }
            Assert.AreEqual(55, blocked); Assert.AreEqual(25, stagesWithCover);
            int shipped = 0;
            foreach (var s in Pack().Stages) shipped += s.BlockedCount;
            Assert.AreEqual(55 + 8, shipped, "shipped pack carries every obstacle: the 55 imported plus 8 in Levels 26-30");
            var ghost = new List<string>();
            foreach (var s in Pack().Stages) if (s.GhostOffered) ghost.Add(s.Code);
            CollectionAssert.AreEquivalent(new[] { "LEVEL-18", "LEVEL-19", "LEVEL-24", "LEVEL-25", "LEVEL-28", "LEVEL-29", "LEVEL-30" }, ghost); // LEVEL-24's flag corrected to true in the no-WAIT pack (D-044) // stored flags; LEVEL-24's stored flag is stale pending the cone decision
        }
    }
}
