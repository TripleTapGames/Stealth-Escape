using System.Collections.Generic;
using ShadowSteps.V3;

namespace ShadowSteps.Data
{
    /// <summary>Best result for one level in one rules mode. Only written after a verified win.</summary>
    public sealed class LevelResult
    {
        public const string StealthRulesVersion = "stealth-v4";
        public bool Completed;
        /// <summary>Rule set the record was earned under (D-040). Legacy untagged records are Stealth under the original rules.</summary>
        public string RulesVersion = StealthRulesVersion;
        /// <summary>Fewest counted actions (Moves + Waits) in a successful attempt.</summary>
        public int BestTurns;
        /// <summary>Best star rating ever earned (0 when never completed).</summary>
        public int BestStars;
        /// <summary>Ghost earned in any successful attempt (independent of stars).</summary>
        public bool Ghost;

        public LevelResult() { }
        public LevelResult(bool completed, int bestTurns) { Completed = completed; BestTurns = bestTurns; }
        public LevelResult(bool completed, int bestTurns, int bestStars, bool ghost) { Completed = completed; BestTurns = bestTurns; BestStars = bestStars; Ghost = ghost; }
    }

    public sealed class SettingsData
    {
        public bool MusicOn = true;
        public bool SfxOn = true;
        public bool HapticsOn = true;
        public bool ReducedMotion = false;
        public bool ColorBlindAssist = false;

        public SettingsData Clone() => (SettingsData)MemberwiseClone();
    }

    /// <summary>
    /// Everything persisted locally. Mutable plain object; <see cref="SaveRepository"/> handles serialisation.
    /// Add new fields with sensible defaults and bump <see cref="CurrentVersion"/> if an existing field changes meaning.
    /// </summary>
    public sealed class SaveData
    {
        /// <summary>2 (2026-11-04, D-040): results carry a mode and a rules version; the selected mode is remembered. Version-1 files load unchanged (untagged results = Stealth).</summary>
        public const int CurrentVersion = 2;

        public int Version = CurrentVersion;
        /// <summary>Highest level id the player may start. Level 1 is always unlocked.</summary>
        public int HighestUnlockedLevelId = 1;
        /// <summary>Stealth results by stable level id (the legacy table; never overwritten by a Knockout run).</summary>
        public Dictionary<int, LevelResult> Results = new Dictionary<int, LevelResult>();
        /// <summary>Knockout results under the current rules (<see cref="StageModeUtil.RulesVersion"/>) by stable level id.</summary>
        public Dictionary<int, LevelResult> KnockoutResults = new Dictionary<int, LevelResult>();
        /// <summary>Knockout results earned under an older rules revision (e.g. with WAIT), preserved and labelled but never compared to current pars (D-044).</summary>
        public Dictionary<int, LevelResult> KnockoutLegacyResults = new Dictionary<int, LevelResult>();
        /// <summary>Remembered rules mode; Stealth when nothing was ever selected.</summary>
        public StageMode SelectedMode = StageModeUtil.Default;
        public SettingsData Settings = new SettingsData();
        public List<string> SeenTutorials = new List<string>();
        /// <summary>Last level the player opened; used by the Home "Play" button.</summary>
        public int LastPlayedLevelId = 1;
        /// <summary>Development playtest only: every stage selectable. Never persisted as true in release builds.</summary>
        public bool DebugUnlockAll;

        public bool IsUnlocked(int levelId) => levelId >= 1 && levelId <= HighestUnlockedLevelId;

        /// <summary>The Stealth record (legacy accessor).</summary>
        public LevelResult GetResult(int levelId) => GetResult(levelId, StageMode.Stealth);
        public LevelResult GetResult(int levelId, StageMode mode) => Table(mode).TryGetValue(levelId, out var r) ? r : null;
        public Dictionary<int, LevelResult> Table(StageMode mode) => mode == StageMode.Knockout ? KnockoutResults : Results;

        /// <summary>Completed in either mode or under older rules: unlocks and progress are never lost.</summary>
        public bool IsCompleted(int levelId) => IsCompleted(levelId, StageMode.Stealth) || IsCompleted(levelId, StageMode.Knockout) || (KnockoutLegacyResults.TryGetValue(levelId, out var legacy) && legacy.Completed);
        /// <summary>Best to show for a level: current-rules Knockout, else the labelled legacy Knockout record, else the legacy Stealth record.</summary>
        public LevelResult DisplayResult(int levelId) => GetResult(levelId, StageMode.Knockout) ?? (KnockoutLegacyResults.TryGetValue(levelId, out var l) ? l : null) ?? GetResult(levelId, StageMode.Stealth);
        public bool IsCompleted(int levelId, StageMode mode) => GetResult(levelId, mode)?.Completed ?? false;

        /// <summary>Records a win; keeps the lower turn count; unlocks the next level. Returns true when a new best was set.</summary>
        public bool RecordWin(int levelId, int turns, int nextLevelId)
        {
            bool newBest;
            if (Results.TryGetValue(levelId, out var r) && r.Completed)
            {
                newBest = turns < r.BestTurns;
                if (newBest) r.BestTurns = turns;
            }
            else
            {
                Results[levelId] = new LevelResult(true, turns);
                newBest = true;
            }
            if (nextLevelId > HighestUnlockedLevelId) HighestUnlockedLevelId = nextLevelId;
            return newBest;
        }

        /// <summary>V3 win record (Stealth): keeps the lowest action count, the highest star rating and any Ghost; unlocks the next stage.</summary>
        public bool RecordWin(int levelId, int actions, int stars, bool ghost, int nextLevelId) => RecordWin(levelId, actions, stars, ghost, nextLevelId, StageMode.Stealth);

        /// <summary>Records a win in the given mode's table only (a Knockout run never touches the Stealth best); unlocks are shared.</summary>
        public bool RecordWin(int levelId, int actions, int stars, bool ghost, int nextLevelId, StageMode mode)
        {
            bool newBest; var table = Table(mode);
            string rules = mode == StageMode.Knockout ? StageModeUtil.RulesVersion : LevelResult.StealthRulesVersion;
            if (mode == StageMode.Knockout && table.TryGetValue(levelId, out var stale) && stale.RulesVersion != rules)
            {
                // A record from an older rules revision is not comparable: archive it (labelled) and start the current-rules record fresh.
                KnockoutLegacyResults[levelId] = stale; table.Remove(levelId);
            }
            if (table.TryGetValue(levelId, out var r) && r.Completed)
            {
                newBest = actions < r.BestTurns;
                if (newBest) r.BestTurns = actions;
                if (stars > r.BestStars) r.BestStars = stars;
                r.Ghost |= ghost;
                r.RulesVersion = rules;
            }
            else
            {
                table[levelId] = new LevelResult(true, actions, stars, ghost) { RulesVersion = rules };
                newBest = true;
            }
            if (nextLevelId > HighestUnlockedLevelId) HighestUnlockedLevelId = nextLevelId;
            return newBest;
        }

        public bool HasSeenTutorial(string id) => SeenTutorials.Contains(id);
        public void MarkTutorialSeen(string id) { if (!SeenTutorials.Contains(id)) SeenTutorials.Add(id); }

        /// <summary>Levels completed in at least one mode.</summary>
        public int CompletedCount()
        {
            var ids = new HashSet<int>();
            foreach (var kv in Results) if (kv.Value.Completed) ids.Add(kv.Key);
            foreach (var kv in KnockoutResults) if (kv.Value.Completed) ids.Add(kv.Key);
            foreach (var kv in KnockoutLegacyResults) if (kv.Value.Completed) ids.Add(kv.Key);
            return ids.Count;
        }
    }
}
