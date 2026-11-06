using NUnit.Framework;
using ShadowSteps.Core;
using ShadowSteps.Data;
using ShadowSteps.V3;
using static ShadowSteps.Tests.EditMode.V3.V3TestKit;

namespace ShadowSteps.Tests.EditMode.V3
{
    /// <summary>
    /// D-047 long edges: passage cells ('-' horizontal, '|' vertical, '+' both) hold no node and let one connection span two or
    /// three squares. One action traverses one edge whatever its length; cuts, doors and knockouts key on the two end nodes;
    /// guards look across passages and see two nodes, not two squares; patrols step a long edge in one turn and face along it.
    /// </summary>
    public class V3PassageTests
    {
        // (0,0) ─────── (3,0) ── (4,0) ── (5,0)     passages (1,0),(2,0) carry the horizontal edge; (1,1),(2,1) have no node above them
        // (0,1) ──(1,1)──(2,1)──(3,1)──(4,1)──(5,1)
        private static StageBuilder Fixture() { var b = Board(".--...", "......"); b.Start = C(0, 0); b.Exit = C(5, 0); return b; }

        [Test]
        public void Rows_ParsePassages_RoundTripThroughJson_AndKeepThemOffTheNodeList()
        {
            var s = Fixture().Build();
            Assert.IsTrue(s.IsPassage(C(1, 0))); Assert.AreEqual(PassageAxis.Horizontal, s.PassageAxisAt(C(1, 0))); Assert.IsFalse(s.IsFloor(C(1, 0)), "a passage is not a node");
            Assert.AreEqual(2, s.PassageCount); Assert.IsTrue(s.HasPassages); Assert.AreEqual(PassageAxis.None, s.PassageAxisAt(C(0, 0)));
            CollectionAssert.DoesNotContain(new System.Collections.Generic.List<GridCoord>(s.FloorNodes()), C(1, 0));
            Assert.AreEqual(10, new System.Collections.Generic.List<GridCoord>(s.FloorNodes()).Count, "12 cells minus 2 passages");
            CollectionAssert.AreEqual(new[] { ".--...", "......" }, StageBuilder.RowsOf(s));
            var all = new StageBuilder { Id = 2, Code = "P" }.FromRows(new[] { ".-.", "|+|", "..." }).Build();
            Assert.AreEqual(PassageAxis.Vertical, all.PassageAxisAt(C(0, 1))); Assert.AreEqual(PassageAxis.Both, all.PassageAxisAt(C(1, 1)));
            CollectionAssert.AreEqual(new[] { ".-.", "|+|", "..." }, StageBuilder.RowsOf(all));
            var back = StageJson.Parse(StageJson.ToJson(new[] { s })).Stages[0];
            Assert.IsTrue(back.IsPassage(C(2, 0))); CollectionAssert.AreEqual(StageBuilder.RowsOf(s), StageBuilder.RowsOf(back));
            Assert.IsTrue(back.ToBuilder().Build().IsPassage(C(2, 0)), "ToBuilder keeps passages");
            Assert.Throws<System.ArgumentException>(() => Board(".?."), "unknown glyphs still fail loudly");
        }

