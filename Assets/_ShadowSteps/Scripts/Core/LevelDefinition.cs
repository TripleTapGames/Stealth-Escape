using System;
using System.Collections.Generic;

namespace ShadowSteps.Core
{
    /// <summary>
    /// Immutable authored level. Built by the Data layer from levels.json (via DTO validation) or by the level editor.
    /// All simulation code reads from this and never mutates it.
    /// </summary>
    public sealed class LevelDefinition
    {
        public int Id { get; }
        public string Name { get; }
        public int Width { get; }
        public int Height { get; }
        public GridCoord Start { get; }
        public GridCoord Exit { get; }
        public IReadOnlyList<GridCoord> Walls { get; }
        public IReadOnlyList<GuardDefinition> Guards { get; }
        public int SmokeCharges { get; }
        public int EmpCharges { get; }
        public GridCoord? Key { get; }
        public GridCoord? Door { get; }
        /// <summary>Optional short onboarding hint shown at level start (not present in the original reference data).</summary>
        public string Hint { get; }

        private readonly HashSet<GridCoord> _wallSet;
        private int _period = -1;

        public LevelDefinition(
            int id, string name, int width, int height, GridCoord start, GridCoord exit,
            IReadOnlyList<GridCoord> walls, IReadOnlyList<GuardDefinition> guards,
            int smokeCharges, int empCharges, GridCoord? key, GridCoord? door, string hint = null)
        {
            Id = id;
            Name = name ?? string.Empty;
            Width = width;
            Height = height;
            Start = start;
            Exit = exit;
            Walls = walls ?? Array.Empty<GridCoord>();
            Guards = guards ?? Array.Empty<GuardDefinition>();
            SmokeCharges = smokeCharges;
            EmpCharges = empCharges;
            Key = key;
            Door = door;
            Hint = hint;
            _wallSet = new HashSet<GridCoord>(Walls);
        }

        public bool HasKey => Key.HasValue;
        public bool HasDoor => Door.HasValue;
        public bool IsInside(GridCoord c) => c.IsInside(Width, Height);
        public bool IsWall(GridCoord c) => _wallSet.Contains(c);
        public bool IsKey(GridCoord c) => Key.HasValue && Key.Value == c;
        public bool IsDoor(GridCoord c) => Door.HasValue && Door.Value == c;

        /// <summary>Least common multiple of all guard cycles; guard phase is always stored modulo this value (reference period()).</summary>
        public int Period
        {
            get
            {
                if (_period < 0) _period = GuardSchedule.Period(this);
                return _period;
            }
        }

        /// <summary>Enumerates every board cell in row-major order (y outer, x inner).</summary>
        public IEnumerable<GridCoord> AllCells()
        {
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    yield return new GridCoord(x, y);
        }

        public LevelDefinition With(
            int? id = null, string name = null, int? width = null, int? height = null,
            GridCoord? start = null, GridCoord? exit = null,
            IReadOnlyList<GridCoord> walls = null, IReadOnlyList<GuardDefinition> guards = null,
            int? smokeCharges = null, int? empCharges = null,
            GridCoord? key = null, bool clearKey = false, GridCoord? door = null, bool clearDoor = false, string hint = null)
        {
            return new LevelDefinition(
                id ?? Id, name ?? Name, width ?? Width, height ?? Height, start ?? Start, exit ?? Exit,
                walls ?? Walls, guards ?? Guards, smokeCharges ?? SmokeCharges, empCharges ?? EmpCharges,
                clearKey ? null : (key ?? Key), clearDoor ? null : (door ?? Door), hint ?? Hint);
        }
    }
}
