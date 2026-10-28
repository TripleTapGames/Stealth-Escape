using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using ShadowSteps.Core;
using ShadowSteps.Data;

namespace ShadowSteps.Tests.EditMode
{
    /// <summary>
    /// V2 M5 gate: the shipped schema-2 pack is the migrated reference set (level 18 redesigned), every level has a
    /// fresh C# solver entry in validation_v2.json, and solvable traces replay to wins with the exact move count.
    /// Under rules.detection = beforeOrAfterGuardMove (D-025) most legacy levels have no solution; that set is asserted
    /// here so a redesign changes it deliberately (Docs/KNOWN_ISSUES.md #18).
    /// </summary>
    public class PackagedPackTests
    {
        [Test]
        public void PackagedLevels_MatchMigratedReference_ExceptDocumentedLevel18Change()
        {
            var packaged = TestPaths.LoadPackagedPack();
            var reference = TestPaths.LoadPack();
            foreach (var r in reference.Levels)
            {
                var p = packaged.FindById(r.Id);
                Assert.IsNotNull(p, "packaged level " + r.Id);
                Assert.AreEqual(r.Name, p.Name);
                Assert.AreEqual(r.Width, p.Width); Assert.AreEqual(r.Height, p.Height);
                Assert.AreEqual(r.Start, p.Start); Assert.AreEqual(r.Exit, p.Exit);
                Assert.AreEqual(r.Key, p.Key); Assert.AreEqual(r.Door, p.Door);
                CollectionAssert.AreEqual(r.Walls, p.Walls);
                Assert.AreEqual(r.Graph.EdgeCount, p.Graph.EdgeCount, "edges of level " + r.Id);
                foreach (var e in r.Graph.Edges) Assert.IsTrue(p.Graph.TryGetEdge(e.From, e.To, out var t) && t == e.Type, "edge " + e + " in level " + r.Id);
                Assert.AreEqual(r.Guards.Count, p.Guards.Count);
                for (int g = 0; g < r.Guards.Count; g++)
                {
                    bool isLevel18PatrolChange = r.Id == 18 && g == 0;
                    if (isLevel18PatrolChange)
                    {
                        CollectionAssert.AreEqual(new[] { new GridCoord(3, 1), new GridCoord(2, 1), new GridCoord(1, 1), new GridCoord(0, 1) }, p.Guards[g].Route);
                        continue;
                    }
                    CollectionAssert.AreEqual(r.Guards[g].Route, p.Guards[g].Route, "route of guard " + g + " in level " + r.Id);
                    Assert.AreEqual(r.Guards[g].Mode, p.Guards[g].Mode);
                    Assert.AreEqual(r.Guards[g].Facing, p.Guards[g].Facing);
                    Assert.AreEqual(r.Guards[g].Range, p.Guards[g].Range);
                    Assert.AreEqual(r.Guards[g].Rotation, p.Guards[g].Rotation);
                }
                Assert.AreEqual(r.SmokeCharges, p.SmokeCharges); Assert.AreEqual(r.EmpCharges, p.EmpCharges);
            }
        }

        [Test]
        public void PackagedLevel18_HasNoGuardOverlap_AndKeepsReferenceSolution()
        {
            var level = TestPaths.LoadPackagedPack().FindById(18);
            var report = LevelValidator.Validate(level, new ValidationOptions { GuardOverlapIsErrorForLegacy = true });
            Assert.IsTrue(report.IsValid, report.ToString());
            // The redesign was proven under the V1 detection rule (it kept the 14-move reference solution). Under the V2
            // rule level 18 is unsolvable like most legacy content (KNOWN_ISSUES #18), so replay with V1 semantics here.
            var referenceSolution = TestPaths.LoadReferenceSolutions()[17].Actions;
            var replay = GameSession.Replay(level.With(detection: DetectionRule.AfterGuardMove), referenceSolution);
            Assert.IsTrue(replay.Won, replay.Trace());
            Assert.AreEqual(14, replay.ValidTurns);
            Assert.IsFalse(Solver.Solve(level).Found, "level 18 is recorded as unsolvable under the V2 rule");
            StringAssert.Contains("row 1", string.Join("\n", level.ConversionNotes));
        }

        [Test]
        public void PackagedPack_HasNoGuardOverlapsAnywhere()
        {
            var report = LevelValidator.ValidatePack(TestPaths.LoadPackagedPack().Levels, new ValidationOptions { GuardOverlapIsErrorForLegacy = true });
            Assert.IsTrue(report.IsValid, report.ToString());
        }

