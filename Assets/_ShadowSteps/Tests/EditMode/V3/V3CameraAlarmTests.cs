using NUnit.Framework;
using ShadowSteps.Core;
using ShadowSteps.V3;
using static ShadowSteps.Tests.EditMode.V3.V3TestKit;

namespace ShadowSteps.Tests.EditMode.V3
{
    /// <summary>Handover §5 and §13 "Cameras/alarms": lockdown budget, Ghost, win on the last countdown action.</summary>
    public class V3CameraAlarmTests
    {
        /// <summary>Room 5 wide, camera on the bottom wall at (3,4) looking north: cone lights (3,3) and (2..4, 2); start (1,3), exit (5,3) along row 3, so the second E step enters the cone.</summary>
        private static StageBuilder Room(int budget)
        {
            var b = Board("#######", "#.....#", "#.....#", "#.....#", "#######"); b.Start = C(1, 3); b.Exit = C(5, 3); b.AlarmBudget = budget;
            b.Cameras.Add(Camera(C(3, 4), Direction.North, Direction.North));
            return b;
        }

        [Test]
        public void CameraDetection_TriggersAlarmButDoesNotStopTheAction_OrTheEnemyUpdate()
        {
            var b = Room(5); b.Guards.Add(Rotating(C(1, 1), Direction.North));
            var stage = b.Build();
            var first = StageRules.TryApply(stage, StageState.Initial(stage), E); // into (2,3), dark (63° beside the lens node)
            Assert.IsTrue(first.Valid); Assert.IsFalse(first.AlarmTriggered);
            Assert.AreEqual(Direction.East, first.GuardsAfter[0].Facing);
            var o = StageRules.TryApply(stage, first.After, E); // into (3,3), the lens node: lit
            Assert.IsTrue(o.Valid); Assert.IsFalse(o.Caught); Assert.IsTrue(o.AlarmTriggered);
            Assert.AreEqual(5, o.After.Alarm); Assert.IsTrue(o.After.AlarmEver); Assert.AreEqual(2, o.After.T);
            Assert.AreEqual(Direction.South, o.GuardsAfter[0].Facing, "guards still updated on the trigger action");
        }

        [Test]
        public void Budget_CountsSubsequentMovesAndWaits_NotFreeGadgets_AndNeverRefills()
        {
            var b = Room(5); b.SmokeCharges = 1;
            var stage = b.Build();
            var s = StageState.Initial(stage);
            Step(stage, ref s, E); Assert.AreEqual(-1, s.Alarm, "(2,3) is dark");
            Step(stage, ref s, E); Assert.AreEqual(5, s.Alarm, "the trigger action (into the lens node (3,3)) grants the full budget");
            Step(stage, ref s, WAIT); Assert.AreEqual(4, s.Alarm, "WAIT counts");
            var free = Step(stage, ref s, StageAction.Smoke(C(2, 2)));
            Assert.IsTrue(free.Free); Assert.AreEqual(4, s.Alarm, "free deployment does not count");
            Step(stage, ref s, E); Assert.AreEqual(3, s.Alarm, "moving on counts; no refill");
            Step(stage, ref s, N); Assert.AreEqual(2, s.Alarm, "(4,2) is lit again: still no refill");
            Step(stage, ref s, N); Assert.AreEqual(1, s.Alarm, "leaving sight does not stop the countdown");
        }