        [Test]
        public void Step_CrossesPassagesAlongTheirAxisOnly_AndConnectionsKeyOnTheEndNodes()
        {
            var s = Fixture().Build();
            Assert.AreEqual(C(3, 0), s.Step(C(0, 0), Direction.East), "one edge from (0,0) to (3,0) across two passages");
            Assert.AreEqual(C(0, 0), s.Step(C(3, 0), Direction.West));
            Assert.AreEqual(C(4, 0), s.Step(C(3, 0), Direction.East), "plain lattice step is unchanged");
            Assert.IsNull(s.Step(C(1, 1), Direction.North), "a horizontal passage does not carry a vertical connection");
            Assert.IsNull(s.Step(C(0, 0), Direction.North), "board edge");
            Assert.IsTrue(s.IsConnected(C(0, 0), C(3, 0))); Assert.IsFalse(s.IsConnected(C(0, 0), C(1, 0)), "a passage is not a node"); Assert.IsFalse(s.IsConnected(C(0, 0), C(4, 0)), "another node lies between");
            Assert.AreEqual(Direction.East, s.DirectionTo(C(0, 0), C(3, 0))); Assert.IsNull(s.DirectionTo(C(0, 0), C(4, 0))); Assert.IsNull(s.DirectionTo(C(0, 0), C(1, 1)), "diagonal");
            Assert.IsTrue(s.Spans(C(0, 0), C(3, 0))); Assert.AreEqual(3, s.EdgeLength(C(0, 0), C(3, 0))); Assert.AreEqual(1, s.EdgeLength(C(3, 0), C(4, 0)));
            Assert.AreEqual(C(3, 0), s.Neighbor(C(0, 0), Direction.East));
            var cut = Fixture(); cut.Cuts.Add(new EdgeKey(C(0, 0), C(3, 0))); var sc = cut.Build();
            Assert.IsNull(sc.Neighbor(C(0, 0), Direction.East), "the long edge is cut"); Assert.IsTrue(sc.Spans(C(0, 0), C(3, 0)), "…but the geometry still spans it"); Assert.IsFalse(sc.IsConnected(C(0, 0), C(3, 0)));
            Assert.AreEqual(Direction.East, DirectionUtil.Toward(3, 0)); Assert.AreEqual(Direction.North, DirectionUtil.Toward(0, -2)); Assert.IsNull(DirectionUtil.Toward(1, 1)); Assert.IsNull(DirectionUtil.Toward(0, 0));
            var vertical = new StageBuilder { Id = 3, Code = "V" }.FromRows(new[] { "..", "|.", "|.", ".." }).Build();
            Assert.AreEqual(C(0, 3), vertical.Step(C(0, 0), Direction.South)); Assert.IsNull(vertical.Step(C(1, 1), Direction.West), "a vertical passage does not carry a horizontal connection");
        }

        [Test]
        public void Move_AcrossALongEdge_IsOneCountedAction_AndCutsAndDoorsOnItBehaveAsOnAnyEdge()
        {
            var s = Fixture().Build(); var st = StageState.Initial(s);
            var o = Step(s, ref st, E); Assert.IsTrue(o.Valid); Assert.AreEqual(C(3, 0), st.Player); Assert.AreEqual(1, st.T, "one action for the three-square edge");
            o = Step(s, ref st, W); Assert.IsTrue(o.Valid); Assert.AreEqual(C(0, 0), st.Player);
            Assert.IsFalse(StageRules.TryApply(s, StageState.Initial(s), N).Valid);
            var cut = Fixture(); cut.Cuts.Add(new EdgeKey(C(0, 0), C(3, 0)));
            Assert.AreEqual(RejectReason.NoConnection, StageRules.TryApply(cut.Build(), StageState.Initial(cut.Build()), E).Reason);
            var door = Fixture(); door.Doors.Add(new DoorSpec(C(0, 0), C(3, 0))); door.Key = C(0, 1); var sd = door.Build();
            var ds = StageState.Initial(sd);
            Assert.AreEqual(RejectReason.KeyRequired, StageRules.TryApply(sd, ds, E).Reason, "locked long edge");
            Step(sd, ref ds, S); Assert.IsTrue(ds.HasKey); Step(sd, ref ds, N);
            o = Step(sd, ref ds, E); Assert.IsTrue(o.Valid); Assert.AreEqual(-1, o.DoorOpened, "the lock opened on arrival at its near end (D-068)"); Assert.IsTrue(ds.IsDoorOpen(0)); Assert.AreEqual(C(3, 0), ds.Player); Assert.AreEqual(3, ds.T);
            // From (1,1) the lattice neighbour above is a passage of the other axis: no move, nothing counted.
            var mid = StageState.Initial(s); mid = StageRules.TryApply(s, mid, S).After; mid = StageRules.TryApply(s, mid, E).After; Assert.AreEqual(C(1, 1), mid.Player);
            Assert.AreEqual(RejectReason.Blocked, StageRules.TryApply(s, mid, N).Reason); Assert.AreEqual(2, mid.T);
            // The solver counts it the same way.
            var sol = StageSolver.Solve(s); Assert.IsTrue(sol.Found); Assert.AreEqual(3, sol.CountedActions, "(0,0) → (3,0) → (4,0) → (5,0)");
        }

