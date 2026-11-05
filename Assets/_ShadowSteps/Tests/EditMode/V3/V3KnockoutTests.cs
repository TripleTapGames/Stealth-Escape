using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using ShadowSteps.Core;
using ShadowSteps.Data;
using ShadowSteps.V3;
using static ShadowSteps.Tests.EditMode.V3.V3TestKit;

namespace ShadowSteps.Tests.EditMode.V3
{
    /// <summary>
    /// Global Stealth/Knockout modes (D-040) on the real C# resolver. Mirrors the handoff's focused checks (every facing
    /// × approach, doors, cuts, smoke, patrol reversal facing, survivors catching the attacker at the landing node,
    /// persistence, tap/direction normalisation, gadget durations, lockdown budget on expiry, last-action exit) and adds
    /// the Unity-side guarantees: Stealth untouched, the solver per mode, the state key, and every shipped level
    /// checked in both modes with Stealth-solution parity under Knockout rules.
    /// </summary>
    public class V3KnockoutTests
    {
        private const StageMode KO = StageMode.Knockout;
        private static StageAction KO0 => StageAction.Knockout(0);

        /// <summary>5×5 open board, static guard at (2,2) facing N (so (2,1) is its front), exit (4,4).</summary>
        private static StageBuilder Fixture(GridCoord start, Direction guardFacing = Direction.North)
        {
            var b = Open(5, 5, start, C(4, 4)); b.AlarmBudget = 1;
            b.Guards.Add(Static(C(2, 2), guardFacing));
            return b;
        }

        private static StageState With(StageState s, bool? hasKey = null, int? openDoors = null, IReadOnlyList<SmokeCloud> clouds = null, IReadOnlyList<EmpEffect> emps = null) =>
            new StageState(s.Player, s.T, hasKey ?? s.HasKey, openDoors ?? s.OpenDoors, clouds ?? s.Clouds, emps ?? s.Emps, s.SmokeCharges, s.EmpCharges, s.Alarm, s.AlarmEver, s.Result, s.Cause, s.WaitsUsed, s.Mode, s.Defeated);

        [Test]
        public void Stealth_KnockoutIsUnavailable_AndSteppingOntoAGuardStaysFatal()
        {
            var stage = Fixture(C(3, 2)).Build();
            var s = StageState.Initial(stage); // Stealth
            Assert.AreEqual(StageMode.Stealth, s.Mode); Assert.IsFalse(s.KnockoutsEnabled);
            var ko = StageRules.TryApply(stage, s, KO0);
            Assert.IsFalse(ko.Valid); Assert.AreEqual(RejectReason.KnockoutUnavailable, ko.Reason); Assert.AreSame(s, ko.After);
            var move = StageRules.TryApply(stage, s, W); // onto the guard from its side: in Stealth this is the original fatal collision
            Assert.IsTrue(move.Valid); Assert.IsTrue(move.Caught); Assert.AreEqual(CaptureCause.Collision, move.CaptureCause); Assert.AreEqual(CapturePhase.BeforeEnemyUpdate, move.CapturePhase);
            Assert.AreEqual(0, move.After.DefeatedCount);
            Assert.AreEqual(0, StageRules.KnockoutTargets(stage, s).Count, "no targets offered in Stealth");
        }

