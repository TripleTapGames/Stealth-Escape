using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using ShadowSteps.Core;
using ShadowSteps.Data;
using static ShadowSteps.Tests.EditMode.LevelBuilder;

namespace ShadowSteps.Tests.EditMode
{
    /// <summary>V2 gate C: schema 2 import/export, legacy migration records, strict errors with paths.</summary>
    public class SchemaV2Tests
    {
        private const string Sample = @"{
  ""schemaVersion"": 2,
  ""id"": 101,
  ""name"": ""Training Junction"",
  ""board"": {
    ""width"": 4, ""height"": 4, ""nodeSpacing"": 1.0,
    ""nodes"": [[0,0],[1,0],[2,0],[3,0],[0,1],[1,1],[2,1],[3,1],[0,2],[1,2],[2,2],[3,2],[0,3],[1,3],[2,3],[3,3]],
    ""edges"": ""autoCardinalExceptBlocked"",
    ""blockedEdges"": [ [[1,1],[2,1]] ],
    ""walls"": [[2,2]]
  },
  ""player"": { ""start"": [0,3] },
  ""objective"": { ""type"": ""reachExit"", ""exit"": [3,0] },
  ""guards"": [{
    ""id"": ""g1"", ""movementMode"": ""rotate"", ""route"": [[1,1]],
    ""facing"": ""E"", ""rotationStep"": 1, ""rotateEvery"": 1,
    ""visionRangeNodes"": 2, ""coneAngleDegrees"": 90
  }],
  ""gadgets"": { ""smokeCharges"": 0, ""empCharges"": 0 },
  ""presentation"": { ""theme"": ""gardenCompound"" }
}";

        [Test]
        public void SampleFromDesignDoc_LoadsAsSingleLevel()
        {
            var pack = LevelPackLoader.Parse(Sample);
            Assert.AreEqual(2, pack.FormatVersion);
            Assert.AreEqual(1, pack.Levels.Count);
            var l = pack.Levels[0];
            Assert.AreEqual(101, l.Id);
            Assert.AreEqual(16, l.Graph.NodeCount);
            Assert.IsFalse(l.Graph.HasEdge(C(1, 1), C(2, 1)));
            Assert.IsFalse(l.Graph.HasEdge(C(2, 1), C(1, 1)));
            Assert.IsFalse(l.Graph.HasEdge(C(2, 1), C(2, 2)), "no edge into the wall");
            Assert.AreEqual(GuardMode.Rotate, l.Guards[0].Mode);
            Assert.AreEqual(Direction.East, l.Guards[0].Facing);
            Assert.AreEqual("g1", l.Guards[0].Id);
            Assert.AreEqual(2, l.Guards[0].Range);
            Assert.AreEqual("gardenCompound", l.Theme);
            Assert.IsTrue(LevelValidator.Validate(l).IsValid, LevelValidator.Validate(l).ToString());
            Assert.AreEqual(0, pack.Warnings.Count, string.Join("\n", pack.Warnings));
        }

        [Test]
        public void V2Export_RoundTripsTopologyAndFields()
        {
            var pack = LevelPackLoader.Parse(Sample);
            string json = LevelPackWriter.ToJson(pack);
            var again = LevelPackLoader.Parse(json);
            var a = pack.Levels[0]; var b = again.Levels[0];
            Assert.AreEqual(a.Graph.EdgeCount, b.Graph.EdgeCount);
            foreach (var e in a.Graph.Edges) Assert.IsTrue(b.Graph.TryGetEdge(e.From, e.To, out var t) && t == e.Type, "edge " + e);
            Assert.AreEqual(a.Walls, b.Walls);
            Assert.AreEqual(a.Theme, b.Theme);
            Assert.AreEqual(a.Guards[0].Id, b.Guards[0].Id);
            Assert.AreEqual(a.NodeSpacing, b.NodeSpacing);
        }

        [Test]
        public void LegacyPack_MigratesToSchema2WithConversionNotes()
        {
            var pack = TestPaths.LoadPack();
            Assert.AreEqual(1, pack.FormatVersion, "pack reports the format it was read from");
            foreach (var l in pack.Levels)
            {
                Assert.AreEqual(2, l.SchemaVersion);
                Assert.AreEqual(1, l.MigratedFromSchema);
                Assert.IsTrue(l.IsLegacy);
                Assert.IsTrue(l.ConversionNotes.Count >= 2, "level " + l.Id);
                StringAssert.Contains("generated", l.ConversionNotes[1]);
                if (l.HasDoor) StringAssert.Contains("locked incoming edges", l.ConversionNotes[2]);
            }
        }

