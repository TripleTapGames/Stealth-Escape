using NUnit.Framework;
using ShadowSteps.Core;
using ShadowSteps.V3;
using static ShadowSteps.Tests.EditMode.V3.V3TestKit;

namespace ShadowSteps.Tests.EditMode.V3
{
    /// <summary>Handover §5 and §13 "Cameras/alarms": lockdown budget, Ghost, win on the last countdown action.</summary>
    public class V3CameraAlarmTests
    {
        /// <summary>Room 5 wide, camera on the top wall facing S covers (2..4, 2..3); start (1,3), exit (5,3) along the lit row.</summary>
        private static StageBuilder Room(int budget)
        {
            var b = Board("#######", "#.....#", "#.....#", "#.....#", "#######"); b.Start = C(1, 3); b.Exit = C(5, 3); b.AlarmBudget = budget;
            b.Cameras.Add(Camera(C(3, 0), Direction.South, Direction.South));
            return b;
        }

        [Test]
        public void CameraDetection_TriggersAlarmButDoesNotStopTheAction_OrTheEnemyUpdate()
        {
            var b = Room(5); b.Guards.Add(Rotating(C(1, 1), Direction.North));
            var stage = b.Build();
            var o = StageRules.TryApply(stage, StageState.Initial(stage), E); // into (2,3), lit
            Assert.IsTrue(o.Valid); Assert.IsFalse(o.Caught); Assert.IsTrue(o.AlarmTriggered);
            Assert.AreEqual(5, o.After.Alarm); Assert.IsTrue(o.After.AlarmEver); Assert.AreEqual(1, o.After.T);
            Assert.AreEqual(Direction.East, o.GuardsAfter[0].Facing, "guards still updated on the trigger action");
        }

        [Test]
        public void Budget_CountsSubsequentMovesAndWaits_NotFreeGadgets_AndNeverRefills()
        {
            var b = Room(5); b.SmokeCharges = 1;
            var stage = b.Build();
            var s = StageState.Initial(stage);
            Step(stage, ref s, E); Assert.AreEqual(5, s.Alarm, "the trigger action grants the full budget");
            Step(stage, ref s, WAIT); Assert.AreEqual(4, s.Alarm, "WAIT counts");
            var free = Step(stage, ref s, StageAction.Smoke(C(1, 2)));
            Assert.IsTrue(free.Free); Assert.AreEqual(4, s.Alarm, "free deployment does not count");
            Step(stage, ref s, E); Assert.AreEqual(3, s.Alarm, "still in sight: no refill");
            Step(stage, ref s, N); Step(stage, ref s, N); Assert.AreEqual(1, s.Alarm, "leaving sight does not stop the countdown");
        }

        [Test]
        public void LastCountdownAction_OnTheExitWins_ElsewhereLocksDown()
        {
            var win = Room(3).Build();
            var s = StageState.Initial(win);
            Step(win, ref s, E); Step(win, ref s, E); Step(win, ref s, E);
            Assert.AreEqual(1, s.Alarm);
            var o = Step(win, ref s, E);
            Assert.IsTrue(o.Won); Assert.AreEqual(0, s.Alarm); Assert.AreEqual(StageResult.Won, s.Result);

            var lose = Room(2).Build();
            s = StageState.Initial(lose);
            Step(lose, ref s, E); Step(lose, ref s, E);
            var last = Step(lose, ref s, E); // at (4,3), alarm reaches 0 off the exit
            Assert.IsTrue(last.Lockdown); Assert.AreEqual(StageResult.Lockdown, s.Result); Assert.AreEqual(0, s.Alarm);
            Assert.AreEqual(RejectReason.GameOver, StageRules.TryApply(lose, s, E).Reason);
        }