        [Test]
        public void GuardVision_LooksAcrossPassages_ButIsMeasuredInSquares()
        {
            // D-050: a walkway never lengthens sight. Across the 3-square fixture edge the guard at (0,0) sees nothing (both cells within reach are passages).
            var s = Fixture().Build(); var st = StageState.Initial(s);
            CollectionAssert.IsEmpty(Sight.GuardVision(s, st, new GuardPose3(C(0, 0), Direction.East)), "two squares of passage: nothing seen");
            CollectionAssert.AreEqual(Cells((3, 0)), Sight.GuardVision(s, st, new GuardPose3(C(4, 0), Direction.West)), "(3,0) at one square; (2,0) at two squares is a passage; (0,0) is out of reach");
            CollectionAssert.IsEmpty(Sight.GuardVision(s, st, new GuardPose3(C(1, 1), Direction.North)), "a passage is never seen and nothing lies beyond it");
            // Across a 2-square edge the far node is exactly two squares away: seen, and the fan reaches half a square past it.
            var two = new StageBuilder { Id = 12, Code = "T" }.FromRows(new[] { ".-...", "....." }).Build(); var ts = StageState.Initial(two);
            CollectionAssert.AreEqual(Cells((2, 0)), Sight.GuardVision(two, ts, new GuardPose3(C(0, 0), Direction.East)), "the far end of a 2-square edge, nothing beyond");
            CollectionAssert.AreEqual(Cells((1, 0), (2, 0)).GetRange(1, 1), Sight.GuardVision(two, ts, new GuardPose3(C(0, 0), Direction.East)));
            var cone = Sight.GuardConeOutline(two, 0, null, new GuardPose3(C(0, 0), Direction.East), 12.0, 4, 0.5);
            Assert.AreEqual(2.5, cone[1 + 2].X, 1e-6, "central ray reaches half a square past the last seen node");
            CollectionAssert.AreEqual(Cells((3, 0), (4, 0)), Sight.GuardVision(two, ts, new GuardPose3(C(2, 0), Direction.East)), "plain lattice: two nodes, two squares");
            // Passages of either axis are open ground to sight; cover and walls still stop it.
            var v = new StageBuilder { Id = 4, Code = "S" }.FromRows(new[] { "..|..", "..|.." }).Build();
            CollectionAssert.AreEqual(Cells((1, 0)), Sight.GuardVision(v, StageState.Initial(v), new GuardPose3(C(0, 0), Direction.East)), "(2,0) is a passage at two squares; (3,0) is out of reach");
            var x = new StageBuilder { Id = 5, Code = "X" }.FromRows(new[] { ".-X..", "....." }).Build();
            CollectionAssert.IsEmpty(Sight.GuardVision(x, StageState.Initial(x), new GuardPose3(C(0, 0), Direction.East)));
            // Capture across a 2-square edge: stepping onto the far node in front of a guard is seen.
            var g = new StageBuilder { Id = 13, Code = "G" }.FromRows(new[] { ".-...", "....." }); g.Guards.Add(Static(C(2, 0), Direction.West)); g.Start = C(0, 1); g.Exit = C(4, 1); var sg = g.Build();
            var gs = StageState.Initial(sg); gs = StageRules.TryApply(sg, gs, N).After; Assert.AreEqual(C(0, 0), gs.Player); Assert.AreEqual(StageResult.Caught, gs.Result, "(0,0) is two squares in front of the guard");
            // Across the 3-square fixture edge the same step is safe: the far node is out of reach.
            var far = Fixture(); far.Guards.Add(Static(C(3, 0), Direction.West)); far.Start = C(0, 1); var sf = far.Build();
            var fs = StageRules.TryApply(sf, StageState.Initial(sf), N).After; Assert.AreEqual(StageResult.Playing, fs.Result, "three squares away: unseen");
        }

