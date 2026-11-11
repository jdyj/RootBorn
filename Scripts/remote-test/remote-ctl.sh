#!/usr/bin/env bash
# 원격 실행 관리. 이 PC의 run.sh가 ssh로 부른다(늘 이 PC의 스크립트가 돈다).
#   remote-ctl.sh <작업 폴더> start <실행 id> <커밋> <받는 쪽> <받는 쪽 이름> <force 0|1> [dotnet test 인자...]
#       runs/<실행 id>/bin/에 이미 풀린 remote-run.sh를 연결에서 떼어 띄운다. 먼저 오래된 결과를 지우고,
#       같은 커밋·같은 인자의 실행이 있으면(force 0) 새로 띄우지 않고 "ATTACH <그 실행 id>"를 낸다.
#   remote-ctl.sh <작업 폴더> state <실행 id>          한 실행의 상태(key=value 줄)
#   remote-ctl.sh <작업 폴더> release <실행 id> <받는 쪽>  받아 간 뒤: 받는 쪽 표시를 지우고, 남은 쪽이 없으면 실행 폴더를 지운다
#   remote-ctl.sh <작업 폴더> claim <실행 id> <받는 쪽>    이어 받기 전에 받는 쪽 표시를 단다
#   remote-ctl.sh <작업 폴더> status                    도는 실행과 아직 안 받아 간 실행 목록, 자리
#   remote-ctl.sh <작업 폴더> cancel <실행 id>           그 실행의 프로세스(표시가 붙은 것)만 끝낸다
#
# 원격 폴더 모양(runs/<id>/{src,results,home})은 옛 스크립트와 같다. 새 스크립트가 더하는 것:
#   runs/<id>/pid     주 프로세스의 pid와 시작 시각     runs/<id>/meta    커밋, 인자, 같은 실행 찾기 열쇠, 시작 시각
#   runs/<id>/bin/    이 실행이 쓰는 스크립트 사본     runs/<id>/claims/ 결과를 받아 갈 쪽(이 PC의 작업 폴더)마다 파일 하나
#   results/exit-code 끝난 실행의 종료 코드             locks/slot-N.owner 자리 주인(실행 id, pid, 시작 시각)
# 옛 스크립트로 시작한 실행(pid 파일이 없다)은 상태를 "옛 형식"으로만 보이고 건드리지 않는다.
set -uo pipefail