        [Test]
        public void LastCountdownAction_OnTheExitWins_ElsewhereLocksDown()
        {
            // Trigger on the second E (lens node (3,3)); budget 2 then covers (4,3) and the exit.
            var win = Room(2).Build();
            var s = StageState.Initial(win);
            Step(win, ref s, E); Step(win, ref s, E); Step(win, ref s, E);
            Assert.AreEqual(1, s.Alarm);
            var o = Step(win, ref s, E);
            Assert.IsTrue(o.Won); Assert.AreEqual(0, s.Alarm); Assert.AreEqual(StageResult.Won, s.Result);

            var lose = Room(1).Build();
            s = StageState.Initial(lose);
            Step(lose, ref s, E); Step(lose, ref s, E);
            var last = Step(lose, ref s, E); // at (4,3), alarm reaches 0 off the exit
            Assert.IsTrue(last.Lockdown); Assert.AreEqual(StageResult.Lockdown, s.Result); Assert.AreEqual(0, s.Alarm);
            Assert.AreEqual(RejectReason.GameOver, StageRules.TryApply(lose, s, E).Reason);
        }

        [Test]
        public void CameraSeeingThePlayerOnTheExitMove_StillWins_ButForfeitsGhost()
        {
            var b = Board("#######", "#.....#", "#.....#", "#.....#", "#.....#", "#######"); b.Start = C(3, 4); b.Exit = C(3, 1); b.AlarmBudget = 4;
            b.Cameras.Add(Camera(C(3, 0), Direction.South, Direction.South));
            var stage = b.Build();
            var s = StageState.Initial(stage);
            Assert.IsFalse(Sight.AnyCameraSees(stage, s, 0, s.Player), "start (3,4) is at forward 3.5, beyond the cone's reach (D-053: reach 3)");
            Step(stage, ref s, N); Assert.IsTrue(s.AlarmEver);
            Step(stage, ref s, N); Assert.IsTrue(s.AlarmEver);
            var o = Step(stage, ref s, N);
            Assert.IsTrue(o.Won); Assert.IsTrue(s.AlarmEver);
        }

        [Test]
        public void Ghost_OfferedOnlyWithACameraAndAnAlarmFreeRoute()
        {
            // 7×6 room, camera on the bottom wall (3,5) facing N: with reach 3 (D-053) it lights rows 4, 3 and 2, so the Ghost route goes round by row 1.
            var tall = Board("#######", "#.....#", "#.....#", "#.....#", "#.....#", "#######"); tall.Start = C(1, 4); tall.Exit = C(5, 4); tall.AlarmBudget = 5; tall.Cameras.Add(Camera(C(3, 5), Direction.North, Direction.North));
            var ghost = StageMetrics.Analyze(tall.Build());
            Assert.AreEqual(4, ghost.Par); Assert.AreEqual(10, ghost.ShortestAlarmFree); Assert.IsTrue(ghost.GhostOffered); Assert.IsFalse(ghost.ForcedAlarm);
            Assert.IsFalse(StageMetrics.Analyze(Room(5).Build()).GhostOffered, "in the 7×5 room the cone reaches row 1: no alarm-free route");

            // Corridor: the camera lights (2,2), (1,2) and (0,2); every route is lit, so the alarm is forced. Budget 6 covers the actions after the trigger.
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
            // Corridor like LEVEL-23: the cone from (4,1.5) facing W lights (3,2), (2,2) and (1,2) (D-053: reach 3). Without EMP the alarm fires at (1,2) and the exit (5,2) is 4 actions away: budget 3 fails.
            // With EMP (fired at the start): (1,2) and (2,2) are crossed while disabled, the camera reactivates on (3,2) and the remaining 2 actions fit the budget.
            var corridor = Board("#######", "#######", "......#", "#######"); corridor.Start = C(0, 2); corridor.Exit = C(5, 2); corridor.AlarmBudget = 3;
            corridor.Cameras.Add(Camera(C(4, 1), Direction.South, Direction.West));
            var m = StageMetrics.Analyze(corridor.Build());
            Assert.IsFalse(m.Solvable, "budget 3 cannot be beaten without EMP: " + m);
            var withEmp = corridor; withEmp.EmpCharges = 1;
            var m2 = StageMetrics.Analyze(withEmp.Build());
            Assert.AreEqual(5, m2.Par); Assert.IsTrue(m2.EmpRequired); Assert.IsTrue(m2.ForcedAlarm);
        }
    }
}
