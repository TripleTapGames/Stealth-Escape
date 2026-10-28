using System.Collections.Generic;
using ShadowSteps.Core;

namespace ShadowSteps.Data
{
    /// <summary>Read-only access to the loaded level pack plus lookups by id/index.</summary>
    public sealed class LevelCatalog
    {
        public LevelPack Pack { get; }
        public IReadOnlyList<LevelDefinition> Levels => Pack.Levels;
        public int Count => Pack.Levels.Count;
        public LevelCatalog(LevelPack pack) { Pack = pack; }
        public LevelDefinition ById(int id) => Pack.FindById(id);
        public LevelDefinition ByIndex(int index) => index >= 0 && index < Count ? Pack.Levels[index] : null;
        public int IndexOf(int id) => Pack.IndexOf(id);
        public LevelDefinition NextAfter(int id) { int i = IndexOf(id); return i >= 0 ? ByIndex(i + 1) : null; }
    }

    /// <summary>
    /// Unlock and scoring rules over the catalog order (not numeric ids), so packs can be reordered or inserted.
    /// Stars follow DESIGN.md: 3 = par (solver shortest), 2 = par + 2 or better, 1 = any completion.
    /// </summary>
    public static class Progression
    {
        public static bool IsUnlocked(LevelCatalog catalog, SaveData save, int levelId)
        {
            int index = catalog.IndexOf(levelId);
            if (index < 0) return false;
            if (index == 0) return true;
            if (save == null) return false;
            return save.IsCompleted(catalog.ByIndex(index - 1).Id);
        }

        public static LevelDefinition FirstUnfinished(LevelCatalog catalog, SaveData save)
        {
            foreach (var l in catalog.Levels) if (save == null || !save.IsCompleted(l.Id)) return l;
            return catalog.ByIndex(catalog.Count - 1);
        }

        public static int Stars(int turns, int par)
        {
            if (par <= 0) return 1;
            if (turns <= par) return 3;
            if (turns <= par + 2) return 2;
            return 1;
        }

        /// <summary>Records a win and unlocks the next catalog entry. Returns true when it is a new best.</summary>
        public static bool RecordWin(LevelCatalog catalog, SaveData save, int levelId, int turns)
        {
            var next = catalog.NextAfter(levelId);
            return save.RecordWin(levelId, turns, next != null ? next.Id : levelId);
        }
    }

    /// <summary>Par turns per level, read from the exported validation files (solver shortest). 0 when unknown.</summary>
    public sealed class ParTable
    {
        private readonly Dictionary<int, int> _par = new Dictionary<int, int>();

        public int Count => _par.Count;

        public void AddFromValidationJson(string json)
        {
            var arr = JsonAccess.AsArray(MiniJson.Parse(json), "validation");
            foreach (var item in arr)
            {
                var o = (JsonObject)item;
                int moves = JsonAccess.AsInt(o["moves"], "moves");
                if (moves < 0) continue; // exported as unsolvable under the level's detection rule: no par, 1 star for any completion
                _par[JsonAccess.AsInt(o["level"], "level")] = moves;
            }
        }

        public int ParFor(int levelId) => _par.TryGetValue(levelId, out var p) ? p : 0;
    }
}