DIR_NAME="${1:?작업 폴더}"; CMD="${2:?명령}"; shift 2
case "$DIR_NAME" in /*|*..*) echo "작업 폴더는 홈 아래 상대 경로여야 한다" >&2; exit 2 ;; esac
BASE="$HOME/$DIR_NAME"
[ -f "$BASE/env.sh" ] || { echo "원격 준비가 안 됐다: $BASE/env.sh 없음. 'bash Scripts/remote-test/run.sh --setup'을 먼저 돌린다." >&2; exit 2; }
# shellcheck disable=SC1091
. "$BASE/env.sh"
TTL_H="${ROOTBORN_TEST_RESULT_TTL_HOURS:-24}"
MAX_RUNS="${ROOTBORN_TEST_MAX_RUNS:-2}"

check_id() { case "$1" in *[!A-Za-z0-9._-]*|"") echo "쓸 수 없는 이름: $1" >&2; exit 2 ;; esac; }
proc_start() { sed 's/.*) //' "/proc/$1/stat" 2>/dev/null | awk '{print $20}'; }

# 실행 하나의 상태: running(도는 중) / done(끝남) / dead(주 프로세스가 정리 없이 죽음) / legacy(옛 형식, 모름) / missing
run_state() {
  local run="$BASE/runs/$1" pid st
  [ -d "$run" ] || { echo missing; return; }
  [ -f "$run/results/.done" ] && { echo done; return; }
  [ -f "$run/pid" ] || { echo legacy; return; }
  read -r pid st < "$run/pid"
  if [ -n "$pid" ] && [ "$(proc_start "$pid")" = "$st" ]; then echo running; else echo dead; fi
}

# 실행 id의 표시가 붙은 내 프로세스. 그 실행의 것만 고른다(다른 실행과 다른 일은 건드리지 않는다).
marked_pids() {
  local d
  for d in /proc/[0-9]*; do
    [ "${d#/proc/}" = "$$" ] && continue
    [ -O "$d" ] || continue
    { tr '\0' '\n' < "$d/environ"; } 2>/dev/null | grep -qx "ROOTBORN_TEST_RUN_ID=$1" && echo "${d#/proc/}"
  done
}

# 끝난 프로젝트별 건수(지금까지 TRX가 나온 것). "이름 통과/전체" 를 공백으로 잇는다.
progress() {
  local trx="$BASE/runs/$1/results/trx"
  [ -d "$trx" ] || return 0
  python3 - "$trx" <<'PY'
import glob, os, re, sys, xml.etree.ElementTree as ET
ns = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
out = []
for path in sorted(glob.glob(os.path.join(sys.argv[1], "*.trx"))):
    try:
        root = ET.parse(path).getroot()
    except Exception:
        continue  # 아직 쓰는 중
    m = root.find(".//t:UnitTest/t:TestMethod", ns)
    name = re.sub(r"\.dll$", "", os.path.basename(m.get("codeBase")) if m is not None else os.path.basename(path), flags=re.I)
    c = root.find(".//t:ResultSummary/t:Counters", ns)
    if c is None:
        continue
    total, passed, failed = (int(c.get(k, 0)) for k in ("total", "passed", "failed"))
    out.append("%s %d/%d%s" % (name.replace("Rootborn.", "").replace(".Tests", ""), passed, total, " 실패%d" % failed if failed else ""))
print(", ".join(out))
PY
}

meta_get() { sed -n "s/^$2=//p" "$BASE/runs/$1/meta" 2>/dev/null | head -1; }

# 정리가 안 된 채 죽은 실행의 뒷정리(체크아웃, ref, 표시). 프로세스가 남아 있으면 하지 않는다.
finalize_dead() {
  local id="$1" run="$BASE/runs/$1"
  [ -n "$(marked_pids "$id")" ] && return 1
  if [ -d "$run/src" ]; then git -C "$BASE/repo.git" worktree remove --force "$run/src" 2>/dev/null || rm -rf "$run/src"; fi
  git -C "$BASE/repo.git" worktree prune 2>/dev/null
  git -C "$BASE/repo.git" update-ref -d "refs/runs/$id" 2>/dev/null
  mkdir -p "$run/results"
  [ -f "$run/results/exit-code" ] || echo 137 > "$run/results/exit-code"
  : > "$run/results/.done"
}

# 끝난 지 TTL이 지난 결과(아무도 받아 가지 않은 것)를 지운다. 도는 실행과 옛 형식의 끝나지 않은 실행은 건드리지 않는다.
purge_old() {
  local run id st age now
  now=$(date +%s)
  for run in "$BASE"/runs/*/; do
    [ -d "$run" ] || continue
    id=$(basename "$run")
    st=$(run_state "$id")
    case "$st" in
      done) age=$(( (now - $(stat -c %Y "$run/results/.done")) / 3600 )) ;;
      dead) age=$(( (now - $(stat -c %Y "$run/pid")) / 3600 )) ;;
      *) continue ;;
    esac
    [ "$age" -ge "$TTL_H" ] || continue
    if [ "$st" = dead ]; then finalize_dead "$id" || continue; fi
    rm -rf "$run"
    echo "오래된 결과를 지웠다: $id (끝난 지 ${age}시간, 한도 ${TTL_H}시간)"
  done
}

