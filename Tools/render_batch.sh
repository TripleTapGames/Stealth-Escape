#!/bin/sh
# Headless art rendering while the Unity Editor GUI holds the main project lock.
# Syncs the working tree into a scratch clone (../StealthEscape_render, Library included), runs one editor method there
# in -batchmode WITH graphics (Camera.Render needs a GPU device), then copies Docs/Reports/Renders back here.
# Usage: Tools/render_batch.sh <ShadowSteps.Art3D.Editor.SS3DBatchRender.RenderScene|PrefabSheet> [-ss... args]
# Example: Tools/render_batch.sh ShadowSteps.Art3D.Editor.SS3DBatchRender.RenderScene -ssScene Assets/_ShadowSteps/Art3D/Levels/Town/SS3D_Town_Plaza_Authored.unity -ssOut Docs/Reports/Renders/plaza.png -ssGuides
UNITY="/Applications/Unity/Hub/Editor/2022.3.62f3/Unity.app/Contents/MacOS/Unity"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
CLONE="$(dirname "$ROOT")/$(basename "$ROOT")_render"
METHOD="$1"; shift
[ -z "$METHOD" ] && { echo "usage: $0 <method> [args]"; exit 2; }
mkdir -p "$CLONE/Logs" "$ROOT/Docs/Reports/Renders"
rsync -a --delete --exclude Temp --exclude Logs --exclude .git --exclude obj --exclude 'Library/Bee/artifacts' --exclude '*.csproj' --exclude '*.sln' \
  "$ROOT/Assets/" "$CLONE/Assets/" || exit 1
rsync -a "$ROOT/Docs/Reports/Renders/" "$CLONE/Docs/Reports/Renders/"
rsync -a "$ROOT/ProjectSettings/" "$CLONE/ProjectSettings/"
rsync -a "$ROOT/Packages/" "$CLONE/Packages/"
[ -f "$CLONE/Temp/UnityLockfile" ] && { echo "clone is locked ($CLONE/Temp/UnityLockfile); another render is running"; exit 3; }
LOG="$CLONE/Logs/render_$(date +%H%M%S).log"
cd "$CLONE" || exit 1
"$UNITY" -batchmode -projectPath "$CLONE" -executeMethod "$METHOD" "$@" -logFile "$LOG" -quit
CODE=$?
rsync -a "$CLONE/Docs/Reports/Renders/" "$ROOT/Docs/Reports/Renders/"
echo "--- unity exit $CODE; log $LOG"
grep -E "\[SS3D\]|error CS|Exception|failed" "$LOG" | grep -v "^$" | tail -40
exit $CODE
