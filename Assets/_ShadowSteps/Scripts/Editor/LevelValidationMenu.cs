using System.IO;
using System.Text;
using ShadowSteps.Data;
using ShadowSteps.V3;
using UnityEditor;
using UnityEngine;

namespace ShadowSteps.EditorTools
{
    /// <summary>Editor entry points for the V3 stage pack: structural + solver validation and scoring re-export.</summary>
    public static class LevelValidationMenu
    {
        public const string PackagedStages = "Assets/StreamingAssets/ShadowSteps/levels_v4.json";
        public const string PackagedValidation = "Assets/StreamingAssets/ShadowSteps/validation_v4.json";
        public const string ReportPath = "Docs/Reports/stage_validation_v4.txt";
        /// <summary>Pack backups live outside Assets so Unity never packages or imports them (D-072).</summary>
        public const string BackupDir = "Backups/levels";

        [MenuItem("Shadow Steps/Stages/Validate packaged levels_v4.json (structure + solver)")]
        public static void ValidatePackaged()
        {
            var pack = StageJson.Parse(File.ReadAllText(PackagedStages));
            var sb = new StringBuilder();
            int invalid = 0;
            foreach (var s in pack.Stages)
            {
                var m = new StageMetrics();
                var r = StageValidator.Validate(s, null, m);
                if (!r.IsValid) invalid++;
                sb.AppendLine((r.IsValid ? "valid   " : "INVALID ") + m);
                if (!r.IsValid || r.WarningCount > 0) sb.Append(r.ToString());
            }
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, sb.ToString());
            Debug.Log("[ShadowSteps] " + pack.Stages.Count + " stages, " + invalid + " invalid. Report: " + ReportPath + "\n" + sb);
        }

        [MenuItem("Shadow Steps/Stages/Re-export scoring into levels_v4.json (with backup)")]
        public static void ReexportScoring()
        {
            var pack = StageJson.Parse(File.ReadAllText(PackagedStages));
            Directory.CreateDirectory(BackupDir);
            string backup = Path.Combine(BackupDir, "levels_v4_" + System.DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".json");
            File.Copy(PackagedStages, backup, true);
            var updated = new System.Collections.Generic.List<Stage>();
            int unsolvable = 0;
            try
            {
                for (int i = 0; i < pack.Stages.Count; i++)
                {
                    var s = pack.Stages[i];
                    EditorUtility.DisplayProgressBar("Re-exporting scoring (shipped rules)", s.Code, (float)i / pack.Stages.Count);
                    updated.Add(StagePackTools.WithRecomputedScoring(s, out var m)); // D-072: same routine as the stage editor
                    if (!m.Solvable) unsolvable++;
                }
            }
            finally { EditorUtility.ClearProgressBar(); }
            File.WriteAllText(PackagedStages, StageJson.ToJson(updated));
            AssetDatabase.Refresh();
            Debug.Log("[ShadowSteps] scoring re-exported for " + updated.Count + " stages (backup " + backup + ")" + (unsolvable > 0 ? "; " + unsolvable + " UNSOLVABLE" : "") + ". Run Shadow Steps ▸ Stages ▸ Export validation_v4.json (or Tools/CoreTestRunner --v3-export-validation) to refresh validation_v4.json.");
        }

        [MenuItem("Shadow Steps/Stages/Export validation_v4.json from levels_v4.json")]
        public static void ExportValidation()
        {
            var pack = StageJson.Parse(File.ReadAllText(PackagedStages));
            string json;
            int unsolvable;
            try
            {
                json = StagePackTools.ValidationJson(pack.Stages, out unsolvable, (i, n, m) =>
                    !EditorUtility.DisplayCancelableProgressBar("Solving stages for validation_v4.json", (i + 1) + " / " + n + "  " + m.Code, (i + 1f) / n));
            }
            finally { EditorUtility.ClearProgressBar(); }
            if (json == null) { Debug.Log("[ShadowSteps] validation export cancelled"); return; }
            File.WriteAllText(PackagedValidation, json);
            AssetDatabase.ImportAsset(PackagedValidation);
            Debug.Log("[ShadowSteps] wrote " + PackagedValidation + (unsolvable > 0 ? " with " + unsolvable + " UNSOLVABLE stage(s)" : ""));
        }
    }
}
