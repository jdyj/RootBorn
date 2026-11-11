#!/usr/bin/env bash
# 배포 폴더가 저장소 없이 도는지 확인한다. 자세한 것은 README.md "확인하는 법".
#   Tools/publish/verify.sh [플랫폼]                  (기본: 이 PC 의 플랫폼 — Windows 는 win-x64, Linux 는 linux-x64)
#   Tools/publish/verify.sh --contents-only <플랫폼>  1번(목록 대조)만 한다. 이 PC 에서 실행할 수 없는 플랫폼(예: Windows 에서 linux-x64)용
#   Tools/publish/verify.sh --server [--bind <IP>] [플랫폼]   데디케이티드 서버 배포 폴더(server-<플랫폼>)를 확인한다(아래 "서버")
#
# 하는 일
#   1. content.txt 가 가리키는 파일(publish.sh --list)이 배포 폴더에 다 있는지, Unity 전용 파일이 없는지 본다.
#   2. 배포 폴더를 저장소 밖 임시 폴더로 복사한다. 저장소를 가리키는 환경 변수(ROOTBORN_REPO_ROOT 등)를 지우고
#      --root 없이 실행한다 — 실행 파일 위치에서 루트를 찾으므로 복사본 밖의 게임 파일은 읽을 수 없다.
#   3. 화면마다 한 번씩 실행해 N 프레임 뒤 스크린샷을 남기고 끝내게 한다(--frames, --screenshot):
#        슬롯 선택(한국어·영어), 새 게임(--slot), 같은 슬롯 이어 하기, 슬롯이 있는 슬롯 선택, 견본 화면(--sample),
#        맵 목록(game/world/maps.json)의 맵마다(--world), 첫 맵을 영어로 한 번 더.
#      종료 코드 0, 스크린샷이 생겼고 PNG 이며 비어 있지 않은지, 표준 오류에 아무것도 없는지 본다.
#      (Host 는 읽기 문제를 "[월드] …", "[NPC 그림] …" 처럼 표준 오류에 쓰고, 멈추는 문제는 종료 코드 2 로 알린다.)
#   4. strace 가 있으면(Linux) 슬롯 선택 실행을 strace 로 감싸 실행 중 연 파일 가운데 복사본 밖의 것을 보인다.
#      Windows 에는 같은 수단이 없다 — Host 가 연 파일 목록을 내는 인자가 생기면 그것으로 바꾼다.
#      (Host 는 이 일에서 고치지 않는다. 대신 3번의 화면 범위로 "읽지 못한 파일"이 표준 오류에 나오는지 본다.)
#
# 서버(--server)
#   1·2 는 같다(content-server.txt 목록 대조, 저장소 밖 임시 폴더로 복사). 3 이 다르다:
#   3. 임시 세이브 폴더·슬롯, 빈 포트(--port 0)로 실행 → 로그에 "듣는 중:" → 데이터를 복사본의 data 에서 읽었는지 →
#      콘솔에 stop 을 넣어 끝내고 종료 코드 0 과 "저장하고 닫음" → 같은 세이브로 다시 켜서 "방을 이어서 엶" → 다시 stop.
#      인자 없이 켜면 종료 코드 2 와 도움말.
#      --root·ROOTBORN_REPO_ROOT·ROOTBORN_DATA_DIR 없이 돈다(실행 파일 위치에서 data/registry.json 을 찾는다). 표준 오류는 비어야 한다.
#   듣는 주소는 --bind(또는 VERIFY_BIND)로 받는다. 기본 127.0.0.1 은 이 시험용이다: 같은 기계 안에서만 닿고, 방화벽 창이 뜨지 않고,
#   밖에 열리지 않아 안전하다. 실제 서버를 여는 값(LAN 주소 등)을 이 스크립트에 적지 않는다.
#
# 스크린샷이 "제대로 그려졌는지"(그림·글자)는 이 스크립트가 판단하지 않는다. 남긴 스크린샷을 사람이 열어 본다.
#
# 환경 변수
#   PUBLISH_OUT     배포 폴더들이 있는 곳(publish.sh 와 같다, 기본 output/publish)
#   VERIFY_KEEP=1   임시 폴더를 지우지 않는다(스크린샷·로그를 보려면). 실패하면 늘 남긴다.
#   VERIFY_FRAMES   화면마다 그릴 프레임 수(기본 180)
#   VERIFY_TIMEOUT  실행 하나의 제한 시간(초, 기본 120)
#   VERIFY_BIND     서버가 들을 주소(기본 127.0.0.1, --server 에서만)
#   VERIFY_MAPS     띄울 맵 id(공백으로 나눔). 없으면 복사본의 game/world/maps.json 에서 읽는다
#   ROOTBORN_FONT   한글 글꼴(운영체제에서 못 찾는 곳에서만. 배포용 글꼴은 아직 정하지 않았다)
set -uo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$HERE/../.." && pwd)"
OUT_ROOT="${PUBLISH_OUT:-$REPO/output/publish}"
FRAMES="${VERIFY_FRAMES:-180}"
LIMIT="${VERIFY_TIMEOUT:-120}"

