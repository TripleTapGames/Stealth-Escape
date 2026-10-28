using System.Collections.Generic;
using NUnit.Framework;
using ShadowSteps.Core;

namespace ShadowSteps.Tests.EditMode
{
    /// <summary>
    /// The gate for M1: every validated solution from reference/validation.json must win in C# with the exact
    /// action count, and the C# solver must reproduce the Python breadth-first search exactly
    /// (same path, same number of explored states).
    /// </summary>
    public class ReferenceParityTests
    {
        [Test]
        public void ValidationFile_Covers20Levels()
        {
            var sols = TestPaths.LoadReferenceSolutions();
            Assert.AreEqual(20, sols.Count);
            for (int i = 0; i < sols.Count; i++)
            {
                Assert.AreEqual(i + 1, sols[i].Level);
                Assert.AreEqual(sols[i].Moves, sols[i].Actions.Count, "moves count of level " + sols[i].Level);
                Assert.AreEqual(sols[i].Name, TestPaths.Level(sols[i].Level).Name);
            }
        }

        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        [TestCase(6)] [TestCase(7)] [TestCase(8)] [TestCase(9)] [TestCase(10)]
        [TestCase(11)] [TestCase(12)] [TestCase(13)] [TestCase(14)] [TestCase(15)]
        [TestCase(16)] [TestCase(17)] [TestCase(18)] [TestCase(19)] [TestCase(20)]
        public void ReferenceSolution_WinsWithExactTurnCount(int levelId)
        {
            var sol = TestPaths.LoadReferenceSolutions()[levelId - 1];
            var report = GameSession.Replay(TestPaths.Level(levelId), sol.Actions);
            Assert.IsTrue(report.Won, "level " + levelId + " replay did not win:\n" + report.Trace());
            Assert.AreEqual(sol.Moves, report.ValidTurns, "turn count mismatch for level " + levelId + "\n" + report.Trace());
            Assert.AreEqual(-1, report.FailedAtIndex);
        }

        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        [TestCase(6)] [TestCase(7)] [TestCase(8)] [TestCase(9)] [TestCase(10)]
        [TestCase(11)] [TestCase(12)] [TestCase(13)] [TestCase(14)] [TestCase(15)]
        [TestCase(16)] [TestCase(17)] [TestCase(18)] [TestCase(19)] [TestCase(20)]
        public void Solver_ReproducesReferencePathAndStateCount(int levelId)
        {
            var sol = TestPaths.LoadReferenceSolutions()[levelId - 1];
            var result = Solver.Solve(TestPaths.Level(levelId));
            Assert.IsTrue(result.Found, "solver found no solution for level " + levelId);
            Assert.AreEqual(GameActionUtil.ToTokenString(sol.Actions), result.ActionTokens, "path mismatch for level " + levelId);
            Assert.AreEqual(sol.StatesChecked, result.StatesExplored, "explored-state count mismatch for level " + levelId);
        }

        [Test]
        public void EveryPrefixOfEverySolution_IsSafe()
        {
            foreach (var sol in TestPaths.LoadReferenceSolutions())
            {
                var level = TestPaths.Level(sol.Level);
                var session = new GameSession(level);
                for (int i = 0; i < sol.Actions.Count; i++)
                {
                    var r = session.Apply(sol.Actions[i]);
                    Assert.IsTrue(r.IsValid, "level " + sol.Level + " action " + (i + 1) + " invalid: " + r.Validation.Reason);
                    Assert.IsFalse(r.Captured, "level " + sol.Level + " action " + (i + 1) + " captured");
                    Assert.AreEqual(i == sol.Actions.Count - 1, r.Won, "level " + sol.Level + " action " + (i + 1) + " win flag");
                }
            }
        }

        [TestCase(14)] [TestCase(17)] [TestCase(19)] [TestCase(20)]
        public void GadgetLevels_ShortestSolutionUsesAGadget(int levelId)
        {
            // build.py enforced this for ids 14, 17, 19, 20 when the levels were generated.
            var sol = TestPaths.LoadReferenceSolutions()[levelId - 1];
            Assert.IsTrue(sol.Actions.Contains(GameAction.Smoke) || sol.Actions.Contains(GameAction.Emp));
        }

        [Test]
        public void ConstrainedSolves_ReportWhichGadgetsAreTrulyRequired()
        {
            // The spec asks to prove or disprove "required mechanic" claims with constrained solves instead of trusting
            // the generator. The sets below are the measured truth for the shipped data (see Docs/DECISIONS.md);
            // a change here means the rules or the data drifted.
            var expectedUnsolvableWithoutSmoke = new HashSet<int> { 11, 17, 19, 20 };
            var expectedUnsolvableWithoutEmp = new HashSet<int>();
            var actualNoSmoke = new HashSet<int>();
            var actualNoEmp = new HashSet<int>();
            var lines = new List<string>();
            foreach (var level in TestPaths.LoadPack().Levels)
            {
                if (level.SmokeCharges == 0 && level.EmpCharges == 0) continue;
                var noSmoke = Solver.Solve(level, new SolverOptions { AllowSmoke = false });
                var noEmp = Solver.Solve(level, new SolverOptions { AllowEmp = false });
                var none = Solver.Solve(level, new SolverOptions { AllowSmoke = false, AllowEmp = false });
                lines.Add("level " + level.Id + ": noSmoke=" + (noSmoke.Found ? noSmoke.Actions.Count.ToString() : "unsolvable") +
                          " noEmp=" + (noEmp.Found ? noEmp.Actions.Count.ToString() : "unsolvable") +
                          " noGadgets=" + (none.Found ? none.Actions.Count.ToString() : "unsolvable"));
                if (level.SmokeCharges > 0 && !noSmoke.Found) actualNoSmoke.Add(level.Id);
                if (level.EmpCharges > 0 && !noEmp.Found) actualNoEmp.Add(level.Id);
            }
            TestContext.WriteLine(string.Join("\n", lines));
            CollectionAssert.AreEquivalent(expectedUnsolvableWithoutSmoke, actualNoSmoke, "levels that truly require smoke\n" + string.Join("\n", lines));
            CollectionAssert.AreEquivalent(expectedUnsolvableWithoutEmp, actualNoEmp, "levels that truly require EMP\n" + string.Join("\n", lines));
        }
    }
}
