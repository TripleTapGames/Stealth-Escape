using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using ShadowSteps.Core;
using ShadowSteps.Data;
using ShadowSteps.V3;

namespace ShadowSteps.Tests.EditMode.V3
{
    /// <summary>D-072: the helpers behind the Unity stage editor (single-stage JSON round trip, scoring recompute, validation export) must be exact on the shipped pack.</summary>
    public class StagePackToolsTests
    {
        private static string P(string rel) => Path.Combine(TestPaths.ProjectRoot, rel);
        private static StagePack Pack() => StageJson.Parse(File.ReadAllText(P(V3PackParityTests.PackRelative)));

        [Test]
        public void StageJsonRoundTrip_PreservesEveryShippedStage()
        {
            var pack = Pack();
            var again = new List<Stage>();
            foreach (var s in pack.Stages) again.Add(StagePackTools.StageFromJson(StagePackTools.StageToJson(s)));
            Assert.AreEqual(StageJson.ToJson(pack.Stages), StageJson.ToJson(again));
        }

        [Test]
        public void RecomputedScoring_MatchesStoredScoring_OnEveryShippedStage()
        {
            foreach (var s in Pack().Stages)
            {
                var u = StagePackTools.WithRecomputedScoring(s, out var m);
                Assert.IsTrue(m.Solvable, s.Code + " must stay solvable under the shipped rules");
                Assert.AreEqual(s.Par, u.Par, s.Code + " stealth par");
                Assert.AreEqual(s.ThreeStarActions, u.ThreeStarActions, s.Code + " stealth 3*");
                Assert.AreEqual(s.TwoStarActions, u.TwoStarActions, s.Code + " stealth 2*");
                Assert.AreEqual(s.GhostOffered, u.GhostOffered, s.Code + " ghost");
                Assert.AreEqual(s.KnockoutPar, u.KnockoutPar, s.Code + " knockout par");
                Assert.AreEqual(s.KnockoutThreeStarActions, u.KnockoutThreeStarActions, s.Code + " knockout 3*");
                Assert.AreEqual(s.KnockoutTwoStarActions, u.KnockoutTwoStarActions, s.Code + " knockout 2*");
                Assert.AreEqual(StageBuilder.RowsOf(s), StageBuilder.RowsOf(u), s.Code + " layout untouched");
                Assert.AreEqual(m.Par, u.Par); Assert.AreEqual(u.Par, u.KnockoutPar, s.Code + " both blocks carry the shipped-rules par");
            }
        }

        [Test]
        public void ValidationJson_ReproducesShippedValidationFile()
        {
            string expected = File.ReadAllText(P(V3PackParityTests.ValidationRelative));
            string actual = StagePackTools.ValidationJson(Pack().Stages, out int unsolvable);
            Assert.AreEqual(0, unsolvable);
            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void ValidationJson_CanBeCancelledByTheProgressCallback()
        {
            int calls = 0;
            string result = StagePackTools.ValidationJson(Pack().Stages, out _, (i, n, m) => ++calls < 2);
            Assert.IsNull(result);
            Assert.AreEqual(2, calls);
        }

        [Test]
        public void SweepArc_RoundTripsThroughJson_AndIsValidated()
        {
            // D-073: "sweep": 3 survives a save/load; omitted means 2; 4 is rejected.
            var b = new StageBuilder { Id = 901, Code = "T-SWEEP", Title = "sweep", Start = new GridCoord(0, 2), Exit = new GridCoord(2, 2) };
            b.FromRows(new[] { "#####", ".....", ".....", "....." });
            b.Cameras.Add(new CameraSpec("cam1", new GridCoord(4, 0), Direction.South, Direction.West, CameraKind.Sweeping, true, 3));
            var stage = StagePackTools.StageFromJson(StagePackTools.StageToJson(b.Build()));
            Assert.AreEqual(3, stage.Cameras[0].SweepFacings);
            Assert.AreEqual(8, stage.Period);
            StringAssert.Contains("\"sweep\": 3", StagePackTools.StageToJson(stage));
            b.Cameras[0] = new CameraSpec("cam1", new GridCoord(4, 0), Direction.South, Direction.West, CameraKind.Sweeping);
            StringAssert.DoesNotContain("\"sweep\":", StagePackTools.StageToJson(b.Build()), "default arc is not written, so existing packs stay byte-identical");
            b.Cameras[0] = new CameraSpec("cam1", new GridCoord(4, 0), Direction.South, Direction.West, CameraKind.Sweeping, true, 4);
            var report = StageValidator.Validate(b.Build(), new StageValidationOptions { RunSolver = false });
            Assert.IsFalse(report.IsValid); StringAssert.Contains("sweep must be 2 or 3", report.ToString());
        }

        [Test]
        public void RecomputedScoring_WritesMinusOneKnockoutBlock_WhenNoKnockoutRouteExists()
        {
            // A sealed board: exit unreachable in any mode.
            var b = new StageBuilder { Id = 900, Code = "T-SEALED", Title = "sealed", Start = new GridCoord(0, 0), Exit = new GridCoord(2, 0) };
            b.FromRows(new[] { ".#." });
            var u = StagePackTools.WithRecomputedScoring(b.Build(), out var m);
            Assert.IsFalse(m.Solvable);
            Assert.AreEqual(-1, u.Par); Assert.AreEqual(-1, u.ThreeStarActions); Assert.AreEqual(-1, u.KnockoutPar); Assert.AreEqual(-1, u.KnockoutThreeStarActions); Assert.AreEqual(-1, u.KnockoutTwoStarActions);
            Assert.IsFalse(u.HasKnockoutScoring);
        }
    }
}