case "$(uname -s)" in
  MINGW*|MSYS*|CYGWIN*) default_rid=win-x64 ;;
  Linux) default_rid=linux-x64 ;;
  *) default_rid="" ;;
esac
CONTENTS_ONLY=0
SERVER=0
# 서버 시험용 듣는 주소: 같은 기계 안에서만 닿는 루프백이라 시험에 안전하다(밖에 열지 않는다).
BIND="${VERIFY_BIND:-127.0.0.1}"
while [ $# -gt 0 ]; do
  case "$1" in
    --contents-only) CONTENTS_ONLY=1; shift ;;
    --server) SERVER=1; shift ;;
    --bind) [ $# -ge 2 ] || { echo "--bind 에 값이 없다" >&2; exit 2; }; BIND="$2"; shift 2 ;;
    --*) echo "모르는 인자: $1" >&2; exit 2 ;;
    *) break ;;
  esac
done
RID="${1:-$default_rid}"
[ -n "$RID" ] || { echo "플랫폼을 준다: Tools/publish/verify.sh <win-x64|linux-x64>" >&2; exit 2; }
if [ $SERVER -eq 1 ]; then
  SRC="$OUT_ROOT/server-$RID"; PROJECT_NAME=Rootborn.Server; LIST_ARGS=(--server --list); PUBLISH_HINT="publish.sh --server $RID"
else
  SRC="$OUT_ROOT/$RID"; PROJECT_NAME=Rootborn.Host; LIST_ARGS=(--list); PUBLISH_HINT="publish.sh $RID"
fi
case "$RID" in win-*) EXE_NAME=$PROJECT_NAME.exe ;; *) EXE_NAME=$PROJECT_NAME ;; esac
[ -d "$SRC" ] || { echo "배포 폴더가 없다: $SRC (먼저 Tools/publish/$PUBLISH_HINT)" >&2; exit 2; }
[ -f "$SRC/$EXE_NAME" ] || { echo "실행 파일이 없다: $SRC/$EXE_NAME" >&2; exit 2; }

failures=()
fail() { failures+=("$1"); echo "  실패: $1"; }

# Windows 실행 파일에는 Windows 경로를 넘긴다.
native_path() { if command -v cygpath >/dev/null 2>&1; then cygpath -m "$1"; else printf '%s' "$1"; fi; }

# ---- 1. 목록대로 들어갔는가 ----
echo "== 1. 목록 대조 ($SRC)"
list_file="$(mktemp)"
if ! bash "$HERE/publish.sh" "${LIST_ARGS[@]}" > "$list_file" 2> "$list_file.err"; then
  cat "$list_file.err" >&2
  fail "publish.sh ${LIST_ARGS[*]} 가 실패했다"
fi
listed=0; missing=0
while IFS= read -r f; do
  [ -n "$f" ] || continue
  listed=$((listed + 1))
  if [ ! -f "$SRC/$f" ]; then
    missing=$((missing + 1))
    [ $missing -le 20 ] && echo "  배포 폴더에 없다: $f"
  fi
done < "$list_file"
echo "  목록 ${listed}개, 빠진 것 ${missing}개"
[ $missing -eq 0 ] || fail "목록 가운데 ${missing}개가 배포 폴더에 없다(publish.sh 를 다시 돌린다)"
if [ -s "$list_file.err" ]; then
  echo "  publish.sh ${LIST_ARGS[*]} 경고:"; sed 's/^/    /' "$list_file.err"
