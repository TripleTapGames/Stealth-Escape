#!/bin/sh
# Regenerates the shipped packs from the curriculum specs with the real engine/solver, then re-exports validation traces.
# Deterministic per spec seed. Usage: Tools/generate_levels.sh [tutorial|world1|all]
set -e
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"
dotnet build Tools/CoreTestRunner/CoreTestRunner.csproj -nologo -v q -clp:NoSummary >/dev/null
R="/tmp/shadowsteps_coretests/bin/CoreTestRunner.dll"
D="Assets/StreamingAssets/ShadowSteps"
WHAT="${1:-all}"
if [ "$WHAT" = "tutorial" ] || [ "$WHAT" = "all" ]; then
  dotnet "$R" --generate Tools/LevelSpecs/tutorial_v2.json "$D/levels_tutorial_v2.json"
  dotnet "$R" --export-validation "$D/levels_tutorial_v2.json" "$D/validation_tutorial_v2.json"
fi
if [ "$WHAT" = "world1" ] || [ "$WHAT" = "all" ]; then
  dotnet "$R" --generate Tools/LevelSpecs/world1_v2.json "$D/levels.json"
  dotnet "$R" --export-validation "$D/levels.json" "$D/validation_v2.json"
fi
echo "Now run Tools/run_core_tests.sh and update the measured sets in PackagedPackTests/TutorialPackTests if they changed."
