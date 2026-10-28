using System.Collections.Generic;
using NUnit.Framework;
using ShadowSteps.Core;
using static ShadowSteps.Tests.EditMode.LevelBuilder;

namespace ShadowSteps.Tests.EditMode
{
    /// <summary>V2 gates A/B: movement follows explicit edges; sight ignores edges; cadences; restrictions.</summary>
    public class GraphRulesTests
    {
        private static LevelDefinition Corridor(params KeyValuePair<GridCoord, GridCoord>[] blocked)
        {
            var s = new LevelSpec { Id = 1, Name = "corridor", Width = 4, Height = 2, Start = C(0, 1), Exit = C(3, 1) };
            s.BlockedEdges.AddRange(blocked);
            return LevelDefinition.FromSpec(s);
        }

        private static KeyValuePair<GridCoord, GridCoord> Pair(GridCoord a, GridCoord b) => new KeyValuePair<GridCoord, GridCoord>(a, b);

        [Test]
        public void MissingEdge_RejectsMoveWithoutSpendingTurn()
        {
            var level = Corridor(Pair(C(0, 1), C(1, 1)));
            var s = new GameSession(level);
            var r = s.Apply(GameAction.East);
            Assert.AreEqual(InvalidActionReason.NoEdge, r.Validation.Reason);
            Assert.AreEqual(0, s.State.TurnCount);
            Assert.IsTrue(s.Apply(GameAction.North).IsValid, "other edges still work");
        }

        [Test]
        public void MissingNode_IsOutOfBounds()
        {
            var s = new LevelSpec { Id = 1, Name = "t", Width = 2, Height = 2, Start = C(0, 1), Exit = C(1, 1) };
            s.NodeMask = new List<GridCoord> { C(0, 1), C(1, 1), C(1, 0) };
            var level = LevelDefinition.FromSpec(s);
            Assert.AreEqual(InvalidActionReason.OutOfBounds, RulesEngine.Validate(level, RulesEngine.CreateInitialState(level), GameAction.North).Reason);
        }

        [Test]
        public void OneWayEdge_AllowsForwardOnly()
        {
            var s = new LevelSpec { Id = 1, Name = "t", Width = 3, Height = 1, Start = C(0, 0), Exit = C(2, 0) };
            s.ExplicitEdges = new List<Edge> { new Edge(C(0, 0), C(1, 0), EdgeType.Open), new Edge(C(1, 0), C(2, 0), EdgeType.Open), new Edge(C(2, 0), C(1, 0), EdgeType.Open) };
            var session = new GameSession(LevelDefinition.FromSpec(s));
            Assert.IsTrue(session.Apply(GameAction.East).IsValid);
            Assert.AreEqual(InvalidActionReason.NoEdge, session.Apply(GameAction.West).Validation.Reason);
            Assert.IsTrue(session.Apply(GameAction.East).Won);
        }

        [Test]
        public void LockedEdge_NeedsKey()
        {
            var s = new LevelSpec { Id = 1, Name = "t", Width = 3, Height = 2, Start = C(0, 1), Exit = C(2, 1), Key = C(0, 0), Door = C(2, 1) };
            s.LockedEdges.Add(Pair(C(1, 1), C(2, 1)));
            s.Door = null; s.Key = C(0, 0);
            var level = LevelDefinition.FromSpec(s);
            Assert.IsTrue(LevelValidator.Validate(level).IsValid, LevelValidator.Validate(level).ToString());
            var session = new GameSession(level);
            session.Apply(GameAction.East);
            Assert.AreEqual(InvalidActionReason.LockedDoor, session.Apply(GameAction.East).Validation.Reason);
            session.Restart();
            session.Apply(GameAction.North); session.Apply(GameAction.South); session.Apply(GameAction.East);
            Assert.IsTrue(session.Apply(GameAction.East).Won);
        }

        [Test]
        public void MissingEdge_DoesNotBlockSight()
        {
            // Guard at (0,1) facing E, range 3; the edge (1,1)-(2,1) is removed. Player at (2,1) must still be seen.
            var s = new LevelSpec { Id = 1, Name = "t", Width = 4, Height = 2, Start = C(3, 0), Exit = C(3, 1) };
            s.BlockedEdges.Add(Pair(C(1, 1), C(2, 1)));
            s.Guards.Add(Guard(GuardMode.Fixed, Direction.East, 3, C(0, 1)));
            var level = LevelDefinition.FromSpec(s);
            var pose = GuardSchedule.Evaluate(level.Guards[0], 0);
            Assert.IsTrue(VisionSystem.ConeVisible(pose, C(2, 1), VisionSystem.BlockedPredicate(level, false)));
            Assert.IsTrue(VisionSystem.ConeVisible(pose, C(3, 1), VisionSystem.BlockedPredicate(level, false)));
        }