        [Test]
        public void CameraSeeingThePlayerOnTheExitMove_StillWins_ButForfeitsGhost()
        {
            var b = Board("#######", "#.....#", "#.....#", "#.....#", "#######"); b.Start = C(3, 1); b.Exit = C(3, 3); b.AlarmBudget = 4;
            b.Cameras.Add(Camera(C(3, 0), Direction.South, Direction.South));
            var stage = b.Build();
            var s = StageState.Initial(stage);
            Assert.IsFalse(Sight.AnyCameraSees(stage, s, 0, s.Player), "start is in the blind spot in front of the lens");
            Step(stage, ref s, S); Assert.IsTrue(s.AlarmEver);
            var o = Step(stage, ref s, S);
            Assert.IsTrue(o.Won); Assert.IsTrue(s.AlarmEver);
        }

        [Test]
        public void Ghost_OfferedOnlyWithACameraAndAnAlarmFreeRoute()
        {
            var ghost = StageMetrics.Analyze(Room(5).Build());
            Assert.AreEqual(4, ghost.Par); Assert.AreEqual(8, ghost.ShortestAlarmFree); Assert.IsTrue(ghost.GhostOffered); Assert.IsFalse(ghost.ForcedAlarm);

            // Corridor: the camera lights (1,2) and (2,2); every route is lit, so the alarm is forced. Budget 6 covers the 5 actions after the trigger.
            var corridor = Board("#######", "#######", ".......", "#######"); corridor.Start = C(0, 2); corridor.Exit = C(6, 2); corridor.AlarmBudget = 6;
            corridor.Cameras.Add(Camera(C(3, 1), Direction.South, Direction.West));
            var forced = StageMetrics.Analyze(corridor.Build());
            Assert.IsTrue(forced.Solvable); Assert.IsTrue(forced.ForcedAlarm); Assert.IsFalse(forced.GhostOffered);

            var noCamera = StageMetrics.Analyze(Open(4, 1, C(0, 0), C(3, 0)).Build());
            Assert.IsFalse(noCamera.GhostOffered); Assert.IsFalse(noCamera.ForcedAlarm);
        }

        [Test]
        public void RotatingAndSweepingCameras_FollowTheClock_WhileDisabledOnesKeepTurning()
        {
            var b = Room(4); b.Cameras.Clear(); b.Cameras.Add(Camera(C(3, 0), Direction.South, Direction.South, CameraKind.Rotating)); b.EmpCharges = 1;
            var stage = b.Build();
            var s = StageState.Initial(stage);
            Step(stage, ref s, StageAction.Emp(0));
            Assert.IsTrue(s.IsCameraDisabled(0));
            CollectionAssert.IsEmpty(Sight.CameraVision(stage, s, 0, s.T));
            CollectionAssert.IsNotEmpty(Sight.CameraVision(stage, s, 0, s.T, ignoreDisabled: true), "footprint still computed for the inactive overlay");
            Step(stage, ref s, WAIT);
            Assert.AreEqual(Direction.West, CameraTrack.FacingAt(stage.Cameras[0], s.T), "schedule unaffected by EMP");
        }

        [Test]
        public void ForcedAlarmBudget_RequiresTheIntendedNumberOfSubsequentActions()
        {
            // Corridor level like LEVEL-23 without EMP: alarm at the first lit node, 2 more actions needed than budget 1 allows.
            var corridor = Board("#######", "#######", ".....##", "#######"); corridor.Start = C(0, 2); corridor.Exit = C(4, 2); corridor.AlarmBudget = 1;
            corridor.Cameras.Add(Camera(C(4, 1), Direction.South, Direction.West));
            var m = StageMetrics.Analyze(corridor.Build());
            Assert.IsFalse(m.Solvable, "budget 1 cannot be beaten without EMP: " + m);
            var withEmp = corridor; withEmp.EmpCharges = 1;
            var m2 = StageMetrics.Analyze(withEmp.Build());
            Assert.AreEqual(4, m2.Par); Assert.IsTrue(m2.EmpRequired);
        }
    }
}
