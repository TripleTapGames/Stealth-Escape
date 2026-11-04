using System;
using System.IO;
using ShadowSteps.Runtime;
using UnityEditor;
using UnityEngine;

namespace ShadowSteps.Editor
{
    public static class ProgressMenu
    {
        private const string ClearMenu = "Shadow Steps/Save/Clear Progress";

        [MenuItem(ClearMenu)]
        public static void ClearProgress()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            string path = AppServices.SavePath;
            if (!EditorUtility.DisplayDialog("Clear Shadow Steps progress?",
                "Delete this Editor's progress, stars, tutorial history and saved settings? " +
                "This cannot be undone. Phone saves are unaffected.\n\n" + path,
                "Clear Progress", "Cancel")) return;

            try
            {
                // Remove recovery files first so a leftover backup cannot restore cleared progress.
                foreach (string suffix in new[] { ".bak", ".tmp", ".corrupt", "" })
                {
                    string file = path + suffix;
                    if (File.Exists(file)) File.Delete(file);
                }
                // Also handles Editor sessions with domain reload disabled.
                AppServices.ResetForTests();
                Debug.Log("[ShadowSteps] Editor progress cleared: " + path);
                EditorUtility.DisplayDialog("Progress cleared",
                    "Open Bootstrap.unity and press Play to start at FTUE-1. " +
                    "Directly opening a prototype scene can still use its Editor debug level override.", "OK");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Could not clear all progress", exception.Message, "OK");
            }
        }

        [MenuItem(ClearMenu, true)]
        private static bool CanClearProgress() => !EditorApplication.isPlayingOrWillChangePlaymode;
    }
}
