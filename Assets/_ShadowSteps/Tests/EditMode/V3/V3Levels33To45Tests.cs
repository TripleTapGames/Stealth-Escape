using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using ShadowSteps.Core;
using ShadowSteps.Data;
using ShadowSteps.V3;

namespace ShadowSteps.Tests.EditMode.V3
{
    /// <summary>
    /// D-081: the thirteen main levels 33–45 (ids 38–50, the "Levels 38–50" design handoff of 2026-11-11) on the shipped C# rules.
    /// Pins what the handoff asked the real solver to prove: pars and star thresholds, Ghost offers, mandatory Smoke on 38 and 45,
    /// mandatory EMP on 41 and 45, mandatory keys on every door level, which levels still have a knockout-free route, the two
    /// sentries no walkway reaches, and the kit's own example routes replayed on the engine (twelve win; the kit-38 route is
    /// caught on its first step by the engine's ping-pong facing rule, as on LEVEL-32 in D-078).
    /// </summary>
    public class V3Levels33To45Tests
    {
        private static StagePack _pack;
        private static StagePack Pack() => _pack ?? (_pack = StageJson.Parse(File.ReadAllText(Path.Combine(TestPaths.ProjectRoot, V3PackParityTests.PackRelative))));
        private static Stage L(int n) { var s = Pack().FindByCode("LEVEL-" + n); Assert.IsNotNull(s, "LEVEL-" + n); return s; }
        private static StageMetrics M(Stage s) => StageMetrics.Analyze(s, new SolveOptions { Mode = StageMode.Knockout, MaxWaits = 0 });

        // code -> (kit number, par, knockouts in the par route, Ghost offered)
        private static readonly (int code, int kit, int par, int ko, bool ghost)[] Expected =
        {
            (33, 38, 6, 1, false), (34, 39, 9, 1, true), (35, 40, 7, 1, true), (36, 41, 11, 2, true), (37, 42, 11, 2, true),
            (38, 43, 11, 2, false), (39, 44, 11, 2, false), (40, 45, 12, 2, false), (41, 46, 10, 2, true), (42, 47, 12, 2, true),
            (43, 48, 10, 2, true), (44, 49, 14, 3, true), (45, 50, 16, 1, true),
        };

        /// <summary>Shortest alarm-free (Ghost) route per level; the kit's provisional "Ghost shortest" numbers, confirmed by the C# solver.</summary>
        private static readonly Dictionary<int, int> GhostShortest = new Dictionary<int, int> { { 34, 11 }, { 35, 7 }, { 36, 11 }, { 37, 11 }, { 41, 10 }, { 42, 12 }, { 43, 10 }, { 44, 14 }, { 45, 16 } };

        [Test]
        public void Ids38To50_AreMainLevels33To45_WithTheKitTitles_NoWait()
        {
            string[] titles = { "Crossed Blades", "Eyes on the Junction", "Sweep and Strike", "Key Under Watch", "Three-Way Watch", "Smoke at the Fork", "Overlapping Lenses",
                                "Gatehouse Circuit", "Power Cut", "Broken Line of Sight", "The Four Sentinels", "Three Eyes, One Key", "The Last Exit" };
            for (int i = 0; i < 13; i++)
            {
                var s = Pack().FindById(38 + i); Assert.IsNotNull(s, "id " + (38 + i));
                Assert.AreEqual("LEVEL-" + (33 + i), s.Code); Assert.AreEqual("Main", s.Group); Assert.AreEqual(titles[i], s.Title); Assert.AreEqual(0, s.WaitLimit, s.Code + " no WAIT");
                Assert.IsTrue(s.Notes.Count > 0 && s.Notes[0].Contains("\"LEVEL-" + (38 + i) + "\""), s.Code + " keeps the kit number in its first note");
                Assert.IsTrue(s.Width >= 11 && s.Width <= 15 && s.Height >= 9 && s.Height <= 13, s.Code + " board " + s.Width + "x" + s.Height);
                int nodes = 0; foreach (var _ in s.FloorNodes()) nodes++;
                Assert.LessOrEqual(nodes, 25, s.Code + " at most 25 playable nodes");
            }
        }

