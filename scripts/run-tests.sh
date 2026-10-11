#!/usr/bin/env bash
# Runs Throughput's EditMode tests headless through the Unity CLI and prints a short summary.
# The Unity editor must be CLOSED (batch mode locks the project). If it's open, use the Test Runner window.
# Usage: scripts/run-tests.sh [filter] [--strict]
#   filter    regex on the full test name, e.g. "Throughput.Core.Tests.Randomness"
#   --strict  run only [Category("Strict")] tests (default: everything except Strict)
set -u
cd "$(dirname "$0")/.."

UNITY="${UNITY_CLI:-$LOCALAPPDATA/Unity/bin/unity.exe}"
FILTER=""
CATEGORY='!Strict'
for arg in "$@"; do
  case "$arg" in
    --strict) CATEGORY='Strict' ;;
    *) FILTER="$arg" ;;
  esac
done

OUT="Unity/Logs/test-results.xml"  # not Temp/: Unity wipes Temp on exit
ARGS=(test Unity --mode EditMode --output "$OUT" --no-banner)
if [ -n "$FILTER" ]; then ARGS+=(--filter "$FILTER"); fi

rm -f "$OUT"  # never report a previous run's results
UNITY_NO_CONSENT_PROMPT=1 "$UNITY" "${ARGS[@]}" -- -testCategory "$CATEGORY"
code=$?

if [ -f "$OUT" ]; then
  PYTHONIOENCODING=utf-8 python - "$OUT" <<'PY'
import sys, xml.etree.ElementTree as ET
root = ET.parse(sys.argv[1]).getroot()
for case in root.iter('test-case'):
    if case.get('result') != 'Passed':
        print(f"{case.get('result').upper()}: {case.get('fullname')}")
        msg = case.find('./failure/message')
        if msg is None:
            msg = case.find('./reason/message')
        if msg is not None and msg.text:
            print("   " + msg.text.strip().replace("\n", "\n   "))
for case in root.iter("test-case"):
    out = case.find("./output")
    if out is not None and out.text and out.text.strip():
        print("   " + out.text.strip())
print(f"total {root.get('total')}, passed {root.get('passed')}, failed {root.get('failed')}, "
      f"inconclusive {root.get('inconclusive')}, skipped {root.get('skipped')}")
PY
fi
exit $code
