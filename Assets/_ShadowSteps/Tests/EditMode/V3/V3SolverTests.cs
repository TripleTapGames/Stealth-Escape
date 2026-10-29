using NUnit.Framework;
using ShadowSteps.Core;
using ShadowSteps.V3;
using static ShadowSteps.Tests.EditMode.V3.V3TestKit;

namespace ShadowSteps.Tests.EditMode.V3
{
    /// <summary>Handover §11: 0-cost gadgets, exact counted-action minimisation, Ghost search, replay fidelity.</summary>
    public class V3SolverTests
    {
        [Test]
        public void GadgetDeployments_CostZeroActions()
        {
            var b = Board("#######", "###.###", ".......", "#######"); b.Start = C(0, 2); b.Exit = C(6, 2); b.SmokeCharges = 1;
            b.Guards.Add(Static(C(3, 1), Direction.South));
            var stage = b.Build();
            var r = StageSolver.Solve(stage);
            Assert.IsTrue(r.Found);
            Assert.AreEqual(6, r.CountedActions, r.Tokens);
            Assert.AreEqual(7, r.Actions.Count, "six moves plus one free smoke: " + r.Tokens);
            Assert.IsFalse(StageSolver.Solve(stage, new SolveOptions { AllowSmoke = false }).Found, "smoke is mandatory here");
            var replay = StageReplay.Run(stage, r.Actions);
            Assert.IsTrue(replay.Won, replay.Trace()); Assert.AreEqual(6, replay.CountedActions);
        }

        [Test]
        public void AlarmFreeSearch_FindsTheLongerGhostRoute()
        {
            var b = Board("#######", "#.....#", "#.....#", "#.....#", "#######"); b.Start = C(1, 3); b.Exit = C(5, 3); b.AlarmBudget = 5;
            b.Cameras.Add(Camera(C(3, 0), Direction.South, Direction.South));
            var stage = b.Build();
            var fast = StageSolver.Solve(stage);
            var ghost = StageSolver.Solve(stage, new SolveOptions { AlarmFreeOnly = true });
            Assert.AreEqual(4, fast.CountedActions); Assert.IsTrue(fast.AlarmTriggered);
            Assert.AreEqual(8, ghost.CountedActions); Assert.IsFalse(ghost.AlarmTriggered);
            var replay = StageReplay.Run(stage, ghost.Actions);
            Assert.IsTrue(replay.Won); Assert.IsFalse(replay.Final.AlarmEver);
        }

        [Test]
        public void Solver_RespectsLockdown_AndReportsUnsolvable()
        {
            var b = Board("#######", "#######", ".....##", "#######"); b.Start = C(0, 2); b.Exit = C(4, 2); b.AlarmBudget = 1;
            b.Cameras.Add(Camera(C(4, 1), Direction.South, Direction.West));
            Assert.IsFalse(StageSolver.Solve(b.Build()).Found);
            b.AlarmBudget = 2;
            Assert.IsTrue(StageSolver.Solve(b.Build()).Found, "budget 2: alarm at (2,2), then (3,2) and the exit");
        }

        [Test]
        public void StarThresholds_FollowTheProvisionalFormula()
        {
            Assert.AreEqual(5, StageScoring.ThreeStarThreshold(4)); Assert.AreEqual(6, StageScoring.TwoStarThreshold(4));
            Assert.AreEqual(12, StageScoring.ThreeStarThreshold(11)); Assert.AreEqual(15, StageScoring.TwoStarThreshold(11));
            Assert.AreEqual(22, StageScoring.ThreeStarThreshold(20)); Assert.AreEqual(26, StageScoring.TwoStarThreshold(20));
            Assert.AreEqual(33, StageScoring.ThreeStarThreshold(30), "tolerance caps at 3");
            Assert.AreEqual(3, StageScoring.Stars(5, 4)); Assert.AreEqual(2, StageScoring.Stars(6, 4)); Assert.AreEqual(1, StageScoring.Stars(7, 4));
        }

        [Test]
        public void ActionTokens_RoundTrip()
        {
            foreach (var a in new[] { StageAction.Wait, StageAction.Move(Direction.West), StageAction.Smoke(C(3, 2)), StageAction.Emp(1) })
            {
                Assert.IsTrue(StageAction.TryParse(a.Token, out var back), a.Token);
                Assert.AreEqual(a, back);
            }
            Assert.IsFalse(StageAction.TryParse("DANCE", out _));
        }
    }
}
