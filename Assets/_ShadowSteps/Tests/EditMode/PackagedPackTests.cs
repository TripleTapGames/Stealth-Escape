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
        public void ValidationV2_MovesEqualReferenceMoves_ForAll20()
        {
            // The migrated topology is movement-equivalent to V1, so shortest lengths must match the V1 reference
            // (level 18's redesign happens to keep the same length; this test documents that measured fact).
            var v2 = JsonAccess.AsArray(MiniJson.Parse(File.ReadAllText(TestPaths.PackagedValidationPath)), "validation_v2");
            var v1 = TestPaths.LoadReferenceSolutions();
            for (int i = 0; i < 20; i++)
                Assert.AreEqual(v1[i].Moves, JsonAccess.AsInt(((JsonObject)v2[i])["moves"], "moves"), "level " + (i + 1));
        }
    }
}
