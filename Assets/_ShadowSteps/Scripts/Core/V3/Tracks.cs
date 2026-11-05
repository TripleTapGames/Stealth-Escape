using System;
using ShadowSteps.Core;

namespace ShadowSteps.V3
{
    /// <summary>Resolved guard placement at one counted-action boundary.</summary>
    public readonly struct GuardPose3 : IEquatable<GuardPose3>
    {
        public readonly GridCoord Position;
        public readonly Direction Facing;
        public GuardPose3(GridCoord position, Direction facing) { Position = position; Facing = facing; }
        public bool Equals(GuardPose3 o) => Position == o.Position && Facing == o.Facing;
        public override bool Equals(object obj) => obj is GuardPose3 p && Equals(p);
        public override int GetHashCode() => unchecked(Position.GetHashCode() * 31 + (int)Facing);
        public override string ToString() => Position + " facing " + DirectionUtil.ToToken(Facing);
    }

    /// <summary>
    /// Deterministic guard schedule: pure function of (guard, counted action index t). Port of <c>guardAt</c> in the
    /// v3 prototype. Moving guards step one node per action and face the direction they just travelled; at a
    /// ping-pong endpoint the facing still points toward the endpoint while <see cref="NextStep"/> points back.
    /// </summary>
    public static class GuardTrack
    {
        public static int Period(GuardSpec g)
        {
            switch (g.Kind)
            {
                case GuardKind.Static: return 1;
                case GuardKind.Rotating: return 4;
                case GuardKind.Loop: return Math.Max(1, g.Route.Count);
                default: return Math.Max(1, 2 * g.Route.Count - 2);
            }
        }

        /// <summary>Route index at update count k (k already includes the offset).</summary>
        public static int RouteIndex(GuardSpec g, int k)
        {
            int n = g.Route.Count;
            if (n <= 1) return 0;
            int period = Period(g);
            int m = DirectionUtil.Mod(k, period);
            if (g.Kind == GuardKind.Loop) return m;
            return m < n ? m : period - m;
        }

        public static GridCoord PositionAt(GuardSpec g, int t)
        {
            if (!g.Moves) return g.Anchor;
            return g.Route[RouteIndex(g, t + g.Offset)];
        }

        public static GuardPose3 PoseAt(GuardSpec g, int t)
        {
            if (t < 0) throw new ArgumentOutOfRangeException(nameof(t));
            switch (g.Kind)
            {
                case GuardKind.Static: return new GuardPose3(g.Anchor, g.Facing);
                case GuardKind.Rotating: return new GuardPose3(g.Anchor, DirectionUtil.Rotate(g.Facing, g.Clockwise ? t : -t));
                default:
                {
                    var pos = PositionAt(g, t);
                    if (g.Route.Count <= 1) return new GuardPose3(pos, g.Facing);
                    var prev = g.Route[RouteIndex(g, t + g.Offset - 1)];
                    var d = DirectionUtil.Toward(pos.X - prev.X, pos.Y - prev.Y); // a route step may span passage cells (D-047)
                    if (d == null)
                    {
                        var next = g.Route[RouteIndex(g, t + g.Offset + 1)];
                        d = DirectionUtil.Toward(next.X - pos.X, next.Y - pos.Y);
                    }
                    return new GuardPose3(pos, d ?? Direction.North);
                }
            }
        }

        /// <summary>Direction of the guard's next actual step (position(t+1) − position(t)), or null when it does not move.</summary>
        public static Direction? NextStep(GuardSpec g, int t)
        {
            if (!g.Moves) return null;
            var now = PositionAt(g, t);
            var next = PositionAt(g, t + 1);
            return DirectionUtil.Toward(next.X - now.X, next.Y - now.Y);
        }

        /// <summary>Poses of every guard at t, in stage order.</summary>
        public static GuardPose3[] PosesAt(Stage stage, int t)
        {
            var poses = new GuardPose3[stage.Guards.Count];
            for (int i = 0; i < poses.Length; i++) poses[i] = PoseAt(stage.Guards[i], t);
            return poses;
        }
    }

    /// <summary>Deterministic camera facing schedule. Port of <c>camDir</c> in the v3 prototype.</summary>
    public static class CameraTrack
    {
        public static int Period(CameraSpec c) => c.Kind == CameraKind.Fixed ? 1 : 4;

        public static Direction FacingAt(CameraSpec c, int t)
        {
            switch (c.Kind)
            {
                case CameraKind.Rotating: return DirectionUtil.Rotate(c.Facing, c.Clockwise ? t : -t);
                case CameraKind.Sweeping: return (t / 2) % 2 == 1 ? DirectionUtil.Rotate(c.Facing, c.Clockwise ? 1 : -1) : c.Facing;
                default: return c.Facing;
            }
        }
    }
}
