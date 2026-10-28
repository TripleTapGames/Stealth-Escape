using NUnit.Framework;
using ShadowSteps.Core;
using static ShadowSteps.Tests.EditMode.LevelBuilder;

namespace ShadowSteps.Tests.EditMode
{
    public class RulesEngineTests
    {
        private static LevelDefinition Empty5(params GuardDefinition[] guards) => Make(5, 5, C(0, 4), C(4, 0), guards: guards);

        [Test]
        public void InvalidMoves_DoNotSpendTurnPhaseOrCharges()
        {
            var level = Make(5, 5, C(0, 4), C(4, 0), new[] { C(1, 4) },
                new[] { Guard(GuardMode.Rotate, Direction.North, 1, C(3, 1)) }, smoke: 1, emp: 1);
            var s = new GameSession(level);
            Assert.AreEqual(InvalidActionReason.OutOfBounds, s.Apply(GameAction.South).Validation.Reason);
            Assert.AreEqual(InvalidActionReason.OutOfBounds, s.Apply(GameAction.West).Validation.Reason);
            Assert.AreEqual(InvalidActionReason.Wall, s.Apply(GameAction.East).Validation.Reason);
            Assert.AreEqual(0, s.State.Phase);
            Assert.AreEqual(0, s.State.TurnCount);
            Assert.AreEqual(1, s.State.SmokeCharges);
            Assert.AreEqual(0, s.History.Count);
        }

        [Test]
        public void GadgetWithoutCharge_IsInvalid()
        {
            var s = new GameSession(Empty5());
            Assert.AreEqual(InvalidActionReason.NoSmokeCharge, s.Apply(GameAction.Smoke).Validation.Reason);
            Assert.AreEqual(InvalidActionReason.NoEmpCharge, s.Apply(GameAction.Emp).Validation.Reason);
            Assert.AreEqual(0, s.State.TurnCount);
        }

        [Test]
        public void Wait_SpendsTurnAndAdvancesPhase()
        {
            var s = new GameSession(Empty5(Guard(GuardMode.Rotate, Direction.North, 1, C(2, 1))));
            var r = s.Apply(GameAction.Wait);
            Assert.IsTrue(r.IsValid);
            Assert.IsTrue(r.PhaseAdvanced);
            Assert.AreEqual(1, s.State.Phase);
            Assert.AreEqual(1, s.State.TurnCount);
            Assert.AreEqual(C(0, 4), s.State.PlayerPosition);
        }

        [Test]
        public void Phase_WrapsAtPeriod()
        {
            var s = new GameSession(Empty5(Guard(GuardMode.Rotate, Direction.North, 1, C(2, 1))));
            Assert.AreEqual(4, s.Level.Period);
            for (int i = 0; i < 4; i++) s.Apply(GameAction.Wait);
            Assert.AreEqual(0, s.State.Phase);
            Assert.AreEqual(4, s.State.TurnCount);
        }

        [Test]
        public void WalkingIntoCone_IsSeen()
        {
            var level = Empty5(Guard(GuardMode.Fixed, Direction.South, 2, C(0, 2)));
            var r = RulesEngine.Step(level, RulesEngine.CreateInitialState(level), GameAction.North); // to (0,3), directly ahead of guard
            Assert.AreEqual(CaptureKind.Seen, r.Capture);
            Assert.AreEqual(0, r.CapturingGuard);
            Assert.AreEqual(GameOutcome.Captured, r.StateAfter.Outcome);
        }

        [Test]
        public void Smoke_ConcealsForDeploymentAndNextActionOnly()
        {
            var level = Make(5, 5, C(0, 4), C(4, 0), guards: new[] { Guard(GuardMode.Fixed, Direction.South, 3, C(0, 1)) }, smoke: 1);
            var s = new GameSession(level);
            Assert.AreEqual(CaptureKind.Seen, s.Preview(GameAction.North).Capture, "without smoke, (0,3) is seen");
            var r1 = s.Apply(GameAction.Smoke);
            Assert.IsTrue(r1.IsValid); Assert.AreEqual(2, s.State.SmokeRemaining); Assert.AreEqual(0, s.State.SmokeCharges);
            var r2 = s.Apply(GameAction.North);
            Assert.AreEqual(CaptureKind.None, r2.Capture, "next action is concealed");
            Assert.AreEqual(1, s.State.SmokeRemaining);
            var r3 = s.Apply(GameAction.Wait);
            Assert.AreEqual(CaptureKind.Seen, r3.Capture, "smoke has expired on the second action after deployment");
            Assert.AreEqual(0, r3.StateAfter.SmokeRemaining);
        }