        [Test]
        public void SolverPars_StarThresholds_KnockoutsAndGhost_MatchTheStoredScoring()
        {
            foreach (var e in Expected)
            {
                var s = L(e.code); var m = M(s);
                Assert.IsTrue(m.Solvable && !m.HitLimit, s.Code);
                Assert.AreEqual(e.par, m.Par, s.Code + " par"); Assert.AreEqual(e.par, s.Par, s.Code + " stored par"); Assert.AreEqual(e.par, s.KnockoutPar, s.Code + " stored knockout par");
                Assert.AreEqual(StageScoring.ThreeStarThreshold(e.par), s.ThreeStarActions, s.Code); Assert.AreEqual(StageScoring.TwoStarThreshold(e.par), s.TwoStarActions, s.Code);
                Assert.AreEqual(e.ko, m.KnockoutsInPar, s.Code + " knockouts in par");
                Assert.AreEqual(e.ghost, m.GhostOffered, s.Code + " ghost (solver)"); Assert.AreEqual(e.ghost, s.GhostOffered, s.Code + " ghost (stored)");
                if (e.ghost) Assert.AreEqual(GhostShortest[e.code], m.ShortestAlarmFree, s.Code + " shortest alarm-free route (the kit's 'Ghost shortest')");
                else if (s.Cameras.Count > 0) Assert.AreEqual(-1, m.ShortestAlarmFree, s.Code + " no alarm-free route (forced alarm)");
                else Assert.AreEqual(e.par, m.ShortestAlarmFree, s.Code + " no camera: trivially alarm-free, Ghost never offered");
                var r = StageReplay.Run(s, m.Solution, StageMode.Knockout); Assert.IsTrue(r.Won && r.CountedActions == e.par, s.Code + "\n" + r.Trace());
            }
        }

        [Test]
        public void Smoke_IsMandatoryOn38And45_Emp_On41And45_NowhereElse()
        {
            var smoke = new List<int>(); var emp = new List<int>(); var alarm = new List<int>();
            foreach (var e in Expected)
            {
                var m = M(L(e.code));
                if (m.SmokeRequired) smoke.Add(e.code); if (m.EmpRequired) emp.Add(e.code); if (m.ForcedAlarm) alarm.Add(e.code);
            }
            CollectionAssert.AreEquivalent(new[] { 38, 45 }, smoke, "kit 43 'Smoke at the Fork' and kit 50 'The Last Exit' need their Smoke");
            CollectionAssert.AreEquivalent(new[] { 41, 45 }, emp, "kit 46 'Power Cut' and kit 50 'The Last Exit' need their EMP");
            CollectionAssert.AreEquivalent(new[] { 38, 39, 40 }, alarm, "kit 43/44/45 cannot be finished without tripping a camera (no Ghost)");
            Assert.AreEqual(1, L(38).SmokeCharges); Assert.AreEqual(1, L(41).EmpCharges); Assert.AreEqual(1, L(45).SmokeCharges); Assert.AreEqual(1, L(45).EmpCharges);
        }

        [Test]
        public void Keys_AreMandatory_OnEveryDoorLevel()
        {
            foreach (int code in new[] { 36, 40, 42, 44, 45 })
            {
                var s = L(code); Assert.IsTrue(s.Key.HasValue && s.Doors.Count == 1, s.Code + " has one locked door and a key");
                var b = s.ToBuilder(); foreach (var d in s.Doors) b.Cuts.Add(new EdgeKey(d.A, d.B)); b.Doors.Clear(); b.Key = null;
                var noKey = M(b.Build());
                Assert.IsFalse(noKey.Solvable, s.Code + ": with the door edge cut and no key there must be no route to the exit");
            }
            foreach (int code in new[] { 33, 34, 35, 37, 38, 39, 41, 43 }) Assert.IsFalse(L(code).Key.HasValue, "LEVEL-" + code + " has no key");
        }

        [Test]
        public void KnockoutFreeRoutes_ExistOnlyOn33_35_37_AndAreLonger()
        {
            Assert.AreEqual(8, M(L(33)).ShortestWithoutKnockout, "kit 38: 8 without a knockout vs par 6");
            Assert.AreEqual(9, M(L(35)).ShortestWithoutKnockout, "kit 40: 9 vs par 7");
            Assert.AreEqual(15, M(L(37)).ShortestWithoutKnockout, "kit 42: 15 vs par 11");
            foreach (int code in new[] { 34, 36, 38, 39, 40, 41, 42, 43, 44, 45 }) Assert.AreEqual(-1, M(L(code)).ShortestWithoutKnockout, "LEVEL-" + code + " has no knockout-free route");
        }

        [Test]
        public void IsolatedSentries_On38And45_HaveNoWalkwayIntoTheirNode()
        {
            foreach (var (code, guard, at) in new[] { (38, 1, new GridCoord(7, 3)), (45, 0, new GridCoord(5, 3)) })
            {
                var s = L(code); var g = s.Guards[guard];
                Assert.AreEqual(GuardKind.Static, g.Kind); Assert.AreEqual(at, g.Anchor, s.Code + " sentry");
                foreach (Direction d in System.Enum.GetValues(typeof(Direction))) Assert.IsNull(s.Neighbor(at, d), s.Code + ": no connection " + DirectionUtil.ToToken(d) + " of the sentry node " + at);
            }
        }

