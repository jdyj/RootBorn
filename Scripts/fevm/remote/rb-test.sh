#!/bin/bash
# Usage: rb-test.sh <project-dir> <EditMode|PlayMode> [filter] [asset|built|rebuild]
# rebuild = build Addressables content first, then read the built bundles.
# Both platforms run on Xvfb + VirtualGL GPU. -nographics caps textures at 8192px, so a first import without a GPU
# fails large sheets (e.g. 1024x8288) and caches the failure in Library. RB_NOGRAPHICS=1 forces -nographics for EditMode.
# Unity can hang on exit in the MCP plugin after saving results; a watchdog stops it 90 s after the run completes.
# Results: <project-dir>/rb-results/<platform>-<time>.{xml,log}; prints a summary.
set -u
. ~/farmer-unity-verify/tools/rb-env.sh
P=$(cd "$1" && pwd -P); PLAT=$2; FILTER=${3:-}; ADDR=${4:-asset}
if rb_project_busy "$P"; then echo "Unity already running on $P"; exit 1; fi
O=$P/rb-results; mkdir -p "$O"; T=$(date +%Y%m%d-%H%M%S)
X=$O/$PLAT-$T.xml; L=$O/$PLAT-$T.log
ARGS=(-batchmode -projectPath "$P" -buildTarget StandaloneWindows64 -runTests -testPlatform "$PLAT" -testResults "$X" -logFile "$L")
[ -n "$FILTER" ] && ARGS+=(-testFilter "$FILTER")
if [ "$ADDR" = rebuild ]; then
  rb_xvfb || exit 1
  rb_unity_gpu -batchmode -quit -projectPath "$P" -buildTarget StandaloneWindows64     -executeMethod Rootborn.Editor.BuildScripts.BuildScript.BuildAddressablesContent -logFile "$O/aa-build-$T.log"     || { echo "Addressables build failed: $O/aa-build-$T.log"; exit 2; }
  ADDR=built
fi
export ROOTBORN_AA_PLAYMODE=$ADDR
free -g | sed -n 2p
S=$(date +%s)
if [ "$PLAT" = EditMode ] && [ "${RB_NOGRAPHICS:-0}" = 1 ]; then "$RB_UNITY" -nographics "${ARGS[@]}" & UP=$!
else rb_xvfb || exit 1; rb_unity_gpu "${ARGS[@]}" & UP=$!; fi
( while kill -0 $UP 2>/dev/null; do
    if grep -q "Test run completed" "$L" 2>/dev/null; then
      sleep 90; if rb_project_busy "$P"; then echo "watchdog: Unity still running 90 s after results; stopping it"; pkill -TERM -f "[E]ditor/Unity.*-projectPath $P( |$)"; sleep 15; pkill -KILL -f "[E]ditor/Unity.*-projectPath $P( |$)"; fi
      break
    fi; sleep 10; done ) & WD=$!
wait $UP; RC=$?; kill $WD 2>/dev/null
grep -q "Test run completed. Exiting with code 0" "$L" 2>/dev/null && RC=0
grep -q "Test run completed. Exiting with code 2" "$L" 2>/dev/null && RC=3
echo "exit=$RC addressables=$ADDR seconds=$(( $(date +%s) - S )) xml=$X log=$L"
grep -m1 -E "Renderer:|GfxDevice: creating device client" "$L" 2>/dev/null
[ -f "$X" ] && python3 - "$X" <<'PY'
import sys, xml.etree.ElementTree as E
r = E.parse(sys.argv[1]).getroot()
print("total=%s passed=%s failed=%s skipped=%s" % (r.get("total"), r.get("passed"), r.get("failed"), r.get("skipped")))
for t in r.iter("test-case"):
    if t.get("result") == "Failed": print("FAIL", t.get("fullname"))
PY
grep -E "error CS[0-9]+" "$L" 2>/dev/null | sort -u | head -20
echo "files changed by the run (git status, results excluded):"
git -C "$P" status --short -- . ":!rb-results" ":!*.out" | head -20
exit $RC
