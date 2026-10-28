using System;
using System.Collections.Generic;

namespace ShadowSteps.Core
{
    /// <summary>Authored guard behaviour modes. Variants are configured by data, not by separate scripts.</summary>
    public enum GuardMode
    {
        /// <summary>Holds route[0] and its facing forever.</summary>
        Fixed,
        /// <summary>Holds route[0], rotates one quarter turn every <c>RotateEvery</c> phases.</summary>
        Rotate,
        /// <summary>Walks the route forward then backward (ping-pong) every <c>MoveEvery</c> phases; faces its travel direction. V2 name: pingPong.</summary>
        Patrol,
        /// <summary>Ping-pong patrol whose facing rotates every <c>RotateEvery</c> phases instead of following travel. V2 name: moveRotate.</summary>
        PatrolRotate,
        /// <summary>Walks the route and wraps from the last point back to the first; faces its travel direction.</summary>
        Loop
    }

    public static class GuardModeUtil
    {
        /// <summary>Legacy (schema 1) token.</summary>
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

        /// <summary>Schema 2 token (LEVEL_DESIGN_PARAMETERS.md: fixed, rotate, pingPong, loop, moveRotate).</summary>
        public static string ToV2Token(GuardMode mode)
        {
            switch (mode)
            {
                case GuardMode.Fixed: return "fixed";
                case GuardMode.Rotate: return "rotate";
                case GuardMode.Patrol: return "pingPong";
                case GuardMode.PatrolRotate: return "moveRotate";
                case GuardMode.Loop: return "loop";
                default: throw new ArgumentOutOfRangeException(nameof(mode));
            }
        }

        /// <summary>Accepts both legacy and schema 2 tokens.</summary>
        public static bool TryParse(string token, out GuardMode mode)
        {
            switch ((token ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "fixed": mode = GuardMode.Fixed; return true;
                case "rotate": mode = GuardMode.Rotate; return true;
                case "patrol": case "pingpong": case "ping_pong": mode = GuardMode.Patrol; return true;
                case "patrol_rotate": case "moverotate": case "move_rotate": mode = GuardMode.PatrolRotate; return true;
                case "loop": mode = GuardMode.Loop; return true;
                default: mode = GuardMode.Fixed; return false;
            }
        }

        public static bool Moves(GuardMode mode) =>
            mode == GuardMode.Patrol || mode == GuardMode.PatrolRotate || mode == GuardMode.Loop;

        public static bool RotatesOnSchedule(GuardMode mode) =>
            mode == GuardMode.Rotate || mode == GuardMode.PatrolRotate;

        /// <summary>Patrol (ping-pong) walks its route in both directions; loop walks forward and wraps.</summary>
        public static bool WalksBothWays(GuardMode mode) => mode == GuardMode.Patrol || mode == GuardMode.PatrolRotate;
    }

    /// <summary>Immutable authored guard configuration.</summary>
    public sealed class GuardDefinition
    {
        public GuardMode Mode { get; }
        /// <summary>Authored route; route[0] is the start node. Fixed/Rotate guards use only route[0]. Consecutive nodes must be joined by edges.</summary>
        public IReadOnlyList<GridCoord> Route { get; }
        /// <summary>Initial facing.</summary>
        public Direction Facing { get; }
        /// <summary>Cone length in nodes (visionRangeNodes).</summary>
        public int Range { get; }
        /// <summary>Phases per position update (moveEvery). 1 = moves every turn.</summary>
        public int MoveEvery { get; }
        /// <summary>Phases per facing update for Rotate/PatrolRotate (rotateEvery). 1 = rotates every turn.</summary>
        public int RotateEvery { get; }
        /// <summary>+1 clockwise, -1 counter-clockwise, applied once per rotation update (rotationStep).</summary>
        public int Rotation { get; }
        /// <summary>Optional authored identifier (schema 2 "id"); null for legacy data.</summary>
        public string Id { get; }

        /// <summary>Legacy constructor: a single <paramref name="step"/> drives both movement and rotation (schema 1 semantics).</summary>
        public GuardDefinition(GuardMode mode, IReadOnlyList<GridCoord> route, Direction facing, int range, int step, int rotation)
            : this(mode, route, facing, range, step, step, rotation, null) { }

        public GuardDefinition(GuardMode mode, IReadOnlyList<GridCoord> route, Direction facing, int range, int moveEvery, int rotateEvery, int rotation, string id)
        {
            Mode = mode;
            Route = route ?? throw new ArgumentNullException(nameof(route));
            Facing = facing;
            Range = range;
            MoveEvery = moveEvery;
            RotateEvery = rotateEvery;
            Rotation = rotation;
            Id = id;
        }

        public GridCoord StartPosition => Route.Count > 0 ? Route[0] : new GridCoord(-1, -1);
        /// <summary>True when movement and rotation share one cadence, i.e. the guard can be expressed with the legacy single "step".</summary>
        public bool HasUniformCadence => MoveEvery == RotateEvery;

        public GuardDefinition With(GuardMode? mode = null, IReadOnlyList<GridCoord> route = null, Direction? facing = null, int? range = null,
            int? moveEvery = null, int? rotateEvery = null, int? rotation = null, string id = null) =>
            new GuardDefinition(mode ?? Mode, route ?? Route, facing ?? Facing, range ?? Range, moveEvery ?? MoveEvery, rotateEvery ?? RotateEvery, rotation ?? Rotation, id ?? Id);
    }
}
