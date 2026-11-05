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
    /// Guards and smoke throws are a port of <c>hitsRect</c>, <c>segmentsMeet</c>, <c>obstructed</c>, <c>guardVision</c> from the v3
    /// prototype; cameras use the 70° cone rule (2026-11-03). A line touching the edge or corner of a wall node, a blocked
    /// cover node ('X') or a closed door counts as blocked.
    /// </summary>
    public static class Sight
    {
        private const double Eps = 1e-8;
        public const int GuardRange = 2;
        /// <summary>Camera field of view in degrees (35° to either side of the facing).</summary>
        public const double CameraFovDegrees = 70.0;
        public const double CameraHalfAngleDegrees = CameraFovDegrees * 0.5;
        /// <summary>Maximum forward projection (node spacings from the wall-face origin) a detected node centre may have.</summary>
        public const double CameraRange = 2.0;
        /// <summary>Tolerance applied to every cone boundary comparison (forward reach and half-angle), so a node exactly on the boundary counts as inside.</summary>
        public const double ConeTolerance = 1e-6;
        private static readonly double TanHalfAngle = Math.Tan(CameraHalfAngleDegrees * Math.PI / 180.0);
        /// <summary>Camera origin: the logical wall-face point, half a spacing from the wall cell centre toward the mount face.</summary>
        public const double CameraMountOffset = 0.5;
        /// <summary>Half-length of the opaque segment drawn across a closed door edge.</summary>
        public const double DoorHalfWidth = 0.48;
        /// <summary>Rays sampled across the field of view when tracing the visible cone outline for drawing.</summary>
        public const int ConeOutlineRays = 48;

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

        /// <summary>True when any wall node, blocked cover node (V4 'X') or closed door lies on the segment a→b (endpoints included; corner contact counts).</summary>
        public static bool Obstructed(Stage stage, int openDoorMask, Vec2 a, Vec2 b) => Obstructed(stage, openDoorMask, a, b, null);

        /// <summary>As above; <paramref name="ignore"/> (a camera's own mounting wall, whose face the origin sits on) never counts as an obstruction.</summary>
        public static bool Obstructed(Stage stage, int openDoorMask, Vec2 a, Vec2 b, GridCoord? ignore)
        {
            foreach (var w in stage.Occluders) { if (ignore.HasValue && w == ignore.Value) continue; if (HitsRect(a, b, w.X, w.Y)) return true; }
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
        public static List<GridCoord> GuardVision(Stage stage, StageState state, GuardPose3 pose) => GuardVision(stage, state.OpenDoors, state.Clouds, pose);

        /// <summary>Guard sight from explicit state ingredients (shared by gameplay and the renderer's cone tracing).</summary>
        public static List<GridCoord> GuardVision(Stage stage, int openDoorMask, IReadOnlyList<SmokeCloud> clouds, GuardPose3 pose)
        {
            var seen = new List<GridCoord>(GuardRange);
            if (InCloud(clouds, pose.Position)) return seen;
            var origin = Vec2.Of(pose.Position);
            for (int k = 1; k <= GuardRange; k++)
            {
                var q = new GridCoord(pose.Position.X + DirectionUtil.DeltaX(pose.Facing) * k, pose.Position.Y + DirectionUtil.DeltaY(pose.Facing) * k);
                if (!stage.IsFloor(q) || Obstructed(stage, openDoorMask, origin, Vec2.Of(q)) || InCloud(clouds, q)) break;
                seen.Add(q);
            }
            return seen;
        }

        private static bool InCloud(IReadOnlyList<SmokeCloud> clouds, GridCoord node)
        {
            if (clouds == null) return false;
            foreach (var c in clouds) if (c.Node == node) return true;
            return false;
        }

        /// <summary>
        /// Drawing-only silhouette of a guard's sight: a narrow circular sector from the guard's node along its facing, reaching
        /// 'overshoot' spacings past the last node it sees (so it ends on the edge of the wall, door, smoke or board limit that stops it),
        /// each ray clipped by the same occluders as the detection. Empty when the guard sees nothing. The rule itself is
        /// still <see cref="GuardVision"/> (two nodes straight ahead); the fan only visualises it.
        /// </summary>
        public static List<Vec2> GuardConeOutline(Stage stage, int openDoorMask, IReadOnlyList<SmokeCloud> clouds, GuardPose3 pose, double halfAngleDegrees, int rays = 24, double overshoot = 0.5, double curvature = 1.0)
        {
            var pts = new List<Vec2>(rays + 2);
            var seen = GuardVision(stage, openDoorMask, clouds, pose);
            if (seen.Count == 0) return pts;
            var origin = Vec2.Of(pose.Position);
            pts.Add(origin);
            double fx = DirectionUtil.DeltaX(pose.Facing), fy = DirectionUtil.DeltaY(pose.Facing);
            // Drawn reach: the last seen node plus 'overshoot' spacings (0.5 = up to the next cell edge); never past a blocker thanks to the ray clipping.
            double reach = seen.Count + Math.Max(0.0, Math.Min(1.0, overshoot)), half = halfAngleDegrees * Math.PI / 180.0;
            for (int i = 0; i <= rays; i++)
            {
                double a = -half + (2.0 * half) * i / rays;
                double ca = Math.Cos(a), sa = Math.Sin(a);
                var dir = new Vec2(fx * ca - fy * sa, fx * sa + fy * ca);
                double dist = RayDistance(stage, openDoorMask, clouds, origin, dir, FarEdgeRayLength(reach, a, curvature), null);
                pts.Add(new Vec2(origin.X + dir.X * dist, origin.Y + dir.Y * dist));
            }
            return pts;
        }

        public static bool GuardSees(Stage stage, StageState state, GuardPose3 pose, GridCoord target)
        {
            foreach (var c in GuardVision(stage, state, pose)) if (c == target) return true;
            return false;
        }

        /// <summary>Camera origin: the logical wall-face point (wall cell centre + ½ spacing toward the mount face). Independent of any mount artwork.</summary>
        public static Vec2 CameraOrigin(CameraSpec cam) =>
            new Vec2(cam.WallCell.X + DirectionUtil.DeltaX(cam.MountFace) * CameraMountOffset, cam.WallCell.Y + DirectionUtil.DeltaY(cam.MountFace) * CameraMountOffset);

        /// <summary>
        /// Cone test in logical grid coordinates: the target's forward projection on the facing must be positive and at most
        /// <see cref="CameraRange"/>, and its lateral offset within the 35° half-angle (|lateral| ≤ forward·tan 35°), each with
        /// <see cref="ConeTolerance"/>. Nothing behind the camera or beside its lens can pass.
        /// </summary>
        public static bool InCameraCone(Vec2 origin, Direction facing, Vec2 target)
        {
            double fx = DirectionUtil.DeltaX(facing), fy = DirectionUtil.DeltaY(facing);
            double dx = target.X - origin.X, dy = target.Y - origin.Y;
            double forward = dx * fx + dy * fy;
            if (forward <= ConeTolerance || forward > CameraRange + ConeTolerance) return false;
            double lateral = Math.Abs(dx * fy - dy * fx);
            return lateral <= forward * TanHalfAngle + ConeTolerance;
        }

        /// <summary>True when the open segment origin→target crosses an active smoke cloud's node square.</summary>
        public static bool Smoked(IReadOnlyList<SmokeCloud> clouds, Vec2 origin, Vec2 target)
        {
            if (clouds == null) return false;
            foreach (var cloud in clouds) if (HitsRect(origin, target, cloud.Node.X, cloud.Node.Y)) return true;
            return false;
        }

        /// <summary>
        /// Nodes a camera detects at counted-action index t: every floor node whose centre lies inside the 70° cone from the
        /// wall-face origin along the facing (<see cref="InCameraCone"/>), with an unobstructed line of sight from the origin to
        /// the node centre (walls, solid cover, closed doors; corner contact blocks) that crosses no smoke cloud. Disabled
        /// cameras see nothing unless <paramref name="ignoreDisabled"/> is set (for drawing the inactive cone).
        /// Node order is row-major (top-left first) so callers get a deterministic list.
        /// </summary>
        public static List<GridCoord> CameraVision(Stage stage, StageState state, int cameraIndex, int t, bool ignoreDisabled = false)
        {
            var seen = new List<GridCoord>(6);
            if (!ignoreDisabled && state.IsCameraDisabled(cameraIndex)) return seen;
            var cam = stage.Cameras[cameraIndex];
            var facing = CameraTrack.FacingAt(cam, t);
            return CameraVision(stage, state.OpenDoors, state.Clouds, cam, facing);
        }

        /// <summary>Cone detection for an explicit facing and state ingredients (shared by gameplay, preview and the renderer).</summary>
        public static List<GridCoord> CameraVision(Stage stage, int openDoorMask, IReadOnlyList<SmokeCloud> clouds, CameraSpec cam, Direction facing)
        {
            var seen = new List<GridCoord>(6);
            var origin = CameraOrigin(cam);
            int reach = (int)Math.Ceiling(CameraRange) + 1;
            int x0 = (int)Math.Floor(origin.X) - reach, x1 = (int)Math.Ceiling(origin.X) + reach;
            int y0 = (int)Math.Floor(origin.Y) - reach, y1 = (int)Math.Ceiling(origin.Y) + reach;
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    var p = new GridCoord(x, y);
                    if (!stage.IsFloor(p)) continue;
                    var target = Vec2.Of(p);
                    if (!InCameraCone(origin, facing, target)) continue;
                    if (Obstructed(stage, openDoorMask, origin, target, cam.WallCell)) continue;
                    if (Smoked(clouds, origin, target)) continue;
                    seen.Add(p);
                }
            return seen;
        }

        /// <summary>
        /// Distance along the ray origin + dir·t (dir unit length) to the first occluder: wall / cover squares (except
        /// <paramref name="ignore"/>), closed door segments and smoke cloud squares; <paramref name="maxLen"/> when clear.
        /// Shares the geometry of <see cref="HitsRect"/> / <see cref="SegmentsMeet"/> so the drawn cone and the detection agree.
        /// </summary>
        public static double RayDistance(Stage stage, int openDoorMask, IReadOnlyList<SmokeCloud> clouds, Vec2 origin, Vec2 dir, double maxLen, GridCoord? ignore)
        {
            double best = maxLen;
            foreach (var w in stage.Occluders) { if (ignore.HasValue && w == ignore.Value) continue; RaySquare(origin, dir, w.X, w.Y, ref best); }
            if (clouds != null) foreach (var c in clouds) RaySquare(origin, dir, c.Node.X, c.Node.Y, ref best);
            for (int i = 0; i < stage.Doors.Count; i++)
            {
                if ((openDoorMask & (1 << i)) != 0) continue;
                var door = stage.Doors[i];
                double mx = (door.A.X + door.B.X) / 2.0, my = (door.A.Y + door.B.Y) / 2.0;
                double ex = -(door.B.Y - door.A.Y) * DoorHalfWidth, ey = (door.B.X - door.A.X) * DoorHalfWidth;
                RaySegment(origin, dir, new Vec2(mx - ex, my - ey), new Vec2(mx + ex, my + ey), ref best);
            }
            return best;
        }

        private static void RaySquare(Vec2 o, Vec2 d, int x, int y, ref double best)
        {
            double tmin = 0, tmax = best;
            for (int axis = 0; axis < 2; axis++)
            {
                double po = axis == 0 ? o.X : o.Y, pd = axis == 0 ? d.X : d.Y;
                double lo = (axis == 0 ? x : y) - 0.5, hi = (axis == 0 ? x : y) + 0.5;
                if (Math.Abs(pd) < Eps) { if (po < lo - Eps || po > hi + Eps) return; continue; }
                double u = (lo - po) / pd, v = (hi - po) / pd;
                if (u > v) { double tmp = u; u = v; v = tmp; }
                tmin = Math.Max(tmin, u); tmax = Math.Min(tmax, v);
                if (tmin > tmax + Eps) return;
            }
            if (tmin < best) best = tmin;
        }

        private static void RaySegment(Vec2 o, Vec2 d, Vec2 a, Vec2 b, ref double best)
        {
            double ex = b.X - a.X, ey = b.Y - a.Y;
            double denom = d.X * ey - d.Y * ex;
            if (Math.Abs(denom) < Eps) return;
            double ax = a.X - o.X, ay = a.Y - o.Y;
            double t = (ax * ey - ay * ex) / denom;
            double u = (ax * d.Y - ay * d.X) / denom;
            if (t >= -Eps && t < best && u >= -Eps && u <= 1 + Eps) best = Math.Max(0, t);
        }

        /// <summary>
        /// Visible cone silhouette for drawing: the origin followed by <see cref="ConeOutlineRays"/>+1 ray end points from
        /// −35° to +35°, forming a circular sector (curved far edge) of radius <see cref="CameraRange"/>, each ray clipped
        /// at the first occluder / smoke cloud. The detection limit is a forward projection, so a node detected slightly
        /// beyond the radius (a sideways camera's node at forward 2, lateral ½ sits at 2.06) widens the radius to its
        /// distance: every detected node always lies inside the drawn sector. Logical coordinates; the renderer maps them
        /// like node positions (so the portrait rotation applies).
        /// </summary>
        /// <summary>
        /// Length of the ray at angle <paramref name="a"/> (radians from the facing) to the drawn far edge, for a cone whose
        /// central ray has length <paramref name="reach"/>. <paramref name="curvature"/> 1 = circular sector centred on the
        /// apex (edge rays as long as the centre); smaller values flatten the far edge by moving the arc's centre behind the
        /// apex (radius = reach / curvature); 0 = straight far edge (the forward plane). Never shorter than the sector, so a
        /// node inside the sector stays inside at any curvature.
        /// </summary>
        public static double FarEdgeRayLength(double reach, double a, double curvature)
        {
            double k = Math.Max(0.0, Math.Min(1.0, curvature));
            double ca = Math.Cos(a);
            if (k < 1e-4) return reach / Math.Max(1e-6, ca);
            double radius = reach / k, e = radius - reach, sa = Math.Sin(a);
            return -e * ca + Math.Sqrt(Math.Max(0.0, radius * radius - e * e * sa * sa));
        }

        public static List<Vec2> CameraConeOutline(Stage stage, int openDoorMask, IReadOnlyList<SmokeCloud> clouds, CameraSpec cam, Direction facing, int rays = ConeOutlineRays, double curvature = 1.0)
        {
            var origin = CameraOrigin(cam);
            var pts = new List<Vec2>(rays + 2) { origin };
            double fx = DirectionUtil.DeltaX(facing), fy = DirectionUtil.DeltaY(facing);
            double half = CameraHalfAngleDegrees * Math.PI / 180.0;
            double radius = CameraRange;
            foreach (var n in CameraVision(stage, openDoorMask, clouds, cam, facing))
            {
                double dx = n.X - origin.X, dy = n.Y - origin.Y;
                radius = Math.Max(radius, Math.Sqrt(dx * dx + dy * dy) + ConeTolerance);
            }
            for (int i = 0; i <= rays; i++)
            {
                double a = -half + (2.0 * half) * i / rays;
                double ca = Math.Cos(a), sa = Math.Sin(a);
                var dir = new Vec2(fx * ca - fy * sa, fx * sa + fy * ca);
                double dist = RayDistance(stage, openDoorMask, clouds, origin, dir, FarEdgeRayLength(radius, a, curvature), cam.WallCell);
                pts.Add(new Vec2(origin.X + dir.X * dist, origin.Y + dir.Y * dist));
            }
            return pts;
        }

        /// <summary>True when any active camera detects the player's node at t.</summary>
        /// <summary>
        /// Nominal camera cone for GRAPHICS (D-042): apex on the logical wall face, flat far edge two spacings forward,
        /// half-angle <see cref="CameraHalfAngleDegrees"/>; three vertices O, O + D·f + D·tan(35°)·r, O + D·f − D·tan(35°)·r.
        /// Never trimmed by walls, cover, doors, smoke or the board edge; detection is <see cref="CameraVision"/>, unchanged.
        /// </summary>
        public static List<Vec2> CameraConeGeometry(CameraSpec cam, Direction facing)
        {
            var o = CameraOrigin(cam);
            double fx = DirectionUtil.DeltaX(facing), fy = DirectionUtil.DeltaY(facing);
            double rx = -fy, ry = fx; // perpendicular in the board plane
            double d = CameraRange, half = d * TanHalfAngle;
            return new List<Vec2>(3)
            {
                o,
                new Vec2(o.X + d * fx + half * rx, o.Y + d * fy + half * ry),
                new Vec2(o.X + d * fx - half * rx, o.Y + d * fy - half * ry)
            };
        }

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
