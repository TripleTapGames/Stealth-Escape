using System.Collections.Generic;

namespace ShadowSteps.Data
{
    /// <summary>Best result for one level. Only written after a verified win.</summary>
    public sealed class LevelResult
    {
        public bool Completed;
        public int BestTurns;

        public LevelResult() { }
        public LevelResult(bool completed, int bestTurns) { Completed = completed; BestTurns = bestTurns; }
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
        public const int CurrentVersion = 1;

        public int Version = CurrentVersion;
        /// <summary>Highest level id the player may start. Level 1 is always unlocked.</summary>
        public int HighestUnlockedLevelId = 1;
        public Dictionary<int, LevelResult> Results = new Dictionary<int, LevelResult>();
        public SettingsData Settings = new SettingsData();
        public List<string> SeenTutorials = new List<string>();
        /// <summary>Last level the player opened; used by the Home "Play" button.</summary>
        public int LastPlayedLevelId = 1;

        public bool IsUnlocked(int levelId) => levelId >= 1 && levelId <= HighestUnlockedLevelId;

        public LevelResult GetResult(int levelId) => Results.TryGetValue(levelId, out var r) ? r : null;

        public bool IsCompleted(int levelId) => GetResult(levelId)?.Completed ?? false;

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

        public bool HasSeenTutorial(string id) => SeenTutorials.Contains(id);
        public void MarkTutorialSeen(string id) { if (!SeenTutorials.Contains(id)) SeenTutorials.Add(id); }

        public int CompletedCount()
        {
            int n = 0;
            foreach (var r in Results.Values) if (r.Completed) n++;
            return n;
        }
    }
}
