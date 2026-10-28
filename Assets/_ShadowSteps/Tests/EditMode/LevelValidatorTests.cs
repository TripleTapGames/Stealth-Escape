using NUnit.Framework;
using ShadowSteps.Core;
using static ShadowSteps.Tests.EditMode.LevelBuilder;

namespace ShadowSteps.Tests.EditMode
{
    public class LevelValidatorTests
    {
        [Test]
        public void AllReferenceLevels_PassWithoutErrors()
        {
            var report = LevelValidator.ValidatePack(TestPaths.LoadPack().Levels);
            Assert.IsTrue(report.IsValid, report.ToString());
            // Known authored design smells (guard routes crossing key/door tiles in levels 10, 16, 19) are warnings only.
            Assert.AreEqual(3, report.WarningCount, report.ToString());
        }

        [Test]
        public void ExitOutsideBoard_IsError()
        {
            var r = LevelValidator.Validate(Make(4, 4, C(0, 3), C(4, 0)));
            Assert.IsFalse(r.IsValid);
            StringAssert.Contains("exit", r.ToString());
        }

        [Test]
        public void StartOnWall_IsError()
        {
            var r = LevelValidator.Validate(Make(4, 4, C(0, 3), C(3, 0), new[] { C(0, 3) }));
            Assert.IsFalse(r.IsValid);
            StringAssert.Contains("overlaps wall", r.ToString());
        }

        [Test]
        public void DoorWithoutKey_IsError()
        {
            var r = LevelValidator.Validate(Make(4, 4, C(0, 3), C(3, 0), door: C(2, 2)));
            Assert.IsFalse(r.IsValid);
            StringAssert.Contains("key and door", r.ToString());
        }

        [Test]
        public void NonAdjacentRoute_IsError()
        {
            var g = Guard(GuardMode.Patrol, Direction.North, 1, C(1, 1), C(3, 1));
            var r = LevelValidator.Validate(Make(4, 4, C(0, 3), C(3, 0), guards: new[] { g }));
            Assert.IsFalse(r.IsValid);
            StringAssert.Contains("orthogonally adjacent", r.ToString());
        }

        [Test]
        public void OpenLoop_IsError()
        {
            var g = Guard(GuardMode.Loop, Direction.North, 1, C(1, 1), C(2, 1), C(3, 1));
            var r = LevelValidator.Validate(Make(5, 5, C(0, 4), C(4, 0), guards: new[] { g }));
            Assert.IsFalse(r.IsValid);
            StringAssert.Contains("loop route must close", r.ToString());
        }

        [Test]
        public void GuardOnStart_IsError()
        {
            var g = Guard(GuardMode.Fixed, Direction.North, 1, C(0, 3));
            var r = LevelValidator.Validate(Make(4, 4, C(0, 3), C(3, 0), guards: new[] { g }));
            Assert.IsFalse(r.IsValid);
        }

        [Test]
        public void UnreachableExit_IsError()
        {
            var walls = new[] { C(2, 0), C(2, 1), C(2, 2), C(2, 3) };
            var r = LevelValidator.Validate(Make(4, 4, C(0, 3), C(3, 0), walls));
            Assert.IsFalse(r.IsValid);
            StringAssert.Contains("not reachable", r.ToString());
        }

        [Test]
        public void ExitBehindDoor_WithReachableKey_IsValid()
        {
            // Column 2 is wall except the door at (2,1); key at (0,0) is reachable first.
            var walls = new[] { C(2, 0), C(2, 2), C(2, 3) };
            var r = LevelValidator.Validate(Make(4, 4, C(0, 3), C(3, 0), walls, key: C(0, 0), door: C(2, 1)));
            Assert.IsTrue(r.IsValid, r.ToString());
        }

        [Test]
        public void DuplicateIds_AreErrors()
        {
            var a = Make(4, 4, C(0, 3), C(3, 0), id: 7);
            var b = Make(4, 4, C(0, 3), C(3, 0), id: 7);
            var r = LevelValidator.ValidatePack(new[] { a, b });
            Assert.IsFalse(r.IsValid);
            StringAssert.Contains("duplicate level id", r.ToString());
        }
    }
}
