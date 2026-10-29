using System;
using System.Collections.Generic;
using ShadowSteps.Core;

namespace ShadowSteps.V3
{
    /// <summary>Continuous board position (node centres are at integer coordinates).</summary>
    public readonly struct Vec2
    {
        public readonly double X, Y;
        public Vec2(double x, double y) { X = x; Y = y; }
        public static Vec2 Of(GridCoord c) => new Vec2(c.X, c.Y);
        public override string ToString() => "(" + X + ", " + Y + ")";
    }

    /// <summary>
    /// The single occlusion/vision routine shared by guards, cameras, smoke throws, preview and the solver (handover §2).
    /// Port of <c>hitsRect</c>, <c>segmentsMeet</c>, <c>obstructed</c>, <c>guardVision</c>, <c>camVision</c> from the v3
    /// prototype. A line touching the edge or corner of a wall node or a closed door counts as blocked.
    /// </summary>
    public static class Sight
    {
        private const double Eps = 1e-8;
        public const int GuardRange = 2;
        public const int CameraForward = 2;
        public const int CameraHalfWidth = 1;
        /// <summary>Camera origin sits this far outside its wall, on the mounting face.</summary>
        public const double CameraMountOffset = 0.501;
        /// <summary>Half-length of the opaque segment drawn across a closed door edge.</summary>
        public const double DoorHalfWidth = 0.48;

        /// <summary>Segment a→b intersects (or touches) the unit square centred on (x, y).</summary>
        public static bool HitsRect(Vec2 a, Vec2 b, int x, int y)
        {
            double t0 = 0, t1 = 1;
            for (int axis = 0; axis < 2; axis++)
            {
                double pa = axis == 0 ? a.X : a.Y, pb = axis == 0 ? b.X : b.Y;
                double lo = (axis == 0 ? x : y) - 0.5, hi = (axis == 0 ? x : y) + 0.5;
                double delta = pb - pa;
                if (Math.Abs(delta) < Eps)
                {
                    if (pa < lo - Eps || pa > hi + Eps) return false;
                }
                else
                {
                    double u = (lo - pa) / delta, v = (hi - pa) / delta;
                    if (u > v) { double tmp = u; u = v; v = tmp; }
                    t0 = Math.Max(t0, u); t1 = Math.Min(t1, v);
                    if (t0 > t1 + Eps) return false;
                }
            }
            return t1 >= -Eps && t0 <= 1 + Eps;
        }

        /// <summary>Segments a→b and c→d intersect or touch.</summary>
        public static bool SegmentsMeet(Vec2 a, Vec2 b, Vec2 c, Vec2 d)
        {
            double Cross(Vec2 u, Vec2 v, Vec2 w) => (v.X - u.X) * (w.Y - u.Y) - (v.Y - u.Y) * (w.X - u.X);
            double s1 = Cross(a, b, c), s2 = Cross(a, b, d), s3 = Cross(c, d, a), s4 = Cross(c, d, b);
            return s1 * s2 <= 1e-9 && s3 * s4 <= 1e-9
                && Math.Max(Math.Min(a.X, b.X), Math.Min(c.X, d.X)) <= Math.Min(Math.Max(a.X, b.X), Math.Max(c.X, d.X)) + Eps
                && Math.Max(Math.Min(a.Y, b.Y), Math.Min(c.Y, d.Y)) <= Math.Min(Math.Max(a.Y, b.Y), Math.Max(c.Y, d.Y)) + Eps;
        }

        /// <summary>True when any wall node or closed door lies on the segment a→b (endpoints included).</summary>
        public static bool Obstructed(Stage stage, int openDoorMask, Vec2 a, Vec2 b)
        {
            foreach (var w in stage.Walls) if (HitsRect(a, b, w.X, w.Y)) return true;
            for (int i = 0; i < stage.Doors.Count; i++)
            {
                if ((openDoorMask & (1 << i)) != 0) continue;
                var door = stage.Doors[i];
                double mx = (door.A.X + door.B.X) / 2.0, my = (door.A.Y + door.B.Y) / 2.0;
                double dx = door.B.X - door.A.X, dy = door.B.Y - door.A.Y;
                var v = new Vec2(-dy * DoorHalfWidth, dx * DoorHalfWidth);
                if (SegmentsMeet(a, b, new Vec2(mx - v.X, my - v.Y), new Vec2(mx + v.X, my + v.Y))) return true;
            }
            return false;
        }

        public static bool Obstructed(Stage stage, StageState state, GridCoord a, GridCoord b) => Obstructed(stage, state.OpenDoors, Vec2.Of(a), Vec2.Of(b));

