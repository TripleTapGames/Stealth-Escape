#!/bin/sh
# Draws, per level, the nodes the player can ever stand on under the level's detection rule (exhaustive state search).
# Usage: Tools/level_reach.sh <levels.json>
set -e
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"
dotnet build Tools/CoreTestRunner/CoreTestRunner.csproj -nologo -v q -clp:NoSummary >/dev/null
dotnet /tmp/shadowsteps_coretests/bin/CoreTestRunner.dll --reach "$1"
