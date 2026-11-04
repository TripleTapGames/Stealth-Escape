using NUnit.Framework;
using ShadowSteps.Core;
using ShadowSteps.V3;
using static ShadowSteps.Tests.EditMode.V3.V3TestKit;

namespace ShadowSteps.Tests.EditMode.V3
{
    /// <summary>Handover §7 and §13 "Gadgets".</summary>
    public class V3GadgetTests
    {
        /// <summary>LEVEL-21 shape: static guard in a notch above a corridor, looking S onto (3,2) and (3,3 = wall).</summary>
        private static StageBuilder SmokeCorridor()
        {
            var b = Board("#######", "###.###", ".......", "#######"); b.Start = C(0, 2); b.Exit = C(6, 2); b.SmokeCharges = 1;
            b.Guards.Add(Static(C(3, 1), Direction.South));
            return b;
        }

        [Test]
        public void SmokeTargets_AreTheReachable3x3_IncludingOwnAndGuardNodes()
        {
            var open = Open(5, 5, C(2, 2), C(4, 4)); open.SmokeCharges = 1; open.Guards.Add(Static(C(3, 3), Direction.South));
            var stage = open.Build();
            var targets = StageRules.SmokeTargets(stage, StageState.Initial(stage));
            Assert.AreEqual(9, targets.Count);
            CollectionAssert.Contains(targets, C(2, 2)); CollectionAssert.Contains(targets, C(3, 3));
            var corridor = SmokeCorridor().Build();
            var s = StageState.Initial(corridor);
            Step(corridor, ref s, E); Step(corridor, ref s, E); // (2,2)
            CollectionAssert.AreEquivalent(Cells((1, 2), (2, 2), (3, 2)), StageRules.SmokeTargets(corridor, s), "walls above and below are not floor; (3,1) is around the corner");
        }

        [Test]
        public void BlockedThrow_IsRejectedWithoutLosingACharge()
        {
            var b = Board("...", ".#.", "..."); b.Start = C(0, 1); b.Exit = C(2, 2); b.SmokeCharges = 1;
            var stage = b.Build();
            var s = StageState.Initial(stage);
            var o = StageRules.TryApply(stage, s, StageAction.Smoke(C(1, 2)));
            Assert.IsFalse(o.Valid); Assert.AreEqual(RejectReason.BadSmokeTarget, o.Reason); Assert.AreEqual(1, s.SmokeCharges);
            Assert.AreEqual(RejectReason.BadSmokeTarget, StageRules.TryApply(stage, s, StageAction.Smoke(C(1, 1))).Reason, "wall node");
            var ok = StageRules.TryApply(stage, s, StageAction.Smoke(C(0, 2)));
            Assert.IsTrue(ok.Valid); Assert.AreEqual(0, ok.After.SmokeCharges);
            Assert.AreEqual(RejectReason.NoSmokeCharge, StageRules.TryApply(stage, ok.After, StageAction.Smoke(C(0, 0))).Reason);
            Assert.AreEqual(RejectReason.BadSmokeTarget, StageRules.TryApply(stage, new StageState(C(0, 1), 0, false, 0, ok.After.Clouds, null, 1, 0, -1, false, StageResult.Playing, null), StageAction.Smoke(C(0, 2))).Reason, "cannot overwrite an active cloud");
        }

        [Test]
        public void Smoke_IsFree_ProtectsTwoCountedActions_ThenExposesOnTheSameAction()
        {
            var stage = SmokeCorridor().Build();
            var s = StageState.Initial(stage);
            Step(stage, ref s, E); Step(stage, ref s, E); // (2,2), t=2
            Assert.IsTrue(StageRules.TryApply(stage, s, E).Caught, "without smoke (3,2) is lit");
            var deploy = Step(stage, ref s, StageAction.Smoke(C(3, 2)));
            Assert.IsTrue(deploy.Free); Assert.AreEqual(2, s.T); Assert.AreEqual(2, s.SmokeLeftAt(C(3, 2)));
            var first = Step(stage, ref s, E); // protected action 1
            Assert.IsFalse(first.Caught); Assert.AreEqual(1, s.SmokeLeftAt(C(3, 2)));
            var second = Step(stage, ref s, WAIT); // protected action 2: safe during the checks, exposed after expiry
            Assert.IsTrue(second.Caught);
            Assert.AreEqual(CapturePhase.AfterGadgetExpiry, second.CapturePhase);
            Assert.IsTrue(second.SmokeExpired);
            Assert.AreEqual(0, s.SmokeLeftAt(C(3, 2)));
        }

