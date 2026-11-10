using NUnit.Framework;
using ShadowSteps.Core;
using ShadowSteps.V3;
using static ShadowSteps.Tests.EditMode.V3.V3TestKit;

namespace ShadowSteps.Tests.EditMode.V3
{
    /// <summary>Handover §4: schedules, endpoint reversal, corner turning, NEXT markers, rotation direction.</summary>
    public class V3TrackTests
    {
        [Test]
        public void PingPong_ReachesEndNode_ThenReversesWithoutPause()
        {
            var g = PingPong(0, C(2, 1), C(2, 2), C(2, 3));
            Assert.AreEqual(4, GuardTrack.Period(g));
            CollectionAssert.AreEqual(new[] { C(2, 1), C(2, 2), C(2, 3), C(2, 2), C(2, 1), C(2, 2) },
                new[] { GuardTrack.PositionAt(g, 0), GuardTrack.PositionAt(g, 1), GuardTrack.PositionAt(g, 2), GuardTrack.PositionAt(g, 3), GuardTrack.PositionAt(g, 4), GuardTrack.PositionAt(g, 5) });
        }

        [Test]
        public void PingPong_FacesTravel_AndNextArrowPointsBackAtTheEndpoint()
        {
            var g = PingPong(0, C(2, 1), C(2, 2), C(2, 3));
            Assert.AreEqual(Direction.South, GuardTrack.PoseAt(g, 1).Facing, "travelling down");
            Assert.AreEqual(Direction.South, GuardTrack.PoseAt(g, 2).Facing, "at the end node the facing still points at the endpoint");
            Assert.AreEqual(Direction.North, GuardTrack.NextStep(g, 2), "NEXT marker already points back");
            Assert.AreEqual(Direction.North, GuardTrack.PoseAt(g, 3).Facing);
            Assert.AreEqual(Direction.North, GuardTrack.PoseAt(g, 0).Facing, "t=0: facing derived from the virtual previous node");
            Assert.AreEqual(Direction.South, GuardTrack.NextStep(g, 0));
        }

        [Test]
        public void PingPong_Offset_ShiftsTheSchedule()
        {
            var g = PingPong(1, C(2, 1), C(2, 2), C(2, 3));
            Assert.AreEqual(C(2, 2), GuardTrack.PositionAt(g, 0));
            Assert.AreEqual(Direction.South, GuardTrack.PoseAt(g, 0).Facing);
            Assert.AreEqual(C(2, 3), GuardTrack.PositionAt(g, 1));
        }

        [Test]
        public void Loop_TurnsAtCorners_AndNextArrowMatchesActualStep()
        {
            var g = Loop(0, C(3, 1), C(4, 1), C(4, 2), C(3, 2));
            Assert.AreEqual(4, GuardTrack.Period(g));
            Assert.AreEqual(Direction.North, GuardTrack.PoseAt(g, 0).Facing, "arrived from (3,2)");
            Assert.AreEqual(Direction.East, GuardTrack.NextStep(g, 0));
            Assert.AreEqual(Direction.East, GuardTrack.PoseAt(g, 1).Facing);
            Assert.AreEqual(Direction.South, GuardTrack.NextStep(g, 1), "corner: next step turns");
            Assert.AreEqual(Direction.South, GuardTrack.PoseAt(g, 2).Facing);
            Assert.AreEqual(Direction.West, GuardTrack.PoseAt(g, 3).Facing);
            Assert.AreEqual(C(3, 1), GuardTrack.PositionAt(g, 4), "wraps");
        }

        [Test]
        public void Rotating_Turns90EveryAction_ClockwiseOrCounterClockwise()
        {
            var cw = Rotating(C(2, 2), Direction.East);
            Assert.AreEqual(Direction.South, GuardTrack.PoseAt(cw, 1).Facing);
            Assert.AreEqual(Direction.West, GuardTrack.PoseAt(cw, 2).Facing);
            Assert.AreEqual(Direction.East, GuardTrack.PoseAt(cw, 4).Facing);
            var ccw = Rotating(C(2, 2), Direction.East, false);
            Assert.AreEqual(Direction.North, GuardTrack.PoseAt(ccw, 1).Facing);
            Assert.IsNull(GuardTrack.NextStep(cw, 0), "stationary guards have no NEXT step");
            Assert.AreEqual(4, GuardTrack.Period(cw));
            Assert.AreEqual(1, GuardTrack.Period(Static(C(0, 0), Direction.North)));
        }

