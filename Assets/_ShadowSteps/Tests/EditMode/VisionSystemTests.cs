using System.Collections.Generic;
using NUnit.Framework;
using ShadowSteps.Core;
using static ShadowSteps.Tests.EditMode.LevelBuilder;

namespace ShadowSteps.Tests.EditMode
{
    public class VisionSystemTests
    {
        private static bool Open(GridCoord c) => false;

        [Test]
        public void NorthCone_Range2_ContainsExpectedCells()
        {
            var g = new GuardPose(C(3, 3), Direction.North, 2);
            var expected = new HashSet<GridCoord> { C(2, 2), C(3, 2), C(4, 2), C(1, 1), C(2, 1), C(3, 1), C(4, 1), C(5, 1) };
            for (int y = 0; y < 7; y++)
                for (int x = 0; x < 7; x++)
                {
                    var c = C(x, y);
                    if (c == g.Position) continue;
                    Assert.AreEqual(expected.Contains(c), VisionSystem.ConeVisible(g, c, Open), "cell " + c);
                }
        }

        [TestCase(Direction.East, 4, 3)]
        [TestCase(Direction.South, 3, 4)]
        [TestCase(Direction.West, 2, 3)]
        [TestCase(Direction.North, 3, 2)]
        public void EachFacing_SeesTheTileDirectlyAhead(Direction d, int x, int y)
        {
            var g = new GuardPose(C(3, 3), d, 1);
            Assert.IsTrue(VisionSystem.ConeVisible(g, C(x, y), Open));
            Assert.IsFalse(VisionSystem.ConeVisible(g, C(6 - x, 6 - y), Open), "tile behind must be hidden");
        }

        [Test]
        public void Range1_IncludesDiagonals()
        {
            var g = new GuardPose(C(3, 3), Direction.South, 1);
            Assert.IsTrue(VisionSystem.ConeVisible(g, C(2, 4), Open));
            Assert.IsTrue(VisionSystem.ConeVisible(g, C(4, 4), Open));
            Assert.IsFalse(VisionSystem.ConeVisible(g, C(3, 5), Open), "beyond range");
            Assert.IsFalse(VisionSystem.ConeVisible(g, C(1, 4), Open), "outside lateral bound");
        }

        [Test]
        public void WallDirectlyAhead_BlocksStraightLine()
        {
            var g = new GuardPose(C(0, 0), Direction.East, 3);
            var walls = new HashSet<GridCoord> { C(1, 0) };
            Assert.IsFalse(VisionSystem.ConeVisible(g, C(2, 0), walls.Contains));
            Assert.IsFalse(VisionSystem.ConeVisible(g, C(3, 0), walls.Contains));
            Assert.IsTrue(VisionSystem.ConeVisible(g, C(1, 0), walls.Contains), "the wall cell itself is an endpoint and is 'visible'");
        }

        [Test]
        public void Diagonal_IsBlockedByEitherTouchedCell()
        {
            // Guard at (0,0) facing E looking at (2,2): samples pass through (1,1) only on the exact diagonal.
            var g = new GuardPose(C(0, 0), Direction.East, 3);
            Assert.IsTrue(VisionSystem.ConeVisible(g, C(2, 2), Open));
            Assert.IsFalse(VisionSystem.ConeVisible(g, C(2, 2), c => c == C(1, 1)));
            // Supercover sampling: a wall at (1,0) or (0,1) is not on the centre-to-centre diagonal, so it does not occlude.
            Assert.IsTrue(VisionSystem.ConeVisible(g, C(2, 2), c => c == C(1, 0)));
        }

        [Test]
        public void KnightMoveCell_SampledLineTouchesBothIntermediateCells()
        {
            // (0,0) -> (2,1): line passes through (1,0) and (1,1) region; either blocks.
            var g = new GuardPose(C(0, 0), Direction.East, 3);
            Assert.IsTrue(VisionSystem.ConeVisible(g, C(2, 1), Open));
            Assert.IsFalse(VisionSystem.ConeVisible(g, C(2, 1), c => c == C(1, 0)));
            Assert.IsFalse(VisionSystem.ConeVisible(g, C(2, 1), c => c == C(1, 1)));
        }

        [Test]
        public void LockedDoor_BlocksUntilKeyHeld()
        {
            var level = Make(5, 1, C(0, 0), C(4, 0), key: C(3, 0), door: C(2, 0),
                guards: new[] { Guard(GuardMode.Fixed, Direction.East, 4, C(1, 0)) });
            var pose = GuardSchedule.Evaluate(level.Guards[0], 0);
            Assert.IsFalse(VisionSystem.ConeVisible(pose, C(3, 0), VisionSystem.BlockedPredicate(level, false)));
            Assert.IsTrue(VisionSystem.ConeVisible(pose, C(3, 0), VisionSystem.BlockedPredicate(level, true)));
        }

        [Test]
        public void GetVisionCells_ExcludesGuardTileAndClipsToBoard()
        {
            var level = Make(3, 3, C(0, 2), C(2, 0), guards: new[] { Guard(GuardMode.Fixed, Direction.North, 3, C(1, 1)) });
            var cells = VisionSystem.GetVisionCells(level, GuardSchedule.Evaluate(level.Guards[0], 0), false);
            CollectionAssert.AreEquivalent(new[] { C(0, 0), C(1, 0), C(2, 0) }, cells);
        }
    }
}