        [Test]
        public void Knockout_EveryFacingAndApproach_OnlyTheFrontIsRejected_AndCostsNothing()
        {
            for (int facing = 0; facing < 4; facing++)
                for (int approach = 0; approach < 4; approach++)
                {
                    var f = (Direction)facing; var from = C(2, 2).Neighbor((Direction)approach);
                    var stage = Fixture(from, f).Build();
                    var s = StageState.Initial(stage, KO);
                    var o = StageRules.TryApply(stage, s, KO0);
                    bool frontal = approach == facing;
                    string label = "guard faces " + f + ", player at " + from;
                    Assert.AreEqual(!frontal, o.Valid, label);
                    if (frontal)
                    {
                        Assert.AreEqual(RejectReason.FrontalApproach, o.Reason, label);
                        Assert.AreSame(s, o.After, label + ": state untouched"); Assert.AreEqual(0, o.After.T);
                    }
                    else
                    {
                        Assert.AreEqual(1, o.After.T, label + ": exactly one counted action");
                        Assert.AreEqual(C(2, 2), o.After.Player, label + ": player lands on the guard's node");
                        Assert.IsTrue(o.After.IsDefeated(0)); Assert.AreEqual(1, o.After.DefeatedCount);
                        Assert.AreEqual(StageResult.Playing, o.After.Result, label);
                        Assert.IsTrue(o.Knockout); Assert.AreEqual(0, o.KnockedOutGuard);
                        var expectedSide = approach == DirectionUtil.Mod(facing + 2, 4) ? KnockoutSide.Back : approach == DirectionUtil.Mod(facing + 1, 4) ? KnockoutSide.Right : KnockoutSide.Left;
                        Assert.AreEqual(expectedSide, o.KnockoutSide, label);
                        Assert.AreEqual(StageAction.Knockout(0), o.Action, "normalised action");
                    }
                }
        }

        [Test]
        public void DiagonalAndDistant_AreNotAdjacent()
        {
            foreach (var start in new[] { C(3, 3), C(4, 2), C(2, 2) })
            {
                var stage = Fixture(start).Build(); var s = StageState.Initial(stage, KO);
                var o = StageRules.TryApply(stage, s, KO0);
                Assert.IsFalse(o.Valid, start.ToString()); Assert.AreEqual(RejectReason.GuardNotAdjacent, o.Reason); Assert.AreSame(s, o.After);
            }
        }

        [Test]
        public void ClosedDoor_BlocksTheKnockoutEvenWithTheKey_OpenDoorAllowsIt()
        {
            var b = Fixture(C(3, 2)); b.Key = C(0, 0); b.Doors.Add(new DoorSpec(C(2, 2), C(3, 2)));
            var stage = b.Build();
            var s = With(StageState.Initial(stage, KO), hasKey: true);
            Assert.AreEqual(RejectReason.DoorBlocksKnockout, StageRules.CheckKnockout(stage, s, 0, out _, out _));
            Assert.IsFalse(StageRules.TryApply(stage, s, KO0).Valid);
            Assert.IsFalse(StageRules.TryApply(stage, s, W).Valid, "the move onto the guard normalises to the same rejected knockout");
            var open = With(s, openDoors: 1);
            Assert.AreEqual(RejectReason.None, StageRules.CheckKnockout(stage, open, 0, out var side, out var dir));
            Assert.AreEqual(KnockoutSide.Right, side); Assert.AreEqual(Direction.West, dir);
        }

        [Test]
        public void MissingEdge_BlocksTheKnockout()
        {
            var b = Fixture(C(3, 2)); b.Cuts.Add(new EdgeKey(C(2, 2), C(3, 2)));
            var stage = b.Build();
            Assert.AreEqual(RejectReason.NoConnection, StageRules.CheckKnockout(stage, StageState.Initial(stage, KO), 0, out _, out _));
        }

        [Test]
        public void Smoke_DoesNotEnableAFrontalKnockout()
        {
            var stage = Fixture(C(2, 1)).Build();
            var s = With(StageState.Initial(stage, KO), clouds: new[] { new SmokeCloud(C(2, 1), 2) });
            Assert.AreEqual(RejectReason.FrontalApproach, StageRules.CheckKnockout(stage, s, 0, out _, out _));
            var o = StageRules.TryApply(stage, s, S);
            Assert.IsFalse(o.Valid); Assert.AreEqual(RejectReason.FrontalApproach, o.Reason); Assert.AreEqual(0, o.After.T);
        }

