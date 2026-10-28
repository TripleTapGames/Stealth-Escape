using NUnit.Framework;
using ShadowSteps.Core;
using static ShadowSteps.Tests.EditMode.LevelBuilder;

namespace ShadowSteps.Tests.EditMode
{
    public class GuardScheduleTests
    {
        [Test]
        public void Period_MatchesReferenceForAll20Levels()
        {
            foreach (var s in TestPaths.LoadReferenceSolutions())
                Assert.AreEqual(s.Period, TestPaths.Level(s.Level).Period, "period of level " + s.Level);
        }

        [Test]
        public void Fixed_NeverChanges()
        {
            var g = Guard(GuardMode.Fixed, Direction.East, 2, C(2, 2), C(3, 2));
            for (int t = 0; t < 10; t++)
                Assert.AreEqual(new GuardPose(C(2, 2), Direction.East, 2), GuardSchedule.Evaluate(g, t));
        }

        [TestCase(1, new[] { 0, 1, 2, 3, 0, 1 })]
        [TestCase(-1, new[] { 0, 3, 2, 1, 0, 3 })]
        public void Rotate_StepsOneQuarterTurnPerPhase(int rotation, int[] expectedFacings)
        {
            var g = Guard(GuardMode.Rotate, Direction.North, 1, 1, rotation, C(1, 1));
            for (int t = 0; t < expectedFacings.Length; t++)
                Assert.AreEqual((Direction)expectedFacings[t], GuardSchedule.Evaluate(g, t).Facing, "phase " + t);
        }

        [Test]
        public void Rotate_WithStep2_ChangesEveryOtherPhase()
        {
            var g = Guard(GuardMode.Rotate, Direction.North, 1, 2, 1, C(1, 1));
            var expected = new[] { Direction.North, Direction.North, Direction.East, Direction.East, Direction.South };
            for (int t = 0; t < expected.Length; t++) Assert.AreEqual(expected[t], GuardSchedule.Evaluate(g, t).Facing, "phase " + t);
            Assert.AreEqual(8, GuardSchedule.GuardCycle(g));
        }

        [Test]
        public void Patrol_PingPongsAndFacesTravelDirection()
        {
            var g = Guard(GuardMode.Patrol, Direction.West, 1, C(1, 1), C(2, 1), C(3, 1));
            var pos = new[] { C(1, 1), C(2, 1), C(3, 1), C(2, 1), C(1, 1), C(2, 1) };
            var fac = new[] { Direction.West, Direction.East, Direction.East, Direction.West, Direction.West, Direction.East };
            for (int t = 0; t < pos.Length; t++)
            {
                var p = GuardSchedule.Evaluate(g, t);
                Assert.AreEqual(pos[t], p.Position, "pos phase " + t);
                Assert.AreEqual(fac[t], p.Facing, "facing phase " + t);
            }
            Assert.AreEqual(4, GuardSchedule.GuardCycle(g));
        }

        [Test]
        public void Loop_WrapsAndFacesTravelDirection()
        {
            var g = Guard(GuardMode.Loop, Direction.North, 2, C(1, 1), C(2, 1), C(2, 2), C(1, 2));
            var pos = new[] { C(1, 1), C(2, 1), C(2, 2), C(1, 2), C(1, 1) };
            var fac = new[] { Direction.North, Direction.East, Direction.South, Direction.West, Direction.North };
            for (int t = 0; t < pos.Length; t++)
            {
                var p = GuardSchedule.Evaluate(g, t);
                Assert.AreEqual(pos[t], p.Position, "pos phase " + t);
                Assert.AreEqual(fac[t], p.Facing, "facing phase " + t);
            }
        }

        [Test]
        public void PatrolRotate_RotatesIndependentlyOfTravel()
        {
            var g = Guard(GuardMode.PatrolRotate, Direction.South, 1, 1, 1, C(1, 1), C(1, 2));
            Assert.AreEqual(new GuardPose(C(1, 1), Direction.South, 1), GuardSchedule.Evaluate(g, 0));
            Assert.AreEqual(new GuardPose(C(1, 2), Direction.West, 1), GuardSchedule.Evaluate(g, 1));
            Assert.AreEqual(new GuardPose(C(1, 1), Direction.North, 1), GuardSchedule.Evaluate(g, 2));
            Assert.AreEqual(4, GuardSchedule.GuardCycle(g));
        }

        [Test]
        public void Level10_PatrolFacingResetsToAuthoredAtPhaseZero_ReferenceQuirk()
        {
            // Reference guard_at() derives travel facing from phase-1, so at phase 0 (after period wrap) the facing is the
            // authored one even though the unwrapped schedule would be mid-travel. Level 10 guard A: route (3,2)->(3,1)->(3,0), facing E.
            var g = TestPaths.Level(10).Guards[0];
            Assert.AreEqual(Direction.East, GuardSchedule.Evaluate(g, 0).Facing);
            Assert.AreEqual(Direction.North, GuardSchedule.Evaluate(g, 1).Facing);
            Assert.AreEqual(Direction.South, GuardSchedule.Evaluate(g, 3).Facing);
            Assert.AreEqual(4, TestPaths.Level(10).Period);
            // Phase is stored mod period, so the engine must evaluate phase 0 (facing E), never phase 4 (facing S).
            Assert.AreEqual(Direction.South, GuardSchedule.Evaluate(g, 4).Facing);
        }

        [Test]
        public void SingleTileMovingRoute_StaysPut()
        {
            var g = Guard(GuardMode.Patrol, Direction.East, 1, C(2, 2));
            for (int t = 0; t < 5; t++) Assert.AreEqual(new GuardPose(C(2, 2), Direction.East, 1), GuardSchedule.Evaluate(g, t));
        }
    }
}
