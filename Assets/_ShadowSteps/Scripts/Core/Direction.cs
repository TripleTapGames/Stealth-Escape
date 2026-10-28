namespace ShadowSteps.Core
{
    /// <summary>Facing / movement direction. Numeric values are canonical: N=0, E=1, S=2, W=3.</summary>
    public enum Direction
    {
        North = 0,
        East = 1,
        South = 2,
        West = 3
    }

    public static class DirectionUtil
    {
        public const int Count = 4;

        /// <summary>Delta on X for each direction (reference DIRS = [(0,-1),(1,0),(0,1),(-1,0)]).</summary>
        public static int DeltaX(Direction d)
        {
            switch (d)
            {
                case Direction.East: return 1;
                case Direction.West: return -1;
                default: return 0;
            }
        }

        public static int DeltaY(Direction d)
        {
            switch (d)
            {
                case Direction.North: return -1;
                case Direction.South: return 1;
                default: return 0;
            }
        }

        /// <summary>Rotates by <paramref name="steps"/> quarter turns; +1 is clockwise (N->E->S->W). Works for negative values.</summary>
        public static Direction Rotate(Direction d, int steps) => (Direction)Mod((int)d + steps, Count);

        public static Direction Opposite(Direction d) => Rotate(d, 2);

        /// <summary>Python-style modulo: result is always in [0, m).</summary>
        public static int Mod(int value, int m)
        {
            int r = value % m;
            return r < 0 ? r + m : r;
        }

        /// <summary>Returns the cardinal direction for a unit delta, or null when the delta is not a unit cardinal step.</summary>
        public static Direction? FromDelta(int dx, int dy)
        {
            if (dx == 0 && dy == -1) return Direction.North;
            if (dx == 1 && dy == 0) return Direction.East;
            if (dx == 0 && dy == 1) return Direction.South;
            if (dx == -1 && dy == 0) return Direction.West;
            return null;
        }

        public static bool IsValid(int value) => value >= 0 && value < Count;

        public static string ToToken(Direction d)
        {
            switch (d)
            {
                case Direction.North: return "N";
                case Direction.East: return "E";
                case Direction.South: return "S";
                default: return "W";
            }
        }
    }
}