        [Test]
        public void MovingGuard_UsesItsCurrentFacingBeforeTheReversal()
        {
            var b = Open(5, 5, C(2, 2), C(4, 4)); b.Guards.Add(PingPong(0, C(1, 2), C(2, 2), C(3, 2)));
            var stage = b.Build();
            Assert.AreEqual(C(1, 2), GuardTrack.PoseAt(stage.Guards[0], 0).Position); Assert.AreEqual(Direction.West, GuardTrack.PoseAt(stage.Guards[0], 0).Facing, "at the route end it faces its last travel direction");
            Assert.AreEqual(Direction.East, GuardTrack.NextStep(stage.Guards[0], 0), "its next patrol step is the reversal, which must not be used");
            var s = StageState.Initial(stage, KO);
            Assert.AreEqual(RejectReason.None, StageRules.CheckKnockout(stage, s, 0, out var side, out _)); Assert.AreEqual(KnockoutSide.Back, side);
            Assert.AreEqual(StageResult.Playing, StageRules.TryApply(stage, s, KO0).After.Result);
            var front = new StageState(C(0, 2), 0, false, 0, null, null, 0, 0, -1, false, StageResult.Playing, null, 0, KO);
            Assert.AreEqual(RejectReason.FrontalApproach, StageRules.CheckKnockout(stage, front, 0, out _, out _));
        }

        [Test]
        public void TargetIsRemovedBeforeItsTurn_OtherGuardsAdvance_AndIndicesStayStable()
        {
            var b = Open(5, 5, C(3, 2), C(4, 4)); b.Guards.Add(Rotating(C(2, 2), Direction.North)); b.Guards.Add(Rotating(C(0, 0), Direction.North));
            var stage = b.Build();
            var o = StageRules.TryApply(stage, StageState.Initial(stage, KO), KO0);
            Assert.IsTrue(o.Valid); Assert.AreEqual(1, o.After.T);
            Assert.IsTrue(o.After.IsDefeated(0)); Assert.IsFalse(o.After.IsDefeated(1));
            Assert.AreEqual(2, o.GuardsAfter.Length, "poses still reported for every authored guard (stable indices)");
            Assert.AreEqual(Direction.East, o.GuardsAfter[1].Facing, "the survivor rotated once");
            Assert.AreEqual(Direction.East, GuardTrack.PoseAt(stage.Guards[1], o.After.T).Facing);
        }

        [Test]
        public void SurvivingGuard_CanSeeTheAttackerAtTheLandingNode_BeforeOrAfterItsUpdate()
        {
            var b = Fixture(C(3, 2)); b.Guards.Add(Static(C(4, 2), Direction.West)); // sees (3,2),(2,2) right now
            var o = StageRules.TryApply(b.Build(), StageState.Initial(b.Build(), KO), KO0);
            Assert.IsTrue(o.Caught); Assert.AreEqual(1, o.CapturingGuard); Assert.AreEqual(CapturePhase.BeforeEnemyUpdate, o.CapturePhase);
            Assert.IsTrue(o.After.IsDefeated(0), "the knocked-out guard stays down in the recorded state");

            var b2 = Fixture(C(3, 2)); b2.Guards.Add(Rotating(C(2, 3), Direction.West)); // faces N after its update: sees (2,2),(2,1)
            var stage2 = b2.Build();
            var o2 = StageRules.TryApply(stage2, StageState.Initial(stage2, KO), KO0);
            Assert.IsTrue(o2.Caught); Assert.AreEqual(1, o2.CapturingGuard); Assert.AreEqual(CapturePhase.AfterEnemyUpdate, o2.CapturePhase); Assert.AreEqual(CaptureCause.Seen, o2.CaptureCause);

            var b3 = Fixture(C(3, 2)); b3.Guards.Add(Rotating(C(3, 4), Direction.West)); // faces N after its update: sees (3,3),(3,2) - the VACATED node
            var stage3 = b3.Build();
            var o3 = StageRules.TryApply(stage3, StageState.Initial(stage3, KO), KO0);
            Assert.IsFalse(o3.Caught, "vision on the vacated node is not a capture"); Assert.AreEqual(C(2, 2), o3.After.Player);
        }