fi
rm -f "$list_file" "$list_file.err"
unity_only="$(find "$SRC" -type f \( -name '*.meta' -o -name '*.unity' -o -name '*.asset' -o -name '*.prefab' -o -name '*.cs' \) | head -5)"
[ -z "$unity_only" ] || fail "Unity 전용 파일이 들어 있다: $(echo "$unity_only" | tr '\n' ' ')"
if [ $SERVER -eq 1 ]; then
  # 서버는 그림·글꼴·테마·맵을 읽지 않는다. 들어 있으면 목록이 새고 있는 것이다.
  extra="$(find "$SRC" -type f \( -iname '*.png' -o -iname '*.ttf' -o -iname '*.otf' -o -iname '*.ldtk' -o -iname '*.ldtkl' \) | head -5)"
  [ -z "$extra" ] || fail "서버 폴더에 그림·글꼴·맵 파일이 들어 있다: $(echo "$extra" | tr '\n' ' ')"
fi

if [ $CONTENTS_ONLY -eq 1 ]; then
  echo
  if [ ${#failures[@]} -eq 0 ]; then echo "===== 통과(목록 대조만, 실행은 하지 않았다): 배포 폴더 $SRC"; exit 0; fi
  echo "===== 실패 ${#failures[@]}건"; printf '  %s\n' "${failures[@]}"; exit 1
fi

# ---- 2. 저장소 밖으로 복사 ----
WORK="$(mktemp -d)"
case "$(cd "$WORK" && pwd -P)/" in
  "$(cd "$REPO" && pwd -P)"/*) echo "임시 폴더가 저장소 안이다: $WORK (TMPDIR 를 바꾼다)" >&2; exit 2 ;;
esac
GAME="$WORK/game"
echo "== 2. 저장소 밖으로 복사: $GAME"
cp -r "$SRC" "$GAME"
mkdir -p "$WORK/save" "$WORK/shots" "$WORK/logs"
EXE="$GAME/$EXE_NAME"
SAVE="$(native_path "$WORK/save")"

TIMEOUT_CMD=()
if command -v timeout >/dev/null 2>&1; then TIMEOUT_CMD=(timeout "$LIMIT"); fi

# ---- 3a. 클라이언트: 화면마다 한 번 ----
# run_case <이름> <인자...> : 복사본을 실행하고 결과를 본다
run_case() {
  local name="$1"; shift
  local shot="$WORK/shots/$name.png" out="$WORK/logs/$name.out" err="$WORK/logs/$name.err" rc
  echo "-- $name: $EXE_NAME $*"
  (
    cd "$WORK" || exit 99
    env -u ROOTBORN_REPO_ROOT -u ROOTBORN_SAVE_ROOT -u ROOTBORN_UI_THEME -u ROOTBORN_WORLD_MAPS -u ROOTBORN_EVIDENCE_DIR \
      "${TIMEOUT_CMD[@]+"${TIMEOUT_CMD[@]}"}" "$EXE" "$@" --size 1280x720 --frames "$FRAMES" --screenshot "$(native_path "$shot")"
  ) > "$out" 2> "$err"
  rc=$?
  if [ $rc -ne 0 ]; then fail "$name: 종료 코드 $rc$([ $rc -eq 124 ] && echo " (제한 시간 ${LIMIT}초 넘김)")"; fi
  if [ ! -s "$shot" ]; then
    fail "$name: 스크린샷이 없거나 비었다"
  elif [ "$(head -c 4 "$shot" | od -An -tx1 | tr -d ' \n')" != "89504e47" ]; then
    fail "$name: 스크린샷이 PNG 가 아니다"
  else
    echo "   스크린샷 $(wc -c < "$shot")바이트: $(native_path "$shot")"
  fi
  if [ -s "$err" ]; then
    fail "$name: 표준 오류에 출력이 있다"
    sed 's/^/     /' "$err" | head -20
  fi
}

run_client_checks() {
  echo "== 3. 실행(클라이언트)"
  run_case start-menu --save-root "$SAVE" --lang ko
  run_case start-menu-en --save-root "$SAVE" --lang en
  run_case new-game --save-root "$SAVE" --slot slot-1 --lang ko
  [ -f "$WORK/save/slot-1/metadata.json" ] || fail "new-game: 세이브 슬롯 파일이 생기지 않았다"
  # 같은 슬롯을 다시 연다(이어 하기). 시작 화면이 고르는 슬롯 id(slot-N, Screens SaveSlots.SlotId)를 써서 그 뒤 시작 화면에 이 슬롯이 보인다.
  run_case resume --save-root "$SAVE" --slot slot-1 --lang ko
  run_case start-menu-with-slot --save-root "$SAVE" --lang ko
  run_case sample --sample --lang ko

  local maps="${VERIFY_MAPS:-}" m first_map=""
  if [ -z "$maps" ] && [ -f "$GAME/game/world/maps.json" ]; then
    # maps.json 은 주석이 든 JSON 이다. 맵 항목의 "id" 는 맵 배열 안 한 단계 깊이(공백 6칸)에 있다.
    maps="$(grep -E '^      "id": *"[^"]+"' "$GAME/game/world/maps.json" | sed -E 's/.*"id": *"([^"]+)".*/\1/' | tr '\n' ' ')"
  fi
  [ -n "$maps" ] || fail "맵 목록에서 맵 id 를 읽지 못했다(VERIFY_MAPS 로 준다)"
  for m in $maps; do
    run_case "world-$m" --world "$m" --lang ko
    [ -n "$first_map" ] || first_map="$m"
  done
  # 영어로도 월드 화면 하나(문구 파일을 배포 폴더에서 읽는지)
  [ -z "$first_map" ] || run_case "world-$first_map-en" --world "$first_map" --lang en

  # ---- 4. 실행 중 연 파일 ----
  echo "== 4. 실행 중 연 파일이 복사본 안에 있는가"
  if command -v strace >/dev/null 2>&1 && [ "$RID" != "${RID#linux-}" ]; then
    local trace="$WORK/logs/open-files.trace" outside
    (
      cd "$WORK" || exit 99
      env -u ROOTBORN_REPO_ROOT -u ROOTBORN_SAVE_ROOT -u ROOTBORN_UI_THEME -u ROOTBORN_WORLD_MAPS -u ROOTBORN_EVIDENCE_DIR \
        strace -f -qq -e trace=openat -o "$trace" "${TIMEOUT_CMD[@]+"${TIMEOUT_CMD[@]}"}" "$EXE" --save-root "$WORK/save" --lang ko \
        --size 1280x720 --frames "$FRAMES" --screenshot "$WORK/shots/trace.png"
    ) > /dev/null 2>&1
    # 성공한 열기만, 시스템 폴더(라이브러리·글꼴·장치)와 복사본·임시 폴더는 뺀다.
    outside="$(grep -v ' = -1 ' "$trace" | grep -oE 'openat\([^,]+, "[^"]+"' | sed -E 's/.*"([^"]+)"/\1/' | sort -u \
      | grep -vE '^(/usr/|/lib|/etc/|/proc/|/sys/|/dev/|/run/|/var/cache/fontconfig|/tmp/\.X|/tmp/clr-debug)' \
      | grep -vF "$WORK/" | grep -vE '^[^/]' || true)"
    if [ -n "$outside" ]; then
      echo "  복사본 밖에서 연 파일:"; echo "$outside" | sed 's/^/    /' | head -40
      case "$outside" in *"$REPO"*) fail "실행 중 저장소의 파일을 열었다" ;; esac
    else
      echo "  복사본 밖에서 연 게임 파일 없음(시스템 폴더 제외)"
    fi
  else
    echo "  확인 못 함: 이 플랫폼에는 strace 가 없고 Host 는 연 파일 목록을 내지 않는다."
    echo "  대신 2번(저장소 밖 복사본, 저장소 환경 변수 없음, --root 없음)으로 저장소 파일에 기댈 수 없게 했고,"
    echo "  3번의 화면 범위에서 읽지 못한 파일이 표준 오류에 나오지 않는 것을 봤다."
  fi
}

