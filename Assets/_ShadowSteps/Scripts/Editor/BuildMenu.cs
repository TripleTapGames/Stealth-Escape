using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ShadowSteps.Editor
{
    /// <summary>
    /// Reproducible builds. Signing secrets are never stored in code or in the repo: the Android keystore path, its
    /// password, the key alias and the key password are read from environment variables
    /// SS_KEYSTORE_PATH, SS_KEYSTORE_PASS, SS_KEY_ALIAS, SS_KEY_PASS. Without them a debug-signed build is produced.
    /// Headless: Unity -batchmode -quit -projectPath . -executeMethod ShadowSteps.Editor.BuildMenu.BuildAndroidAab
    /// </summary>
    public static class BuildMenu
    {
        public const string OutputRoot = "Builds";

        private static string[] Scenes()
        {
            var list = new System.Collections.Generic.List<string>();
            foreach (var s in EditorBuildSettings.scenes) if (s.enabled) list.Add(s.path);
            if (list.Count == 0) throw new InvalidOperationException("No scenes enabled in Build Settings; run Shadow Steps/Setup/Generate All");
            return list.ToArray();
        }

        [MenuItem("Shadow Steps/Build/Android AAB (release)")]
        public static void BuildAndroidAab() => BuildAndroid(aab: true, development: false);

        [MenuItem("Shadow Steps/Build/Android APK (development)")]
        public static void BuildAndroidApkDev() => BuildAndroid(aab: false, development: true);

        public static void BuildAndroid(bool aab, bool development)
        {
            ApplyAndroidPlayerSettings();
            ApplySigningFromEnvironment();
            EditorUserBuildSettings.buildAppBundle = aab;
            Directory.CreateDirectory(OutputRoot);
            string file = Path.Combine(OutputRoot, "ShadowSteps-" + PlayerSettings.bundleVersion + "-" + PlayerSettings.Android.bundleVersionCode + (development ? "-dev" : "") + (aab ? ".aab" : ".apk"));
            var options = new BuildPlayerOptions
            {
                scenes = Scenes(),
                locationPathName = file,
                target = BuildTarget.Android,
                options = development ? BuildOptions.Development | BuildOptions.AllowDebugging : BuildOptions.None
            };
            Report(BuildPipeline.BuildPlayer(options));
        }

        [MenuItem("Shadow Steps/Build/iOS Xcode project")]
        public static void BuildIosXcode()
        {
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneAndiPad;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetArchitecture(BuildTargetGroup.iOS, 1); // ARM64
            string dir = Path.Combine(OutputRoot, "iOS");
            Directory.CreateDirectory(dir);
            Report(BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = Scenes(), locationPathName = dir, target = BuildTarget.iOS, options = BuildOptions.None }));
        }

        /// <summary>Release-relevant player settings, applied explicitly so builds do not depend on editor UI state.</summary>
        [MenuItem("Shadow Steps/Build/Apply release player settings")]
        public static void ApplyAndroidPlayerSettings()
        {
            PlayerSettings.productName = "Shadow Steps";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24; // Firebase 13.x / AppLovin 13.x / Facebook 17 need 23+; the project ships 24
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.Android, ManagedStrippingLevel.Medium);
            PlayerSettings.Android.forceInternetPermission = false;
            PlayerSettings.Android.useCustomKeystore = PlayerSettings.Android.useCustomKeystore; // untouched here; see ApplySigningFromEnvironment
            AssetDatabase.SaveAssets();
        }

        private static void ApplySigningFromEnvironment()
        {
            string ks = Environment.GetEnvironmentVariable("SS_KEYSTORE_PATH");
            if (string.IsNullOrEmpty(ks))
            {
                Debug.LogWarning("[ShadowSteps] SS_KEYSTORE_PATH not set: building with the debug keystore (not uploadable to Google Play).");
                PlayerSettings.Android.useCustomKeystore = false;
                return;
            }
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = ks;
            PlayerSettings.Android.keystorePass = Environment.GetEnvironmentVariable("SS_KEYSTORE_PASS") ?? string.Empty;
            PlayerSettings.Android.keyaliasName = Environment.GetEnvironmentVariable("SS_KEY_ALIAS") ?? string.Empty;
            PlayerSettings.Android.keyaliasPass = Environment.GetEnvironmentVariable("SS_KEY_PASS") ?? string.Empty;
        }

        private static void Report(BuildReport report)
        {
            var s = report.summary;
            string msg = "[ShadowSteps] build " + s.result + " → " + s.outputPath + " (" + s.totalSize / (1024 * 1024) + " MB, " + s.totalErrors + " errors, " + s.totalWarnings + " warnings, " + s.totalTime.TotalSeconds.ToString("0") + " s)";
            if (s.result == BuildResult.Succeeded) Debug.Log(msg); else Debug.LogError(msg);
            if (Application.isBatchMode && s.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}