        [Test]
        public void SurvivingGuard_CollisionAndSwapStillApply()
        {
            // Guard 1 walks (3,2)->(2,2)? It would collide at the landing node after the update.
            var b = Fixture(C(3, 2)); b.Guards.Add(PingPong(0, C(2, 4), C(2, 3), C(2, 2)));
            var stage = b.Build();
            Assert.AreEqual(C(2, 4), GuardTrack.PositionAt(stage.Guards[1], 0)); Assert.AreEqual(C(2, 3), GuardTrack.PositionAt(stage.Guards[1], 1));
            var o = StageRules.TryApply(stage, StageState.Initial(stage, KO), KO0);
            Assert.IsTrue(o.Caught, "guard 1 faces N after stepping to (2,3) and sees (2,2)"); Assert.AreEqual(1, o.CapturingGuard);
        }

        [Test]
        public void Knockout_Persists_VacatedNodeIsTraversable_WaitsNeverRevive()
        {
            var stage = Fixture(C(3, 2)).Build();
            var s = StageRules.TryApply(stage, StageState.Initial(stage, KO), KO0).After;
            var again = StageRules.TryApply(stage, s, KO0);
            Assert.IsFalse(again.Valid); Assert.AreEqual(RejectReason.GuardAlreadyDown, again.Reason);
            s = StageRules.TryApply(stage, s, W).After; Assert.AreEqual(C(1, 2), s.Player);
            s = StageRules.TryApply(stage, s, E).After; Assert.AreEqual(C(2, 2), s.Player); Assert.AreEqual(StageResult.Playing, s.Result, "the vacated node is plain floor");
            for (int i = 0; i < 12; i++) { var o = StageRules.TryApply(stage, s, WAIT); Assert.IsTrue(o.Valid); s = o.After; }
            Assert.IsTrue(s.IsDefeated(0)); Assert.AreEqual(StageResult.Playing, s.Result); Assert.AreEqual(15, s.T);
            Assert.AreEqual(0, StageRules.KnockoutTargets(stage, s).Count);
        }

        [Test]
        public void MoveOntoAGuard_TapOrDirection_IsTheSameActionAsTheTargetedKnockout()
        {
            var stage = Fixture(C(3, 2)).Build(); var s = StageState.Initial(stage, KO);
            var move = StageRules.TryApply(stage, s, W); var hit = StageRules.TryApply(stage, s, KO0);
            Assert.IsTrue(move.Valid && hit.Valid);
            Assert.AreEqual(hit.Action, move.Action); Assert.AreEqual("KO#0", move.Action.Token);
            Assert.AreEqual(hit.After.Key(), move.After.Key()); Assert.AreEqual(C(2, 2), move.After.Player); Assert.AreEqual(1, move.After.T);
            Assert.AreEqual(hit.KnockoutSide, move.KnockoutSide); Assert.AreEqual(move.PlayerFrom, hit.PlayerFrom); Assert.AreEqual(move.PlayerTo, hit.PlayerTo);
            Assert.AreEqual(new List<int> { 0 }, StageRules.KnockoutTargets(stage, s));
        }

        [Test]
        public void GadgetDurations_DecrementOnceOnAKnockout_CameraScheduleContinues()
        {
            var b = Board("#....", ".....", ".....", ".....", "....."); b.Start = C(3, 2); b.Exit = C(4, 4); b.AlarmBudget = 1;
            b.Guards.Add(Static(C(2, 2), Direction.North)); b.Cameras.Add(Camera(C(0, 0), Direction.South, Direction.South, CameraKind.Rotating));
            var stage = b.Build();
            var s = With(StageState.Initial(stage, KO), clouds: new[] { new SmokeCloud(C(4, 4), 2) }, emps: new[] { new EmpEffect(0, 2) });
            var o = StageRules.TryApply(stage, s, KO0);
            Assert.IsTrue(o.Valid); Assert.AreEqual(1, o.After.SmokeLeftAt(C(4, 4))); Assert.AreEqual(1, o.After.EmpLeftOn(0));
            Assert.AreEqual(DirectionUtil.Rotate(Direction.South, 1), CameraTrack.FacingAt(stage.Cameras[0], o.After.T), "disabled camera keeps turning");
        }

