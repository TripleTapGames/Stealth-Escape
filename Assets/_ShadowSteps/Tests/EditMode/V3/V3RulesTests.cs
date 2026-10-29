using NUnit.Framework;
using ShadowSteps.Core;
using ShadowSteps.V3;
using static ShadowSteps.Tests.EditMode.V3.V3TestKit;

namespace ShadowSteps.Tests.EditMode.V3
{
    /// <summary>Handover §3.1 and §13 "Player/guards" and "Doors/visibility" fixtures.</summary>
    public class V3RulesTests
    {
        [Test]
        public void InvalidMove_CostsNothingAndChangesNothing()
        {
            var b = Open(6, 4, C(0, 2), C(5, 2)); b.Walls.Add(C(1, 2));
            var stage = b.Build();
            var s = StageState.Initial(stage);
            var o = StageRules.TryApply(stage, s, E);
            Assert.IsFalse(o.Valid); Assert.AreEqual(RejectReason.Blocked, o.Reason);
            Assert.AreSame(s, o.After);
            Assert.AreEqual(0, o.After.T);
            Assert.AreEqual(RejectReason.Blocked, StageRules.TryApply(stage, s, W).Reason, "off board");
        }

        [Test]
        public void EnteringOldGuardVision_IsCaughtBeforeTheGuardRotates()
        {
            // Rotating guard at (2,1) faces S at t=0 and would face W at t=1.
            var b = Open(6, 4, C(1, 2), C(5, 2)); b.Guards.Add(Rotating(C(2, 1), Direction.South));
            var stage = b.Build();
            var s = StageState.Initial(stage);
            var o = StageRules.TryApply(stage, s, E);
            Assert.IsTrue(o.Valid); Assert.IsTrue(o.Caught);
            Assert.AreEqual(CapturePhase.BeforeEnemyUpdate, o.CapturePhase);
            Assert.AreEqual(CaptureCause.Seen, o.CaptureCause);
            Assert.AreEqual(0, o.After.T, "the clock did not advance; the guard is shown in its old pose");
            Assert.AreEqual(StageResult.Caught, o.After.Result);
        }

        [Test]
        public void SafeInOldVision_SeenAfterRotation_IsCaughtAfterTheUpdate()
        {
            // Guard faces E at t=0 (sees (3,1),(4,1)), S at t=1 (sees (2,2),(2,3)).
            var b = Open(6, 4, C(1, 2), C(5, 2)); b.Guards.Add(Rotating(C(2, 1), Direction.East));
            var stage = b.Build();
            var o = StageRules.TryApply(stage, StageState.Initial(stage), E);
            Assert.IsTrue(o.Caught);
            Assert.AreEqual(CapturePhase.AfterEnemyUpdate, o.CapturePhase);
            Assert.AreEqual(Direction.South, o.GuardsAfter[0].Facing);
        }

        [Test]
        public void SteppingOntoAGuardsCurrentNode_IsCaught_EvenIfItFacesAwayAndMovesOff()
        {
            var b = Open(6, 4, C(0, 2), C(5, 2)); b.Guards.Add(PingPong(0, C(1, 2), C(2, 2), C(3, 2)));
            var stage = b.Build();
            Assert.AreEqual(Direction.West, GuardTrack.PoseAt(stage.Guards[0], 0).Facing, "faces away from the player at t=0");
            var o = StageRules.TryApply(stage, StageState.Initial(stage), E);
            Assert.IsTrue(o.Caught);
            Assert.AreEqual(CaptureCause.Collision, o.CaptureCause);
            Assert.AreEqual(CapturePhase.BeforeEnemyUpdate, o.CapturePhase);
        }

        [Test]
        public void GuardMovingOntoThePlayersDestination_IsCaught()
        {
            // Guard at (2,1) facing N at t=0, steps to (2,2) at t=1.
            var b = Open(6, 4, C(1, 2), C(5, 2)); b.Guards.Add(PingPong(0, C(2, 1), C(2, 2)));
            var stage = b.Build();
            var o = StageRules.TryApply(stage, StageState.Initial(stage), E);
            Assert.IsTrue(o.Caught);
            Assert.AreEqual(CaptureCause.Collision, o.CaptureCause);
            Assert.AreEqual(CapturePhase.AfterEnemyUpdate, o.CapturePhase);
        }

