#!/bin/sh
# Builds Core + Data + EditMode tests with dotnet (no Unity needed) and runs them.
# Usage: Tools/run_core_tests.sh [name-filter]
set -e
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"
dotnet build Tools/CoreTestRunner/CoreTestRunner.csproj -nologo -v q -clp:NoSummary
dotnet /tmp/shadowsteps_coretests/bin/CoreTestRunner.dll "$@"
