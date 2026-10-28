#!/bin/sh
# Runs the Unity Test Runner headless (EditMode + PlayMode). The Unity Editor GUI must NOT have the project open.
# Usage: Tools/run_unity_tests.sh [EditMode|PlayMode|All]
UNITY="/Applications/Unity/Hub/Editor/2022.3.62f3/Unity.app/Contents/MacOS/Unity"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
MODE="${1:-All}"
if [ -f "$ROOT/Temp/UnityLockfile" ]; then echo "Project is open in the Unity Editor (Temp/UnityLockfile). Close it first."; exit 2; fi
mkdir -p "$ROOT/Docs/Reports"
run() {
  "$UNITY" -batchmode -nographics -projectPath "$ROOT" -runTests -testPlatform "$1" -testResults "$ROOT/Docs/Reports/tests_$1.xml" -logFile "$ROOT/Docs/Reports/unity_$1.log"
  code=$?
  python3 -I - "$ROOT/Docs/Reports/tests_$1.xml" <<'PY'
import sys, xml.etree.ElementTree as ET
try:
    r = ET.parse(sys.argv[1]).getroot()
    print("%s: total=%s passed=%s failed=%s skipped=%s" % (sys.argv[1].split('_')[-1][:-4], r.get('total'), r.get('passed'), r.get('failed'), r.get('skipped')))
    for tc in r.iter('test-case'):
        if tc.get('result') != 'Passed':
            print("  ", tc.get('result'), tc.get('fullname'))
            m = tc.find('failure/message')
            if m is not None and m.text: print("     ", m.text.strip().splitlines()[0][:200])
except Exception as e:
    print("could not parse results:", e)
PY
  return $code
}
status=0
if [ "$MODE" = "EditMode" ] || [ "$MODE" = "All" ]; then run EditMode || status=1; fi
if [ "$MODE" = "PlayMode" ] || [ "$MODE" = "All" ]; then run PlayMode || status=1; fi
exit $status