        [Test]
        public void Smoke_DoesNotPreventCollisions()
        {
            var b = Open(6, 4, C(1, 2), C(5, 2)); b.SmokeCharges = 1; b.Guards.Add(PingPong(0, C(2, 1), C(2, 2)));
            var stage = b.Build();
            var s = StageState.Initial(stage);
            Step(stage, ref s, StageAction.Smoke(C(2, 2)));
            var o = Step(stage, ref s, E);
            Assert.IsTrue(o.Caught); Assert.AreEqual(CaptureCause.Collision, o.CaptureCause);
        }

        [Test]
        public void WinningOnTheSecondProtectedAction_PrecedesTheExpiryCheck()
        {
            var b = Board("#######", "###.###", ".......", "#######"); b.Start = C(1, 2); b.Exit = C(3, 2); b.SmokeCharges = 1;
            b.Guards.Add(Static(C(3, 1), Direction.South));
            var stage = b.Build();
            var s = StageState.Initial(stage);
            Step(stage, ref s, StageAction.Smoke(C(2, 2)));
            Step(stage, ref s, StageAction.Smoke(C(3, 2))); // only one charge: invalid, ignored
            Assert.AreEqual(0, s.SmokeCharges);
            Step(stage, ref s, E);               // protected 1 at (2,2)
            // (3,2) is lit, but the move to the exit happens while the cloud at (2,2)... does not cover (3,2): use a second fixture below.
            var b2 = Board("#######", "###.###", ".......", "#######"); b2.Start = C(1, 2); b2.Exit = C(3, 2); b2.SmokeCharges = 1;
            b2.Guards.Add(Static(C(3, 1), Direction.South));
            var st2 = b2.Build();
            var s2 = StageState.Initial(st2);
            Step(st2, ref s2, StageAction.Smoke(C(2, 2)));
            Step(st2, ref s2, E);                // protected 1, now at (2,2) next to the exit
            var s3 = new StageState(s2.Player, s2.T, false, 0, new[] { new SmokeCloud(C(3, 2), 1) }, null, 0, 0, -1, false, StageResult.Playing, null);
            var o = StageRules.TryApply(st2, s3, E); // cloud on the exit with one action left: win first, expiry never checked
            Assert.IsTrue(o.Won); Assert.AreEqual(StageResult.Won, o.After.Result);
        }

        [Test]
        public void Emp_DisablesOneCameraForTwoActions_ThenReactivationCanTriggerTheAlarm()
        {
            var b = Board("#######", "#######", ".....##", "#######"); b.Start = C(0, 2); b.Exit = C(4, 2); b.EmpCharges = 1; b.AlarmBudget = 1;
            b.Cameras.Add(Camera(C(4, 1), Direction.South, Direction.West));
            var stage = b.Build();
            var s = StageState.Initial(stage);
            CollectionAssert.AreEquivalent(Cells((3, 2), (2, 2)), Sight.CameraVision(stage, s, 0, 0), "cone from (4,1.5) facing W: forward 1 and 2 along the corridor; (4,2) is beside the origin");
            Step(stage, ref s, E); // (1,2) dark
            var emp = Step(stage, ref s, StageAction.Emp(0));
            Assert.IsTrue(emp.Free); Assert.AreEqual(0, s.EmpCharges); Assert.AreEqual(2, s.EmpLeftOn(0)); Assert.AreEqual(1, s.T);
            Assert.AreEqual(RejectReason.NoEmpCharge, StageRules.TryApply(stage, s, StageAction.Emp(0)).Reason);
            Assert.AreEqual(RejectReason.CameraAlreadyDisabled, StageRules.TryApply(stage, new StageState(s.Player, s.T, false, 0, null, s.Emps, 0, 1, -1, false, StageResult.Playing, null), StageAction.Emp(0)).Reason);
            var a1 = Step(stage, ref s, E); Assert.IsFalse(a1.AlarmTriggered); Assert.AreEqual(1, s.EmpLeftOn(0));   // (2,2) protected
            var a2 = Step(stage, ref s, WAIT);                                                                      // still (2,2): expiry recheck
            Assert.IsTrue(a2.EmpExpired); Assert.IsTrue(a2.AlarmTriggered); Assert.AreEqual(1, s.Alarm); Assert.IsFalse(s.IsCameraDisabled(0));
        }