# ---- 3b. 서버: 듣기 → stop → 저장하고 닫기 → 이어서 열기 ----
SERVER_ENV_CLEAR=(-u ROOTBORN_REPO_ROOT -u ROOTBORN_DATA_DIR -u ROOTBORN_SAVE_ROOT -u ROOTBORN_SAVE_SLOT -u ROOTBORN_SERVER_BIND -u ROOTBORN_SERVER_PORT -u ROOTBORN_MAX_PLAYERS)

# 콘솔에 stop 을 넣으려고 stdin 을 파이프로 잇는다: 앞쪽 묶음이 stopfile 이 생길 때까지 기다렸다가 "stop" 을 쓴다.
# (서버는 stdin 이 닫혀도 끝나지 않는다 — 서비스로 도는 경우를 위해서. 그래서 stopfile 로 끝낸다.)
# run_server <이름> <슬롯> <로그에 있어야 할 열기 문구>
run_server() {
  local name="$1" slot="$2" expect="$3"
  local out="$WORK/logs/$name.out" err="$WORK/logs/$name.err" stopfile="$WORK/logs/$name.stop" rcfile="$WORK/logs/$name.rc"
  local waited=0 rc
  rm -f "$stopfile" "$rcfile"
  echo "-- $name: $EXE_NAME --slot $slot --bind $BIND --port 0"
  (
    cd "$WORK" || exit 99
    {
      while [ ! -f "$stopfile" ]; do sleep 0.2; done
      echo stop
    } 2>/dev/null | (
      env "${SERVER_ENV_CLEAR[@]}" "${TIMEOUT_CMD[@]+"${TIMEOUT_CMD[@]}"}" "$EXE"         --save-root "$SAVE" --slot "$slot" --bind "$BIND" --port 0 > "$out" 2> "$err"
      echo $? > "$rcfile"
      touch "$stopfile"   # 서버가 stop 없이 먼저 끝났어도(인자 오류 등) 앞쪽 기다림이 끝나게
    )
  ) &
  until grep -q "듣는 중: " "$out" 2>/dev/null; do
    sleep 0.3; waited=$((waited + 1))
    if [ -f "$rcfile" ] || [ $waited -gt $((LIMIT * 3)) ]; then break; fi
  done
  if grep -q "듣는 중: " "$out" 2>/dev/null; then
    echo "   $(grep -m1 '듣는 중: ' "$out")"
  else
    fail "$name: 로그에 '듣는 중:' 이 없다(듣기 시작하지 못했다)"
  fi
  grep -qF "$expect" "$out" || fail "$name: 로그에 '$expect' 이 없다"
  # 데이터는 복사본(저장소 밖)의 data 에서 읽어야 한다. 서버는 Windows 에서 경로를 \ 로 찍는다.
  if ! tr '\\' '/' < "$out" | grep -iqF "데이터 읽는 중: $(native_path "$GAME")/data"; then
    fail "$name: 데이터를 복사본의 data 에서 읽지 않았다(로그: $(grep -m1 '데이터 읽는 중' "$out"))"
  fi
  touch "$stopfile"
  waited=0
  until [ -f "$rcfile" ]; do
    sleep 0.3; waited=$((waited + 1))
    if [ $waited -gt $((LIMIT * 3)) ]; then fail "$name: stop 을 넣은 뒤 ${LIMIT}초 안에 끝나지 않았다"; return; fi
  done
  wait
  rc="$(cat "$rcfile")"
  [ "$rc" = "0" ] || fail "$name: 종료 코드 $rc"
  grep -qF "저장하고 닫음" "$out" || fail "$name: 로그에 '저장하고 닫음' 이 없다"
  if [ -s "$err" ]; then
    fail "$name: 표준 오류에 출력이 있다"
    sed 's/^/     /' "$err" | head -20
  fi
}