        [Test]
        public void PlayerAndGuardSwappingAlongAConnection_IsCaught()
        {
            // Guard at (2,2) at t=0 moving to (1,2) at t=1 while the player moves (1,2)->(2,2).
            var b = Open(6, 4, C(1, 2), C(5, 2)); b.Guards.Add(PingPong(1, C(1, 2), C(2, 2), C(3, 2)));
            var stage = b.Build();
            Assert.AreEqual(C(2, 2), GuardTrack.PositionAt(stage.Guards[0], 0));
            var o = StageRules.TryApply(stage, StageState.Initial(stage), E);
            Assert.IsTrue(o.Caught);
            Assert.AreEqual(CaptureCause.Collision, o.CaptureCause);
        }

        [Test]
        public void WaitingOutsideVision_IsSafe_AndAdvancesTheClock()
        {
            var b = Open(6, 4, C(0, 2), C(5, 2)); b.Guards.Add(Rotating(C(2, 1), Direction.East));
            var stage = b.Build();
            var s = StageState.Initial(stage);
            var o = StageRules.TryApply(stage, s, WAIT);
            Assert.IsTrue(o.Valid); Assert.IsFalse(o.Caught); Assert.IsFalse(o.Free);
            Assert.AreEqual(1, o.After.T);
            Assert.AreEqual(Direction.South, GuardTrack.PoseAt(stage.Guards[0], o.After.T).Facing);
        }

        [Test]
        public void ReachingTheExit_WinsAfterTheEnemyUpdate()
        {
            var stage = Open(4, 1, C(0, 0), C(3, 0)).Build();
            var s = StageState.Initial(stage);
            Step(stage, ref s, E); Step(stage, ref s, E);
            var o = Step(stage, ref s, E);
            Assert.IsTrue(o.Won); Assert.AreEqual(StageResult.Won, s.Result); Assert.AreEqual(3, s.T);
            Assert.AreEqual(RejectReason.GameOver, StageRules.TryApply(stage, s, W).Reason);
        }

        // ---- doors and keys

        private static Stage DoorStage(params GuardSpec[] guards)
        {
            // 6x3, key at (1,0), door between (2,1) and (3,1); row 0 and 2 are cut at the same column so the door is the only way east.
            var b = Open(6, 3, C(0, 1), C(5, 1)); b.Key = C(1, 0);
            b.Doors.Add(new DoorSpec(C(2, 1), C(3, 1)));
            b.Cuts.Add(new EdgeKey(C(2, 0), C(3, 0))); b.Cuts.Add(new EdgeKey(C(2, 2), C(3, 2)));
            b.Guards.AddRange(guards);
            return b.Build();
        }

        [Test]
        public void LockedConnection_WithoutKeyIsInvalidAndFree_WithKeyOpensAndMovesInOneAction()
        {
            var stage = DoorStage();
            var s = StageState.Initial(stage);
            Step(stage, ref s, E); Step(stage, ref s, E); // (2,1)
            var o = StageRules.TryApply(stage, s, E);
            Assert.IsFalse(o.Valid); Assert.AreEqual(RejectReason.KeyRequired, o.Reason); Assert.AreEqual(2, s.T);
            // fetch the key: (2,1)->(1,1)->(1,0)->(1,1)->(2,1)
            Step(stage, ref s, W); var pick = Step(stage, ref s, N);
            Assert.IsTrue(pick.KeyCollected); Assert.IsTrue(s.HasKey); Assert.IsFalse(s.IsDoorOpen(0), "collecting the key opens no door");
            Step(stage, ref s, S); Step(stage, ref s, E);
            var cross = Step(stage, ref s, E);
            Assert.IsTrue(cross.Valid); Assert.AreEqual(0, cross.DoorOpened); Assert.IsTrue(s.IsDoorOpen(0)); Assert.AreEqual(C(3, 1), s.Player);
            Assert.IsTrue(s.HasKey, "the key is reusable and not consumed");
            Assert.AreEqual(RejectReason.NoConnection, StageRules.TryApply(stage, new StageState(C(2, 0), 0, true, 0, null, null, 0, 0, -1, false, StageResult.Playing, null), E).Reason, "cut edge is a missing connection");
        }

