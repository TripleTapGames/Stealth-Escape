using NUnit.Framework;
using ShadowSteps.Core;
using ShadowSteps.Data;
using static ShadowSteps.Tests.EditMode.LevelBuilder;

namespace ShadowSteps.Tests.EditMode
{
    /// <summary>
    /// V2 detection: after the player acts, a cone captures if the player's node is in guard vision either before or
    /// after the guards move. V1 fixtures keep the single after-move check, selected per level by <see cref="DetectionRule"/>.
    /// </summary>
    public class DetectionRuleTests
    {
        private static LevelDefinition V2(LevelDefinition legacy) => legacy.With(detection: DetectionRule.BeforeOrAfterGuardMove);

        [Test]
        public void LegacyConstructor_UsesV1Rule_AndSpecDefaultsToV2Rule()
        {
            Assert.AreEqual(DetectionRule.AfterGuardMove, Empty().Detection);
            Assert.AreEqual(DetectionRule.BeforeOrAfterGuardMove, LevelDefinition.FromSpec(new LevelSpec { Id = 1, Name = "t", Width = 2, Height = 1, Start = C(0, 0), Exit = C(1, 0) }).Detection);
            Assert.AreEqual(DetectionRule.BeforeOrAfterGuardMove, LevelDefinition.DefaultDetection);
        }

        [Test]
        public void FixedGuard_SeesBeforeAndAfter_Captures()
        {
            // Guard at (0,2) facing S, range 2: (0,3) is lit at every phase, so stepping into it is lethal under both rules.
            var legacy = Empty(Guard(GuardMode.Fixed, Direction.South, 2, C(0, 2)));
            foreach (var level in new[] { legacy, V2(legacy) })
            {
                var r = RulesEngine.Step(level, RulesEngine.CreateInitialState(level), GameAction.North);
                Assert.AreEqual(CaptureKind.Seen, r.Capture, level.Detection.ToString());
                Assert.AreEqual(0, r.CapturingGuard);
            }
        }

        [Test]
        public void GuardTurningOntoThePlayer_CapturesUnderBothRules()
        {
            // Rotating guard at (2,2) range 2. Phase 0 faces N; the guard turns E on the first action.
            // Player at (4,3) steps N to (4,2): dark at phase 0, lit at phase 1 (E cone covers (3,2),(4,2)).
            var legacy = Make(5, 5, C(4, 3), C(0, 0), guards: new[] { Guard(GuardMode.Rotate, Direction.North, 2, C(2, 2)) });
            foreach (var level in new[] { legacy, V2(legacy) })
            {
                var r = RulesEngine.Step(level, RulesEngine.CreateInitialState(level), GameAction.North);
                Assert.AreEqual(CaptureKind.Seen, r.Capture, level.Detection.ToString());
                Assert.AreEqual(0, r.CapturingGuard);
            }
        }

        [Test]
        public void WalkingIntoAConeThatThenTurnsAway_IsLethalUnderV2_SafeUnderV1()
        {
            // Rotating guard at (2,2), range 2, faces E at phase 0 then S. Player at (4,3) steps N to (4,2):
            // lit before the guard turns (E cone), dark after (S cone). V1 only looks after the turn.
            var legacy = Make(5, 5, C(4, 3), C(0, 0), guards: new[] { Guard(GuardMode.Rotate, Direction.East, 2, C(2, 2)) });
            var v1 = RulesEngine.Step(legacy, RulesEngine.CreateInitialState(legacy), GameAction.North);
            Assert.AreEqual(CaptureKind.None, v1.Capture, "V1: dark after the guard turned");

            var level = V2(legacy);
            var v2 = RulesEngine.Step(level, RulesEngine.CreateInitialState(level), GameAction.North);
            Assert.AreEqual(CaptureKind.Seen, v2.Capture, "V2: the player walked through the cone before the guard turned");
            Assert.AreEqual(0, v2.CapturingGuard);
            Assert.AreEqual(GameOutcome.Captured, v2.StateAfter.Outcome);
        }

        [Test]
        public void WaitingInADarkNode_IsSafeUntilAConeReachesIt()
        {
            // Rotating guard at (2,2), range 2, faces N at phase 0. Player at (0,4): dark for N and E, lit for S (forward 2, side 2) and W.
            var legacy = Make(5, 5, C(0, 4), C(4, 0), guards: new[] { Guard(GuardMode.Rotate, Direction.North, 2, C(2, 2)) });
            var s = new GameSession(V2(legacy));
            Assert.AreEqual(CaptureKind.None, s.Apply(GameAction.Wait).Capture, "N before, E after: dark both");
            Assert.AreEqual(CaptureKind.Seen, s.Apply(GameAction.Wait).Capture, "E before (dark), S after (lit): captured");
        }