run_server_checks() {
  echo "== 3. 실행(서버, 듣는 주소 $BIND)"
  run_server first-start verify "새 방을 만듦"
  [ -n "$(ls -A "$WORK/save/verify" 2>/dev/null)" ] || fail "first-start: 세이브 슬롯에 아무것도 저장되지 않았다"
  run_server reopen verify "방을 이어서 엶"
  # 인자 없이 켜면 빠진 값을 알리고 종료 코드 2 로 끝난다(도움말에 값 이름이 나온다)
  local help_out="$WORK/logs/no-args.out" rc
  ( cd "$WORK" && env "${SERVER_ENV_CLEAR[@]}" "${TIMEOUT_CMD[@]+"${TIMEOUT_CMD[@]}"}" "$EXE" < /dev/null > "$help_out" 2>&1 )
  rc=$?
  echo "-- no-args: 종료 코드 $rc"
  [ $rc -eq 2 ] || fail "no-args: 인자 없이 켜면 종료 코드 2 여야 한다(실제 $rc)"
  grep -qF -- "--port" "$help_out" || fail "no-args: 도움말에 --port 가 없다"
}

if [ $SERVER -eq 1 ]; then run_server_checks; else run_client_checks; fi

# ---- 요약 ----
echo
if [ ${#failures[@]} -eq 0 ]; then
  echo "===== 통과: 배포 폴더 $SRC"
  if [ "${VERIFY_KEEP:-0}" = "1" ]; then echo "임시 폴더(스크린샷·로그): $(native_path "$WORK")"; else rm -rf "$WORK"; fi
  exit 0
fi
echo "===== 실패 ${#failures[@]}건 (임시 폴더를 남겼다: $(native_path "$WORK"))"
printf '  %s\n' "${failures[@]}"
exit 1