        [Test]
        public void EmpExpiryOnTheKnockout_StartsTheFullLockdownBudget_AndTheLastActionExitWins()
        {
            var b = Board("...#.", ".....", ".....", ".....", "....."); b.Start = C(2, 1); b.Exit = C(4, 1); b.AlarmBudget = 1;
            b.Guards.Add(Static(C(3, 1), Direction.North)); b.Cameras.Add(Camera(C(3, 0), Direction.South, Direction.South));
            var stage = b.Build();
            var s = With(StageState.Initial(stage, KO), emps: new[] { new EmpEffect(0, 1) });
            var o = StageRules.TryApply(stage, s, KO0);
            Assert.IsTrue(o.Valid); Assert.AreEqual(StageResult.Playing, o.After.Result);
            Assert.AreEqual(1, o.After.Alarm, "newly triggered on expiry: full budget, the trigger action does not consume it"); Assert.AreEqual(1, o.After.T); Assert.AreEqual(0, o.After.Emps.Count);
            Assert.IsTrue(o.AlarmTriggered); Assert.IsTrue(o.EmpExpired);
            var end = StageRules.TryApply(stage, o.After, E);
            Assert.IsTrue(end.Won); Assert.AreEqual(0, end.After.Alarm); Assert.AreEqual(StageResult.Won, end.After.Result);
        }

        [Test]
        public void SmokeExpiryAfterTheKnockout_CanStillCauseACapture()
        {
            var b = Fixture(C(3, 2)); b.Guards.Add(Static(C(2, 4), Direction.North)); // sees (2,3),(2,2) once the smoke on (2,2) clears
            var stage = b.Build();
            var s = With(StageState.Initial(stage, KO), clouds: new[] { new SmokeCloud(C(2, 2), 1) });
            var o = StageRules.TryApply(stage, s, KO0);
            Assert.IsTrue(o.Caught); Assert.AreEqual(CapturePhase.AfterGadgetExpiry, o.CapturePhase); Assert.AreEqual(1, o.CapturingGuard);
        }

        [Test]
        public void Camera_ObservesAnOrdinaryKnockout()
        {
            var b = Board("...#.", ".....", ".....", ".....", "....."); b.Start = C(2, 1); b.Exit = C(4, 4); b.AlarmBudget = 3;
            b.Guards.Add(Static(C(3, 1), Direction.North)); b.Cameras.Add(Camera(C(3, 0), Direction.South, Direction.South));
            var stage = b.Build();
            var o = StageRules.TryApply(stage, StageState.Initial(stage, KO), KO0);
            Assert.IsTrue(o.Valid); Assert.IsTrue(o.AlarmTriggered); Assert.AreEqual(3, o.After.Alarm); Assert.IsTrue(o.After.AlarmEver);
        }

        [Test]
        public void GuardFreeLevel_ModeSelectionHasNoEffect()
        {
            var stage = Open(4, 1, C(0, 0), C(3, 0)).Build();
            var st = StageState.Initial(stage); var ko = StageState.Initial(stage, KO);
            Assert.AreEqual(0, StageRules.KnockoutTargets(stage, ko).Count);
            for (int i = 0; i < 3; i++) { st = StageRules.TryApply(stage, st, E).After; ko = StageRules.TryApply(stage, ko, E).After; }
            Assert.AreEqual(StageResult.Won, st.Result); Assert.AreEqual(StageResult.Won, ko.Result); Assert.AreEqual(st.T, ko.T);
            Assert.AreEqual(RejectReason.BadGuard, StageRules.CheckKnockout(stage, StageState.Initial(stage, KO), 0, out _, out _));
        }

