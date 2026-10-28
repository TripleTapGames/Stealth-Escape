using System;

namespace ShadowSteps.Core
{
    /// <summary>
    /// Integer board coordinate. Origin (0,0) is the TOP-LEFT tile, X grows east (right), Y grows south (down).
    /// This matches reference/DESIGN.md and reference/build.py exactly.
    /// </summary>
    public readonly struct GridCoord : IEquatable<GridCoord>
    {
        public readonly int X;
        public readonly int Y;

        public GridCoord(int x, int y)
        {
            X = x;
            Y = y;
        }

        public GridCoord Offset(int dx, int dy) => new GridCoord(X + dx, Y + dy);

        public GridCoord Neighbor(Direction direction) =>
            Offset(DirectionUtil.DeltaX(direction), DirectionUtil.DeltaY(direction));

        public bool IsInside(int width, int height) => X >= 0 && X < width && Y >= 0 && Y < height;

        /// <summary>Manhattan distance; 1 means orthogonally adjacent.</summary>
        public int ManhattanDistance(GridCoord other) => Math.Abs(X - other.X) + Math.Abs(Y - other.Y);

        public bool Equals(GridCoord other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is GridCoord other && Equals(other);
        public override int GetHashCode() => unchecked(X * 397 ^ Y);
        public static bool operator ==(GridCoord a, GridCoord b) => a.Equals(b);
        public static bool operator !=(GridCoord a, GridCoord b) => !a.Equals(b);

        /// <summary>Formats like the Python reference tuple, e.g. "(2, 5)".</summary>
        public override string ToString() => "(" + X + ", " + Y + ")";
    }
}
