using System.IO;
using System.Text;
using ShadowSteps.Core;
using ShadowSteps.Data;
using UnityEditor;
using UnityEngine;

namespace ShadowSteps.Editor
{
    /// <summary>Editor commands over the packaged level data: validate, solve, metrics, migration export. Never edits the shipped file in place.</summary>
    public static class LevelValidationMenu
    {
        public const string PackagedLevels = "Assets/StreamingAssets/ShadowSteps/levels.json";
        public const string ReportsFolder = "Docs/Reports";

        [MenuItem("Shadow Steps/Levels/Validate packaged levels.json")]
        public static void ValidatePackaged()
        {
            var pack = LevelPackLoader.Parse(File.ReadAllText(PackagedLevels));
            var report = LevelValidator.ValidatePack(pack.Levels);
            foreach (var w in pack.Warnings) Debug.LogWarning(w);
            Debug.Log("[ShadowSteps] " + pack.Levels.Count + " levels, " + report.ErrorCount + " errors, " + report.WarningCount + " warnings\n" + report);
        }

        [MenuItem("Shadow Steps/Levels/Solve all and write metrics report")]
        public static void SolveAll()
        {
            var pack = LevelPackLoader.Parse(File.ReadAllText(PackagedLevels));
            var sb = new StringBuilder();
            sb.AppendLine("# Solver metrics (" + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm") + ")");
            sb.AppendLine();
            sb.AppendLine("| level | name | shortest | no WAIT | no smoke | no EMP | period | safe ratio | branching | shortest solutions | solution |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|");
            foreach (var l in pack.Levels)
            {
                var m = LevelMetricsAnalyzer.Analyze(l);
                string F(int v) => v < 0 ? "unsolvable" : v.ToString();
                sb.AppendLine("| " + l.Id + " | " + l.Name + " | " + F(m.ShortestWinTurns) + " | " + F(m.ShortestWithoutWait) + " | " + F(m.ShortestWithoutSmoke) + " | " + F(m.ShortestWithoutEmp) +
                              " | " + m.GuardCycleLcm + " | " + m.SafeNodeRatio.ToString("0.00") + " | " + m.AverageBranchingFactor.ToString("0.00") + " | " + m.ShortestSolutionCount + (m.ShortestSolutionCountCapped ? "+" : "") +
                              " | `" + GameActionUtil.ToTokenString(m.ShortestSolution) + "` |");
            }
            Directory.CreateDirectory(ReportsFolder);
            string path = Path.Combine(ReportsFolder, "solver_metrics.md");
            File.WriteAllText(path, sb.ToString());
            Debug.Log("[ShadowSteps] wrote " + path + "\n" + sb);
        }

        [MenuItem("Shadow Steps/Levels/Export packaged levels as schema 2 (with backup)")]
        public static void ExportSchema2()
        {
            var pack = LevelPackLoader.Parse(File.ReadAllText(PackagedLevels));
            string backup = PackagedLevels + "." + System.DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".bak";
            File.Copy(PackagedLevels, backup, true);
            string outPath = Path.Combine(Path.GetDirectoryName(PackagedLevels), "levels_v2.json");
            File.WriteAllText(outPath, LevelPackWriter.ToJson(pack));
            AssetDatabase.Refresh();
            Debug.Log("[ShadowSteps] exported " + outPath + " (backup of original at " + backup + "). The packaged levels.json was not replaced.");
        }
    }
}