        [Test]
        public void Cover_BlocksSight_PassableCoverIsWalkable()
        {
            var s = new LevelSpec { Id = 1, Name = "t", Width = 4, Height = 1, Start = C(3, 0), Exit = C(2, 0) };
            s.Cover.Add(new CoverDefinition(C(1, 0), traversable: true));
            s.Guards.Add(Guard(GuardMode.Fixed, Direction.East, 3, C(0, 0)));
            var level = LevelDefinition.FromSpec(s);
            var pose = GuardSchedule.Evaluate(level.Guards[0], 0);
            Assert.IsFalse(VisionSystem.ConeVisible(pose, C(2, 0), VisionSystem.BlockedPredicate(level, false)), "cover occludes");
            Assert.IsTrue(VisionSystem.ConeVisible(pose, C(1, 0), VisionSystem.BlockedPredicate(level, false)), "the cover node itself is visible (endpoint)");
            Assert.IsTrue(level.Graph.IsTraversable(C(1, 0)));
            Assert.IsTrue(level.Graph.HasEdge(C(2, 0), C(1, 0)));

            var s2 = new LevelSpec { Id = 2, Name = "t", Width = 4, Height = 1, Start = C(3, 0), Exit = C(2, 0) };
            s2.Cover.Add(new CoverDefinition(C(1, 0), traversable: false));
            var level2 = LevelDefinition.FromSpec(s2);
            Assert.IsFalse(level2.Graph.IsTraversable(C(1, 0)));
            Assert.IsFalse(level2.Graph.HasEdge(C(2, 0), C(1, 0)), "no edges into solid cover");
        }

        [Test]
        public void IndependentCadences_MoveEvery2_RotateEvery1()
        {
            var g = new GuardDefinition(GuardMode.PatrolRotate, new[] { C(1, 0), C(2, 0) }, Direction.North, 1, moveEvery: 2, rotateEvery: 1, rotation: 1, id: "g");
            var expectedPos = new[] { C(1, 0), C(1, 0), C(2, 0), C(2, 0), C(1, 0) };
            var expectedFacing = new[] { Direction.North, Direction.East, Direction.South, Direction.West, Direction.North };
            for (int t = 0; t < expectedPos.Length; t++)
            {
                var p = GuardSchedule.Evaluate(g, t);
                Assert.AreEqual(expectedPos[t], p.Position, "pos at phase " + t);
                Assert.AreEqual(expectedFacing[t], p.Facing, "facing at phase " + t);
            }
            Assert.AreEqual(4, GuardSchedule.GuardCycle(g)); // move cycle 4, rotation cycle 4
        }

        [Test]
        public void IndependentCadences_TravelFacingUsesMoveCadence()
        {
            var g = new GuardDefinition(GuardMode.Patrol, new[] { C(1, 0), C(2, 0), C(3, 0) }, Direction.South, 1, moveEvery: 3, rotateEvery: 1, rotation: 1, id: "g");
            Assert.AreEqual(Direction.South, GuardSchedule.Evaluate(g, 2).Facing, "authored facing until the first move");
            Assert.AreEqual(C(2, 0), GuardSchedule.Evaluate(g, 3).Position);
            Assert.AreEqual(Direction.East, GuardSchedule.Evaluate(g, 3).Facing);
            Assert.AreEqual(Direction.East, GuardSchedule.Evaluate(g, 5).Facing, "facing holds between moves");
            Assert.AreEqual(Direction.West, GuardSchedule.Evaluate(g, 9).Facing);
            Assert.AreEqual(12, GuardSchedule.GuardCycle(g));
        }

        [Test]
        public void LegacyCadence_EqualsV1Period_ForAll20Levels()
        {
            foreach (var sol in TestPaths.LoadReferenceSolutions())
                Assert.AreEqual(sol.Period, TestPaths.Level(sol.Level).Period, "period of level " + sol.Level);
        }

        [Test]
        public void NoWaitRestriction_RejectsWait()
        {
            var s = new LevelSpec { Id = 1, Name = "t", Width = 2, Height = 1, Start = C(0, 0), Exit = C(1, 0), NoWait = true };
            var level = LevelDefinition.FromSpec(s);
            Assert.AreEqual(InvalidActionReason.WaitDisabled, RulesEngine.Validate(level, RulesEngine.CreateInitialState(level), GameAction.Wait).Reason);
        }

        [Test]
        public void SmokeTurns_IsConfigurable()
        {
            var s = new LevelSpec { Id = 1, Name = "t", Width = 5, Height = 5, Start = C(0, 4), Exit = C(4, 0), SmokeCharges = 1, SmokeTurns = 3 };
            s.Guards.Add(Guard(GuardMode.Fixed, Direction.South, 3, C(0, 1)));
            var session = new GameSession(LevelDefinition.FromSpec(s));
            session.Apply(GameAction.Smoke);
            Assert.AreEqual(3, session.State.SmokeRemaining);
            Assert.AreEqual(CaptureKind.None, session.Apply(GameAction.North).Capture);
            Assert.AreEqual(CaptureKind.None, session.Apply(GameAction.Wait).Capture);
            Assert.AreEqual(CaptureKind.Seen, session.Apply(GameAction.Wait).Capture);
        }

        [Test]
        public void SolverWithoutWait_ReportsNecessity()
        {
            // Level 2 ("Clockwork Eye") reference solution uses WAIT; check whether WAIT is strictly necessary.
            var noWait = Solver.Solve(TestPaths.Level(2), new SolverOptions { AllowWait = false });
            var full = Solver.Solve(TestPaths.Level(2));
            Assert.IsTrue(full.Found);
            TestContext.WriteLine("level 2 without WAIT: " + (noWait.Found ? noWait.Actions.Count.ToString() : "unsolvable"));
            Assert.IsFalse(noWait.Found, "measured fact: level 2 (Clockwork Eye) is unsolvable without WAIT, so WAIT is a required mechanic there");
        }
    }
}
