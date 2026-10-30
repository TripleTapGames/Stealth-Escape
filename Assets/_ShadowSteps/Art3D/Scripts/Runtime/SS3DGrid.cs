using ShadowSteps.Core;
using ShadowSteps.V3;
using UnityEngine;

namespace ShadowSteps.Art3D
{
    /// <summary>
    /// The only place that maps logical V4 coordinates to the 3D world (ART_DIRECTION_AND_TECH_SPEC.md contract):
    /// X → Unity +X, row Y increasing downward → Unity −Z, tile size 1, board centred on the renderer root.
    /// Facing N/E/S/W = +Z/+X/−Z/−X. Nothing here touches gameplay.
    /// </summary>
    public static class SS3DGrid
    {
        public static Vector3 ToWorld(Stage stage, int x, int y, float spacing = 1f) =>
            new Vector3((x - (stage.Width - 1) * 0.5f) * spacing, 0f, ((stage.Height - 1) * 0.5f - y) * spacing);

        public static Vector3 ToWorld(Stage stage, GridCoord c, float spacing = 1f) => ToWorld(stage, c.X, c.Y, spacing);

        /// <summary>Nearest logical cell to a world point on the XZ plane (may be outside the board).</summary>
        public static GridCoord ToGrid(Stage stage, Vector3 world, float spacing = 1f)
        {
            int x = Mathf.RoundToInt(world.x / spacing + (stage.Width - 1) * 0.5f);
            int y = Mathf.RoundToInt((stage.Height - 1) * 0.5f - world.z / spacing);
            return new GridCoord(x, y);
        }

        public static Vector3 Heading(Direction d)
        {
            switch (d)
            {
                case Direction.East: return Vector3.right;
                case Direction.South: return Vector3.back;
                case Direction.West: return Vector3.left;
                default: return Vector3.forward;
            }
        }

        public static Quaternion Facing(Direction d) => Quaternion.LookRotation(Heading(d), Vector3.up);

        /// <summary>Intersects a camera ray with the horizontal plane at the given height; false when the ray is parallel.</summary>
        public static bool RayToPlane(Camera camera, Vector2 screenPos, float planeY, out Vector3 hit)
        {
            var ray = camera.ScreenPointToRay(screenPos);
            var plane = new Plane(Vector3.up, new Vector3(0f, planeY, 0f));
            if (plane.Raycast(ray, out float enter)) { hit = ray.GetPoint(enter); return true; }
            hit = Vector3.zero; return false;
        }
    }
}