        /// <summary>The kit's EXAMPLE_SOLUTIONS routes as engine tokens (guard ids g1.. → KO#0.., camera ids cam1.. → EMP#0..).</summary>
        private static readonly Dictionary<int, string> KitRoutes = new Dictionary<int, string>
        {
            { 33, "N E S KO#0 E E" },
            { 34, "N E E E KO#0 E N E S" },
            { 35, "N E KO#0 E E N E" },
            { 36, "E S S N N KO#0 E KO#1 E S E" },
            { 37, "N S E S E N KO#0 E KO#1 N E" },
            { 38, "E E KO#0 SMOKE@7,5 E S E E KO#2 W N E" },
            { 39, "N S E E KO#0 N KO#1 S E N E" },
            { 40, "E S S N N KO#0 E E KO#1 E S E" },
            { 41, "N E N E KO#0 EMP#0 E E KO#1 E E" },
            { 42, "N S E E S N KO#0 E KO#1 N E E" },
            { 43, "N S E E KO#0 E KO#1 N E E" },
            { 44, "KO#2 E E KO#0 S E E S S N N KO#1 E E" },
            { 45, "E W S E S S N N W N E SMOKE@5,5 E E EMP#0 KO#1 E E" },
        };

        [Test]
        public void KitExampleRoutes_ReplayToWins_ExceptKit38_WhichTheEndpointFacingRuleCatches()
        {
            foreach (var e in Expected)
            {
                var s = L(e.code); var r = StageReplay.Run(s, StageAction.ParseSequence(new List<string>(KitRoutes[e.code].Split(' '))), StageMode.Knockout);
                if (e.code == 33)
                {
                    // Kit 38 moves N first; the engine's ping-pong guard at its first route node (3,3) faces W toward (1,3) at t = 0 (it faces the endpoint it is
                    // standing on, D-078), where the kit prototype faced its authored E. The route is caught on the spot; the solver's own 6-action route (S N E KO E E) wins instead.
                    Assert.IsFalse(r.Won, "kit 38 route"); Assert.AreEqual(StageResult.Caught, r.Final.Result); Assert.AreEqual(0, r.CountedActions);
                    continue;
                }
                Assert.IsTrue(r.Won, s.Code + " (kit " + e.kit + ")\n" + r.Trace()); Assert.AreEqual(e.par, r.CountedActions, s.Code + " kit route length");
            }
        }

        /// <summary>
        /// Threat audit (handoff review item 2, "does each extra enemy alter routing or timing?"): a guard or camera is par-neutral when removing it leaves the
        /// Knockout solve unchanged in par, knockouts on the par route, knockout-free status, Ghost offer, forced alarm, Smoke/EMP necessity and the solver's
        /// own path. These are reported to the designer (Docs/Reports/LEVELS_38_50_IMPORT_REPORT.md), not redesigned; the test pins the audit so a later
        /// level edit that changes it is noticed. Note the solver's path is one shortest route: a par-neutral threat can still block other routes of the same length.
        /// </summary>
        private static readonly Dictionary<int, string[]> ParNeutralThreats = new Dictionary<int, string[]>
        {
            { 35, new[] { "g2" } }, { 39, new[] { "cam2" } }, { 40, new[] { "cam1", "cam2", "g3" } }, { 41, new[] { "cam2", "g3" } }, { 42, new[] { "cam1", "cam2" } },
            { 43, new[] { "cam1", "cam2", "g4" } }, { 44, new[] { "cam1", "cam2", "cam3", "g4" } }, { 45, new[] { "cam2", "g3", "g4" } },
        };

        [Test]
        public void ThreatAudit_ParNeutralGuardsAndCameras_AreExactlyTheReportedOnes()
        {
            foreach (var e in Expected)
            {
                var s = L(e.code); var baseM = M(s); string baseTokens = Tokens(s);
                var neutral = new List<string>();
                for (int i = 0; i < s.Guards.Count; i++)
                {
                    var b = s.ToBuilder(); b.Guards.RemoveAt(i); var v = b.Build(); var m = M(v);
                    if (SameSolve(baseM, m) && Tokens(v) == baseTokens) neutral.Add(s.Guards[i].Id);
                }
                for (int i = 0; i < s.Cameras.Count; i++)
                {
                    var b = s.ToBuilder(); b.Cameras.RemoveAt(i); if (b.Cameras.Count == 0) b.EmpCharges = 0; var v = b.Build(); var m = M(v);
                    if (SameSolve(baseM, m) && Tokens(v) == baseTokens) neutral.Add(s.Cameras[i].Id);
                }
                CollectionAssert.AreEquivalent(ParNeutralThreats.TryGetValue(e.code, out var expected) ? expected : new string[0], neutral, s.Code + " par-neutral threats");
            }
        }

        private static string Tokens(Stage s) => StageSolver.Solve(s, new SolveOptions { Mode = StageMode.Knockout, MaxWaits = 0 }).Tokens;
        private static bool SameSolve(StageMetrics a, StageMetrics b) =>
            a.Solvable == b.Solvable && a.Par == b.Par && a.KnockoutsInPar == b.KnockoutsInPar && (a.ShortestWithoutKnockout < 0) == (b.ShortestWithoutKnockout < 0)
            && a.GhostOffered == b.GhostOffered && a.ForcedAlarm == b.ForcedAlarm && a.SmokeRequired == b.SmokeRequired && a.EmpRequired == b.EmpRequired;
    }
}
