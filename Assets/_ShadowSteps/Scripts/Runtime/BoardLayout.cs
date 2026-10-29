using ShadowSteps.Core;
using UnityEngine;

namespace ShadowSteps.Runtime
{
    /// <summary>
    /// The only place that converts logical coordinates to world positions (LEVEL_DESIGN_PARAMETERS.md §1):
    /// <c>world(x,y) = origin + (x * nodeSpacing, -y * nodeSpacing, 0)</c>. The board is centred on the board root,
    /// the camera looks down -Z at the XY plane (strict top-down orthographic). Every move covers exactly nodeSpacing.
    /// </summary>
    public readonly struct BoardLayout
    {
        public readonly int Width;
        public readonly int Height;
        public readonly float NodeSpacing;
        public readonly Vector2 Origin;

        public BoardLayout(int width, int height, float nodeSpacing)
        {
            Width = width;
            Height = height;
            NodeSpacing = nodeSpacing;
            Origin = new Vector2(-(width - 1) * 0.5f * nodeSpacing, (height - 1) * 0.5f * nodeSpacing);
        }

        public static BoardLayout For(LevelDefinition level) => new BoardLayout(level.Width, level.Height, (float)level.NodeSpacing);
        /// <summary>V3 stages always use a spacing of one world unit.</summary>
        public static BoardLayout For(ShadowSteps.V3.Stage stage) => new BoardLayout(stage.Width, stage.Height, 1f);

        public Vector3 WorldPosition(GridCoord c) => new Vector3(Origin.x + c.X * NodeSpacing, Origin.y - c.Y * NodeSpacing, 0f);

        public Vector3 WorldPosition(GridCoord c, float z) { var p = WorldPosition(c); p.z = z; return p; }

        /// <summary>Nearest lattice coordinate to a world point (may be outside the board; callers check HasNode).</summary>
        public GridCoord NearestCoord(Vector3 world)
        {
            int x = Mathf.RoundToInt((world.x - Origin.x) / NodeSpacing);
            int y = Mathf.RoundToInt((Origin.y - world.y) / NodeSpacing);
            return new GridCoord(x, y);
        }

        /// <summary>World-space size of the whole board including half a node of margin on each side.</summary>
        public Vector2 BoardSize => new Vector2(Width * NodeSpacing, Height * NodeSpacing);

        public static Quaternion RotationFor(Direction d)
        {
            // Sprites are authored pointing "up" (north = +Y). Clockwise facing order N, E, S, W => 0, -90, -180, -270 degrees.
            return Quaternion.Euler(0f, 0f, -90f * (int)d);
        }
    }
}