        [Test]
        public void Tokens_StateKey_AndInitialStateCarryTheMode()
        {
            Assert.IsTrue(StageAction.TryParse("KO#2", out var a)); Assert.AreEqual(StageActionKind.Knockout, a.Kind); Assert.AreEqual(2, a.GuardIndex); Assert.AreEqual("KO#2", a.Token); Assert.IsTrue(a.IsCounted);
            var stage = Fixture(C(3, 2)).Build();
            var st = StageState.Initial(stage); var ko = StageState.Initial(stage, KO);
            Assert.AreEqual(StageMode.Stealth, st.Mode); Assert.AreEqual(StageMode.Knockout, ko.Mode); Assert.AreEqual(0UL, ko.Defeated);
            Assert.AreNotEqual(st.Key(), ko.Key(), "mode is part of the search identity");
            var after = StageRules.TryApply(stage, ko, KO0).After;
            Assert.AreNotEqual(ko.Key(), after.Key()); Assert.IsTrue(after.Key().Contains("d1"));
            Assert.IsTrue(StageModeUtil.TryParse("knockout", out var m) && m == KO); Assert.IsFalse(StageModeUtil.TryParse("brawl", out _));
            Assert.AreEqual("knockout-nowait-v3", StageModeUtil.RulesVersion, "D-044 revision");
        }

        [Test]
        public void Validator_RejectsMoreGuardsThanTheDefeatedMaskHolds()
        {
            var b = Open(9, 9, C(0, 0), C(8, 8));
            int n = 0; for (int y = 0; y < 9 && n < 65; y++) for (int x = 0; x < 9 && n < 65; x++) { if ((x == 0 && y == 0) || (x == 8 && y == 8)) continue; b.Guards.Add(new GuardSpec("g" + n, GuardKind.Static, new[] { C(x, y) }, Direction.North)); n++; }
            var r = StageValidator.Validate(b.Build(), null, null);
            Assert.IsFalse(r.IsValid); Assert.IsTrue(r.ToString().Contains("knockout bitmask"));
        }

        [Test]
        public void Solver_KnockoutMode_FindsTheShorterRoute_AndZeroKnockoutsEqualsStealth()
        {
            // The exit corridor (row 1) is plugged by a static guard; the only other way up, (3,2), ends beside the guard. Stealth has no route; Knockout takes the guard from its side.
            var b = Board("#######", "#.....#", "#.#.###", "#.....#", "#######"); b.Start = C(1, 1); b.Exit = C(5, 1);
            b.Guards.Add(Static(C(3, 1), Direction.West)); // sees (2,1),(1,1); the player already stands on (1,1) and is only checked when acting
            var stage = b.Build();
            var stealth = StageSolver.Solve(stage);
            var knockout = StageSolver.Solve(stage, new SolveOptions { Mode = KO });
            Assert.IsTrue(knockout.Found, "knockout route exists: " + knockout.Tokens);
            Assert.IsTrue(knockout.Tokens.Contains("KO#0"), knockout.Tokens);
            Assert.AreEqual(1, knockout.Knockouts);
            var noKo = StageSolver.Solve(stage, new SolveOptions { Mode = KO, MaxKnockouts = 0 });
            Assert.AreEqual(stealth.Found, noKo.Found, "Knockout mode without knockouts is exactly Stealth");
            if (stealth.Found) Assert.AreEqual(stealth.CountedActions, noKo.CountedActions);
            var replay = StageReplay.Run(stage, knockout.Actions, KO);
            Assert.IsTrue(replay.Won, replay.Trace()); Assert.AreEqual(knockout.CountedActions, replay.CountedActions); Assert.AreEqual(1, replay.Final.DefeatedCount);
            Assert.IsFalse(StageReplay.Run(stage, knockout.Actions, StageMode.Stealth).Won, "the same tokens are rejected in Stealth");
        }

        private static StagePack ShippedPack() => StageJson.Parse(File.ReadAllText(Path.Combine(TestPaths.ProjectRoot, "Assets/StreamingAssets/ShadowSteps/levels_v4.json")));