        [Test]
        public void StayingInsideTheConeAcrossTheGuardMove_Captures()
        {
            // Patrol (1,0)->(1,1)->(1,2) facing its travel direction (S for the first two phases), range 3, on a 3x5 board.
            // Player steps N from (0,4) to (0,3): lit from (1,0) (forward 3, side 1) and from (1,1) (forward 2, side 1).
            var legacy = Make(3, 5, C(0, 4), C(2, 0), guards: new[] { Guard(GuardMode.Patrol, Direction.South, 3, C(1, 0), C(1, 1), C(1, 2)) });
            var level = V2(legacy);
            var r = RulesEngine.Step(level, RulesEngine.CreateInitialState(level), GameAction.North);
            Assert.AreEqual(CaptureKind.Seen, r.Capture);
            Assert.AreEqual(Direction.South, r.GuardsBefore[0].Facing);
            Assert.AreEqual(Direction.South, r.GuardsAfter[0].Facing);
        }

        [Test]
        public void ASightingByAnyGuardAtEitherLook_Captures_AndReportsTheGuard()
        {
            // Guard A rotates at (0,1), range 2, faces S at phase 0 (lights (0,3)) and W at phase 1 (dark).
            // Guard B rotates at (2,3), range 2, faces S at phase 0 (dark for (0,3)) and W at phase 1 (lights (0,3)).
            var legacy = Make(5, 5, C(0, 4), C(4, 0), guards: new[]
            {
                Guard(GuardMode.Rotate, Direction.South, 2, C(0, 1)),
                Guard(GuardMode.Rotate, Direction.South, 2, C(2, 3))
            });
            var level = V2(legacy);
            var blocked = VisionSystem.BlockedPredicate(level, false);
            Assert.IsTrue(VisionSystem.ConeVisible(GuardSchedule.PosesAt(level, 0)[0], C(0, 3), blocked));
            Assert.IsFalse(VisionSystem.ConeVisible(GuardSchedule.PosesAt(level, 1)[0], C(0, 3), blocked));
            Assert.IsFalse(VisionSystem.ConeVisible(GuardSchedule.PosesAt(level, 0)[1], C(0, 3), blocked));
            Assert.IsTrue(VisionSystem.ConeVisible(GuardSchedule.PosesAt(level, 1)[1], C(0, 3), blocked));

            var both = RulesEngine.Step(level, RulesEngine.CreateInitialState(level), GameAction.North);
            Assert.AreEqual(CaptureKind.Seen, both.Capture);
            Assert.AreEqual(1, both.CapturingGuard, "the guard that sees the player after the move is reported first");

            // Only guard A (before the move): still captured, and A is reported.
            var onlyA = V2(Make(5, 5, C(0, 4), C(4, 0), guards: new[] { Guard(GuardMode.Rotate, Direction.South, 2, C(0, 1)) }));
            var r = RulesEngine.Step(onlyA, RulesEngine.CreateInitialState(onlyA), GameAction.North);
            Assert.AreEqual(CaptureKind.Seen, r.Capture);
            Assert.AreEqual(0, r.CapturingGuard);
        }

        [Test]
        public void Emp_FreezesGuards_SoASingleSightingCaptures()
        {
            var legacy = Make(5, 5, C(0, 4), C(4, 0), guards: new[] { Guard(GuardMode.Rotate, Direction.West, 1, C(1, 3)) }, emp: 1);
            var s = new GameSession(V2(legacy));
            var r = s.Apply(GameAction.Emp); // phase stays 0: guard faces W and lights (0,4) before and after
            Assert.AreEqual(CaptureKind.Seen, r.Capture);
        }

        [Test]
        public void Smoke_ConcealsBothLooks()
        {
            var legacy = Make(5, 5, C(0, 4), C(4, 0), guards: new[] { Guard(GuardMode.Fixed, Direction.South, 3, C(0, 1)) }, smoke: 1);
            var s = new GameSession(V2(legacy));
            Assert.AreEqual(CaptureKind.Seen, s.Preview(GameAction.North).Capture);
            Assert.AreEqual(CaptureKind.None, s.Apply(GameAction.Smoke).Capture);
            Assert.AreEqual(CaptureKind.None, s.Apply(GameAction.North).Capture, "concealed");
            Assert.AreEqual(CaptureKind.Seen, s.Apply(GameAction.Wait).Capture, "smoke expired; fixed guard lights (0,3) before and after");
        }

