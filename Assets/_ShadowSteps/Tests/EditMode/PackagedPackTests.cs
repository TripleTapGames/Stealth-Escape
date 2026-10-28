using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using ShadowSteps.Core;
using ShadowSteps.Data;

namespace ShadowSteps.Tests.EditMode
{
    /// <summary>
    /// V2 M5 gate: the shipped schema-2 pack is the migrated reference set (level 18 redesigned), every level has a
    /// fresh C# solver trace in validation_v2.json, and those traces replay to wins with the exact move count.
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
            var referenceSolution = TestPaths.LoadReferenceSolutions()[17].Actions;
            var replay = GameSession.Replay(level, referenceSolution);
            Assert.IsTrue(replay.Won, replay.Trace());
            Assert.AreEqual(14, replay.ValidTurns);
            StringAssert.Contains("row 1", string.Join("\n", level.ConversionNotes));
        }

        [Test]
        public void PackagedPack_HasNoGuardOverlapsAnywhere()
        {
            var report = LevelValidator.ValidatePack(TestPaths.LoadPackagedPack().Levels, new ValidationOptions { GuardOverlapIsErrorForLegacy = true });
            Assert.IsTrue(report.IsValid, report.ToString());
        }

        [Test]
        public void ValidationV2_ReplaysToWins_AndMatchesCurrentSolver()
        {
            var pack = TestPaths.LoadPackagedPack();
            var entries = JsonAccess.AsArray(MiniJson.Parse(File.ReadAllText(TestPaths.PackagedValidationPath)), "validation_v2");
            Assert.AreEqual(20, entries.Count);
            foreach (JsonObject e in entries)
            {
                int id = JsonAccess.AsInt(e["level"], "level");
                var level = pack.FindById(id);
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
        }

        [Test]
        public void ValidationV2_MovesNeverExceedReferenceMoves_ForAll20()
        {
            // The migrated topology is movement-equivalent to V1 and the V2 detection rule is never stricter than V1
            // (DetectionRuleTests), so V2 shortest lengths are at most the V1 reference lengths.
            var v2 = JsonAccess.AsArray(MiniJson.Parse(File.ReadAllText(TestPaths.PackagedValidationPath)), "validation_v2");
            var v1 = TestPaths.LoadReferenceSolutions();
            for (int i = 0; i < 20; i++)
                Assert.LessOrEqual(JsonAccess.AsInt(((JsonObject)v2[i])["moves"], "moves"), v1[i].Moves, "level " + (i + 1));
        }

        [Test]
        public void PackagedPack_MechanicNecessity_IsMeasuredUnderV2Detection()
        {
            // Measured with constrained solves under rules.detection = beforeAndAfterGuardMove (Docs/KNOWN_ISSUES.md #2).
            var expectedWait = new HashSet<int> { 17 };
            var expectedSmoke = new HashSet<int> { 17, 19 };
            var expectedEmp = new HashSet<int>();
            var expectedKey = new HashSet<int> { 9, 10, 13, 16, 19, 20 };
            var wait = new HashSet<int>(); var smoke = new HashSet<int>(); var emp = new HashSet<int>(); var key = new HashSet<int>();
            foreach (var level in TestPaths.LoadPackagedPack().Levels)
            {
                var m = LevelMetricsAnalyzer.Analyze(level);
                Assert.IsTrue(m.ShortestWinTurns > 0, "level " + level.Id + " unsolvable");
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
