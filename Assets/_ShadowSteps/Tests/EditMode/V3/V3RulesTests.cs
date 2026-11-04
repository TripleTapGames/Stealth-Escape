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
        public void LockedConnection_WithoutKeyIsInvalid_WithKeyOpensWhenCrossed()
        {
            var stage = DoorStage();
            var s = StageState.Initial(stage);
            Step(stage, ref s, E); Step(stage, ref s, E); // (2,1)
            var o = StageRules.TryApply(stage, s, E);
            Assert.IsFalse(o.Valid); Assert.AreEqual(RejectReason.KeyRequired, o.Reason); Assert.AreEqual(2, s.T);
            // fetch the key: (2,1)->(1,1)->(1,0)->(1,1)->(2,1)
            Step(stage, ref s, W); var pick = Step(stage, ref s, N);
            Assert.IsTrue(pick.KeyCollected); Assert.IsTrue(s.HasKey); Assert.IsFalse(s.IsDoorOpen(0), "a distant key pickup does not open the door");
            Step(stage, ref s, S); var approach = Step(stage, ref s, E);
            Assert.AreEqual(-1, approach.DoorOpened); Assert.IsFalse(s.IsDoorOpen(0), "D-038: standing beside a locked edge with the key does not open it");
            Assert.AreEqual(C(2, 1), s.Player);
            var cross = Step(stage, ref s, E);
            Assert.IsTrue(cross.Valid); Assert.AreEqual(0, cross.DoorOpened); Assert.IsTrue(s.IsDoorOpen(0)); Assert.AreEqual(C(3, 1), s.Player, "the door opens as part of the crossing");
            Assert.IsTrue(s.HasKey, "the key is reusable and not consumed");
            Assert.AreEqual(RejectReason.NoConnection, StageRules.TryApply(stage, new StageState(C(2, 0), 0, true, 0, null, null, 0, 0, -1, false, StageResult.Playing, null), E).Reason, "cut edge is a missing connection");
        }

        [Test]
        public void ClosedDoor_KeepsBlockingSight_UntilTheCrossingOpensIt()
        {
            // Guard at (4,1) looks W through the locked edge (2,1)-(3,1). With the key, stepping onto (2,1) is safe (door still closed);
            // the crossing to (3,1) opens it as part of the movement and the guard sees the player on arrival (old-state check, door open).
            var stage = DoorStage(Static(C(4, 1), Direction.West));
            var withKey = new StageState(C(1, 1), 0, true, 0, null, null, 0, 0, -1, false, StageResult.Playing, null);
            var o = StageRules.TryApply(stage, withKey, E);
            Assert.IsFalse(o.After.IsDoorOpen(0)); Assert.IsFalse(o.Caught, "the closed door still blocks the guard's sight");
            var cross = StageRules.TryApply(stage, o.After, E);
            Assert.IsTrue(cross.After.IsDoorOpen(0)); Assert.IsTrue(cross.Caught, "crossing opens the door and exposes the player"); Assert.AreEqual(CapturePhase.BeforeEnemyUpdate, cross.CapturePhase);
            // Legacy switch (measurement only): approach-opening exposes the player one step earlier.
            StageRules.DoorsOpenOnApproach = true;
            try { var legacy = StageRules.TryApply(stage, withKey, E); Assert.IsTrue(legacy.After.IsDoorOpen(0)); Assert.IsTrue(legacy.Caught); }
            finally { StageRules.DoorsOpenOnApproach = false; }
        }

        [Test]
        public void OneKey_OpensSeveralDoors_WhenCrossed_WithoutBeingConsumed()
        {
            var b = Open(6, 1, C(0, 0), C(5, 0)); b.Key = C(1, 0);
            b.Doors.Add(new DoorSpec(C(2, 0), C(3, 0))); b.Doors.Add(new DoorSpec(C(4, 0), C(5, 0)));
            var stage = b.Build();
            var s = StageState.Initial(stage);
            Step(stage, ref s, E); Assert.IsTrue(s.HasKey); Assert.AreEqual(0, s.OpenDoors);
            Step(stage, ref s, E); Assert.AreEqual(0, s.OpenDoors, "beside the first lock: still closed (D-038)");
            Step(stage, ref s, E); Assert.AreEqual(1, s.OpenDoors, "crossing (2,0)-(3,0) opens door 0");
            Step(stage, ref s, E); Assert.AreEqual(1, s.OpenDoors);
            Assert.IsTrue(s.HasKey);
            var win = Step(stage, ref s, E); // crossing (4,0)-(5,0) opens door 1 and lands on the exit
            Assert.IsTrue(win.Won); Assert.AreEqual(3, s.OpenDoors);
        }

        [Test]
        public void KeyPickup_AtSharedEndpoint_OpensNothing_EachLockOpensOnItsOwnCrossing()
        {
            var b = Open(5, 3, C(1, 0), C(4, 2)); b.Key = C(1, 1);
            b.Doors.Add(new DoorSpec(C(1, 1), C(2, 1)));
            b.Doors.Add(new DoorSpec(C(1, 1), C(1, 2)));
            b.Doors.Add(new DoorSpec(C(3, 1), C(4, 1)));
            var stage = b.Build(); var initial = StageState.Initial(stage);
            var o = StageRules.TryApply(stage, initial, S);
            Assert.IsTrue(o.KeyCollected); Assert.AreEqual(0, o.After.OpenDoors, "D-038: collecting the key on a shared endpoint opens no lock");
            Assert.AreEqual(1, o.After.T);
            var east = StageRules.TryApply(stage, o.After, E); Assert.AreEqual(1, east.After.OpenDoors, "crossing (1,1)-(2,1) opens door 0 only"); Assert.AreEqual(0, east.DoorOpened);
            var south = StageRules.TryApply(stage, o.After, S); Assert.AreEqual(2, south.After.OpenDoors, "crossing (1,1)-(1,2) opens door 1 only"); Assert.AreEqual(1, south.DoorOpened);
            Assert.AreEqual(0, initial.OpenDoors, "preview/solver inputs remain immutable");
            Assert.AreEqual(0, StageState.Initial(stage).OpenDoors, "retry closes locks");
        }

        [Test]
        public void Lock_OpensOnlyOnTheCrossing_FromEitherSide()
        {
            var stage = DoorStage(); // door (2,1)-(3,1)
            var below = new StageState(C(3, 2), 0, true, 0, null, null, 0, 0, -1, false, StageResult.Playing, null);
            Assert.IsFalse(StageRules.TryApply(stage, below, WAIT).After.IsDoorOpen(0));
            var onEndpoint = StageRules.TryApply(stage, below, N).After; Assert.IsFalse(onEndpoint.IsDoorOpen(0), "arriving on (3,1) with the key does not open the lock");
            var crossedWest = StageRules.TryApply(stage, onEndpoint, W); Assert.IsTrue(crossedWest.Valid); Assert.IsTrue(crossedWest.After.IsDoorOpen(0), "crossing from the east side opens it");
            var noKey = new StageState(C(3, 1), 0, false, 0, null, null, 0, 0, -1, false, StageResult.Playing, null);
            Assert.AreEqual(RejectReason.KeyRequired, StageRules.TryApply(stage, noKey, W).Reason);
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
    
        [Test]
        public void WaitLimit_RejectsWaitsBeyondTheLimit_CountsOnlyWaits_AndIsUnlimitedByDefault()
        {
            var b = Board("#######", "#.....#", "#.....#", "#.....#", "#######"); b.Start = C(1, 1); b.Exit = C(5, 3);
            var unlimited = b.Build(); Assert.AreEqual(-1, unlimited.WaitLimit);
            var s = StageState.Initial(unlimited); for (int i = 0; i < 6; i++) { var o = StageRules.TryApply(unlimited, s, StageAction.Wait); Assert.IsTrue(o.Valid); s = o.After; }
            Assert.AreEqual(6, s.WaitsUsed); Assert.AreEqual(int.MaxValue, s.WaitsLeft(unlimited));

            b.WaitLimit = 2; var limited = b.Build(); Assert.AreEqual(2, limited.WaitLimit);
            s = StageState.Initial(limited); Assert.AreEqual(2, s.WaitsLeft(limited));
            s = StageRules.TryApply(limited, s, StageAction.Move(Direction.East)).After; Assert.AreEqual(0, s.WaitsUsed, "moves do not count");
            s = StageRules.TryApply(limited, s, StageAction.Wait).After; s = StageRules.TryApply(limited, s, StageAction.Wait).After;
            Assert.AreEqual(2, s.WaitsUsed); Assert.AreEqual(0, s.WaitsLeft(limited)); Assert.AreEqual(3, s.T);
            var rejected = StageRules.TryApply(limited, s, StageAction.Wait);
            Assert.IsFalse(rejected.Valid); Assert.AreEqual(RejectReason.NoWaitsLeft, rejected.Reason); Assert.AreSame(s, rejected.After);
            Assert.IsTrue(StageRules.TryApply(limited, s, StageAction.Move(Direction.East)).Valid, "moves still allowed");
            b.SmokeCharges = 1; var withSmoke = b.Build(); var s2 = StageState.Initial(withSmoke);
            s2 = StageRules.TryApply(withSmoke, s2, StageAction.Wait).After;
            var smoke = StageRules.TryApply(withSmoke, s2, StageAction.Smoke(C(2, 1))); Assert.IsTrue(smoke.Valid); Assert.AreEqual(1, smoke.After.WaitsUsed, "free actions keep the count");
        }

        [Test]
        public void WaitLimit_RoundTripsThroughJson_AndTheSolverRespectsIt()
        {
            // A guard sweeps the only corridor cell for 2 of 4 actions: the shortest route needs waits. Cap them and the solver must find another way or fail.
            var b = Board("#######", "#.....#", "#..#..#", "#.....#", "#######"); b.Start = C(1, 2); b.Exit = C(5, 2); b.WaitLimit = 1; b.Id = 77; b.Code = "T-WAIT";
            b.Guards.Add(Rotating(C(3, 1), Direction.South));
            var stage = b.Build();
            var json = ShadowSteps.Data.StageJson.ToJsonObject(stage); var back = ShadowSteps.Data.StageJson.ParseStage(json, "T-WAIT", null);
            Assert.AreEqual(1, back.WaitLimit, "waitLimit round-trips");
            var free = StageSolver.Solve(b.Build());
            var capped = StageSolver.Solve(stage);
            int waits = 0; foreach (var a in capped.Actions) if (a.Kind == StageActionKind.Wait) waits++;
            if (capped.Found) Assert.LessOrEqual(waits, 1, "solver never exceeds the stage's wait limit");
            var zero = StageSolver.Solve(stage, new SolveOptions { MaxWaits = 0 });
            int w0 = 0; if (zero.Found) foreach (var a in zero.Actions) if (a.Kind == StageActionKind.Wait) w0++;
            Assert.AreEqual(0, w0, "MaxWaits 0 yields no WAIT");
            var m = StageMetrics.Analyze(b.Build());
            Assert.GreaterOrEqual(m.WaitsInPar, m.MinWaitsForPar); Assert.GreaterOrEqual(m.MinWaitsForPar, 0);
            if (m.ShortestWithoutWait >= 0) Assert.GreaterOrEqual(m.ShortestWithoutWait, m.Par);
        }
}
}
