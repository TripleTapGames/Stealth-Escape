using System.Collections.Generic;
using ShadowSteps.Core;
using ShadowSteps.V3;

namespace ShadowSteps.Tests.EditMode.V3
{
    /// <summary>Fixture helpers for the V3 rules tests. Boards are ASCII rows ('#' wall, '.' floor).</summary>
    public static class V3TestKit
    {
        public static GridCoord C(int x, int y) => new GridCoord(x, y);

        public static StageBuilder Board(params string[] rows) => new StageBuilder { Id = 1, Code = "TEST-1", Title = "fixture" }.FromRows(rows);

        public static StageBuilder Open(int w, int h, GridCoord start, GridCoord exit)
        {
            var rows = new string[h];
            for (int y = 0; y < h; y++) rows[y] = new string('.', w);
            var b = Board(rows); b.Start = start; b.Exit = exit; return b;
        }

        public static GuardSpec Static(GridCoord at, Direction facing) => new GuardSpec("g", GuardKind.Static, new[] { at }, facing);
        public static GuardSpec Rotating(GridCoord at, Direction facing, bool clockwise = true) => new GuardSpec("g", GuardKind.Rotating, new[] { at }, facing, clockwise);
        public static GuardSpec PingPong(int offset, params GridCoord[] route) => new GuardSpec("g", GuardKind.PingPong, route, Direction.North, true, offset);
        public static GuardSpec Loop(int offset, params GridCoord[] route) => new GuardSpec("g", GuardKind.Loop, route, Direction.North, true, offset);
        public static CameraSpec Camera(GridCoord wall, Direction mountFace, Direction facing, CameraKind kind = CameraKind.Fixed, bool clockwise = true) =>
            new CameraSpec("cam", wall, mountFace, facing, kind, clockwise);

        public static ActionOutcome Step(Stage stage, ref StageState state, StageAction action)
        {
            var o = StageRules.TryApply(stage, state, action);
            state = o.After;
            return o;
        }

        public static StageAction N => StageAction.Move(Direction.North);
        public static StageAction E => StageAction.Move(Direction.East);
        public static StageAction S => StageAction.Move(Direction.South);
        public static StageAction W => StageAction.Move(Direction.West);
        public static StageAction WAIT => StageAction.Wait;

        public static List<GridCoord> Cells(params (int x, int y)[] xs) { var l = new List<GridCoord>(); foreach (var c in xs) l.Add(new GridCoord(c.x, c.y)); return l; }
    }
}
