using System;
using System.Collections.Generic;

namespace ShadowSteps.Core
{
    /// <summary>Resolved guard placement for one logical phase.</summary>
    public readonly struct GuardPose : IEquatable<GuardPose>
    {
        public readonly GridCoord Position;
        public readonly Direction Facing;
        public readonly int Range;

        public GuardPose(GridCoord position, Direction facing, int range)
        {
            Position = position;
            Facing = facing;
            Range = range;
        }

        public bool Equals(GuardPose o) => Position == o.Position && Facing == o.Facing && Range == o.Range;
        public override bool Equals(object obj) => obj is GuardPose o && Equals(o);
        public override int GetHashCode() => unchecked(Position.GetHashCode() * 31 + (int)Facing * 7 + Range);
        public override string ToString() => "(" + Position.X + ", " + Position.Y + ", " + (int)Facing + ", " + Range + ")";
    }

    /// <summary>
    /// Pure function from (guard definition, global phase) to pose. This is a line-by-line port of
    /// <c>guard_at</c> and <c>period</c> in reference/build.py. Do not "improve" it: the 20 shipped levels
    /// and their validated solutions depend on these exact semantics (including the facing reset at phase 0
    /// for travel-facing guards after the period wraps).
    /// </summary>
    public static class GuardSchedule
    {
        public static GuardPose Evaluate(GuardDefinition g, int phase)
        {
            if (phase < 0) throw new ArgumentOutOfRangeException(nameof(phase), "phase must be >= 0");
            int step = Math.Max(1, g.Step);
            int q = phase / step;
            var route = g.Route;

            switch (g.Mode)
            {
                case GuardMode.Fixed:
                    return new GuardPose(route[0], g.Facing, g.Range);

                case GuardMode.Rotate:
                    return new GuardPose(route[0], DirectionUtil.Rotate(g.Facing, q * g.Rotation), g.Range);

                case GuardMode.Patrol:
                case GuardMode.PatrolRotate:
                case GuardMode.Loop:
                {
                    var pos = PositionAtIndex(g, q);
                    Direction facing;
                    if (g.Mode == GuardMode.PatrolRotate)
                    {
                        facing = DirectionUtil.Rotate(g.Facing, q * g.Rotation);
                    }
                    else if (q == 0)
                    {
                        facing = g.Facing;
                    }
                    else
                    {
                        // Reference: prev = guard_at(g, max(t-step, 0)); facing = direction of (pos - prev) if cardinal else authored facing.
                        int prevPhase = Math.Max(phase - step, 0);
                        var prev = PositionAtIndex(g, prevPhase / step);
                        var d = DirectionUtil.FromDelta(pos.X - prev.X, pos.Y - prev.Y);
                        facing = d ?? g.Facing;
                    }
                    return new GuardPose(pos, facing, g.Range);
                }

                default:
                    throw new ArgumentOutOfRangeException(nameof(g.Mode), "Unknown guard mode " + g.Mode);
            }
        }

        /// <summary>Route index for update count q: loop wraps, patrol ping-pongs (0..n-1, n-2..1).</summary>
        public static int RouteIndex(GuardMode mode, int routeLength, int q)
        {
            if (routeLength <= 1) return 0;
            if (mode == GuardMode.Loop) return DirectionUtil.Mod(q, routeLength);
            int cycle = 2 * routeLength - 2;
            int k = DirectionUtil.Mod(q, cycle);
            return k < routeLength ? k : cycle - k;
        }

        private static GridCoord PositionAtIndex(GuardDefinition g, int q) =>
            g.Route[RouteIndex(g.Mode, g.Route.Count, q)];

        /// <summary>Cycle length of one guard's schedule (reference period() per-guard term).</summary>
        public static int GuardCycle(GuardDefinition g)
        {
            int n = g.Route.Count;
            int s = Math.Max(1, g.Step);
            int z;
            switch (g.Mode)
            {
                case GuardMode.Fixed: z = 1; break;
                case GuardMode.Rotate: z = 4 * s / Gcd(4, Math.Abs(g.Rotation)); break;
                case GuardMode.Loop: z = n * s; break;
                default: z = Math.Max(1, 2 * n - 2) * s; break;
            }
            if (g.Mode == GuardMode.PatrolRotate) z = Lcm(z, 4 * s / Gcd(4, Math.Abs(g.Rotation)));
            return z;
        }

        /// <summary>Global period: least common multiple of all guard cycles (1 when there are no guards).</summary>
        public static int Period(LevelDefinition level)
        {
            int p = 1;
            foreach (var g in level.Guards) p = Lcm(p, GuardCycle(g));
            return p;
        }

        /// <summary>Poses of every guard at a phase, in level guard order.</summary>
        public static GuardPose[] PosesAt(LevelDefinition level, int phase)
        {
            var poses = new GuardPose[level.Guards.Count];
            for (int i = 0; i < poses.Length; i++) poses[i] = Evaluate(level.Guards[i], phase);
            return poses;
        }

        public static int Gcd(int a, int b)
        {
            a = Math.Abs(a); b = Math.Abs(b);
            while (b != 0) { int t = a % b; a = b; b = t; }
            return a;
        }

        public static int Lcm(int a, int b)
        {
            if (a == 0 || b == 0) return 0;
            return Math.Abs(a / Gcd(a, b) * b);
        }
    }
}
