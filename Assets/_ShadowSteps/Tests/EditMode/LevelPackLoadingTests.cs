using System.IO;
using NUnit.Framework;
using ShadowSteps.Core;
using ShadowSteps.Data;

namespace ShadowSteps.Tests.EditMode
{
    public class LevelPackLoadingTests
    {
        [Test]
        public void ReferencePack_Loads20ValidLevels()
        {
            var pack = LevelPackLoader.ParseAndValidate(File.ReadAllText(TestPaths.LevelsJsonPath), out var report);
            Assert.AreEqual(1, pack.FormatVersion);
            Assert.AreEqual(20, pack.Levels.Count);
            Assert.IsTrue(report.IsValid, report.ToString());
            Assert.AreEqual(0, pack.Warnings.Count, string.Join("\n", pack.Warnings));
            for (int i = 0; i < 20; i++) Assert.AreEqual(i + 1, pack.Levels[i].Id, "ids must be 1..20 in order");
        }

        [Test]
        public void PackagedPack_IsSchema2_With20Levels()
        {
            var pack = TestPaths.LoadPackagedPack();
            Assert.AreEqual(2, pack.FormatVersion, "shipped levels.json must be schema 2");
            Assert.AreEqual(20, pack.Levels.Count);
            for (int i = 0; i < 20; i++)
            {
                Assert.AreEqual(i + 1, pack.Levels[i].Id);
                // Only 1 and 4 are kept from the migrated reference; the rest are generated schema-2 levels (D-026).
                Assert.AreEqual(i + 1 == 1 || i + 1 == 4 ? 1 : 0, pack.Levels[i].MigratedFromSchema, "level " + (i + 1));
            }
        }

        [TestCase(1, "First Watch", 5, 5, 1)]
        [TestCase(9, "The Brass Key", 6, 6, 2)]
        [TestCase(17, "Three Blind Angles", 7, 7, 3)]
        [TestCase(20, "Final Extraction", 7, 7, 3)]
        public void KnownLevels_HaveExpectedShape(int id, string name, int w, int h, int guards)
        {
            var l = TestPaths.Level(id);
            Assert.AreEqual(name, l.Name);
            Assert.AreEqual(w, l.Width);
            Assert.AreEqual(h, l.Height);
            Assert.AreEqual(guards, l.Guards.Count);
            Assert.AreEqual(new GridCoord(0, h - 1), l.Start);
            Assert.AreEqual(new GridCoord(w - 1, 0), l.Exit);
        }

        [Test]
        public void Level20_HasKeyDoorAndBothGadgets()
        {
            var l = TestPaths.Level(20);
            Assert.AreEqual(new GridCoord(1, 3), l.Key);
            Assert.AreEqual(new GridCoord(2, 3), l.Door);
            Assert.AreEqual(1, l.SmokeCharges);
            Assert.AreEqual(1, l.EmpCharges);
            Assert.AreEqual(GuardMode.PatrolRotate, l.Guards[0].Mode);
            Assert.AreEqual(GuardMode.Loop, l.Guards[1].Mode);
            Assert.AreEqual(GuardMode.Rotate, l.Guards[2].Mode);
        }

        [Test]
        public void WriteThenParse_RoundTripsEveryLevel()
        {
            var pack = TestPaths.LoadPack();
            string json = LevelPackWriter.ToJson(pack);
            var again = LevelPackLoader.ParseAndValidate(json, out var report);
            Assert.IsTrue(report.IsValid, report.ToString());
            Assert.AreEqual(pack.Levels.Count, again.Levels.Count);
            for (int i = 0; i < pack.Levels.Count; i++)
            {
                var a = pack.Levels[i]; var b = again.Levels[i];
                Assert.AreEqual(a.Id, b.Id); Assert.AreEqual(a.Name, b.Name);
                Assert.AreEqual(a.Width, b.Width); Assert.AreEqual(a.Height, b.Height);
                Assert.AreEqual(a.Start, b.Start); Assert.AreEqual(a.Exit, b.Exit);
                Assert.AreEqual(a.Key, b.Key); Assert.AreEqual(a.Door, b.Door);
                Assert.AreEqual(a.SmokeCharges, b.SmokeCharges); Assert.AreEqual(a.EmpCharges, b.EmpCharges);
                CollectionAssert.AreEqual(a.Walls, b.Walls);
                Assert.AreEqual(a.Guards.Count, b.Guards.Count);
                for (int g = 0; g < a.Guards.Count; g++)
                {
                    Assert.AreEqual(a.Guards[g].Mode, b.Guards[g].Mode);
                    CollectionAssert.AreEqual(a.Guards[g].Route, b.Guards[g].Route);
                    Assert.AreEqual(a.Guards[g].Facing, b.Guards[g].Facing);
                    Assert.AreEqual(a.Guards[g].Range, b.Guards[g].Range);
                    Assert.AreEqual(a.Guards[g].MoveEvery, b.Guards[g].MoveEvery);
                    Assert.AreEqual(a.Guards[g].RotateEvery, b.Guards[g].RotateEvery);
                    Assert.AreEqual(a.Guards[g].Rotation, b.Guards[g].Rotation);
                }
            }
        }

        [Test]
        public void MissingFormatVersion_Throws()
        {
            var ex = Assert.Throws<LevelDataException>(() => LevelPackLoader.Parse("{\"levels\":[]}"));
            StringAssert.Contains("format_version", ex.Message);
        }

        [Test]
        public void NewerFormatVersion_Throws()
        {
            Assert.Throws<LevelDataException>(() => LevelPackLoader.Parse("{\"format_version\":99,\"levels\":[]}"));
        }

        [Test]
        public void BadCoordinate_ReportsJsonPath()
        {
            string json = "{\"format_version\":1,\"levels\":[{\"id\":1,\"name\":\"x\",\"size\":[5,5],\"start\":[0,4],\"exit\":[4,0],\"walls\":[[1,1],[2]],\"guards\":[],\"smoke\":0,\"emp\":0,\"key\":null,\"door\":null}]}";
            var ex = Assert.Throws<LevelDataException>(() => LevelPackLoader.Parse(json));
            StringAssert.Contains("levels[0].walls[1]", ex.Message);
        }

        [Test]
        public void UnknownGuardMode_ReportsJsonPath()
        {
            string json = "{\"format_version\":1,\"levels\":[{\"id\":1,\"name\":\"x\",\"size\":[5,5],\"start\":[0,4],\"exit\":[4,0],\"walls\":[],\"guards\":[{\"mode\":\"teleport\",\"route\":[[1,1]],\"facing\":0,\"range\":1}],\"smoke\":0,\"emp\":0}]}";
            var ex = Assert.Throws<LevelDataException>(() => LevelPackLoader.Parse(json));
            StringAssert.Contains("levels[0].guards[0].mode", ex.Message);
        }

        [Test]
        public void UnknownField_IsWarningNotError()
        {
            string json = "{\"format_version\":1,\"levels\":[{\"id\":1,\"name\":\"x\",\"size\":[5,5],\"start\":[0,4],\"exit\":[4,0],\"walls\":[],\"guards\":[],\"smoke\":0,\"emp\":0,\"future_field\":true}]}";
            var pack = LevelPackLoader.Parse(json);
            Assert.AreEqual(1, pack.Warnings.Count);
            StringAssert.Contains("future_field", pack.Warnings[0]);
        }

        [Test]
        public void InvalidJsonText_ThrowsLevelDataException()
        {
            Assert.Throws<LevelDataException>(() => LevelPackLoader.Parse("{\"format_version\":1, \"levels\": [ }"));
        }
    }
}
