using System;
using System.Collections.Generic;

namespace ShadowSteps.Core
{
    /// <summary>
    /// Authoritative discrete cone-of-vision rules. Port of <c>cone_visible</c> in reference/build.py.
    /// A cell is in the geometric cone when 1 &lt;= forward &lt;= range and |lateral| &lt;= forward in the guard's
    /// facing frame. Line of sight is then sampled from tile centre to tile centre (12 samples per tile of
    /// Chebyshev distance); any sampled cell other than the endpoints that is blocked occludes the target.
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

        /// <summary>
        /// Every board cell the guard currently sees, excluding its own tile. Deterministic row-major order.
        /// Blocking cells: walls, plus the door while the player has no key.
        /// </summary>
        public static List<GridCoord> GetVisionCells(LevelDefinition level, GuardPose guard, bool doorBlocks)
        {
            var cells = new List<GridCoord>();
            Func<GridCoord, bool> blocked = c => level.IsWall(c) || (doorBlocks && level.IsDoor(c));
            int r = guard.Range;
            // Only cells within the bounding box of the cone can qualify; iterate rows for stable ordering.
            int minY = Math.Max(0, guard.Position.Y - r), maxY = Math.Min(level.Height - 1, guard.Position.Y + r);
            int minX = Math.Max(0, guard.Position.X - r), maxX = Math.Min(level.Width - 1, guard.Position.X + r);
            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    var c = new GridCoord(x, y);
                    if (c == guard.Position) continue;
                    if (ConeVisible(guard, c, blocked)) cells.Add(c);
                }
            return cells;
        }

        public static Func<GridCoord, bool> BlockedPredicate(LevelDefinition level, bool hasKey)
        {
            bool doorBlocks = level.HasDoor && !hasKey;
            return c => level.IsWall(c) || (doorBlocks && level.IsDoor(c));
        }
    }
}
