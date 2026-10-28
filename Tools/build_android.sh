#!/bin/sh
# Headless Android build. Set SS_KEYSTORE_PATH/SS_KEYSTORE_PASS/SS_KEY_ALIAS/SS_KEY_PASS for a release-signed AAB.
# Usage: Tools/build_android.sh [aab|apk]
UNITY="/Applications/Unity/Hub/Editor/2022.3.62f3/Unity.app/Contents/MacOS/Unity"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
if [ -f "$ROOT/Temp/UnityLockfile" ]; then echo "Project is open in the Unity Editor. Close it first."; exit 2; fi
METHOD="ShadowSteps.Editor.BuildMenu.BuildAndroidAab"
[ "$1" = "apk" ] && METHOD="ShadowSteps.Editor.BuildMenu.BuildAndroidApkDev"
mkdir -p "$ROOT/Docs/Reports"
"$UNITY" -batchmode -nographics -quit -projectPath "$ROOT" -buildTarget Android -executeMethod "$METHOD" -logFile "$ROOT/Docs/Reports/build_android.log"
code=$?
grep -E "\[ShadowSteps\] build|error CS|Error building" "$ROOT/Docs/Reports/build_android.log" | tail -5
exit $code
