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
        public const string PackagedStages = "Assets/StreamingAssets/ShadowSteps/levels_v3.json";
        public const string PackagedValidation = "Assets/StreamingAssets/ShadowSteps/validation_v3.json";
        public const string ReportPath = "Docs/Reports/stage_validation_v3.txt";

        [MenuItem("Shadow Steps/Stages/Validate packaged levels_v3.json (structure + solver)")]
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

        [MenuItem("Shadow Steps/Stages/Re-export scoring into levels_v3.json (with backup)")]
        public static void ReexportScoring()
        {
            var pack = StageJson.Parse(File.ReadAllText(PackagedStages));
            string backup = PackagedStages + "." + System.DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".bak";
            File.Copy(PackagedStages, backup, true);
            var updated = new System.Collections.Generic.List<Stage>();
            foreach (var s in pack.Stages)
            {
                var m = StageMetrics.Analyze(s);
                var b = s.ToBuilder();
                b.Par = m.Par; b.ThreeStarActions = m.ThreeStar; b.TwoStarActions = m.TwoStar; b.GhostOffered = m.GhostOffered;
                updated.Add(b.Build());
            }
            File.WriteAllText(PackagedStages, StageJson.ToJson(updated));
            AssetDatabase.Refresh();
            Debug.Log("[ShadowSteps] scoring re-exported for " + updated.Count + " stages (backup " + backup + "). Run Tools/CoreTestRunner --v3-export-validation to refresh validation_v3.json.");
        }
    }
}