        [Test]
        public void ShippedLevels_StealthSolutionReplaysIdenticallyUnderKnockoutRules()
        {
            var pack = ShippedPack(); int checked_ = 0;
            foreach (var stage in pack.Stages)
            {
                var sol = StageSolver.Solve(stage);
                if (!sol.Found) continue;
                var a = StageState.Initial(stage, StageMode.Stealth); var k = StageState.Initial(stage, KO);
                foreach (var action in sol.Actions)
                {
                    var oa = StageRules.TryApply(stage, a, action); var ok = StageRules.TryApply(stage, k, action);
                    Assert.AreEqual(oa.Valid, ok.Valid, stage.Code + " " + action);
                    Assert.AreEqual(oa.Action.Token, ok.Action.Token, stage.Code + ": a Stealth solution never steps onto a guard, so nothing normalises to a knockout");
                    a = oa.After; k = ok.After;
                    Assert.AreEqual(a.Player, k.Player); Assert.AreEqual(a.T, k.T); Assert.AreEqual(a.HasKey, k.HasKey); Assert.AreEqual(a.OpenDoors, k.OpenDoors);
                    Assert.AreEqual(a.Clouds.Count, k.Clouds.Count); Assert.AreEqual(a.Emps.Count, k.Emps.Count); Assert.AreEqual(a.SmokeCharges, k.SmokeCharges); Assert.AreEqual(a.EmpCharges, k.EmpCharges);
                    Assert.AreEqual(a.Alarm, k.Alarm); Assert.AreEqual(a.AlarmEver, k.AlarmEver); Assert.AreEqual(a.Result, k.Result); Assert.AreEqual(a.WaitsUsed, k.WaitsUsed);
                    Assert.AreEqual(0UL, k.Defeated);
                }
                Assert.AreEqual(StageResult.Won, k.Result, stage.Code);
                checked_++;
            }
            Assert.GreaterOrEqual(checked_, 30, "almost every shipped level still has a (legacy, no-WAIT) Stealth solution");
        }

        [Test]
        public void ShippedLevels_SolvableInBothModes_KnockoutNeverLongerThanStealth()
        {
            var pack = ShippedPack(); var lines = new List<string>(); var failures = new List<string>();
            foreach (var stage in pack.Stages)
            {
                var st = StageSolver.Solve(stage); var ko = StageSolver.Solve(stage, new SolveOptions { Mode = KO });
                lines.Add(stage.Code + ": stealth " + (st.Found ? st.CountedActions.ToString() : "none") + ", knockout " + (ko.Found ? ko.CountedActions + " (ko " + ko.Knockouts + ")" : "none") + (ko.HitLimit || st.HitLimit ? " LIMIT" : ""));
                if (st.Found && !ko.Found) failures.Add(stage.Code + " solvable in Stealth but not in Knockout");
                if (st.Found && ko.Found && ko.CountedActions > st.CountedActions) failures.Add(stage.Code + " knockout par " + ko.CountedActions + " > stealth par " + st.CountedActions);
                if (!ko.Found || ko.HitLimit) failures.Add(stage.Code + " unsolvable in Knockout (the shipped mode) on the shipped layout");
                // Stealth is legacy (D-043) and WAIT is gone (D-044): boards designed for Knockout (D-046/D-048 redesign notes) may require a knockout
                // outright, so a missing Stealth route is only a failure on a board that was never redesigned.
                bool redesigned = false; foreach (var n in stage.Notes) if (n.Contains("redesigned")) redesigned = true;
                if (!st.Found && !redesigned) failures.Add(stage.Code + " unexpectedly unsolvable in legacy Stealth");
                if (ko.Found)
                {
                    var replay = StageReplay.Run(stage, ko.Actions, KO);
                    if (!replay.Won || replay.CountedActions != ko.CountedActions) failures.Add(stage.Code + " knockout solution does not replay: " + replay.Failure);
                    if (stage.HasKnockoutScoring && stage.KnockoutPar != ko.CountedActions) failures.Add(stage.Code + " stored knockoutPar " + stage.KnockoutPar + " != solver " + ko.CountedActions);
                }
            }
            TestContext.Out.WriteLine(string.Join("\n", lines));
            Assert.IsEmpty(failures, string.Join("\n", failures));
        }
    }
}
