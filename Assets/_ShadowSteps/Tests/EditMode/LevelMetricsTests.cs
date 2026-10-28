using NUnit.Framework;
using ShadowSteps.Core;

namespace ShadowSteps.Tests.EditMode
{
    public class LevelMetricsTests
    {
        [Test]
        public void Level1_MetricsAreConsistentWithSolver()
        {
            var m = LevelMetricsAnalyzer.Analyze(TestPaths.Level(1));
            Assert.AreEqual(8, m.ShortestWinTurns);
            Assert.AreEqual(8, m.ShortestWithoutWait);
            Assert.AreEqual(8, m.ShortestWithoutGadgets);
            Assert.AreEqual(1, m.GuardCycleLcm);
            Assert.IsTrue(m.ShortestSolutionCount >= 1);
            Assert.IsTrue(m.AverageBranchingFactor > 1.0);
            Assert.IsTrue(m.SafeNodeRatio > 0 && m.SafeNodeRatio < 1);
            TestContext.WriteLine(m.ToString());
        }

        [Test]
        public void UniquenessCount_MatchesBruteForceOnTinyLevel()
        {
            // 2x2 open board, start (0,1) exit (1,0): exactly two shortest 2-move solutions (E,N) and (N,E).
            var s = new LevelSpec { Id = 1, Name = "t", Width = 2, Height = 2, Start = new GridCoord(0, 1), Exit = new GridCoord(1, 0) };
            var level = LevelDefinition.FromSpec(s);
            Assert.AreEqual(2, LevelMetricsAnalyzer.CountShortestSolutions(level, 2, 100, out var capped));
            Assert.IsFalse(capped);
        }

        [Test]
        public void MetricsForAllLegacyLevels_Print()
        {
            foreach (var l in TestPaths.LoadPack().Levels)
            {
                var m = LevelMetricsAnalyzer.Analyze(l);
                Assert.IsTrue(m.ShortestWinTurns > 0, "level " + l.Id);
                TestContext.WriteLine(m.ToString());
            }
        }
    }
}