case "$CMD" in
start)
  RUN_ID="$1"; SHA="$2"; CLIENT="$3"; CLIENT_NAME="$4"; FORCE="$5"; shift 5
  check_id "$RUN_ID"; check_id "$CLIENT"
  case "$SHA" in *[!0-9a-f]*|"") echo "커밋 해시가 아니다: $SHA" >&2; exit 2 ;; esac
  RUN="$BASE/runs/$RUN_ID"
  [ -f "$RUN/bin/remote-run.sh" ] || { echo "스크립트가 풀리지 않았다: $RUN/bin" >&2; exit 2; }
  purge_old
  ARGS="$(printf '%q ' "$@")"
  KEY="$SHA $(printf '%s' "$ARGS" | sha1sum | cut -c1-16)"
  if [ "$FORCE" != 1 ]; then
    for other in "$BASE"/runs/*/; do
      oid=$(basename "$other"); [ "$oid" = "$RUN_ID" ] && continue
      [ "$(meta_get "$oid" key)" = "$KEY" ] || continue
      ost=$(run_state "$oid")
      if [ "$ost" = done ]; then
        # 메모리가 모자라 거절된 실행과 취소된 실행에는 붙지 않는다.
        [ -f "$other/results/refused.txt" ] && continue
        [ -f "$other/results/cancelled" ] && continue
      elif [ "$ost" != running ]; then continue; fi
      mkdir -p "$other/claims" && echo "$CLIENT_NAME" > "$other/claims/$CLIENT"
      rm -rf "$RUN"
      git -C "$BASE/repo.git" update-ref -d "refs/runs/$RUN_ID" 2>/dev/null
      echo "ATTACH $oid $ost"
      exit 0
    done
  fi
  mkdir -p "$RUN/results" "$RUN/claims"
  echo "$CLIENT_NAME" > "$RUN/claims/$CLIENT"
  {
    echo "commit=$SHA"
    echo "args=$ARGS"
    echo "key=$KEY"
    echo "started=$(date +%s)"
    echo "client=$CLIENT_NAME"
  } > "$RUN/meta"
  # 연결에서 뗀다: 새 세션(setsid), HUP 무시(nohup), 표준 입출력을 연결과 끊는다. 표시 변수는 주 프로세스부터 붙인다.
  ROOTBORN_TEST_RUN_ID="$RUN_ID" setsid nohup bash "$RUN/bin/remote-run.sh" "$DIR_NAME" "$RUN_ID" "$SHA" "$@" \
    > "$RUN/results/remote.log" 2>&1 < /dev/null &
  for _ in $(seq 1 50); do [ -f "$RUN/pid" ] && break; sleep 0.2; done
  [ -f "$RUN/pid" ] || { echo "원격 실행이 뜨지 않았다:" >&2; cat "$RUN/results/remote.log" >&2; exit 5; }
  echo "STARTED $RUN_ID"
  ;;
state)
  ID="$1"; check_id "$ID"
  st=$(run_state "$ID")
  echo "state=$st"
  [ "$st" = missing ] && exit 0
  started=$(meta_get "$ID" started)
  [ -n "$started" ] && echo "elapsed=$(( $(date +%s) - started ))"
  [ -f "$BASE/runs/$ID/results/exit-code" ] && echo "exit=$(cat "$BASE/runs/$ID/results/exit-code")"
  echo "progress=$(progress "$ID")"
  echo "last=$(grep -v '^$' "$BASE/runs/$ID/results/remote.log" 2>/dev/null | tail -1)"
  ;;
claim)
  ID="$1"; check_id "$ID"; check_id "$2"
  [ -d "$BASE/runs/$ID" ] && mkdir -p "$BASE/runs/$ID/claims" && echo "${3:-}" > "$BASE/runs/$ID/claims/$2"
  ;;
release)
  ID="$1"; CLIENT="$2"; check_id "$ID"; check_id "$CLIENT"
  RUN="$BASE/runs/$ID"
  [ -d "$RUN" ] || exit 0
  [ "$(run_state "$ID")" = done ] || { echo "아직 끝나지 않아 지우지 않는다: $ID" >&2; exit 1; }
  rm -f "$RUN/claims/$CLIENT"
  if [ -d "$RUN/claims" ] && [ -n "$(ls -A "$RUN/claims")" ]; then
    echo "다른 쪽도 이 결과를 기다리고 있어 원격 폴더는 남긴다: $(cat "$RUN"/claims/* | tr '\n' ' ')"
  else
    rm -rf "$RUN"
  fi
  ;;
status)
  printf '%-40s %-9s %-11s %-12s %s\n' "실행 id" "커밋" "시작" "상태" "끝난 프로젝트(통과/전체)"
  n=0
  for run in "$BASE"/runs/*/; do
    [ -d "$run" ] || continue
    id=$(basename "$run"); st=$(run_state "$id"); n=$((n + 1))
    sha=$(meta_get "$id" commit); [ -n "$sha" ] || sha=$(echo "$id" | cut -d- -f3)
    started=$(meta_get "$id" started)
    if [ -n "$started" ]; then when=$(date -d "@$started" +%m-%d\ %H:%M); else when=$(stat -c %y "$run" | cut -c6-16); fi
    case "$st" in
      running) label="도는 중" ;;
      done) label="끝남($(cat "$run/results/exit-code" 2>/dev/null || echo 옛))"
            [ -f "$run/results/cancelled" ] && label="취소됨"
            [ -f "$run/results/refused.txt" ] && label="거절됨" ;;
      dead) label="죽음(정리 안 됨)" ;;
      legacy) label="옛 형식" ;;
    esac
    printf '%-40s %-9s %-11s %-12s %s\n' "$id" "${sha:0:9}" "$when" "$label" "$(progress "$id")"
    args=$(meta_get "$id" args); [ -n "$args" ] && echo "    인자: $args"
    [ -d "$run/claims" ] && [ -n "$(ls -A "$run/claims" 2>/dev/null)" ] && echo "    받을 쪽: $(cat "$run"/claims/* | tr '\n' ' ')"
  done
  [ "$n" = 0 ] && echo "(원격에 남은 실행이 없다)"
  echo
  echo "자리(동시 실행 $MAX_RUNS개), 결과 보관 ${TTL_H}시간"
  for s in $(seq 1 "$MAX_RUNS"); do
    lock="$BASE/locks/slot-$s"
    if [ ! -e "$lock" ] || flock -n "$lock" true 2>/dev/null; then echo "  자리 $s: 빔"; continue; fi
    if [ -f "$lock.owner" ]; then
      read -r o_id o_pid o_start < "$lock.owner"
      if [ "$(proc_start "$o_pid")" = "$o_start" ]; then echo "  자리 $s: $o_id"
      else echo "  자리 $s: 막힘 — 주인 $o_id는 끝났는데 남은 프로세스가 쥐고 있다('run.sh --cancel $o_id')"; fi
    else
      echo "  자리 $s: 옛 형식 실행이 쓰는 중"
    fi
  done
  ;;