        [Test]
        public void Knockout_AcrossALongEdge_UsesTheNodesTheGuardFacesAndFlanks()
        {
            // Guard on (3,0) facing W: its front node is (0,0) at the far end of the long edge; (3,1) below it is its left.
            var b = Fixture(); b.Guards.Add(Static(C(3, 0), Direction.West)); b.Start = C(0, 1); var s = b.Build();
            var st = StageState.Initial(s, StageMode.Knockout);
            st = StageRules.TryApply(s, st, N).After; // (0,0): the guard's front node, but three squares away: unseen (D-050)
            Assert.AreEqual(StageResult.Playing, st.Result);
            Assert.AreEqual(RejectReason.FrontalApproach, StageRules.CheckKnockout(s, st, 0, out _, out _), "still its front: no knockout from there");
            var st2 = StageState.Initial(s, StageMode.Knockout);
            foreach (var a in new[] { E, E, E }) st2 = StageRules.TryApply(s, st2, a).After; Assert.AreEqual(C(3, 1), st2.Player); Assert.AreEqual(StageResult.Playing, st2.Result);
            CollectionAssert.AreEqual(new[] { 0 }, StageRules.KnockoutTargets(s, st2));
            Assert.AreEqual(RejectReason.None, StageRules.CheckKnockout(s, st2, 0, out var side, out var approach)); Assert.AreEqual(KnockoutSide.Left, side); Assert.AreEqual(Direction.North, approach);
            var ko = StageRules.TryApply(s, st2, N); Assert.IsTrue(ko.Valid && ko.Knockout); Assert.AreEqual(C(3, 0), ko.After.Player); Assert.AreEqual(4, ko.After.T);
            // Guard on (0,0) facing S (front (0,1)); the player at (3,0) is across the long edge on its left: a legal knockout that travels the whole edge in one action.
            var c = Fixture(); c.Guards.Add(Static(C(0, 0), Direction.South)); c.Start = C(4, 0); var sc = c.Build();
            var cs = StageState.Initial(sc, StageMode.Knockout); cs = StageRules.TryApply(sc, cs, W).After; Assert.AreEqual(C(3, 0), cs.Player);
            Assert.AreEqual(RejectReason.None, StageRules.CheckKnockout(sc, cs, 0, out side, out approach)); Assert.AreEqual(KnockoutSide.Left, side); Assert.AreEqual(Direction.West, approach);
            var ko2 = StageRules.TryApply(sc, cs, StageAction.Knockout(0)); Assert.IsTrue(ko2.Valid && ko2.Knockout); Assert.AreEqual(C(0, 0), ko2.After.Player);
            // Frontal across the edge is refused as on any edge: guard (0,0) facing E, player (3,0).
            var f = Fixture(); f.Guards.Add(Static(C(0, 0), Direction.East)); f.Start = C(4, 1); var sf = f.Build();
            var fs = new StageState(C(3, 0), 5, false, 0, null, null, 0, 0, -1, false, StageResult.Playing, null, 0, StageMode.Knockout, 0UL);
            Assert.AreEqual(RejectReason.FrontalApproach, StageRules.CheckKnockout(sf, fs, 0, out _, out _));
            // Not adjacent: (4,0) is two nodes from the guard at (0,0) (the long edge's far end lies between).
            var far = new StageState(C(4, 0), 5, false, 0, null, null, 0, 0, -1, false, StageResult.Playing, null, 0, StageMode.Knockout, 0UL);
            Assert.AreEqual(RejectReason.GuardNotAdjacent, StageRules.CheckKnockout(sf, far, 0, out _, out _));
        }

        [Test]
        public void Patrol_StepsALongEdgeInOneTurn_FacesAlongIt_AndValidates()
        {
            var b = Fixture(); b.Guards.Add(PingPong(0, C(0, 0), C(3, 0), C(4, 0))); b.Start = C(0, 1); b.Exit = C(5, 1); var s = b.Build();
            Assert.AreEqual(C(3, 0), GuardTrack.PositionAt(s.Guards[0], 1)); Assert.AreEqual(Direction.East, GuardTrack.PoseAt(s.Guards[0], 1).Facing);
            Assert.AreEqual(Direction.East, GuardTrack.NextStep(s.Guards[0], 0)); Assert.AreEqual(Direction.West, GuardTrack.PoseAt(s.Guards[0], 0).Facing, "endpoint rule: faces the end it just reached");
            Assert.AreEqual(Direction.West, GuardTrack.PoseAt(s.Guards[0], 3).Facing); Assert.AreEqual(C(3, 0), GuardTrack.PositionAt(s.Guards[0], 3));
            var r = StageValidator.Validate(s, new StageValidationOptions { RunSolver = false, MaxEdgeSquares = 3 }); Assert.AreEqual(0, r.ErrorCount, r.ToString());
            StringAssert.Contains("spans 3 squares; at most 2 allowed", StageValidator.Validate(s, new StageValidationOptions { RunSolver = false }).ToString(), "D-050: the shipped cap is two squares");
            var bad = Fixture(); bad.Guards.Add(PingPong(0, C(0, 0), C(1, 0))); bad.Start = C(0, 1); bad.Exit = C(5, 1);
            var rb = StageValidator.Validate(bad.Build(), new StageValidationOptions { RunSolver = false, MaxEdgeSquares = 3 });
            Assert.Greater(rb.ErrorCount, 0); StringAssert.Contains("passage (no node)", rb.ToString());
        }

