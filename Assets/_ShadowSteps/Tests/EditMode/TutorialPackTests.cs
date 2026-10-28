using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using ShadowSteps.Core;
using ShadowSteps.Data;

namespace ShadowSteps.Tests.EditMode
{
    /// <summary>
    /// V2 gate D: the five authored tutorial levels load and validate; exported traces replay to wins. Under the V2 detection
    /// rule (D-025) only 101 is solvable; the others are recorded as unsolvable until redesigned (Docs/KNOWN_ISSUES.md #18).
    /// </summary>
    public class TutorialPackTests
    {
        private const string PackRelative = "Assets/StreamingAssets/ShadowSteps/levels_tutorial_v2.json";
        private const string ValidationRelative = "Assets/StreamingAssets/ShadowSteps/validation_tutorial_v2.json";

        private static LevelPack Pack() => LevelPackLoader.ParseAndValidate(File.ReadAllText(Path.Combine(TestPaths.ProjectRoot, PackRelative)), out _);

        private static List<JsonObject> Validation()
        {
            var arr = JsonAccess.AsArray(MiniJson.Parse(File.ReadAllText(Path.Combine(TestPaths.ProjectRoot, ValidationRelative))), "v");
            var list = new List<JsonObject>();
            foreach (var o in arr) list.Add((JsonObject)o);
            return list;
        }

        [Test]
        public void Pack_IsSchema2_WithFiveLevels_NoWarnings()
        {
            var pack = Pack();
            Assert.AreEqual(2, pack.FormatVersion);
            Assert.AreEqual(5, pack.Levels.Count);
            Assert.AreEqual(0, pack.Warnings.Count, string.Join("\n", pack.Warnings));
            var report = LevelValidator.ValidatePack(pack.Levels);
            Assert.AreEqual(0, report.WarningCount, report.ToString());
            CollectionAssert.AreEqual(new[] { 101, 102, 103, 104, 105 }, new List<LevelDefinition>(pack.Levels).ConvertAll(l => l.Id));
        }

        [Test]
        public void ExportedTraces_ReplayToWins_WithExactMoves()
        {
            var pack = Pack();
            var entries = Validation();
            Assert.AreEqual(5, entries.Count);
            foreach (var e in entries)
            {
                int id = JsonAccess.AsInt(e["level"], "level");
                var level = pack.FindById(id);
                Assert.IsNotNull(level, "level " + id);
                if (JsonAccess.AsInt(e["moves"], "moves") < 0)
                {
                    Assert.IsFalse(Solver.Solve(level).Found, "level " + id + " is recorded as unsolvable but the solver found a path; re-export validation_tutorial_v2.json");
                    continue;
                }
                var tokens = new List<string>();
                foreach (var t in (List<object>)e["actions"]) tokens.Add((string)t);
                var actions = GameActionUtil.ParseSequence(tokens);
                var replay = GameSession.Replay(level, actions);
                Assert.IsTrue(replay.Won, "level " + id + "\n" + replay.Trace());
                Assert.AreEqual(JsonAccess.AsInt(e["moves"], "moves"), replay.ValidTurns, "moves of level " + id);
                var solved = Solver.Solve(level);
                Assert.AreEqual(GameActionUtil.ToTokenString(actions), solved.ActionTokens, "exported trace must be the current solver's path for level " + id);
                Assert.AreEqual(JsonAccess.AsInt(e["states_checked"], "states_checked"), solved.StatesExplored, "explored states for level " + id);
            }
        }

        // Measured under rules.detection = beforeOrAfterGuardMove: only 101 (fixed guard, period 1) survives the rule change;
        // 102-105 were built around entering a cone as the guard turns away and need a redesign (Docs/KNOWN_ISSUES.md #18).
        [TestCase(101, 8, false, false, false, false, 5)]
        [TestCase(102, -1, false, false, false, false, 0)]
        [TestCase(103, -1, false, false, false, false, 0)]
        [TestCase(104, -1, false, false, false, true, 0)]
        [TestCase(105, -1, false, false, false, false, 0)]
        public void MechanicNecessity_IsMeasured(int id, int shortest, bool waitRequired, bool smokeRequired, bool empRequired, bool keyRequired, int shortestSolutions)
        {
            var m = LevelMetricsAnalyzer.Analyze(Pack().FindById(id));
            Assert.AreEqual(shortest, m.ShortestWinTurns, "shortest for " + id);
            Assert.AreEqual(waitRequired, m.WaitRequired, "WAIT required for " + id + ": " + m);
            Assert.AreEqual(smokeRequired, m.SmokeRequired, "smoke required for " + id + ": " + m);
            Assert.AreEqual(empRequired, m.EmpRequired, "EMP required for " + id + ": " + m);
            Assert.AreEqual(keyRequired, m.KeyRequired, "key required for " + id + ": " + m);
            Assert.AreEqual(shortestSolutions, m.ShortestSolutionCount, "shortest solution count for " + id + ": " + m);
        }

        [Test]
        public void TutorialLevels_FitTheGuardAndNodeBudgets()
        {
            foreach (var l in Pack().Levels)
            {
                Assert.IsTrue(l.Guards.Count >= 1 && l.Guards.Count <= 2, "guards in " + l.Id);
                int nodes = l.Graph.TraversableNodeCount;
                Assert.IsTrue(nodes >= 12 && nodes <= 42, "traversable nodes in " + l.Id + " = " + nodes);
            }
        }

        [Test]
        public void MergedCatalog_HasUniqueIds()
        {
            var tutorial = Pack();
            var legacy = TestPaths.LoadPack();
            var all = new List<LevelDefinition>(tutorial.Levels);
            all.AddRange(legacy.Levels);
            var report = LevelValidator.ValidatePack(all);
            Assert.IsTrue(report.IsValid, report.ToString());
            Assert.AreEqual(25, all.Count);
        }
    }
}