        [Test]
        public void Smoke_DoesNotPreventOccupancyCapture()
        {
            // 3x3 board. Guard ping-pongs (1,1)<->(1,2) facing its travel direction with range 1:
            // phase 0 at (1,1) facing N (sees row 0), phase 1 at (1,2) facing S (sees nothing on board).
            var level = Make(3, 3, C(0, 2), C(2, 0), guards: new[] { Guard(GuardMode.Patrol, Direction.North, 1, C(1, 1), C(1, 2)) }, smoke: 1);
            var s = new GameSession(level);
            Assert.AreEqual(CaptureKind.None, s.Apply(GameAction.Wait).Capture);   // phase 1: guard at (1,2)
            Assert.AreEqual(CaptureKind.None, s.Apply(GameAction.Smoke).Capture);  // phase 0: guard at (1,1), cloud 2
            var r = s.Apply(GameAction.East);                                      // player -> (1,2); guard arrives at (1,2)
            Assert.AreEqual(CaptureKind.Occupied, r.Capture, "occupancy captures even while concealed");
            Assert.AreEqual(1, r.StateAfter.SmokeRemaining, "player was still concealed when captured");
        }

        [Test]
        public void Smoke_DoesNotPreventSwapCapture()
        {
            // Guard moves (1,2)->(0,2)? Start tile is (0,2); engine allows it even though the validator would warn.
            var level = Make(3, 3, C(0, 2), C(2, 0), guards: new[] { Guard(GuardMode.Patrol, Direction.North, 1, C(1, 2), C(0, 2)) }, smoke: 1);
            var s = new GameSession(level);
            // Phase 0: guard (1,2) facing N (sees row 1, not the player). SMOKE then move E into its tile while it moves W into ours.
            s.Restart();
            var lvl2 = Make(3, 3, C(0, 2), C(2, 0), guards: new[] { Guard(GuardMode.Patrol, Direction.North, 1, C(1, 1), C(1, 2), C(0, 2)) }, smoke: 1);
            var s2 = new GameSession(lvl2);
            Assert.AreEqual(CaptureKind.None, s2.Apply(GameAction.Smoke).Capture); // phase 1: guard (1,2) facing S, concealed anyway
            var r = s2.Apply(GameAction.East);                                      // player (0,2)->(1,2); guard (1,2)->(0,2): edge swap
            Assert.AreEqual(CaptureKind.Swapped, r.Capture);
        }

        [Test]
        public void Emp_FreezesPhaseButNotSight()
        {
            // Rotating guard at (0,2) facing S with range 2 sees the start tile (0,4) whenever it faces S (phase 0 mod 4).
            var level = Make(5, 5, C(0, 4), C(4, 0), guards: new[] { Guard(GuardMode.Rotate, Direction.South, 2, C(0, 2)) }, emp: 1);
            var s = new GameSession(level);
            s.Apply(GameAction.Wait); s.Apply(GameAction.Wait); s.Apply(GameAction.Wait); // phase 3: guard faces E
            var empStep = s.Apply(GameAction.Emp);
            Assert.IsTrue(empStep.IsValid);
            Assert.IsFalse(empStep.PhaseAdvanced);
            Assert.AreEqual(3, s.State.Phase, "EMP keeps the global phase");
            Assert.AreEqual(0, s.State.EmpCharges);
            Assert.AreEqual(4, s.State.TurnCount, "EMP still spends a turn");
            Assert.AreEqual(CaptureKind.None, empStep.Capture);
            var next = s.Apply(GameAction.Wait); // phase 0: guard faces S again -> (0,4) is 2 tiles ahead -> seen
            Assert.AreEqual(CaptureKind.Seen, next.Capture);
        }

        [Test]
        public void Emp_WhileStandingInCurrentCone_IsStillSeen()
        {
            var level = Make(5, 5, C(0, 4), C(4, 0), guards: new[] { Guard(GuardMode.Rotate, Direction.West, 1, C(1, 3)) }, emp: 1);
            var s = new GameSession(level);
            // phase 0 guard faces W from (1,3): cone cells (0,2),(0,3),(0,4). Player at (0,4) is seen at phase 0 but initial state has no check.
            var r = s.Apply(GameAction.Emp); // phase stays 0 -> player seen
            Assert.AreEqual(CaptureKind.Seen, r.Capture);
        }

        [Test]
        public void Key_UnlocksDoorAndIsRequiredToWin()
        {
            // 1x5 corridor: start (0,0), key (1,0)? Put door between key and exit.
            var level = Make(5, 1, C(0, 0), C(4, 0), key: C(1, 0), door: C(3, 0));
            var s = new GameSession(level);
            var r1 = s.Apply(GameAction.East); // collects key
            Assert.IsTrue(r1.KeyCollected);
            Assert.IsTrue(s.State.HasKey);
            s.Apply(GameAction.East);
            var r3 = s.Apply(GameAction.East); // enters door tile
            Assert.IsTrue(r3.IsValid);
            var r4 = s.Apply(GameAction.East);
            Assert.IsTrue(r4.Won);
            Assert.AreEqual(4, s.State.TurnCount);
        }

        [Test]
        public void LockedDoor_RejectsEntryWithoutKey()
        {
            var level = Make(5, 1, C(0, 0), C(4, 0), key: C(0, 0).Offset(0, 0), door: C(1, 0));
            // key on start tile is pathological; use a 2-row board instead.
            level = Make(3, 2, C(0, 1), C(2, 1), key: C(0, 0), door: C(1, 1));
            var s = new GameSession(level);
            Assert.AreEqual(InvalidActionReason.LockedDoor, s.Apply(GameAction.East).Validation.Reason);
            s.Apply(GameAction.North); // key
            s.Apply(GameAction.South);
            Assert.IsTrue(s.Apply(GameAction.East).IsValid);
        }

