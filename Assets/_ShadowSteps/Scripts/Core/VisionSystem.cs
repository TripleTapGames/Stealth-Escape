using System;
using System.Collections.Generic;

namespace ShadowSteps.Core
{
    /// <summary>
    /// Authoritative discrete cone-of-vision rules. Port of <c>cone_visible</c> in reference/build.py.
    /// A cell is in the geometric cone when 1 &lt;= forward &lt;= range and |lateral| &lt;= forward in the guard's
    /// facing frame. Line of sight is then sampled from node centre to node centre (12 samples per node of
    /// Chebyshev distance); any sampled node other than the endpoints that occludes blocks the target.
    /// Occluders are nodes (walls, cover, the locked door); edges never occlude (V2 contract).
    /// Never replace this with a physics raycast: presentation may visualise it, but this function decides.
    /// </summary>
    public static class VisionSystem
    {
        public const int SamplesPerTile = 12;

        /// <summary>Returns true when the guard can see <paramref name="target"/> (or stands on it).</summary>
        public static bool ConeVisible(GuardPose guard, GridCoord target, Func<GridCoord, bool> isBlocked)
        {
            int gx = guard.Position.X, gy = guard.Position.Y;
            int px = target.X, py = target.Y;
            int dx = px - gx, dy = py - gy;
            if (dx == 0 && dy == 0) return true;

            int forward, side;
            switch (guard.Facing)
            {
                case Direction.North: forward = -dy; side = dx; break;
                case Direction.East: forward = dx; side = dy; break;
                case Direction.South: forward = dy; side = dx; break;
                default: forward = -dx; side = -dy; break;
            }
            if (forward < 1 || forward > guard.Range || Math.Abs(side) > forward) return false;

            int steps = Math.Max(Math.Abs(dx), Math.Abs(dy)) * SamplesPerTile;
            for (int i = 1; i < steps; i++)
            {
                double t = (double)i / steps;
                double xx = gx + 0.5 + dx * t;
                double yy = gy + 0.5 + dy * t;
                int cx = (int)Math.Floor(xx);
                int cy = (int)Math.Floor(yy);
                if ((cx != gx || cy != gy) && (cx != px || cy != py) && isBlocked(new GridCoord(cx, cy))) return false;
            }
            return true;
        }

        /// <summary>Geometric cone membership only (no occlusion). Useful for editor overlays.</summary>
        public static bool InCone(GuardPose guard, GridCoord target)
        {
            return ConeVisible(guard, target, _ => false) && target != guard.Position;
        }

        /// <summary>Occluder predicate for a level: wall/cover nodes always, the legacy door node only while the key is not held.</summary>
        public static Func<GridCoord, bool> BlockedPredicate(LevelDefinition level, bool hasKey)
        {
            bool doorBlocks = level.HasDoor && !hasKey;
            var door = level.Door ?? default;
            var graph = level.Graph;
            return c => graph.BlocksSight(c) || (doorBlocks && c == door);
        }

        /// <summary>
        /// Every existing node the guard currently sees, excluding its own node, in row-major order.
        /// Non-existent positions (holes in the node mask) are skipped: there is nothing to stand on there.
        /// </summary>
        public static List<GridCoord> GetVisionCells(LevelDefinition level, GuardPose guard, bool hasKey)
        {
            var cells = new List<GridCoord>();
            var blocked = BlockedPredicate(level, hasKey);
            int r = guard.Range;
            int minY = Math.Max(0, guard.Position.Y - r), maxY = Math.Min(level.Height - 1, guard.Position.Y + r);
            int minX = Math.Max(0, guard.Position.X - r), maxX = Math.Min(level.Width - 1, guard.Position.X + r);
            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    var c = new GridCoord(x, y);
                    if (c == guard.Position || !level.Graph.HasNode(c)) continue;
                    if (ConeVisible(guard, c, blocked)) cells.Add(c);
                }
            return cells;
        }
    }
}
