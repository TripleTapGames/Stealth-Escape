using System.IO;
using NUnit.Framework;
using ShadowSteps.Core;
using ShadowSteps.Data;
using static ShadowSteps.Tests.EditMode.LevelBuilder;

namespace ShadowSteps.Tests.EditMode
{
    /// <summary>V2 M3: the editor's authoring model round-trips definitions exactly and applies the editing rules.</summary>
    public class LevelAuthoringModelTests
    {
        private static void AssertSameTopology(LevelDefinition a, LevelDefinition b)
        {
            Assert.AreEqual(a.Graph.NodeCount, b.Graph.NodeCount);
            foreach (var n in a.Graph.Nodes()) { Assert.IsTrue(b.Graph.TryGetNode(n, out var info)); a.Graph.TryGetNode(n, out var ai); Assert.AreEqual(ai, info, "node " + n); }
            Assert.AreEqual(a.Graph.EdgeCount, b.Graph.EdgeCount);
            foreach (var e in a.Graph.Edges) Assert.IsTrue(b.Graph.TryGetEdge(e.From, e.To, out var t) && t == e.Type, "edge " + e);
            CollectionAssert.AreEquivalent(a.Walls, b.Walls);
            Assert.AreEqual(a.Guards.Count, b.Guards.Count);
            for (int i = 0; i < a.Guards.Count; i++)
            {
                CollectionAssert.AreEqual(a.Guards[i].Route, b.Guards[i].Route);
                Assert.AreEqual(a.Guards[i].Mode, b.Guards[i].Mode);
                Assert.AreEqual(a.Guards[i].MoveEvery, b.Guards[i].MoveEvery);
                Assert.AreEqual(a.Guards[i].RotateEvery, b.Guards[i].RotateEvery);
            }
            Assert.AreEqual(a.Key, b.Key); Assert.AreEqual(a.Door, b.Door); Assert.AreEqual(a.Start, b.Start); Assert.AreEqual(a.Exit, b.Exit);
            Assert.AreEqual(a.SmokeCharges, b.SmokeCharges); Assert.AreEqual(a.EmpCharges, b.EmpCharges); Assert.AreEqual(a.NoWait, b.NoWait);
        }

        [Test]
        public void RoundTrip_LegacyAndTutorialLevels_PreserveTopologyAndSolutions()
        {
            var levels = new System.Collections.Generic.List<LevelDefinition>(TestPaths.LoadPack().Levels);
            levels.AddRange(LevelPackLoader.Parse(File.ReadAllText(Path.Combine(TestPaths.ProjectRoot, "Assets/StreamingAssets/ShadowSteps/levels_tutorial_v2.json"))).Levels);
            foreach (var level in levels)
            {
                var model = LevelAuthoringModel.FromDefinition(level);
                var back = model.ToDefinition();
                AssertSameTopology(level, back);
                Assert.AreEqual(Solver.Solve(level).ActionTokens, Solver.Solve(back).ActionTokens, "solution after round trip, level " + level.Id);
                // And through JSON as the editor exports it.
                var reloaded = LevelPackLoader.Parse(LevelPackWriter.ToJson(back)).Levels[0];
                AssertSameTopology(level, reloaded);
            }
        }

        [Test]
        public void CycleEdge_FollowsTheDocumentedCycle()
        {
            var m = new LevelAuthoringModel { Width = 2, Height = 1 };
            m.FillRectangle();
            var a = C(0, 0); var b = C(1, 0);
            m.CycleEdge(a, b);
            Assert.IsTrue(m.HasEdge(a, b, out var t1) && t1 == EdgeType.Open); Assert.IsTrue(m.HasEdge(b, a, out _));
            m.CycleEdge(a, b);
            Assert.IsTrue(m.HasEdge(a, b, out _)); Assert.IsFalse(m.HasEdge(b, a, out _), "one-way a->b");
            m.CycleEdge(a, b);
            Assert.IsTrue(m.HasEdge(a, b, out var t3) && t3 == EdgeType.Locked); Assert.IsTrue(m.HasEdge(b, a, out var t4) && t4 == EdgeType.Locked);
            m.CycleEdge(a, b);
            Assert.IsFalse(m.HasEdge(a, b, out _)); Assert.IsFalse(m.HasEdge(b, a, out _));
        }

        [Test]
        public void AutoEdges_SkipsWallsAndLocksDoor()
        {
            var m = new LevelAuthoringModel { Width = 3, Height = 1, Start = C(0, 0), Exit = C(2, 0), HasDoor = true, Door = C(2, 0), HasKey = true, Key = C(1, 0) };
            m.FillRectangle();
            m.AutoEdges();
            Assert.IsTrue(m.HasEdge(C(1, 0), C(2, 0), out var t) && t == EdgeType.Locked);
            m.Walls.Add(C(1, 0));
            m.RemoveEdgesTouching(C(1, 0));
            m.AutoEdges();
            Assert.IsFalse(m.HasEdge(C(0, 0), C(1, 0), out _));
        }

        [Test]
        public void Resize_KeepsContentInsideAndRegeneratesEdges()
        {
            var m = new LevelAuthoringModel { Width = 4, Height = 4, Start = C(0, 3), Exit = C(3, 0) };
            m.FillRectangle();
            m.Walls.Add(C(3, 3));
            m.AutoEdges();
            m.Resize(3, 3);
            Assert.AreEqual(9, m.Nodes.Count);
            Assert.IsFalse(m.Walls.Contains(C(3, 3)));
            Assert.IsTrue(m.HasEdge(C(0, 0), C(1, 0), out _));
            Assert.IsFalse(m.HasEdge(C(2, 0), C(3, 0), out _));
        }

        [Test]
        public void ToDefinition_ProducesValidatableLevel()
        {
            var m = new LevelAuthoringModel { Width = 3, Height = 3, Start = C(0, 2), Exit = C(2, 0), Id = 7, Name = "edit" };
            m.FillRectangle();
            m.AutoEdges();
            m.Guards.Add(new GuardDefinition(GuardMode.Fixed, new[] { C(1, 1) }, Direction.South, 1, 1, 1, 1, "g1")); // facing N would watch the exit row and make the level unsolvable
            var level = m.ToDefinition();
            var r = LevelValidator.Validate(level);
            Assert.IsTrue(r.IsValid, r.ToString());
            Assert.IsTrue(Solver.Solve(level).Found);
        }
    }
}