        /// <summary>Measured with the C# solver under beforeOrAfterGuardMove; every other shipped level is unsolvable.</summary>
        public static readonly HashSet<int> SolvableUnderV2Detection = new HashSet<int> { 1, 4, 12, 14 };

        [Test]
        public void ValidationV2_ReplaysToWins_AndMatchesCurrentSolver()
        {
            var pack = TestPaths.LoadPackagedPack();
            var entries = JsonAccess.AsArray(MiniJson.Parse(File.ReadAllText(TestPaths.PackagedValidationPath)), "validation_v2");
            Assert.AreEqual(20, entries.Count);
            var solvable = new HashSet<int>();
            foreach (JsonObject e in entries)
            {
                int id = JsonAccess.AsInt(e["level"], "level");
                var level = pack.FindById(id);
                if (JsonAccess.AsInt(e["moves"], "moves") < 0)
                {
                    Assert.IsFalse(Solver.Solve(level).Found, "level " + id + " is recorded as unsolvable but the solver found a path; re-export validation_v2.json");
                    continue;
                }
                solvable.Add(id);
                var tokens = new List<string>();
                foreach (var t in (List<object>)e["actions"]) tokens.Add((string)t);
                var actions = GameActionUtil.ParseSequence(tokens);
                var replay = GameSession.Replay(level, actions);
                Assert.IsTrue(replay.Won, "level " + id + "\n" + replay.Trace());
                Assert.AreEqual(JsonAccess.AsInt(e["moves"], "moves"), replay.ValidTurns, "moves for level " + id);
                var solved = Solver.Solve(level);
                Assert.AreEqual(GameActionUtil.ToTokenString(actions), solved.ActionTokens, "solver path drifted for level " + id);
                Assert.AreEqual(JsonAccess.AsInt(e["states_checked"], "states_checked"), solved.StatesExplored, "explored states for level " + id);
                Assert.AreEqual(JsonAccess.AsBool(e["key_required"], "key_required"), LevelMetricsAnalyzer.Analyze(level).KeyRequired, "key requirement for level " + id);
            }
            CollectionAssert.AreEquivalent(SolvableUnderV2Detection, solvable, "levels solvable under the V2 detection rule");
        }

        [Test]
        public void ValidationV2_MovesNeverBelowReferenceMoves_WhereSolvable()
        {
            // The migrated topology is movement-equivalent to V1 and the V2 detection rule is never more lenient than V1
            // (DetectionRuleTests), so a V2 shortest path is at least as long as the V1 reference path.
            var v2 = JsonAccess.AsArray(MiniJson.Parse(File.ReadAllText(TestPaths.PackagedValidationPath)), "validation_v2");
            var v1 = TestPaths.LoadReferenceSolutions();
            for (int i = 0; i < 20; i++)
            {
                int moves = JsonAccess.AsInt(((JsonObject)v2[i])["moves"], "moves");
                if (moves >= 0) Assert.GreaterOrEqual(moves, v1[i].Moves, "level " + (i + 1));
            }
        }

        [Test]
        public void PackagedPack_MechanicNecessity_IsMeasuredUnderV2Detection()
        {
            // Measured with constrained solves under rules.detection = beforeOrAfterGuardMove (Docs/KNOWN_ISSUES.md #2).
            var expectedWait = new HashSet<int>();
            var expectedSmoke = new HashSet<int> { 14 };
            var expectedEmp = new HashSet<int>();
            var expectedKey = new HashSet<int> { 9, 10, 13, 16, 19, 20 };
            var wait = new HashSet<int>(); var smoke = new HashSet<int>(); var emp = new HashSet<int>(); var key = new HashSet<int>();
            foreach (var level in TestPaths.LoadPackagedPack().Levels)
            {
                var m = LevelMetricsAnalyzer.Analyze(level);
                Assert.AreEqual(SolvableUnderV2Detection.Contains(level.Id), m.ShortestWinTurns > 0, "solvability of level " + level.Id + ": " + m);
                if (m.WaitRequired) wait.Add(level.Id);
                if (m.SmokeRequired) smoke.Add(level.Id);
                if (m.EmpRequired) emp.Add(level.Id);
                if (m.KeyRequired) key.Add(level.Id);
            }
            CollectionAssert.AreEquivalent(expectedWait, wait, "WAIT required");
            CollectionAssert.AreEquivalent(expectedSmoke, smoke, "smoke required");
            CollectionAssert.AreEquivalent(expectedEmp, emp, "EMP required");
            CollectionAssert.AreEquivalent(expectedKey, key, "key required");
        }
    }
}
