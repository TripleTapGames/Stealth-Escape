#!/bin/sh
# Validate, solve and print metrics for a level file (schema 1 or 2). Usage: Tools/level_metrics.sh <levels.json> [--trace]
set -e
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"
dotnet build Tools/CoreTestRunner/CoreTestRunner.csproj -nologo -v q -clp:NoSummary >/dev/null
MODE="--metrics"; [ "$2" = "--trace" ] && MODE="--solve"
dotnet /tmp/shadowsteps_coretests/bin/CoreTestRunner.dll "$MODE" "$1"