        [Test]
        public void ReachingExitWithoutKey_DoesNotWin()
        {
            var level = Make(3, 2, C(0, 1), C(2, 1), key: C(0, 0), door: C(2, 0));
            var s = new GameSession(level);
            s.Apply(GameAction.East);
            var r = s.Apply(GameAction.East);
            Assert.IsFalse(r.Won);
            Assert.AreEqual(GameOutcome.Playing, s.State.Outcome);
        }

        [Test]
        public void EdgeSwap_IsCapture()
        {
            // Guard patrols (0,3)<->(0,2) facing away from player initially; player at (0,4) moves N to (0,3) while guard moves (0,3)->(0,2)? That's not a swap.
            // Swap: guard at (0,3) moves to (0,4) while player moves (0,4)->(0,3).
            var level = Make(5, 5, C(0, 4), C(4, 0), guards: new[] { Guard(GuardMode.Patrol, Direction.North, 1, C(0, 3), C(1, 3)) });
            // Use a guard that moves from (0,3) to (0,4)? (0,4) is the start tile - validator forbids but engine allows; build directly.
            var g = Guard(GuardMode.Patrol, Direction.North, 1, C(0, 3), C(0, 4));
            level = Make(5, 5, C(0, 4), C(4, 0), guards: new[] { g });
            var r = RulesEngine.Step(level, RulesEngine.CreateInitialState(level), GameAction.North);
            Assert.AreEqual(CaptureKind.Swapped, r.Capture);
        }

        [Test]
        public void CapturePrecedesWin()
        {
            var level = Make(2, 1, C(0, 0), C(1, 0), guards: new[] { Guard(GuardMode.Patrol, Direction.East, 1, C(1, 0), C(0, 0)) });
            var r = RulesEngine.Step(level, RulesEngine.CreateInitialState(level), GameAction.East);
            Assert.IsTrue(r.Captured);
            Assert.IsFalse(r.Won);
        }

        [Test]
        public void ActionsAfterTerminal_AreRejected()
        {
            var level = Make(2, 1, C(0, 0), C(1, 0));
            var s = new GameSession(level);
            Assert.IsTrue(s.Apply(GameAction.East).Won);
            Assert.AreEqual(InvalidActionReason.GameOver, s.Apply(GameAction.West).Validation.Reason);
        }

        [Test]
        public void Undo_RestoresEveryField()
        {
            var level = TestPaths.Level(20);
            var s = new GameSession(level);
            var snapshots = new System.Collections.Generic.List<string> { s.State.ToReferenceTuple() };
            foreach (var a in new[] { GameAction.North, GameAction.North, GameAction.Smoke, GameAction.Emp, GameAction.North })
            {
                Assert.IsTrue(s.Apply(a).IsValid, "action " + a);
                snapshots.Add(s.State.ToReferenceTuple());
            }
            for (int i = snapshots.Count - 1; i > 0; i--)
            {
                Assert.AreEqual(snapshots[i], s.State.ToReferenceTuple());
                Assert.IsNotNull(s.Undo());
            }
            Assert.AreEqual(snapshots[0], s.State.ToReferenceTuple());
            Assert.IsFalse(s.CanUndo);
            Assert.IsNull(s.Undo());
        }

        [Test]
        public void Undo_AfterCapture_AllowsContinuing()
        {
            var level = Empty5(Guard(GuardMode.Fixed, Direction.South, 2, C(0, 2)));
            var s = new GameSession(level);
            Assert.IsTrue(s.Apply(GameAction.North).Captured);
            Assert.IsTrue(s.IsTerminal);
            s.Undo();
            Assert.IsFalse(s.IsTerminal);
            Assert.IsTrue(s.Apply(GameAction.East).IsValid);
        }

        [Test]
        public void Restart_ReturnsToInitialState()
        {
            var s = new GameSession(TestPaths.Level(11));
            s.Apply(GameAction.East); s.Apply(GameAction.North); s.Apply(GameAction.Smoke);
            s.Restart();
            Assert.AreEqual(s.InitialState.ToReferenceTuple(), s.State.ToReferenceTuple());
            Assert.AreEqual(0, s.History.Count);
        }

        [Test]
        public void Preview_HasNoSideEffects()
        {
            var s = new GameSession(TestPaths.Level(1));
            var before = s.State.ToReferenceTuple();
            s.Preview(GameAction.North);
            s.Preview(GameAction.East);
            Assert.AreEqual(before, s.State.ToReferenceTuple());
            Assert.AreEqual(0, s.History.Count);
        }

        [Test]
        public void Replay_IsDeterministic()
        {
            var sol = TestPaths.LoadReferenceSolutions()[18];
            var a = GameSession.Replay(TestPaths.Level(sol.Level), sol.Actions).Trace();
            var b = GameSession.Replay(TestPaths.Level(sol.Level), sol.Actions).Trace();
            Assert.AreEqual(a, b);
        }
    }
}