        /// <summary>
        /// Nodes a guard sees: up to two nodes straight ahead; stops at the first wall, closed door, board edge or
        /// smoke node (that node is not seen). A guard standing in smoke sees nothing.
        /// </summary>
        public static List<GridCoord> GuardVision(Stage stage, StageState state, GuardPose3 pose)
        {
            var seen = new List<GridCoord>(GuardRange);
            if (state.IsSmoked(pose.Position)) return seen;
            var origin = Vec2.Of(pose.Position);
            for (int k = 1; k <= GuardRange; k++)
            {
                var q = new GridCoord(pose.Position.X + DirectionUtil.DeltaX(pose.Facing) * k, pose.Position.Y + DirectionUtil.DeltaY(pose.Facing) * k);
                if (!stage.IsFloor(q) || Obstructed(stage, state.OpenDoors, origin, Vec2.Of(q)) || state.IsSmoked(q)) break;
                seen.Add(q);
            }
            return seen;
        }

        public static bool GuardSees(Stage stage, StageState state, GuardPose3 pose, GridCoord target)
        {
            foreach (var c in GuardVision(stage, state, pose)) if (c == target) return true;
            return false;
        }

        /// <summary>Camera lens position: just outside the wall on the mounting face.</summary>
        public static Vec2 CameraOrigin(CameraSpec cam) =>
            new Vec2(cam.WallCell.X + DirectionUtil.DeltaX(cam.MountFace) * CameraMountOffset, cam.WallCell.Y + DirectionUtil.DeltaY(cam.MountFace) * CameraMountOffset);

        /// <summary>
        /// Nodes a camera detects at counted-action index t: a 3-wide × 2-forward footprint in front of the node the lens
        /// is rounded onto, each node individually tested for obstruction and smoke from the lens origin. Disabled cameras
        /// see nothing unless <paramref name="ignoreDisabled"/> is set (for drawing the inactive footprint).
        /// </summary>
        public static List<GridCoord> CameraVision(Stage stage, StageState state, int cameraIndex, int t, bool ignoreDisabled = false)
        {
            var seen = new List<GridCoord>(CameraForward * (2 * CameraHalfWidth + 1));
            if (!ignoreDisabled && state.IsCameraDisabled(cameraIndex)) return seen;
            var cam = stage.Cameras[cameraIndex];
            var origin = CameraOrigin(cam);
            var facing = CameraTrack.FacingAt(cam, t);
            int vx = DirectionUtil.DeltaX(facing), vy = DirectionUtil.DeltaY(facing);
            int sx = -vy, sy = vx;
            int cx = (int)Math.Round(origin.X, MidpointRounding.AwayFromZero), cy = (int)Math.Round(origin.Y, MidpointRounding.AwayFromZero);
            for (int forward = 1; forward <= CameraForward; forward++)
                for (int lane = -CameraHalfWidth; lane <= CameraHalfWidth; lane++)
                {
                    var p = new GridCoord(cx + vx * forward + sx * lane, cy + vy * forward + sy * lane);
                    if (!stage.IsFloor(p)) continue;
                    var target = Vec2.Of(p);
                    if (Obstructed(stage, state.OpenDoors, origin, target)) continue;
                    bool smoked = false;
                    foreach (var cloud in state.Clouds) if (HitsRect(origin, target, cloud.Node.X, cloud.Node.Y)) { smoked = true; break; }
                    if (smoked) continue;
                    seen.Add(p);
                }
            return seen;
        }

        /// <summary>True when any active camera detects the player's node at t.</summary>
        public static bool AnyCameraSees(Stage stage, StageState state, int t, GridCoord target)
        {
            for (int i = 0; i < stage.Cameras.Count; i++)
            {
                if (state.IsCameraDisabled(i)) continue;
                foreach (var c in CameraVision(stage, state, i, t)) if (c == target) return true;
            }
            return false;
        }

        /// <summary>A smoke throw from <paramref name="from"/> to <paramref name="to"/> is legal: floor, within the 3×3, not already smoked, not obstructed.</summary>
        public static bool CanThrowSmoke(Stage stage, StageState state, GridCoord from, GridCoord to)
        {
            if (!stage.IsFloor(to)) return false;
            if (Math.Max(Math.Abs(to.X - from.X), Math.Abs(to.Y - from.Y)) > 1) return false;
            if (state.IsSmoked(to)) return false;
            return !Obstructed(stage, state.OpenDoors, Vec2.Of(from), Vec2.Of(to));
        }
    }
}