cancel)
  ID="$1"; check_id "$ID"
  RUN="$BASE/runs/$ID"
  [ -d "$RUN" ] || { echo "그런 실행이 없다: $ID" >&2; exit 1; }
  st=$(run_state "$ID")
  case "$st" in
    done) echo "이미 끝난 실행이다: $ID"; exit 0 ;;
    legacy) echo "옛 스크립트로 시작한 실행이라 끝내지 않는다(주인을 확인할 수 없다): $ID" >&2; exit 1 ;;
  esac
  : > "$RUN/cancelled.pending"
  if [ "$st" = running ]; then
    read -r pid _ < "$RUN/pid"
    kill -TERM "$pid" 2>/dev/null   # 주 프로세스의 정리가 표시 붙은 프로세스와 가상 화면을 끝내고 .done을 남긴다
    for _ in $(seq 1 60); do [ -f "$RUN/results/.done" ] && break; sleep 0.5; done
  fi
  if [ ! -f "$RUN/results/.done" ]; then
    # 주 프로세스가 없거나 정리하지 못했다: 이 실행의 표시가 붙은 프로세스만 끝내고 뒷정리한다.
    for p in $(marked_pids "$ID"); do kill -TERM "$p" 2>/dev/null; done
    sleep 2
    for p in $(marked_pids "$ID"); do kill -KILL "$p" 2>/dev/null; done
    sleep 1
    finalize_dead "$ID" || { echo "프로세스가 남아 정리하지 못했다: $(marked_pids "$ID" | tr '\n' ' ')" >&2; exit 1; }
  fi
  mv "$RUN/cancelled.pending" "$RUN/results/cancelled"
  echo "취소했다: $ID"
  ;;
*) echo "모르는 명령: $CMD" >&2; exit 2 ;;
esac