        [Test]
        public void V2Rule_IsNeverMoreLenientThanV1()
        {
            // Every capture under V1 is a capture under V2 (V2 adds the before-move look), and non-sight captures are identical.
            foreach (var legacy in TestPaths.LoadPack().Levels)
            {
                var v2 = V2(legacy);
                var seen = new System.Collections.Generic.HashSet<string>();
                var frontier = new System.Collections.Generic.Queue<GameState>();
                frontier.Enqueue(RulesEngine.CreateInitialState(legacy));
                int budget = 4000;
                while (frontier.Count > 0 && budget-- > 0)
                {
                    var st = frontier.Dequeue();
                    foreach (var a in GameActionUtil.ReferenceOrder)
                    {
                        var r1 = RulesEngine.Step(legacy, st, a);
                        if (!r1.IsValid) continue;
                        var r2 = RulesEngine.Step(v2, st, a);
                        if (r1.Capture != CaptureKind.None) Assert.AreEqual(r1.Capture, r2.Capture, "level " + legacy.Id + " " + st.ToReferenceTuple() + " " + a);
                        else Assert.IsTrue(r2.Capture == CaptureKind.None || r2.Capture == CaptureKind.Seen, "level " + legacy.Id + " " + st.ToReferenceTuple() + " " + a);
                        if (!r1.IsTerminal && seen.Add(r1.StateAfter.ToReferenceTuple())) frontier.Enqueue(r1.StateAfter);
                    }
                }
            }
        }

        [Test]
        public void DangerCells_AreTheUnionOfCurrentAndNextCones()
        {
            var legacy = Make(5, 5, C(4, 3), C(0, 0), guards: new[] { Guard(GuardMode.Rotate, Direction.North, 2, C(2, 2)) });
            var level = V2(legacy);
            var state = RulesEngine.CreateInitialState(level);
            var now = RulesEngine.GetAllVisionCells(level, state);
            var next = RulesEngine.GetAllVisionCells(level, new GameState(state.PlayerPosition, 1, false, 0, 0, 0, 0, GameOutcome.Playing));
            var expected = new System.Collections.Generic.HashSet<GridCoord>(now); expected.UnionWith(next);
            CollectionAssert.AreEquivalent(expected, RulesEngine.GetDangerCells(level, state));
            CollectionAssert.AreEquivalent(next, RulesEngine.GetDangerCells(legacy, state), "V1 danger = next-phase cones");
        }

        [Test]
        public void SchemaV2_ParsesWritesAndDefaultsTheRule()
        {
            var spec = new LevelSpec { Id = 7, Name = "r", Width = 3, Height = 1, Start = C(0, 0), Exit = C(2, 0), Detection = DetectionRule.AfterGuardMove };
            string json = LevelPackWriter.ToJson(LevelDefinition.FromSpec(spec));
            StringAssert.Contains("\"detection\": \"afterGuardMove\"", json);
            var back = LevelPackLoader.Parse(json).Levels[0];
            Assert.AreEqual(DetectionRule.AfterGuardMove, back.Detection);

            spec.Detection = DetectionRule.BeforeOrAfterGuardMove;
            json = LevelPackWriter.ToJson(LevelDefinition.FromSpec(spec));
            StringAssert.Contains("\"detection\": \"beforeOrAfterGuardMove\"", json);
            Assert.AreEqual(DetectionRule.BeforeOrAfterGuardMove, LevelPackLoader.Parse(json).Levels[0].Detection);

            string noRules = System.Text.RegularExpressions.Regex.Replace(json, ",?\\s*\"rules\": \\{[^}]*\\}", "");
            Assert.IsFalse(noRules.Contains("detection"), "fixture must drop the rules block:\n" + noRules);
            Assert.AreEqual(DetectionRule.BeforeOrAfterGuardMove, LevelPackLoader.Parse(noRules).Levels[0].Detection, "schema 2 default");

            Assert.Throws<LevelDataException>(() => LevelPackLoader.Parse(json.Replace("beforeOrAfterGuardMove", "sometimes")));
        }

        [Test]
        public void LegacyFile_KeepsV1Rule_AndRecordsItInConversionNotes()
        {
            foreach (var l in TestPaths.LoadPack().Levels)
            {
                Assert.AreEqual(DetectionRule.AfterGuardMove, l.Detection, "level " + l.Id);
                StringAssert.Contains("detection rule kept as afterGuardMove", string.Join("\n", l.ConversionNotes));
            }
            foreach (var l in TestPaths.LoadPackagedPack().Levels)
                Assert.AreEqual(DetectionRule.BeforeOrAfterGuardMove, l.Detection, "shipped level " + l.Id);
        }

        [Test]
        public void AuthoringModel_RoundTripsTheRule()
        {
            var legacy = Empty();
            var model = LevelAuthoringModel.FromDefinition(legacy);
            Assert.AreEqual(DetectionRule.AfterGuardMove, model.Detection);
            model.Detection = DetectionRule.BeforeOrAfterGuardMove;
            Assert.AreEqual(DetectionRule.BeforeOrAfterGuardMove, model.ToDefinition().Detection);
        }

        private static LevelDefinition Empty(params GuardDefinition[] guards) => Make(5, 5, C(0, 4), C(4, 0), guards: guards);
    }
}
