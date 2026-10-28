using System;
using System.Collections.Generic;

namespace ShadowSteps.Core
{
    /// <summary>Authored guard behaviour modes. Variants are configured by data, not by separate scripts.</summary>
    public enum GuardMode
    {
        /// <summary>Holds route[0] and its facing forever.</summary>
        Fixed,
        /// <summary>Holds route[0], rotates one quarter turn every <c>step</c> phases.</summary>
        Rotate,
        /// <summary>Walks the route forward then backward (ping-pong); faces its travel direction.</summary>
        Patrol,
        /// <summary>Ping-pong patrol whose facing rotates on the same cadence instead of following travel.</summary>
        PatrolRotate,
        /// <summary>Walks the route and wraps from the last point back to the first; faces its travel direction.</summary>
        Loop
    }

    public static class GuardModeUtil
    {
        public static string ToToken(GuardMode mode)
        {
            switch (mode)
            {
                case GuardMode.Fixed: return "fixed";
                case GuardMode.Rotate: return "rotate";
                case GuardMode.Patrol: return "patrol";
                case GuardMode.PatrolRotate: return "patrol_rotate";
                case GuardMode.Loop: return "loop";
                default: throw new ArgumentOutOfRangeException(nameof(mode));
            }
        }

        public static bool TryParse(string token, out GuardMode mode)
        {
            switch ((token ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "fixed": mode = GuardMode.Fixed; return true;
                case "rotate": mode = GuardMode.Rotate; return true;
                case "patrol": mode = GuardMode.Patrol; return true;
                case "patrol_rotate": mode = GuardMode.PatrolRotate; return true;
                case "loop": mode = GuardMode.Loop; return true;
                default: mode = GuardMode.Fixed; return false;
            }
        }

        public static bool Moves(GuardMode mode) =>
            mode == GuardMode.Patrol || mode == GuardMode.PatrolRotate || mode == GuardMode.Loop;

        public static bool RotatesOnSchedule(GuardMode mode) =>
            mode == GuardMode.Rotate || mode == GuardMode.PatrolRotate;
    }

    /// <summary>Immutable authored guard configuration (mirrors one entry of "guards" in levels.json).</summary>
    public sealed class GuardDefinition
    {
        public GuardMode Mode { get; }
        /// <summary>Authored route; route[0] is the start tile. Fixed/Rotate guards use only route[0].</summary>
        public IReadOnlyList<GridCoord> Route { get; }
        /// <summary>Initial facing.</summary>
        public Direction Facing { get; }
        /// <summary>Cone length in tiles (forward distance).</summary>
        public int Range { get; }
        /// <summary>Phases per schedule update. 1 = updates every turn. (Reserved for "slow watcher" content; all V1 levels use 1.)</summary>
        public int Step { get; }
        /// <summary>+1 clockwise, -1 counter-clockwise, applied once per schedule update for Rotate/PatrolRotate.</summary>
        public int Rotation { get; }

        public GuardDefinition(GuardMode mode, IReadOnlyList<GridCoord> route, Direction facing, int range, int step, int rotation)
        {
            Mode = mode;
            Route = route ?? throw new ArgumentNullException(nameof(route));
            Facing = facing;
            Range = range;
            Step = step;
            Rotation = rotation;
        }

        public GridCoord StartPosition => Route.Count > 0 ? Route[0] : new GridCoord(-1, -1);

        public GuardDefinition WithRoute(IReadOnlyList<GridCoord> route) => new GuardDefinition(Mode, route, Facing, Range, Step, Rotation);
        public GuardDefinition WithMode(GuardMode mode) => new GuardDefinition(mode, Route, Facing, Range, Step, Rotation);
        public GuardDefinition WithFacing(Direction facing) => new GuardDefinition(Mode, Route, facing, Range, Step, Rotation);
        public GuardDefinition WithRange(int range) => new GuardDefinition(Mode, Route, Facing, range, Step, Rotation);
        public GuardDefinition WithStep(int step) => new GuardDefinition(Mode, Route, Facing, Range, step, Rotation);
        public GuardDefinition WithRotation(int rotation) => new GuardDefinition(Mode, Route, Facing, Range, Step, rotation);
    }
}
