using System.Collections.Generic;
using ShadowSteps.V3;

namespace ShadowSteps.Data
{
    /// <summary>Ordered, read-only access to the loaded V3 stages (FTUE first, then the main levels).</summary>
    public sealed class StageCatalog
    {
        public IReadOnlyList<Stage> Stages { get; }
        public int Count => Stages.Count;

        public StageCatalog(IReadOnlyList<Stage> stages) { Stages = stages; }

        public Stage ById(int id) { foreach (var s in Stages) if (s.Id == id) return s; return null; }
        public Stage ByCode(string code) { foreach (var s in Stages) if (s.Code == code) return s; return null; }
        public Stage ByIndex(int index) => index >= 0 && index < Count ? Stages[index] : null;
        public int IndexOf(int id) { for (int i = 0; i < Count; i++) if (Stages[i].Id == id) return i; return -1; }
        public Stage NextAfter(int id) { int i = IndexOf(id); return i >= 0 ? ByIndex(i + 1) : null; }
    }

    /// <summary>
    /// Release progression (handover §9): FTUE 1–5 then Main 1–25 in catalog order; any successful escape unlocks the
    /// next stage. Stars and Ghost are independent persistent achievements, never gates.
    /// </summary>
    public static class StageProgression
    {
        /// <summary>Set by the runtime to true only in the editor / development builds; release builds ignore the saved flag.</summary>
        public static bool DebugUnlockAllowed = true;

        public static bool IsUnlocked(StageCatalog catalog, SaveData save, int stageId)
        {
            int index = catalog.IndexOf(stageId);
            if (index < 0) return false;
            if (index == 0) return true;
            if (save == null) return false;
            if (save.DebugUnlockAll && DebugUnlockAllowed) return true;
            return save.IsCompleted(catalog.ByIndex(index - 1).Id);
        }

        public static Stage FirstUnfinished(StageCatalog catalog, SaveData save)
        {
            foreach (var s in catalog.Stages) if (save == null || !save.IsCompleted(s.Id)) return s;
            return catalog.ByIndex(catalog.Count - 1);
        }

        public static int Stars(Stage stage, int countedActions) => StageScoring.Stars(countedActions, stage.Par);

        /// <summary>Records a win (best actions, best stars, Ghost) and unlocks the next stage. Returns true when the action count is a new best.</summary>
        public static bool RecordWin(StageCatalog catalog, SaveData save, Stage stage, int countedActions, bool ghostEarned)
        {
            var next = catalog.NextAfter(stage.Id);
            int stars = Stars(stage, countedActions);
            return save.RecordWin(stage.Id, countedActions, stars, ghostEarned && stage.GhostOffered, next != null ? next.Id : stage.Id);
        }
    }
}