        [Test]
        public void EmptyGround_HasNoNodeAndNoConnection_ButNeverBlocksSight()
        {
            // (0,0) ── (1,0)   _   (3,0) ── (4,0): the empty cell carries nothing, so (1,0) and (3,0) are not joined; a guard still sees across it.
            var b = new StageBuilder { Id = 9, Code = "E" }.FromRows(new[] { "._._.", "....." }); b.Start = C(0, 0); b.Exit = C(4, 0); var s = b.Build();
            Assert.IsTrue(s.IsEmpty(C(1, 0))); Assert.IsFalse(s.IsFloor(C(1, 0))); Assert.IsFalse(s.IsPassage(C(1, 0))); Assert.IsTrue(s.IsOpenNonNode(C(1, 0)));
            Assert.IsNull(s.Step(C(0, 0), Direction.East), "no connection through empty ground"); Assert.IsNull(s.Step(C(2, 0), Direction.West));
            CollectionAssert.AreEqual(new[] { "._._.", "....." }, StageBuilder.RowsOf(s));
            Assert.IsTrue(StageJson.Parse(StageJson.ToJson(new[] { s })).Stages[0].IsEmpty(C(3, 0)), "round-trips through JSON");
            Assert.IsTrue(s.ToBuilder().Build().IsEmpty(C(3, 0)));
            CollectionAssert.AreEqual(Cells((2, 0)), Sight.GuardVision(s, StageState.Initial(s), new GuardPose3(C(0, 0), Direction.East)), "sight crosses empty ground to the node two squares away; (4,0) is out of reach (D-050)");
            Assert.AreEqual(8, new System.Collections.Generic.List<GridCoord>(s.FloorNodes()).Count);
            var sol = StageSolver.Solve(s); Assert.IsTrue(sol.Found); Assert.AreEqual(6, sol.CountedActions, "around through row 1: no shortcut across empty ground");
            var bad = new StageBuilder { Id = 10, Code = "E2" }.FromRows(new[] { ".-_..", "....." }); bad.Start = C(0, 1); bad.Exit = C(4, 1);
            StringAssert.Contains("leads nowhere toward E", StageValidator.Validate(bad.Build(), new StageValidationOptions { RunSolver = false }).ToString(), "a passage ending on empty ground is dangling");
            var onEmpty = new StageBuilder { Id = 11, Code = "E3" }.FromRows(new[] { "._.", "..." }); onEmpty.Start = C(1, 0); onEmpty.Exit = C(2, 1);
            StringAssert.Contains("empty ground (no node)", StageValidator.Validate(onEmpty.Build(), new StageValidationOptions { RunSolver = false }).ToString());
        }

