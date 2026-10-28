using System.Collections.Generic;
using ShadowSteps.Core;

namespace ShadowSteps.Data
{
    /// <summary>A loaded, validated set of levels plus the file metadata it came from.</summary>
    public sealed class LevelPack
    {
        public const int CurrentFormatVersion = 1;

        public int FormatVersion { get; }
        public string CoordinateSystem { get; }
        public IReadOnlyList<LevelDefinition> Levels { get; }
        /// <summary>Non-fatal notes gathered while loading (unknown fields, migrated values).</summary>
        public IReadOnlyList<string> Warnings { get; }

        public LevelPack(int formatVersion, string coordinateSystem, IReadOnlyList<LevelDefinition> levels, IReadOnlyList<string> warnings)
        {
            FormatVersion = formatVersion;
            CoordinateSystem = coordinateSystem;
            Levels = levels;
            Warnings = warnings ?? new List<string>();
        }

        public LevelDefinition FindById(int id)
        {
            foreach (var l in Levels) if (l.Id == id) return l;
            return null;
        }

        public int IndexOf(int id)
        {
            for (int i = 0; i < Levels.Count; i++) if (Levels[i].Id == id) return i;
            return -1;
        }
    }
}
