using System.Linq;
using NUnit.Framework;
using ShadowSteps.Core;
using static ShadowSteps.Tests.EditMode.LevelBuilder;

namespace ShadowSteps.Tests.EditMode
{
    /// <summary>V2 gate A: graph adjacency, illegal edges, one-way/locked edges, node spacing.</summary>
    public class LevelGraphTests
    {
        [Test]
        public void LegacyGrid_GeneratesBidirectionalEdgesBetweenWalkableNeighbours()
        {
            var g = LevelGraph.BuildLegacyGrid(3, 3, new[] { C(1, 1) }, null);
            Assert.AreEqual(9, g.NodeCount);
            Assert.AreEqual(8, g.TraversableNodeCount);
            // 3x3 grid has 12 adjacencies; the centre wall removes its 4 -> 8 pairs -> 16 directed edges.
            Assert.AreEqual(16, g.EdgeCount);
            Assert.IsTrue(g.HasEdge(C(0, 0), C(1, 0)));
            Assert.IsTrue(g.HasEdge(C(1, 0), C(0, 0)));
            Assert.IsFalse(g.HasEdge(C(1, 0), C(1, 1)), "no edge into a wall");
            Assert.IsFalse(g.IsTraversable(C(1, 1)));
            Assert.IsTrue(g.BlocksSight(C(1, 1)));
            Assert.AreEqual(1.0, g.NodeSpacing);
        }

        [Test]
        public void LegacyGrid_LocksEdgesIntoDoor()
        {
            var g = LevelGraph.BuildLegacyGrid(3, 1, new GridCoord[0], C(1, 0));
            Assert.IsTrue(g.TryGetEdge(C(0, 0), C(1, 0), out var t1) && t1 == EdgeType.Locked);
            Assert.IsTrue(g.TryGetEdge(C(2, 0), C(1, 0), out var t2) && t2 == EdgeType.Locked);
            Assert.IsTrue(g.TryGetEdge(C(1, 0), C(2, 0), out var t3) && t3 == EdgeType.Open, "edges out of the door stay open");
        }

        [Test]
        public void Builder_RejectsDuplicateEdgesAndRecordsDiagnostic()
        {
            var b = new LevelGraphBuilder(2, 1).SetNode(C(0, 0), NodeInfo.Floor).SetNode(C(1, 0), NodeInfo.Floor);
            Assert.IsTrue(b.AddEdge(C(0, 0), C(1, 0)));
            Assert.IsFalse(b.AddEdge(C(0, 0), C(1, 0)));
            Assert.AreEqual(1, b.Diagnostics.Count);
            Assert.AreEqual(1, b.Build().EdgeCount);
        }

        [Test]
        public void OneWayEdge_IsOnlyTraversableForward()
        {
            var b = new LevelGraphBuilder(2, 1).SetNode(C(0, 0), NodeInfo.Floor).SetNode(C(1, 0), NodeInfo.Floor);
            b.AddEdge(C(0, 0), C(1, 0));
            var g = b.Build();
            Assert.IsTrue(g.HasEdge(C(0, 0), C(1, 0)));
            Assert.IsFalse(g.HasEdge(C(1, 0), C(0, 0)));
        }

        [Test]
        public void Spec_AutoEdgesRespectBlockedAndLockedPairsAndNodeMask()
        {
            var s = new LevelSpec { Id = 1, Name = "t", Width = 3, Height = 2, Start = C(0, 0), Exit = C(2, 0), NodeSpacing = 1.5 };
            s.NodeMask = new System.Collections.Generic.List<GridCoord> { C(0, 0), C(1, 0), C(2, 0), C(0, 1), C(1, 1) }; // (2,1) missing
            s.BlockedEdges.Add(new System.Collections.Generic.KeyValuePair<GridCoord, GridCoord>(C(0, 0), C(1, 0)));
            s.LockedEdges.Add(new System.Collections.Generic.KeyValuePair<GridCoord, GridCoord>(C(1, 0), C(2, 0)));
            s.Key = C(0, 1); s.Door = C(2, 0);
            var level = LevelDefinition.FromSpec(s);
            var g = level.Graph;
            Assert.AreEqual(5, g.NodeCount);
            Assert.IsFalse(g.HasNode(C(2, 1)));
            Assert.IsFalse(g.HasEdge(C(0, 0), C(1, 0)));
            Assert.IsFalse(g.HasEdge(C(1, 0), C(0, 0)));
            Assert.IsTrue(g.TryGetEdge(C(1, 0), C(2, 0), out var t) && t == EdgeType.Locked);
            Assert.IsTrue(g.HasEdge(C(0, 0), C(0, 1)));
            Assert.AreEqual(1.5, level.NodeSpacing);
        }

        [Test]
        public void Validator_RejectsDanglingNonAdjacentAndSelfLoopEdges()
        {
            var s = new LevelSpec { Id = 1, Name = "t", Width = 3, Height = 3, Start = C(0, 2), Exit = C(2, 0) };
            s.ExplicitEdges = new System.Collections.Generic.List<Edge>
            {
                new Edge(C(0, 2), C(5, 5), EdgeType.Open),   // dangling
                new Edge(C(0, 2), C(2, 2), EdgeType.Open),   // not adjacent
                new Edge(C(1, 1), C(1, 1), EdgeType.Open),   // self loop
            };
            var r = LevelValidator.Validate(LevelDefinition.FromSpec(s));
            Assert.IsFalse(r.IsValid);
            var text = r.ToString();
            StringAssert.Contains("missing node", text);
            StringAssert.Contains("not between cardinally adjacent nodes", text);
            StringAssert.Contains("self-loop", text);
        }