        [Test]
        public void Smoke_ReachesTheNextNodeAcrossAWalkway_ButNotBeyondIt()
        {
            // (0,1) ─── (2,1) ── (3,1): the far end of the 2-square edge is a smoke target; (3,1) two nodes away is not; the plain 3×3 still applies.
            var b = new StageBuilder { Id = 14, Code = "SM" }.FromRows(new[] { ".....", ".-...", "....." }); b.Start = C(0, 1); b.Exit = C(4, 1); b.SmokeCharges = 1; var s = b.Build();
            var st = StageState.Initial(s);
            Assert.IsTrue(Sight.SmokeInRange(s, C(0, 1), C(2, 1))); Assert.IsFalse(Sight.SmokeInRange(s, C(0, 1), C(3, 1))); Assert.IsTrue(Sight.SmokeInRange(s, C(0, 1), C(1, 0)), "diagonal within the 3×3");
            CollectionAssert.AreEquivalent(Cells((0, 0), (1, 0), (0, 1), (2, 1), (0, 2), (1, 2)), StageRules.SmokeTargets(s, st), "own node, the 3×3 nodes and the walkway's far node; the passage itself is not a node");
            var o = StageRules.TryApply(s, st, StageAction.Smoke(C(2, 1))); Assert.IsTrue(o.Valid); Assert.IsTrue(o.Free); Assert.IsTrue(o.After.IsSmoked(C(2, 1)));
            Assert.AreEqual(RejectReason.BadSmokeTarget, StageRules.TryApply(s, st, StageAction.Smoke(C(3, 1))).Reason);
            // Empty ground between two nodes is not a walkway: no throw across it.
            var e = new StageBuilder { Id = 15, Code = "SE" }.FromRows(new[] { "._..", "...." }); e.Start = C(0, 0); e.Exit = C(3, 1); e.SmokeCharges = 1; var se = e.Build();
            Assert.IsFalse(Sight.SmokeInRange(se, C(0, 0), C(2, 0)));
            // The solver sees the same targets: smoke on the far node hides it from a guard looking back along the row.
            var g = new StageBuilder { Id = 16, Code = "SG" }.FromRows(new[] { ".-..", "...." }); g.Start = C(0, 0); g.Exit = C(3, 1); g.SmokeCharges = 1; g.Guards.Add(Static(C(3, 0), Direction.West)); var sg = g.Build();
            var sol = StageSolver.Solve(sg, new SolveOptions { Mode = StageMode.Knockout, MaxWaits = 0 }); Assert.IsTrue(sol.Found);
            bool smokedFar = false; foreach (var a in sol.Actions) if (a.Kind == StageActionKind.Smoke && a.Target == C(2, 0)) smokedFar = true;
            Assert.IsTrue(smokedFar || sol.CountedActions <= 3, "either the walkway's far node is smoked from the start, or a shorter clean route exists");
        }

        [Test]
        public void Validator_RejectsDanglingPassages_PassagesUsedAsNodes_AndCutsThatSkipANode()
        {
            var dangling = new StageBuilder { Id = 6, Code = "D" }.FromRows(new[] { ".-#..", "....." }); dangling.Start = C(0, 1); dangling.Exit = C(4, 1);
            var r = StageValidator.Validate(dangling.Build(), new StageValidationOptions { RunSolver = false });
            Assert.AreEqual(1, r.ErrorCount, r.ToString()); StringAssert.Contains("passage (1, 0) '-' leads nowhere toward E", r.ToString());
            var axis = new StageBuilder { Id = 7, Code = "A" }.FromRows(new[] { ".|.", "...", "..." }); axis.Start = C(0, 1); axis.Exit = C(2, 1); // '|' at (1,0): nothing above it
            StringAssert.Contains("leads nowhere toward N", StageValidator.Validate(axis.Build(), new StageValidationOptions { RunSolver = false }).ToString());
            var onPassage = Fixture(); onPassage.Start = C(1, 0);
            StringAssert.Contains("start (1, 0) is not a floor node (passage (no node))", StageValidator.Validate(onPassage.Build(), new StageValidationOptions { RunSolver = false }).ToString());
            var skip = Fixture(); skip.Cuts.Add(new EdgeKey(C(0, 0), C(4, 0)));
            StringAssert.Contains("is not between two nodes joined by one straight connection", StageValidator.Validate(skip.Build(), new StageValidationOptions { RunSolver = false }).ToString());
            var touch = Fixture(); touch.Cuts.Add(new EdgeKey(C(0, 0), C(1, 0)));
            var rt = StageValidator.Validate(touch.Build(), new StageValidationOptions { RunSolver = false, MaxEdgeSquares = 3 }); Assert.AreEqual(0, rt.ErrorCount, "a cut touching a non-node cell is a no-op warning, as for legacy data"); StringAssert.Contains("has no effect", rt.ToString());
            var ok = Fixture(); ok.Cuts.Add(new EdgeKey(C(0, 0), C(3, 0)));
            Assert.AreEqual(0, StageValidator.Validate(ok.Build(), new StageValidationOptions { RunSolver = false, MaxEdgeSquares = 3 }).ErrorCount, "a cut on the long edge itself is fine");
        }
    }
}
