using NUnit.Framework;
using ShadowSteps.Core;
using ShadowSteps.V3;
using static ShadowSteps.Tests.EditMode.V3.V3TestKit;

namespace ShadowSteps.Tests.EditMode.V3
{
    /// <summary>Handover §2, §4, §5: straight 2-node guard sight, shared occlusion geometry, camera footprint.</summary>
    public class V3SightTests
    {
        private static Stage OpenStage() => Open(6, 5, C(0, 2), C(5, 2)).Build();

        [Test]
        public void Guard_SeesExactlyTwoNodesStraightAhead_NoDiagonals()
        {
            var stage = OpenStage();
            var seen = Sight.GuardVision(stage, StageState.Initial(stage), new GuardPose3(C(2, 2), Direction.East));
            CollectionAssert.AreEqual(Cells((3, 2), (4, 2)), seen);
            Assert.IsFalse(Sight.GuardSees(stage, StageState.Initial(stage), new GuardPose3(C(2, 2), Direction.East), C(3, 1)));
            Assert.IsFalse(Sight.GuardSees(stage, StageState.Initial(stage), new GuardPose3(C(2, 2), Direction.East), C(5, 2)));
        }

        [Test]
        public void Walls_StopSight_AtTheFirstBlocker()
        {
            var b = Open(6, 5, C(0, 2), C(5, 2)); b.Walls.Add(C(4, 2));
            var stage = b.Build();
            CollectionAssert.AreEqual(Cells((3, 2)), Sight.GuardVision(stage, StageState.Initial(stage), new GuardPose3(C(2, 2), Direction.East)));
            b = Open(6, 5, C(0, 2), C(5, 2)); b.Walls.Add(C(3, 2));
            stage = b.Build();
            CollectionAssert.IsEmpty(Sight.GuardVision(stage, StageState.Initial(stage), new GuardPose3(C(2, 2), Direction.East)));
            CollectionAssert.AreEqual(Cells((2, 1), (2, 0)), Sight.GuardVision(stage, StageState.Initial(stage), new GuardPose3(C(2, 2), Direction.North)), "board edge ends sight");
        }

        [Test]
        public void ClosedDoor_BlocksSight_OpenDoorDoesNot()
        {
            var b = Open(6, 5, C(0, 2), C(5, 2)); b.Key = C(0, 0); b.Doors.Add(new DoorSpec(C(3, 2), C(4, 2)));
            var stage = b.Build();
            var closed = StageState.Initial(stage);
            CollectionAssert.AreEqual(Cells((3, 2)), Sight.GuardVision(stage, closed, new GuardPose3(C(2, 2), Direction.East)));
            var open = new StageState(closed.Player, 0, true, 1, null, null, 0, 0, -1, false, StageResult.Playing, null);
            CollectionAssert.AreEqual(Cells((3, 2), (4, 2)), Sight.GuardVision(stage, open, new GuardPose3(C(2, 2), Direction.East)));
        }

        [Test]
        public void MissingConnection_NeverBlocksSight()
        {
            var b = Open(6, 5, C(0, 2), C(5, 2)); b.Cuts.Add(new EdgeKey(C(2, 2), C(3, 2)));
            var stage = b.Build();
            CollectionAssert.AreEqual(Cells((3, 2), (4, 2)), Sight.GuardVision(stage, StageState.Initial(stage), new GuardPose3(C(2, 2), Direction.East)));
        }

        [Test]
        public void Smoke_BlindsGuardsInsideIt_AndHidesTheCloudNodeAndEverythingBehind()
        {
            var stage = OpenStage();
            var pose = new GuardPose3(C(2, 2), Direction.East);
            var cloudAt3 = new StageState(C(0, 2), 0, false, 0, new[] { new SmokeCloud(C(3, 2), 2) }, null, 0, 0, -1, false, StageResult.Playing, null);
            CollectionAssert.IsEmpty(Sight.GuardVision(stage, cloudAt3, pose));
            var cloudAt4 = new StageState(C(0, 2), 0, false, 0, new[] { new SmokeCloud(C(4, 2), 2) }, null, 0, 0, -1, false, StageResult.Playing, null);
            CollectionAssert.AreEqual(Cells((3, 2)), Sight.GuardVision(stage, cloudAt4, pose));
            var cloudOnGuard = new StageState(C(0, 2), 0, false, 0, new[] { new SmokeCloud(C(2, 2), 2) }, null, 0, 0, -1, false, StageResult.Playing, null);
            CollectionAssert.IsEmpty(Sight.GuardVision(stage, cloudOnGuard, pose));
        }

