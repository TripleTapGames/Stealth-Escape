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
        public void Camera_Cone70Degrees_CoversTheLensNodeAndThreeNodesOfTheSecondRow()
        {
            // Cone rule (2026-11-03): origin on the wall face (3, 0.5), 35° half-angle, forward reach 2. Lens node (3,1) at forward 0.5;
            // its neighbours (2,1)/(4,1) sit at 63° and are outside; the second row (2..4, 2) is at forward 1.5 and ≤ 33.7°; (3,3) is at forward 2.5.
            var b = Board("#######", "#.....#", "#.....#", "#.....#", "#######"); b.Start = C(1, 3); b.Exit = C(5, 3);
            b.Cameras.Add(Camera(C(3, 0), Direction.South, Direction.South));
            var stage = b.Build();
            Assert.AreEqual(70.0, Sight.CameraFovDegrees, 1e-9); Assert.AreEqual(2.0, Sight.CameraRange, 1e-9);
            CollectionAssert.AreEquivalent(Cells((3, 1), (2, 2), (3, 2), (4, 2)), Sight.CameraVision(stage, StageState.Initial(stage), 0, 0));
            Assert.IsTrue(Sight.AnyCameraSees(stage, StageState.Initial(stage), 0, C(3, 1)));
            Assert.IsFalse(Sight.AnyCameraSees(stage, StageState.Initial(stage), 0, C(3, 3)), "forward 2.5 exceeds the 2-spacing reach");
            Assert.IsFalse(Sight.AnyCameraSees(stage, StageState.Initial(stage), 0, C(2, 1)), "63° beside the lens is outside the 35° half-angle");
        }

        [Test]
        public void Camera_ConeIsOccludedPerNode_ByWallsAndSmoke()
        {
            var b = Board("#######", "#.....#", "#..#..#", "#.....#", "#######"); b.Start = C(1, 3); b.Exit = C(5, 3);
            b.Cameras.Add(Camera(C(3, 0), Direction.South, Direction.South));
            var stage = b.Build();
            // The wall at (3,2) hides itself; the lens node (3,1) and the side nodes (2,2),(4,2) stay visible (rays to them pass beside the wall).
            CollectionAssert.AreEquivalent(Cells((3, 1), (2, 2), (4, 2)), Sight.CameraVision(stage, StageState.Initial(stage), 0, 0));
            var open = Board("#######", "#.....#", "#.....#", "#.....#", "#######"); open.Start = C(1, 3); open.Exit = C(5, 3);
            open.Cameras.Add(Camera(C(3, 0), Direction.South, Direction.South));
            var s2 = open.Build();
            var smokedSide = new StageState(C(1, 3), 0, false, 0, new[] { new SmokeCloud(C(3, 2), 2) }, null, 0, 0, -1, false, StageResult.Playing, null);
            CollectionAssert.AreEquivalent(Cells((3, 1), (2, 2), (4, 2)), Sight.CameraVision(s2, smokedSide, 0, 0), "smoke behaves like the wall: its node is hidden, the rest of the cone stays");
            var smokedLens = new StageState(C(1, 3), 0, false, 0, new[] { new SmokeCloud(C(3, 1), 2) }, null, 0, 0, -1, false, StageResult.Playing, null);
            CollectionAssert.IsEmpty(Sight.CameraVision(s2, smokedLens, 0, 0), "a cloud on the lens node blocks every ray out of the cone");
        }

        [Test]
        public void Camera_Smoke_HidesTheCone_AndExpiryRestoresIt()
        {
            var b = Board("#######", "#.....#", "#.....#", "#.....#", "#######"); b.Start = C(2, 1); b.Exit = C(5, 3); b.SmokeCharges = 1;
            b.Cameras.Add(Camera(C(3, 0), Direction.South, Direction.South));
            var stage = b.Build();
            var s = StageState.Initial(stage);
            Assert.IsFalse(Sight.AnyCameraSees(stage, s, 0, s.Player), "(2,1) is beside the lens, outside the 35° half-angle");
            var thrown = StageRules.TryApply(stage, s, StageAction.Smoke(C(3, 1))); Assert.IsTrue(thrown.Valid); s = thrown.After;
            CollectionAssert.IsEmpty(Sight.CameraVision(stage, s, 0, s.T), "cloud on the lens node");
            int guard = 0;
            while (s.Clouds.Count > 0 && guard++ < 6) { var o = StageRules.TryApply(stage, s, StageAction.Wait); Assert.IsTrue(o.Valid); s = o.After; }
            Assert.AreEqual(0, s.Clouds.Count, "smoke expires after its counted actions");
            CollectionAssert.AreEquivalent(Cells((3, 1), (2, 2), (3, 2), (4, 2)), Sight.CameraVision(stage, s, 0, s.T), "cone restored after expiry");
        }

        [Test]
        public void Camera_ConeBoundaries_UseTheHalfAngle_TheForwardReach_AndATolerance()
        {
            var o = new Vec2(0, 0); double tan35 = System.Math.Tan(35.0 * System.Math.PI / 180.0);
            Assert.IsTrue(Sight.InCameraCone(o, Direction.South, new Vec2(tan35, 1.0)), "exactly on the 35° edge counts as inside");
            Assert.IsTrue(Sight.InCameraCone(o, Direction.South, new Vec2(-tan35, 1.0)), "symmetric on the other side");
            Assert.IsFalse(Sight.InCameraCone(o, Direction.South, new Vec2(tan35 + 1e-3, 1.0)), "just past the half-angle is outside");
            Assert.IsTrue(Sight.InCameraCone(o, Direction.South, new Vec2(0, 2.0)), "forward 2.0 is the last reachable projection");
            Assert.IsTrue(Sight.InCameraCone(o, Direction.South, new Vec2(0, 2.0 + 1e-7)), "within the tolerance");
            Assert.IsFalse(Sight.InCameraCone(o, Direction.South, new Vec2(0, 2.01)), "beyond the reach");
            Assert.IsFalse(Sight.InCameraCone(o, Direction.South, new Vec2(0.1, 0)), "zero forward projection (beside the origin) is outside");
            Assert.IsFalse(Sight.InCameraCone(o, Direction.South, new Vec2(0, -1)), "behind the camera");
            Assert.IsTrue(Sight.InCameraCone(o, Direction.East, new Vec2(1.5, -1)), "East: forward is +x, lateral is y");
            Assert.IsTrue(Sight.InCameraCone(o, Direction.North, new Vec2(1, -1.5)), "North: forward is −y");
            Assert.IsTrue(Sight.InCameraCone(o, Direction.West, new Vec2(-1.5, 1)), "West: forward is −x");
        }

        [Test]
        public void Camera_Cone_FromEveryWallFace_AndEveryFacing()
        {
            // 7×7 room; a camera on each wall looking inward sees the lens node plus three nodes of the second row.
            string[] rows = { "#######", "#.....#", "#.....#", "#.....#", "#.....#", "#.....#", "#######" };
            void Check(GridCoord wall, Direction face, (int, int)[] expected)
            {
                var b = Board(rows); b.Start = C(1, 1); b.Exit = C(5, 5); b.Cameras.Add(Camera(wall, face, face));
                var stage = b.Build();
                CollectionAssert.AreEquivalent(Cells(expected), Sight.CameraVision(stage, StageState.Initial(stage), 0, 0), wall + " " + face);
            }
            Check(C(3, 0), Direction.South, new[] { (3, 1), (2, 2), (3, 2), (4, 2) });
            Check(C(3, 6), Direction.North, new[] { (3, 5), (2, 4), (3, 4), (4, 4) });
            Check(C(0, 3), Direction.East, new[] { (1, 3), (2, 2), (2, 3), (2, 4) });
            Check(C(6, 3), Direction.West, new[] { (5, 3), (4, 2), (4, 3), (4, 4) });
            // Sideways along the wall (mount S, facing E): the two nodes along the wall at forward 1 and 2, lateral ½ (26.6°); nothing further out.
            var side = Board(rows); side.Start = C(1, 5); side.Exit = C(5, 5); side.Cameras.Add(Camera(C(3, 0), Direction.South, Direction.East));
            var ss = side.Build();
            CollectionAssert.AreEquivalent(Cells((4, 1), (5, 1)), Sight.CameraVision(ss, StageState.Initial(ss), 0, 0));
            // Facing straight into its own wall: every node has a negative forward projection.
            var into = Board(rows); into.Start = C(1, 5); into.Exit = C(5, 5); into.Cameras.Add(Camera(C(3, 0), Direction.South, Direction.North));
            var si = into.Build();
            CollectionAssert.IsEmpty(Sight.CameraVision(si, StageState.Initial(si), 0, 0));
        }

        [Test]
        public void Camera_ClosedDoorBlocksTheCone_UnlockedDoorDoesNot()
        {
            var b = Board("#######", "#.....#", "#.....#", "#.....#", "#######"); b.Start = C(1, 3); b.Exit = C(5, 3); b.Key = C(1, 1);
            b.Doors.Add(new DoorSpec(C(3, 1), C(3, 2)));
            b.Cameras.Add(Camera(C(3, 0), Direction.South, Direction.South));
            var stage = b.Build();
            var closed = StageState.Initial(stage);
            CollectionAssert.AreEquivalent(Cells((3, 1), (2, 2), (4, 2)), Sight.CameraVision(stage, closed, 0, 0), "the closed door across (3,1)-(3,2) hides (3,2); the diagonal rays pass beside it");
            var opened = new StageState(C(1, 3), 0, true, 1, null, null, 0, 0, -1, false, StageResult.Playing, null);
            CollectionAssert.AreEquivalent(Cells((3, 1), (2, 2), (3, 2), (4, 2)), Sight.CameraVision(stage, opened, 0, 0), "an unlocked door no longer blocks vision");
        }

        [Test]
        public void Cone_FarEdgeCurvature_SectorAtOne_PlaneAtZero_NeverShorterThanTheSector()
        {
            double a = 35.0 * System.Math.PI / 180.0;
            Assert.AreEqual(2.0, Sight.FarEdgeRayLength(2.0, a, 1.0), 1e-9, "curvature 1: circular sector, edge ray = reach");
            Assert.AreEqual(2.0, Sight.FarEdgeRayLength(2.0, 0.0, 0.5), 1e-9, "the central ray is always the reach");
            Assert.AreEqual(2.0 / System.Math.Cos(a), Sight.FarEdgeRayLength(2.0, a, 0.0), 1e-9, "curvature 0: straight far edge (forward plane)");
            double prev = 2.0;
            foreach (var k in new[] { 0.9, 0.7, 0.5, 0.3, 0.1 }) { double len = Sight.FarEdgeRayLength(2.0, a, k); Assert.GreaterOrEqual(len, prev - 1e-12, "flatter edges only lengthen the edge rays"); Assert.LessOrEqual(len, 2.0 / System.Math.Cos(a) + 1e-9); prev = len; }
            // A flattened outline still contains the detected nodes: it is never inside the sector.
            var b = Board("#######", "#.....#", "#.....#", "#.....#", "#######"); b.Start = C(1, 3); b.Exit = C(5, 3);
            b.Cameras.Add(Camera(C(3, 0), Direction.South, Direction.South));
            var stage = b.Build();
            var flat = Sight.CameraConeOutline(stage, 0, null, stage.Cameras[0], Direction.South, 48, 0.0);
            var round = Sight.CameraConeOutline(stage, 0, null, stage.Cameras[0], Direction.South, 48, 1.0);
            for (int i = 1; i < 50; i++)
            {
                double df = System.Math.Sqrt((flat[i].X - 3) * (flat[i].X - 3) + (flat[i].Y - 0.5) * (flat[i].Y - 0.5));
                double dr = System.Math.Sqrt((round[i].X - 3) * (round[i].X - 3) + (round[i].Y - 0.5) * (round[i].Y - 0.5));
                Assert.GreaterOrEqual(df, dr - 1e-9);
            }
            Assert.AreEqual(2.5, flat[1].Y, 1e-9, "flat: edge rays end on the forward plane");
        }

        [Test]
        public void Camera_ConeOutline_StartsAtTheWallFace_AndIsClippedByOccluders()
        {
            var b = Board("#######", "#.....#", "#..#..#", "#.....#", "#######"); b.Start = C(1, 3); b.Exit = C(5, 3);
            b.Cameras.Add(Camera(C(3, 0), Direction.South, Direction.South));
            var stage = b.Build();
            var pts = Sight.CameraConeOutline(stage, 0, null, stage.Cameras[0], Direction.South, 48);
            Assert.AreEqual(50, pts.Count);
            Assert.AreEqual(3.0, pts[0].X, 1e-9); Assert.AreEqual(0.5, pts[0].Y, 1e-9);
            double Dist(Vec2 p) => System.Math.Sqrt((p.X - 3.0) * (p.X - 3.0) + (p.Y - 0.5) * (p.Y - 0.5));
            foreach (var p in pts) { Assert.LessOrEqual(Dist(p), Sight.CameraRange + 1e-6, "never past the sector radius"); Assert.GreaterOrEqual(p.Y, 0.5 - 1e-9, "never behind the wall face"); }
            var centre = pts[1 + 24];
            Assert.AreEqual(3.0, centre.X, 1e-9); Assert.AreEqual(1.5, centre.Y, 1e-9, "the central ray stops at the wall (3,2)'s top edge");
            var first = pts[1]; var last = pts[49];
            Assert.AreEqual(Sight.CameraRange, Dist(first), 1e-9, "the ±35° edge rays reach the sector arc (curved far edge)");
            Assert.AreEqual(Sight.CameraRange, Dist(last), 1e-9);
            double halfWidth = 2.0 * System.Math.Sin(35.0 * System.Math.PI / 180.0);
            Assert.AreEqual(halfWidth, System.Math.Abs(first.X - 3.0), 1e-9); Assert.AreEqual(halfWidth, System.Math.Abs(last.X - 3.0), 1e-9);
            Assert.AreEqual(-1.0, System.Math.Sign(first.X - 3.0) * System.Math.Sign(last.X - 3.0), "the two edge rays are on opposite sides");
            var clear = Board("#######", "#.....#", "#.....#", "#.....#", "#######"); clear.Start = C(1, 3); clear.Exit = C(5, 3);
            clear.Cameras.Add(Camera(C(3, 0), Direction.South, Direction.South));
            var cs = clear.Build();
            var open = Sight.CameraConeOutline(cs, 0, null, cs.Cameras[0], Direction.South, 48);
            foreach (var p in open.GetRange(1, 49)) Assert.AreEqual(Sight.CameraRange, Dist(p), 1e-9, "unobstructed: every ray ends on the arc of radius 2");
            Assert.AreEqual(2.5, open[1 + 24].Y, 1e-9, "the central ray reaches forward 2 exactly");
            // Sideways camera: the node at forward 2 / lateral ½ (distance 2.06) is detected, so the sector widens to keep it inside.
            var side = Board("#######", "#.....#", "#.....#", "#.....#", "#######"); side.Start = C(1, 3); side.Exit = C(5, 3);
            side.Cameras.Add(Camera(C(3, 0), Direction.South, Direction.East));
            var ss = side.Build();
            var so = Sight.CameraConeOutline(ss, 0, null, ss.Cameras[0], Direction.East, 48);
            double rad = 0; foreach (var p in so.GetRange(1, 49)) rad = System.Math.Max(rad, Dist(p));
            Assert.AreEqual(System.Math.Sqrt(4.0 + 0.25), rad, 1e-5, "radius widened to the farthest detected node");
            var smoked = Sight.CameraConeOutline(cs, 0, new[] { new SmokeCloud(C(3, 1), 2) }, cs.Cameras[0], Direction.South, 48);
            Assert.AreEqual(0.5, smoked[1 + 24].Y, 1e-9, "smoke on the lens node clips the central ray at the origin");
        }

        [Test]
        public void Camera_TurnedIntoItsWall_SeesNothing_AndSidewaysSeesAlongTheWall()
        {
            var b = Board("#######", "#.....#", "#.....#", "#.....#", "#######"); b.Start = C(1, 3); b.Exit = C(5, 3);
            b.Cameras.Add(Camera(C(3, 0), Direction.South, Direction.South, CameraKind.Rotating));
            var stage = b.Build();
            Assert.AreEqual(Direction.North, CameraTrack.FacingAt(stage.Cameras[0], 2));
            CollectionAssert.IsEmpty(Sight.CameraVision(stage, StageState.Initial(stage), 0, 2), "facing into its own wall: no node has a positive forward projection");
            Assert.AreEqual(Direction.West, CameraTrack.FacingAt(stage.Cameras[0], 1));
            CollectionAssert.AreEquivalent(Cells((2, 1), (1, 1)), Sight.CameraVision(stage, StageState.Initial(stage), 0, 1), "facing W along the wall: the two nodes along it");
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