        [Test]
        public void MigratedLegacyLevels_ExportAsSchema2_AndReloadEquivalently()
        {
            var pack = TestPaths.LoadPack();
            string json = LevelPackWriter.ToJson(pack);
            var again = LevelPackLoader.ParseAndValidate(json, out var report);
            Assert.AreEqual(2, again.FormatVersion);
            Assert.AreEqual(20, again.Levels.Count);
            foreach (var sol in TestPaths.LoadReferenceSolutions())
            {
                var level = again.FindById(sol.Level);
                var replay = GameSession.Replay(level, sol.Actions);
                Assert.IsTrue(replay.Won, "level " + sol.Level + " after schema-2 export/import:\n" + replay.Trace());
                Assert.AreEqual(sol.Moves, replay.ValidTurns);
                var solved = Solver.Solve(level);
                Assert.AreEqual(GameActionUtil.ToTokenString(sol.Actions), solved.ActionTokens, "solver path after round trip, level " + sol.Level);
            }
        }

        [Test]
        public void OneWayAndLockedEdges_RoundTrip()
        {
            var s = new LevelSpec { Id = 7, Name = "edges", Width = 3, Height = 1, Start = C(0, 0), Exit = C(2, 0), Key = C(1, 0) };
            s.ExplicitEdges = new List<Edge> { new Edge(C(0, 0), C(1, 0), EdgeType.Open), new Edge(C(1, 0), C(2, 0), EdgeType.Locked), new Edge(C(2, 0), C(1, 0), EdgeType.Locked) };
            var level = LevelDefinition.FromSpec(s);
            string json = LevelPackWriter.ToJson(level);
            StringAssert.Contains("\"oneWay\"", json);
            StringAssert.Contains("\"locked\"", json);
            var back = LevelPackLoader.Parse(json).Levels[0];
            Assert.IsTrue(back.Graph.HasEdge(C(0, 0), C(1, 0)));
            Assert.IsFalse(back.Graph.HasEdge(C(1, 0), C(0, 0)));
            Assert.IsTrue(back.Graph.TryGetEdge(C(1, 0), C(2, 0), out var t) && t == EdgeType.Locked);
            Assert.AreEqual(3, back.Graph.EdgeCount);
        }

        [Test]
        public void LegacyWriter_StillProducesSchema1ForGridLevels()
        {
            string json = LevelPackWriter.ToLegacyJson(TestPaths.LoadPack().Levels);
            var again = LevelPackLoader.ParseAndValidate(json, out _);
            Assert.AreEqual(1, again.FormatVersion);
            Assert.AreEqual(20, again.Levels.Count);
            Assert.AreEqual(TestPaths.Level(20).Guards[0].Rotation, again.FindById(20).Guards[0].Rotation);
        }

        [TestCase("\"coneAngleDegrees\": 90", "\"coneAngleDegrees\": 60", "coneAngleDegrees must be 90")]
        [TestCase("\"movementMode\": \"rotate\"", "\"movementMode\": \"teleport\"", "guards[0].movementMode")]
        [TestCase("\"facing\": \"E\"", "\"facing\": \"NE\"", "guards[0].facing")]
        [TestCase("\"edges\": \"autoCardinalExceptBlocked\"", "\"edges\": \"magic\"", "board.edges mode")]
        [TestCase("\"blockedEdges\": [ [[1,1],[2,1]] ]", "\"blockedEdges\": [ [[1,1]] ]", "board.blockedEdges[0]")]
        public void MalformedV2Fields_ReportJsonPath(string find, string replace, string expectedFragment)
        {
            string json = Sample.Replace(find, replace);
            var ex = Assert.Throws<LevelDataException>(() => LevelPackLoader.Parse(json));
            StringAssert.Contains(expectedFragment, ex.Message);
        }

        [Test]
        public void UnsupportedFeatures_AreRejectedExplicitly()
        {
            string switchable = Sample.Replace("\"edges\": \"autoCardinalExceptBlocked\"", "\"edges\": [{\"from\":[0,3],\"to\":[1,3],\"type\":\"switchable\"}]");
            StringAssert.Contains("switchable", Assert.Throws<LevelDataException>(() => LevelPackLoader.Parse(switchable)).Message);
            string decoy = Sample.Replace("\"smokeCharges\": 0", "\"smokeCharges\": 0, \"decoyCharges\": 1");
            var r = LevelValidator.Validate(LevelPackLoader.Parse(decoy).Levels[0]);
            Assert.IsFalse(r.IsValid);
            StringAssert.Contains("decoy", r.ToString());
        }

        [Test]
        public void UnknownSchemaVersion_IsRejected()
        {
            Assert.Throws<LevelDataException>(() => LevelPackLoader.Parse("{\"schemaVersion\": 3, \"levels\": []}"));
        }
    }
}