        [Test]
        public void MultipleFreeGadgets_BetweenCountedActions_DoNotAdvanceTheClock()
        {
            var b = Room(); var stage = b.Build();
            var s = StageState.Initial(stage);
            Step(stage, ref s, StageAction.Smoke(C(1, 2)));
            Step(stage, ref s, StageAction.Emp(0));
            Assert.AreEqual(0, s.T); Assert.AreEqual(0, s.SmokeCharges); Assert.AreEqual(0, s.EmpCharges);
            Assert.AreEqual(1, s.Clouds.Count); Assert.AreEqual(1, s.Emps.Count);
        }

        private static StageBuilder Room()
        {
            var b = Board("#######", "#.....#", "#.....#", "#.....#", "#######"); b.Start = C(1, 3); b.Exit = C(5, 3); b.SmokeCharges = 1; b.EmpCharges = 1;
            b.Cameras.Add(Camera(C(3, 0), Direction.South, Direction.South));
            return b;
        }
    
        [Test]
        public void EmpExpiry_TriggersTheAlarmWithTheFullBudget_AndTheExitOnTheLastPermittedActionWins()
        {
            // Corridor ".......#": camera on (4,1) facing W lights (3,2),(2,2) under the cone. Budget 1.
            // EMP at (1,2); E,E → (3,2) is the action on which the EMP expires and the camera rechecks: the alarm must start at the FULL budget (1),
            // not already decremented by that same action; then E → (4,2) uses the single permitted action (alarm → 0 off the exit = lockdown),
            // whereas with the exit one node nearer the last permitted action lands on it and wins.
            var b = Board("#######", "#######", "......#", "#######"); b.Start = C(0, 2); b.Exit = C(5, 2); b.EmpCharges = 1; b.AlarmBudget = 1;
            b.Cameras.Add(Camera(C(4, 1), Direction.South, Direction.West));
            var stage = b.Build();
            var s = StageState.Initial(stage);
            Step(stage, ref s, E); Step(stage, ref s, StageAction.Emp(0)); Step(stage, ref s, E); // (2,2) protected, 1 EMP action left
            var expiry = Step(stage, ref s, E); // (3,2): EMP expires on this action and the camera sees the player
            Assert.IsTrue(expiry.EmpExpired); Assert.IsTrue(expiry.AlarmTriggered); Assert.AreEqual(1, s.Alarm, "expiry-triggered alarm starts with the full budget; the triggering action does not consume it");
            Assert.AreEqual(StageResult.Playing, s.Result);
            var last = Step(stage, ref s, E); // (4,2): the one permitted action, not the exit
            Assert.IsTrue(last.Lockdown); Assert.AreEqual(0, s.Alarm); Assert.AreEqual(StageResult.Lockdown, s.Result);

            var near = Board("#######", "#######", ".....##", "#######"); near.Start = C(0, 2); near.Exit = C(4, 2); near.EmpCharges = 1; near.AlarmBudget = 1;
            near.Cameras.Add(Camera(C(4, 1), Direction.South, Direction.West)); // lights (3,2),(2,2)
            var ns = near.Build(); var t = StageState.Initial(ns);
            Step(ns, ref t, E); Step(ns, ref t, StageAction.Emp(0)); Step(ns, ref t, E);
            var ex = Step(ns, ref t, E); Assert.IsTrue(ex.EmpExpired && ex.AlarmTriggered); Assert.AreEqual(1, t.Alarm);
            var win = Step(ns, ref t, E); // (4,2) is the exit: reached on the last permitted action
            Assert.IsTrue(win.Won); Assert.AreEqual(0, t.Alarm); Assert.AreEqual(StageResult.Won, t.Result);
        }
}
}