        [Test]
        public void CrossingADoor_OpensLineOfSightBeforeTheOldVisionCheck()
        {
            // Static guard at (5,1) facing W: sees (4,1),(3,1) only once the door is open.
            var stage = DoorStage(Static(C(5, 1), Direction.West));
            var withKeyAtDoor = new StageState(C(2, 1), 0, true, 0, null, null, 0, 0, -1, false, StageResult.Playing, null);
            Assert.IsFalse(StageRules.TryApply(stage, withKeyAtDoor, WAIT).Caught, "closed door hides the player");
            var o = StageRules.TryApply(stage, withKeyAtDoor, E);
            Assert.IsTrue(o.Caught);
            Assert.AreEqual(CapturePhase.BeforeEnemyUpdate, o.CapturePhase, "the door opened as part of the move, so the old-state check already sees through it");
        }

        [Test]
        public void OneKey_OpensSeveralDoors_EachOnlyByCrossing()
        {
            var b = Open(6, 1, C(0, 0), C(5, 0)); b.Key = C(1, 0);
            b.Doors.Add(new DoorSpec(C(2, 0), C(3, 0))); b.Doors.Add(new DoorSpec(C(4, 0), C(5, 0)));
            var stage = b.Build();
            var s = StageState.Initial(stage);
            Step(stage, ref s, E); Assert.IsTrue(s.HasKey); Assert.AreEqual(0, s.OpenDoors);
            Step(stage, ref s, E); Step(stage, ref s, E); Assert.AreEqual(1, s.OpenDoors);
            Step(stage, ref s, E); var win = Step(stage, ref s, E);
            Assert.IsTrue(win.Won); Assert.AreEqual(3, s.OpenDoors);
        }

        [Test]
        public void Validator_RejectsGuardRoutesOverDoors_AndSameNodeGuards_AllowsSwaps()
        {
            var b = Open(6, 3, C(0, 1), C(5, 1)); b.Key = C(1, 0); b.Doors.Add(new DoorSpec(C(2, 1), C(3, 1)));
            b.Guards.Add(PingPong(0, C(2, 1), C(3, 1)));
            var r = StageValidator.Validate(b.Build(), new StageValidationOptions { RunSolver = false });
            Assert.IsFalse(r.IsValid, r.ToString());
            StringAssert.Contains("no door", r.ToString());

            var swap = Open(7, 5, C(0, 2), C(6, 2));
            swap.Guards.Add(PingPong(0, C(2, 0), C(3, 0))); swap.Guards.Add(PingPong(0, C(3, 0), C(2, 0)));
            Assert.IsTrue(StageValidator.Validate(swap.Build(), new StageValidationOptions { RunSolver = false }).IsValid, "guard-guard swap is legal");

            var clash = Open(7, 5, C(0, 2), C(6, 2));
            clash.Guards.Add(PingPong(0, C(2, 0), C(3, 0))); clash.Guards.Add(PingPong(0, C(4, 0), C(3, 0)));
            var cr = StageValidator.Validate(clash.Build(), new StageValidationOptions { RunSolver = false });
            Assert.IsFalse(cr.IsValid); StringAssert.Contains("share node (3, 0) at t=1", cr.ToString());
        }

        [Test]
        public void Validator_RejectsStartInsideInitialVision_AndWarnsOnShortPingPongRoutes()
        {
            var b = Open(6, 4, C(0, 2), C(5, 2)); b.Guards.Add(Static(C(2, 2), Direction.West));
            var r = StageValidator.Validate(b.Build(), new StageValidationOptions { RunSolver = false });
            Assert.IsFalse(r.IsValid); StringAssert.Contains("initial vision", r.ToString());

            var shortRoute = Open(6, 4, C(0, 2), C(5, 2)); shortRoute.Guards.Add(PingPong(0, C(3, 1), C(3, 2)));
            var sr = StageValidator.Validate(shortRoute.Build(), new StageValidationOptions { RunSolver = false });
            Assert.IsTrue(sr.IsValid); StringAssert.Contains("terminal node", sr.ToString());
        }
    }
}