        [Test]
        public void Validator_RejectsLockedEdgesWithoutKey_AndEdgesIntoWalls()
        {
            var s = new LevelSpec { Id = 1, Name = "t", Width = 3, Height = 1, Start = C(0, 0), Exit = C(2, 0) };
            s.Walls.Add(C(1, 0));
            s.ExplicitEdges = new System.Collections.Generic.List<Edge> { new Edge(C(0, 0), C(1, 0), EdgeType.Locked), new Edge(C(1, 0), C(2, 0), EdgeType.Open) };
            var r = LevelValidator.Validate(LevelDefinition.FromSpec(s));
            Assert.IsFalse(r.IsValid);
            StringAssert.Contains("non-traversable node", r.ToString());
            StringAssert.Contains("locked edges but no key", r.ToString());
        }

        [Test]
        public void Validator_RejectsGuardRouteWithoutEdge_AndPingPongWithoutReverse()
        {
            var s = new LevelSpec { Id = 1, Name = "t", Width = 4, Height = 2, Start = C(0, 1), Exit = C(3, 1) };
            s.ExplicitEdges = new System.Collections.Generic.List<Edge>();
            foreach (var y in new[] { 0, 1 }) for (int x = 0; x < 3; x++) { s.ExplicitEdges.Add(new Edge(C(x, y), C(x + 1, y), EdgeType.Open)); s.ExplicitEdges.Add(new Edge(C(x + 1, y), C(x, y), EdgeType.Open)); }
            s.ExplicitEdges.RemoveAll(e => e.From == C(2, 0) && e.To == C(1, 0)); // make (1,0)->(2,0) one-way
            s.ExplicitEdges.Add(new Edge(C(0, 0), C(0, 1), EdgeType.Open));
            s.Guards.Add(Guard(GuardMode.Patrol, Direction.East, 1, C(0, 0), C(1, 0), C(2, 0)));
            var r = LevelValidator.Validate(LevelDefinition.FromSpec(s));
            Assert.IsFalse(r.IsValid);
            StringAssert.Contains("ping-pong route needs the reverse edge (2, 0) -> (1, 0)", r.ToString());

            var s2 = new LevelSpec { Id = 2, Name = "t", Width = 4, Height = 2, Start = C(0, 1), Exit = C(3, 1) };
            s2.BlockedEdges.Add(new System.Collections.Generic.KeyValuePair<GridCoord, GridCoord>(C(1, 0), C(2, 0)));
            s2.Guards.Add(Guard(GuardMode.Loop, Direction.East, 1, C(1, 0), C(2, 0), C(2, 1), C(1, 1)));
            var r2 = LevelValidator.Validate(LevelDefinition.FromSpec(s2));
            Assert.IsFalse(r2.IsValid);
            StringAssert.Contains("no edge for route segment (1, 0) -> (2, 0)", r2.ToString());
        }

        [Test]
        public void Validator_GuardOverlap_IsErrorForSchema2_WarningForLegacy()
        {
            var a = Guard(GuardMode.Patrol, Direction.East, 1, C(1, 0), C(2, 0));
            var b = Guard(GuardMode.Fixed, Direction.North, 1, C(2, 0));
            var s = new LevelSpec { Id = 1, Name = "t", Width = 4, Height = 2, Start = C(0, 1), Exit = C(3, 1) };
            s.Guards.Add(a); s.Guards.Add(b);
            var v2 = LevelValidator.Validate(LevelDefinition.FromSpec(s));
            Assert.IsFalse(v2.IsValid);
            StringAssert.Contains("share node (2, 0) at phase 1", v2.ToString());

            s.SchemaVersion = 2; s.MigratedFromSchema = 1;
            var legacy = LevelValidator.Validate(LevelDefinition.FromSpec(s));
            Assert.IsTrue(legacy.IsValid, legacy.ToString());
            Assert.AreEqual(1, legacy.WarningCount);
        }

        [Test]
        public void Validator_ReachabilityFollowsEdges()
        {
            var s = new LevelSpec { Id = 1, Name = "t", Width = 3, Height = 1, Start = C(0, 0), Exit = C(2, 0) };
            s.BlockedEdges.Add(new System.Collections.Generic.KeyValuePair<GridCoord, GridCoord>(C(1, 0), C(2, 0)));
            var r = LevelValidator.Validate(LevelDefinition.FromSpec(s));
            Assert.IsFalse(r.IsValid);
            StringAssert.Contains("not reachable", r.ToString());
        }

        [Test]
        public void Legacy18_HasOverlapWarningOnly()
        {
            var r = LevelValidator.Validate(TestPaths.Level(18));
            Assert.IsTrue(r.IsValid, r.ToString());
            Assert.IsTrue(r.Issues.Any(i => i.Message.Contains("share node (2, 0) at phase 1")), r.ToString());
        }
    }
}
