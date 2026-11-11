#!/usr/bin/env bash
# 원격에서 한 번의 테스트 실행. 이 PC의 run.sh가 ssh 표준 입력으로 넘긴다(스크립트는 늘 이 PC의 것이 돈다).
# 인자: <작업 폴더 이름> <실행 id> <커밋 해시> [dotnet test 인자...]
# 결과는 <작업 폴더>/runs/<실행 id>/results 에 남고, 가져가는 것과 지우는 것은 run.sh가 한다.
# 이 실행이 띄운 가상 화면과 프로세스는 끝날 때(실패, 끊김 포함) 모두 끝낸다.
set -uo pipefail

DIR_NAME="$1"; RUN_ID="$2"; SHA="$3"; shift 3
case "$DIR_NAME" in /*|*..*) echo "작업 폴더는 홈 아래 상대 경로여야 한다" >&2; exit 2 ;; esac
case "$RUN_ID" in *[!A-Za-z0-9._-]*|"") echo "실행 id에 쓸 수 없는 글자: $RUN_ID" >&2; exit 2 ;; esac
case "$SHA" in *[!0-9a-f]*|"") echo "커밋 해시가 아니다: $SHA" >&2; exit 2 ;; esac

BASE="$HOME/$DIR_NAME"
[ -f "$BASE/env.sh" ] || { echo "원격 준비가 안 됐다: $BASE/env.sh 없음. 'bash Scripts/remote-test/run.sh --setup'을 먼저 돌린다." >&2; exit 2; }
# shellcheck disable=SC1091
. "$BASE/env.sh"
for v in ROOTBORN_TEST_DOTNET ROOTBORN_TEST_XVFB ROOTBORN_TEST_XKBDIR ROOTBORN_FONT; do
  [ -n "${!v:-}" ] || { echo "$BASE/env.sh에 $v가 없다" >&2; exit 2; }
done
MAX_RUNS="${ROOTBORN_TEST_MAX_RUNS:-2}"
PARALLEL="${ROOTBORN_TEST_PARALLEL:-2}"
MIN_GB="${ROOTBORN_TEST_MIN_AVAILABLE_GB:-8}"

RUN="$BASE/runs/$RUN_ID"
SRC="$RUN/src"
RES="$RUN/results"
mkdir -p "$RES"
log() { echo "[원격 $(date +%H:%M:%S)] $*"; }

XVFB_PID=""; SAMPLER_PID=""; WATCH_PID=""
cleanup() {
  [ -n "$WATCH_PID" ] && kill "$WATCH_PID" 2>/dev/null
  [ -n "$SAMPLER_PID" ] && kill "$SAMPLER_PID" 2>/dev/null
  # 이 실행이 띄운 프로세스(환경 변수 표시가 붙은 것)를 모두 끝낸다. 다른 실행이나 다른 일은 건드리지 않는다.
  for p in $(own_pids); do kill "$p" 2>/dev/null; done
  sleep 1
  for p in $(own_pids); do kill -9 "$p" 2>/dev/null; done
  [ -n "$XVFB_PID" ] && kill "$XVFB_PID" 2>/dev/null && wait "$XVFB_PID" 2>/dev/null
  if [ -d "$SRC" ]; then git -C "$BASE/repo.git" worktree remove --force "$SRC" 2>/dev/null || rm -rf "$SRC"; fi
  git -C "$BASE/repo.git" worktree prune 2>/dev/null
  git -C "$BASE/repo.git" update-ref -d "refs/runs/$RUN_ID" 2>/dev/null
  # run.sh는 이 표시가 있어야 결과를 가져가고 실행 폴더를 지운다(연결만 끊긴 채 원격이 돌고 있을 때 지우지 않게).
  [ -d "$RES" ] && : > "$RES/.done"
}
own_pids() {
  local d
  for d in /proc/[0-9]*; do
    [ "${d#/proc/}" = "$$" ] && continue
    [ -O "$d" ] || continue   # 내 프로세스만 본다
    { tr '\0' '\n' < "$d/environ"; } 2>/dev/null | grep -qx "ROOTBORN_TEST_RUN_ID=$RUN_ID" && echo "${d#/proc/}"
  done
}
trap cleanup EXIT
trap 'exit 130' INT TERM HUP

# ---- 연결 감시: pty 없는 ssh는 이 PC 쪽이 끊겨도 원격에 HUP을 보내지 않는다(2026-11-12 실제로 남았다).
# 1분마다 진행 줄을 써 보고(끊겼으면 쓰기가 실패한다), 부모(ssh 세션)가 바뀌었는지 본다. 끊겼으면 이 실행을 끝낸다.
MAIN_PID=$$; MAIN_PARENT=$PPID
(
  trap '' PIPE
  elapsed=0
  while kill -0 "$MAIN_PID" 2>/dev/null; do
    sleep 15; elapsed=$((elapsed + 15))
    parent=$(awk '{print $4}' "/proc/$MAIN_PID/stat" 2>/dev/null)
    lost=""
    [ -n "$parent" ] && [ "$parent" != "$MAIN_PARENT" ] && lost=1
    if [ $((elapsed % 60)) = 0 ] && ! echo "[원격 $(date +%H:%M:%S)] 진행 중 ${elapsed}초" 2>/dev/null; then lost=1; fi
    if [ -n "$lost" ]; then kill -TERM "$MAIN_PID" 2>/dev/null; exit 0; fi
  done
) &
WATCH_PID=$!

# ---- 동시 실행 수 제한: locks/slot-N 중 빈 것을 잡는다(이 셸이 끝나면 풀린다). ----
exec 9>/dev/null
got=""
for wait_round in $(seq 1 180); do
  for n in $(seq 1 "$MAX_RUNS"); do
    exec 9>"$BASE/locks/slot-$n"
    if flock -n 9; then got="$n"; break 2; fi
  done
  [ "$wait_round" = 1 ] && log "다른 실행 $MAX_RUNS개가 돌고 있어 기다린다"
  sleep 10
done
[ -n "$got" ] || { echo "30분 기다려도 빈 자리가 없다" >&2; exit 4; }
log "자리 $got"

# ---- 가용 메모리 ----
avail_kb=$(awk '/^MemAvailable:/ {print $2}' /proc/meminfo)
avail_gb=$((avail_kb / 1048576))
log "가용 메모리 ${avail_gb}GB (필요 ${MIN_GB}GB)"
if [ "$avail_gb" -lt "$MIN_GB" ]; then
  echo "가용 메모리가 ${avail_gb}GB로 ${MIN_GB}GB보다 적어 돌리지 않는다(다른 일이 쓰고 있다)." | tee "$RES/refused.txt" >&2
  exit 3
fi

# ---- 커밋 체크아웃 ----
git -C "$BASE/repo.git" worktree add --quiet --detach "$SRC" "$SHA" || { echo "체크아웃 실패: $SHA" >&2; exit 5; }
log "체크아웃 $(git -C "$SRC" log --oneline -1)"

# ---- 가상 화면: 범위 안에서 빈 번호를 고른다. 이미 떠 있는 번호는 Xvfb가 스스로 거절한다. ----
export LD_LIBRARY_PATH="${ROOTBORN_TEST_XLIB:+$ROOTBORN_TEST_XLIB:}${LD_LIBRARY_PATH:-}"
DISPLAY_NUM=""
for d in $(seq "${ROOTBORN_TEST_DISPLAY_FIRST:-140}" "${ROOTBORN_TEST_DISPLAY_LAST:-199}"); do
  [ -e "/tmp/.X$d-lock" ] || [ -e "/tmp/.X11-unix/X$d" ] && continue
  ROOTBORN_TEST_RUN_ID="$RUN_ID" "$ROOTBORN_TEST_XVFB" ":$d" -screen 0 1920x1080x24 -nolisten tcp -noreset \
    -xkbdir "$ROOTBORN_TEST_XKBDIR" > "$RES/xvfb.log" 2>&1 &
  XVFB_PID=$!
  for _ in $(seq 1 50); do
    [ -e "/tmp/.X11-unix/X$d" ] && break
    kill -0 "$XVFB_PID" 2>/dev/null || break
    sleep 0.1
  done
  if kill -0 "$XVFB_PID" 2>/dev/null && [ -e "/tmp/.X11-unix/X$d" ]; then DISPLAY_NUM="$d"; break; fi
  XVFB_PID=""
done
[ -n "$DISPLAY_NUM" ] || { echo "빈 가상 화면 번호를 찾지 못했다" >&2; cat "$RES/xvfb.log" >&2; exit 6; }
log "가상 화면 :$DISPLAY_NUM"

# ---- 테스트 환경 ----
export ROOTBORN_TEST_RUN_ID="$RUN_ID"
export DISPLAY=":$DISPLAY_NUM"
export LIBGL_ALWAYS_SOFTWARE=1          # GPU를 쓰지 않는다(그림 학습과 데스크톱이 iGPU를 쓴다)
export SDL_AUDIODRIVER=dummy
export DOTNET_ROOT="$(dirname "$ROOTBORN_TEST_DOTNET")"
export PATH="$DOTNET_ROOT:$PATH"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export DOTNET_CLI_HOME="$RUN/home"      # 실행끼리 섞이지 않게
export NUGET_PACKAGES="$BASE/nuget"
export MSBUILDDISABLENODEREUSE=1 DOTNET_CLI_USE_MSBUILD_SERVER=0 UseSharedCompilation=false
mkdir -p "$DOTNET_CLI_HOME"

# ---- 메모리 표본: 1초마다 이 실행의 프로세스 RSS를 더하고, testhost 하나의 최고 값도 적는다. ----
(
  peak_total=0; peak_one=0
  while :; do
    total=0; one=0
    for p in $(own_pids); do
      rss=$(awk '/^VmRSS:/ {print $2}' "/proc/$p/status" 2>/dev/null); [ -n "$rss" ] || continue
      total=$((total + rss))
      # Linux의 testhost는 'dotnet exec .../testhost.dll'로 뜬다
      if { tr '\0' ' ' < "/proc/$p/cmdline"; } 2>/dev/null | grep -q 'testhost'; then
        [ "$rss" -gt "$one" ] && one=$rss
      fi
    done
    [ "$total" -gt "$peak_total" ] && peak_total=$total
    [ "$one" -gt "$peak_one" ] && peak_one=$one
    printf 'peak_total_mb=%d\npeak_testhost_mb=%d\n' $((peak_total / 1024)) $((peak_one / 1024)) > "$RES/memory.txt"
    sleep 1
  done
) &
SAMPLER_PID=$!

# ---- 테스트 ----
# 테스트는 배경에서 띄우고 wait로 기다린다: bash는 앞에서 도는 자식이 끝날 때까지 TERM 처리(정리)를 미룬다.
cd "$SRC" || exit 5
TRX="$RES/trx"
common=(--logger trx --results-directory "$TRX" --disable-build-servers "-maxcpucount:$PARALLEL")
start=$(date +%s)
status=0
if [ $# -eq 0 ]; then
  log "dotnet test Tools/Rootborn.Core.Tests"
  nice -n 10 dotnet test Tools/Rootborn.Core.Tests "${common[@]}" > "$RES/core.log" 2>&1 &
  wait $! || status=1
  log "dotnet test game/Rootborn.sln (동시 $PARALLEL)"
  nice -n 10 dotnet test game/Rootborn.sln "${common[@]}" > "$RES/game.log" 2>&1 &
  wait $! || status=1
else
  case "$1" in -*) set -- game/Rootborn.sln "$@" ;; esac
  log "dotnet test $*"
  nice -n 10 dotnet test "$@" "${common[@]}" > "$RES/test.log" 2>&1 &
  wait $! || status=1
fi
end=$(date +%s)
kill "$SAMPLER_PID" 2>/dev/null; wait "$SAMPLER_PID" 2>/dev/null; SAMPLER_PID=""

# ---- 결과 ----
{
  echo "commit=$SHA"
  echo "display=:$DISPLAY_NUM"
  echo "seconds=$((end - start))"
  cat "$RES/memory.txt" 2>/dev/null
} > "$RES/run.txt"
python3 - "$TRX" "$RES/summary.txt" <<'PY'
import glob, os, re, sys, xml.etree.ElementTree as ET
ns = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
rows, failures = [], []
for path in sorted(glob.glob(os.path.join(sys.argv[1], "*.trx"))):
    root = ET.parse(path).getroot()
    method = root.find(".//t:UnitTest/t:TestMethod", ns)
    project = os.path.basename(method.get("codeBase")) if method is not None else os.path.basename(path)
    project = re.sub(r"\.dll$", "", project, flags=re.I)
    c = root.find(".//t:ResultSummary/t:Counters", ns)
    total, passed, failed = (int(c.get(k, 0)) for k in ("total", "passed", "failed"))
    skipped = total - passed - failed
    rows.append((project, total, passed, failed, skipped))
    for r in root.iterfind(".//t:UnitTestResult", ns):
        if r.get("outcome") == "Failed":
            msg = r.find(".//t:Message", ns)
            first = (msg.text or "").strip().splitlines()[0] if msg is not None and msg.text else ""
            failures.append((project, r.get("testName"), first))
with open(sys.argv[2], "w", encoding="utf-8") as f:
    f.write("%-28s %6s %6s %6s %6s\n" % ("프로젝트", "전체", "통과", "실패", "건너뜀"))
    for row in rows: f.write("%-28s %6d %6d %6d %6d\n" % row)
    if not rows: f.write("(TRX 결과가 없다 — 빌드가 실패했는지 로그를 본다)\n")
    if failures:
        f.write("\n실패 %d건\n" % len(failures))
        for p, n, m in failures: f.write("- %s :: %s\n    %s\n" % (p, n, m))
PY
# 스크린샷(output/ 아래 PNG)만 가져갈 수 있게 옮긴다.
if [ -d "$SRC/output" ]; then
  ( cd "$SRC" && find output -name '*.png' -print0 | tar --null -cf - -T - ) | tar -xf - -C "$RES"
fi
log "끝 ($((end - start))초, 상태 $status)"
exit "$status"