        [Test]
        public void Camera_HasA3x2Footprint_StartingOneNodeOutFromItsMountingWall()
        {
            // Documented prototype geometry (Docs/V3_MIGRATION_AUDIT.md §4.1): the node directly in front of the lens is not covered.
            var b = Board("#######", "#.....#", "#.....#", "#.....#", "#######"); b.Start = C(1, 3); b.Exit = C(5, 3);
            b.Cameras.Add(Camera(C(3, 0), Direction.South, Direction.South));
            var stage = b.Build();
            CollectionAssert.AreEquivalent(Cells((2, 2), (3, 2), (4, 2), (2, 3), (3, 3), (4, 3)), Sight.CameraVision(stage, StageState.Initial(stage), 0, 0));
            Assert.IsFalse(Sight.AnyCameraSees(stage, StageState.Initial(stage), 0, C(3, 1)));
        }

        [Test]
        public void Camera_FootprintIsOccludedPerNode_ByWallsAndSmoke()
        {
            var b = Board("#######", "#.....#", "#..#..#", "#.....#", "#######"); b.Start = C(1, 3); b.Exit = C(5, 3);
            b.Cameras.Add(Camera(C(3, 0), Direction.South, Direction.South));
            var stage = b.Build();
            // The wall at (3,2) hides itself and (3,3); the diagonal rays to (2,3) and (4,3) clip its corners, so they are blocked too (corner contact rule).
            CollectionAssert.AreEquivalent(Cells((2, 2), (4, 2)), Sight.CameraVision(stage, StageState.Initial(stage), 0, 0));
            var open = Board("#######", "#.....#", "#.....#", "#.....#", "#######"); open.Start = C(1, 3); open.Exit = C(5, 3);
            open.Cameras.Add(Camera(C(3, 0), Direction.South, Direction.South));
            var s2 = open.Build();
            var smoked = new StageState(C(1, 3), 0, false, 0, new[] { new SmokeCloud(C(3, 2), 2) }, null, 0, 0, -1, false, StageResult.Playing, null);
            CollectionAssert.AreEquivalent(Cells((2, 2), (4, 2)), Sight.CameraVision(s2, smoked, 0, 0), "smoke behaves like the wall: its node, the node behind it and the clipped diagonals");
        }

        [Test]
        public void Camera_TurnedIntoItsWall_DetectsNothing()
        {
            var b = Board("#######", "#.....#", "#.....#", "#.....#", "#######"); b.Start = C(1, 3); b.Exit = C(5, 3);
            b.Cameras.Add(Camera(C(3, 0), Direction.South, Direction.South, CameraKind.Rotating));
            var stage = b.Build();
            Assert.AreEqual(Direction.North, CameraTrack.FacingAt(stage.Cameras[0], 2));
            CollectionAssert.IsEmpty(Sight.CameraVision(stage, StageState.Initial(stage), 0, 2));
            CollectionAssert.IsNotEmpty(Sight.CameraVision(stage, StageState.Initial(stage), 0, 1), "facing W along the room is allowed");
        }

        [Test]
        public void WallCornerContact_BlocksDiagonalSmokeThrows()
        {
            var b = Board("...", ".#.", "..."); b.Start = C(0, 1); b.Exit = C(2, 2); b.SmokeCharges = 1;
            var stage = b.Build();
            var s = StageState.Initial(stage);
            Assert.IsFalse(Sight.CanThrowSmoke(stage, s, C(0, 1), C(1, 2)), "diagonal throw past the wall corner at (1,1)");
            Assert.IsTrue(Sight.CanThrowSmoke(stage, s, C(0, 1), C(0, 2)));
            Assert.IsTrue(Sight.CanThrowSmoke(stage, s, C(0, 1), C(0, 1)), "own node");
            var openBoard = Board("...", "...", "..."); openBoard.Start = C(0, 1); openBoard.Exit = C(2, 2); openBoard.SmokeCharges = 1;
            var o = openBoard.Build();
            Assert.IsTrue(Sight.CanThrowSmoke(o, StageState.Initial(o), C(0, 1), C(1, 2)), "no wall: diagonal throw allowed");
            Assert.IsFalse(Sight.CanThrowSmoke(o, StageState.Initial(o), C(0, 1), C(2, 1)), "outside the 3x3");
        }
    }
}