        [Test]
        public void Cameras_RotateEveryAction_SweepHoldsTwoActions()
        {
            var rot = Camera(C(3, 0), Direction.South, Direction.South, CameraKind.Rotating);
            Assert.AreEqual(Direction.West, CameraTrack.FacingAt(rot, 1));
            Assert.AreEqual(Direction.North, CameraTrack.FacingAt(rot, 2));
            var sweep = Camera(C(3, 0), Direction.South, Direction.South, CameraKind.Sweeping, clockwise: false);
            Assert.AreEqual(Direction.South, CameraTrack.FacingAt(sweep, 0));
            Assert.AreEqual(Direction.South, CameraTrack.FacingAt(sweep, 1));
            Assert.AreEqual(Direction.East, CameraTrack.FacingAt(sweep, 2), "counter-clockwise neighbour facing");
            Assert.AreEqual(Direction.East, CameraTrack.FacingAt(sweep, 3));
            Assert.AreEqual(Direction.South, CameraTrack.FacingAt(sweep, 4));
            Assert.AreEqual(1, CameraTrack.Period(Camera(C(0, 0), Direction.South, Direction.South)));
            Assert.AreEqual(4, CameraTrack.Period(sweep));
        }

        [Test]
        public void SweepingCamera_ThreeFacings_PansOutAndBack_HoldingTwoActionsEach()
        {
            // D-073: user's example — a west-facing sweep should also cover north and east.
            var cam = new CameraSpec("cam", C(3, 0), Direction.South, Direction.West, CameraKind.Sweeping, clockwise: true, sweepFacings: 3);
            var expected = new[] { Direction.West, Direction.West, Direction.North, Direction.North, Direction.East, Direction.East, Direction.North, Direction.North, Direction.West, Direction.West };
            for (int t = 0; t < expected.Length; t++) Assert.AreEqual(expected[t], CameraTrack.FacingAt(cam, t), "t=" + t);
            Assert.AreEqual(8, CameraTrack.Period(cam));
            CollectionAssert.AreEqual(new[] { Direction.West, Direction.North, Direction.East }, new System.Collections.Generic.List<Direction>(cam.Facings()));
            var ccw = new CameraSpec("cam", C(3, 0), Direction.South, Direction.West, CameraKind.Sweeping, clockwise: false, sweepFacings: 3);
            Assert.AreEqual(Direction.South, CameraTrack.FacingAt(ccw, 2));
            Assert.AreEqual(Direction.East, CameraTrack.FacingAt(ccw, 4));
            Assert.AreEqual(Direction.South, CameraTrack.FacingAt(ccw, 6));
            // Default (2 facings) is unchanged: A A B B.
            var two = new CameraSpec("cam", C(3, 0), Direction.South, Direction.West, CameraKind.Sweeping);
            Assert.AreEqual(2, two.SweepFacings);
            Assert.AreEqual(Direction.North, CameraTrack.FacingAt(two, 2)); Assert.AreEqual(Direction.West, CameraTrack.FacingAt(two, 4));
        }

        [Test]
        public void StagePeriod_IsLcmOfGuardAndCameraCycles()
        {
            var b = Open(7, 5, C(0, 2), C(6, 2));
            b.Guards.Add(PingPong(0, C(2, 0), C(2, 1), C(2, 2)));      // period 4
            b.Guards.Add(Loop(0, C(4, 1), C(5, 1), C(5, 2), C(4, 2), C(4, 3), C(5, 3))); // 6 nodes: period 6
            Assert.AreEqual(12, b.Build().Period);
        }
    }
}
