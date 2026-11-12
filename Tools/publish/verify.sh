#!/usr/bin/env bash
# 배포 폴더가 저장소 없이 도는지 확인한다. 자세한 것은 README.md "확인하는 법".
#   Tools/publish/verify.sh [플랫폼]                  (기본: 이 PC 의 플랫폼 — Windows 는 win-x64, Linux 는 linux-x64)
#   Tools/publish/verify.sh --contents-only <플랫폼>  1번(목록 대조)만 한다. 이 PC 에서 실행할 수 없는 플랫폼(예: Windows 에서 linux-x64)용
#   Tools/publish/verify.sh --server [--bind <IP>] [플랫폼]   데디케이티드 서버 배포 폴더(server-<플랫폼>)를 확인한다(아래 "서버")
#   Tools/publish/verify.sh --remote [--server] linux-x64       1번은 이 PC 에서, 2~4번은 원격 Linux 장치에서(아래 "원격")
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
#      콘솔에 status·save 를 한 번씩 넣어 답이 나오는지 → stop 을 넣어 끝내고 종료 코드 0 과 "저장하고 닫음" →
#      같은 세이브로 다시 켜서 "방을 이어서 엶" → 다시 status·save·stop.
#      인자 없이 켜면 종료 코드 2 와 도움말.
#      --root·ROOTBORN_REPO_ROOT·ROOTBORN_DATA_DIR 없이 돈다(실행 파일 위치에서 data/registry.json 을 찾는다). 표준 오류는 비어야 한다.
#   듣는 주소는 --bind(또는 VERIFY_BIND)로 받는다. 기본 127.0.0.1 은 이 시험용이다: 같은 기계 안에서만 닿고, 방화벽 창이 뜨지 않고,
#   밖에 열리지 않아 안전하다. 실제 서버를 여는 값(LAN 주소 등)을 이 스크립트에 적지 않는다.
#   Linux 에서는 종료 신호(SIGTERM)로 끝내도 저장하고 닫는지 한 번 더 본다(sigterm). Windows 에는 같은 신호가 없어 건너뛴다.
#   같이 쓰는 것(sharing): 복사본 서버를 --share wallet,house,town 으로 띄우고 손님 테스트(Rootborn.Server.Tests 의
#   DeployedServerSharingTests)가 루프백 UDP 로 들어가 가구 놓기 허락(맵 데이터), 방 지갑, 손님 진행의 guests/ 저장과 재시작 뒤 이어짐을 본다.
#   테스트 어셈블리는 저장소에서 빌드한다(원격은 보낸 쪽이 빌드해 보낸다). 둘 다 안 되면 건너뛰고 알린다.
#
# 들어가기(클라이언트 확인에서, 같은 플랫폼의 서버 배포 폴더가 있을 때)
#   새 게임 뒤 그 서버 배포 폴더를 빈 포트로 띄우고, 클라이언트를 --slot slot-1 --join <BIND>:<포트> 로 띄운다.
#   서버 로그에 "들어옴:" 이 있고 "접속 거절" 이 없으면 통과 — 배포한 두 폴더의 버전(프로그램·데이터 지문)이 맞는다는 실제 확인이다.
#   서버 배포 폴더가 없으면 건너뛰고 그렇게 알린다.
#
# 원격(--remote) — 이 PC 에서 실행할 수 없는 linux-x64 용(verify-remote.sh)
#   1번(목록 대조)은 이 PC 에서 한다. 배포 폴더(클라이언트면 같은 플랫폼의 서버 배포 폴더도)와 이 스크립트를 원격의
#   ~/<ROOTBORN_REMOTE_DIR>/deploy/<실행 id>/ 로 보내 실행 권한을 주고, 거기서 이 스크립트를 --copied 로 돌린다.
#   클라이언트는 가상 화면(Xvfb)에서 돈다: Scripts/remote-test 가 준비한 원격 env.sh 의 Xvfb·글꼴(ROOTBORN_FONT)을 쓰고,
#   화면 번호는 원격 테스트의 범위(140~199)와 겹치지 않는 VERIFY_DISPLAY_FIRST~LAST(기본 200~219)에서 빈 것을 고른다.
#   임시 폴더도 그 실행 폴더 안에 둔다. 끝나면 스크린샷·로그를 output/publish-verify/<실행 id>/ 로 가져오고
#   원격 실행 폴더와 화면을 지운다. 원격에서는 sudo 를 쓰지 않고 작업 폴더 밖을 건드리지 않는다.
#   접속 값은 Scripts/remote-test 와 같은 환경 변수(ROOTBORN_REMOTE_HOST·USER·KEY, ROOTBORN_REMOTE_DIR)로만 받는다.
#   없으면 1번만 하고 "원격 실행을 건너뛰었다"고 알린다(종료 코드는 1번 결과).
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
#   VERIFY_SERVER_SRC  들어가기 확인에 쓸 서버 배포 폴더(기본: <PUBLISH_OUT>/server-<플랫폼>)
#   VERIFY_SHARING_TEST_DLL  같이 쓰는 것 확인에 쓸 Rootborn.Server.Tests.dll(없으면 저장소에서 빌드한다)
#   VERIFY_DOTNET   그 테스트를 돌릴 dotnet(기본: PATH 의 dotnet)
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
REMOTE=0
COPIED=""
# 서버 시험용 듣는 주소: 같은 기계 안에서만 닿는 루프백이라 시험에 안전하다(밖에 열지 않는다).
BIND="${VERIFY_BIND:-127.0.0.1}"
while [ $# -gt 0 ]; do
  case "$1" in
    --contents-only) CONTENTS_ONLY=1; shift ;;
    --server) SERVER=1; shift ;;
    --bind) [ $# -ge 2 ] || { echo "--bind 에 값이 없다" >&2; exit 2; }; BIND="$2"; shift 2 ;;
    --remote) REMOTE=1; shift ;;
    # 원격에서 쓰는 내부 인자: 저장소 없이 이미 복사된 배포 폴더를 실행만 한다(1번은 보낸 쪽이 했다)
    --copied) [ $# -ge 2 ] || { echo "--copied 에 값이 없다" >&2; exit 2; }; COPIED="$2"; shift 2 ;;
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
[ -z "$COPIED" ] || SRC="$COPIED"
SERVER_SRC="${VERIFY_SERVER_SRC:-$OUT_ROOT/server-$RID}"
case "$RID" in win-*) EXE_NAME=$PROJECT_NAME.exe; SERVER_EXE_NAME=Rootborn.Server.exe ;; *) EXE_NAME=$PROJECT_NAME; SERVER_EXE_NAME=Rootborn.Server ;; esac
[ -d "$SRC" ] || { echo "배포 폴더가 없다: $SRC (먼저 Tools/publish/$PUBLISH_HINT)" >&2; exit 2; }
[ -f "$SRC/$EXE_NAME" ] || { echo "실행 파일이 없다: $SRC/$EXE_NAME" >&2; exit 2; }

failures=()
fail() { failures+=("$1"); echo "  실패: $1"; }

# Windows 실행 파일에는 Windows 경로를 넘긴다.
native_path() { if command -v cygpath >/dev/null 2>&1; then cygpath -m "$1"; else printf '%s' "$1"; fi; }

# ---- 1. 목록대로 들어갔는가 (--copied 면 보낸 쪽이 이미 했다) ----
if [ -z "$COPIED" ]; then
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
# 테스트·문서는 배포에 들어가지 않는다(게시한 프로젝트가 테스트를 참조하거나 목록이 문서 폴더를 집으면 여기서 잡힌다).
not_shipped="$(find "$SRC" -type f \( -iname '*.md' -o -iname '*.Tests.dll' -o -iname 'nunit*' -o -iname 'Microsoft.TestPlatform*' -o -iname 'testhost*' \) | head -5)"
[ -z "$not_shipped" ] || fail "테스트·문서 파일이 들어 있다: $(echo "$not_shipped" | tr '\n' ' ')"
if [ $SERVER -eq 1 ]; then
  # 서버는 그림·글꼴·테마를 읽지 않는다. 들어 있으면 목록이 새고 있는 것이다.
  extra="$(find "$SRC" -type f \( -iname '*.png' -o -iname '*.ttf' -o -iname '*.otf' -o -iname '*.ttc' \) | head -5)"
  [ -z "$extra" ] || fail "서버 폴더에 그림·글꼴 파일이 들어 있다: $(echo "$extra" | tr '\n' ' ')"
  # 같이 쓰는 집의 판정 칸은 맵 목록과 LDtk(그림 없이)에서 만든다(HouseGrids). 빠지면 집 공유 방의 가구 놓기가 모두 거절된다.
  [ -f "$SRC/game/world/maps.json" ] || fail "서버 폴더에 game/world/maps.json 이 없다(같이 쓰는 집을 판정할 수 없다)"
  [ -n "$(find "$SRC" -type f -iname '*.ldtk' | head -1)" ] || fail "서버 폴더에 LDtk 맵(.ldtk)이 없다(같이 쓰는 집을 판정할 수 없다)"
fi
fi

if [ $CONTENTS_ONLY -eq 1 ]; then
  echo
  if [ ${#failures[@]} -eq 0 ]; then echo "===== 통과(목록 대조만, 실행은 하지 않았다): 배포 폴더 $SRC"; exit 0; fi
  echo "===== 실패 ${#failures[@]}건"; printf '  %s\n' "${failures[@]}"; exit 1
fi

if [ $REMOTE -eq 1 ]; then
  # shellcheck source=/dev/null
  . "$HERE/verify-remote.sh"
  run_remote
  exit $?
fi

# ---- 2. 저장소 밖으로 복사 ----
WORK="$(mktemp -d)"
if [ -z "$COPIED" ]; then
  case "$(cd "$WORK" && pwd -P)/" in
    "$(cd "$REPO" && pwd -P)"/*) echo "임시 폴더가 저장소 안이다: $WORK (TMPDIR 를 바꾼다)" >&2; exit 2 ;;
  esac
else
  REPO="/nonexistent-repo"   # 원격 복사본 곁에는 저장소가 없다(4번의 '저장소 파일을 열었다' 검사가 헛걸리지 않게)
fi
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
  run_join_check

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
SERVER_ENV_CLEAR=(-u ROOTBORN_REPO_ROOT -u ROOTBORN_DATA_DIR -u ROOTBORN_SAVE_ROOT -u ROOTBORN_SAVE_SLOT -u ROOTBORN_SERVER_BIND -u ROOTBORN_SERVER_PORT -u ROOTBORN_MAX_PLAYERS
  -u ROOTBORN_SERVER_LOG -u ROOTBORN_SERVER_LOG_MAX_MB -u ROOTBORN_SERVER_LOG_FILES -u ROOTBORN_AUTOSAVE_SECONDS)

# 콘솔에 명령을 넣으려고 stdin 을 파이프로 잇는다: 앞쪽 묶음이 서버가 명령을 받는다고 찍으면 "status" 와 "save" 를 한 번씩 쓰고,
# stopfile 이 생길 때까지 기다렸다가 "stop" 을 쓴다.
# (서버는 stdin 이 닫혀도 끝나지 않는다 — 서비스로 도는 경우를 위해서. 그래서 stopfile 로 끝낸다.)
# start_server <이름> <서버 폴더> <슬롯> : 백그라운드로 띄우고 "듣는 중:" 을 기다린다. SERVER_PORT 에 들은 포트를 둔다.
start_server() {
  local name="$1" dir="$2" slot="$3"
  local out="$WORK/logs/$name.out" err="$WORK/logs/$name.err" stopfile="$WORK/logs/$name.stop" rcfile="$WORK/logs/$name.rc"
  local waited=0 exe="$2/$SERVER_EXE_NAME"
  rm -f "$stopfile" "$rcfile"
  SERVER_PORT=""
  echo "-- $name: $SERVER_EXE_NAME --slot $slot --bind $BIND --port 0"
  (
    cd "$WORK" || exit 99
    {
      until grep -q "콘솔 명령을 받는다" "$out" 2>/dev/null || [ -f "$stopfile" ]; do sleep 0.2; done
      echo status
      echo save
      while [ ! -f "$stopfile" ]; do sleep 0.2; done
      echo stop
    } 2>/dev/null | (
      env "${SERVER_ENV_CLEAR[@]}" "${TIMEOUT_CMD[@]+"${TIMEOUT_CMD[@]}"}" "$exe" --save-root "$SAVE" --slot "$slot" --bind "$BIND" --port 0 > "$out" 2> "$err"
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
    SERVER_PORT="$(grep -m1 '듣는 중: ' "$out" | sed -E 's/.*:([0-9]+)[[:space:]]*$/\1/')"
  else
    fail "$name: 로그에 '듣는 중:' 이 없다(듣기 시작하지 못했다)"
  fi
}

# console_checks <이름> : 앞쪽 묶음이 넣은 status·save 의 답을 본다(콘솔 명령 — 안내서 game/Rootborn.Server/README.md).
console_checks() {
  local name="$1" out="$WORK/logs/$1.out" waited=0
  until grep -qF "저장함(save 명령)" "$out" 2>/dev/null; do
    sleep 0.3; waited=$((waited + 1))
    if [ $waited -gt $((LIMIT * 3)) ]; then break; fi
  done
  grep -qF "가동 시간: " "$out" || fail "$name: status 명령의 답(가동 시간)이 없다"
  grep -qF "마지막 저장: " "$out" || fail "$name: status 명령의 답(마지막 저장)이 없다"
  grep -qF "저장함(save 명령)" "$out" || fail "$name: save 명령의 답이 없다"
}

# stop_server <이름> : 콘솔에 stop 을 넣고 끝나기를 기다린다. 종료 코드 0, "저장하고 닫음", 빈 표준 오류를 본다.
stop_server() {
  local name="$1" out="$WORK/logs/$1.out" err="$WORK/logs/$1.err" rcfile="$WORK/logs/$1.rc" waited=0 rc
  touch "$WORK/logs/$name.stop"
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

# run_server <이름> <슬롯> <로그에 있어야 할 열기 문구>
run_server() {
  local name="$1" slot="$2" expect="$3" out="$WORK/logs/$1.out"
  start_server "$name" "$GAME" "$slot"
  grep -qF "$expect" "$out" || fail "$name: 로그에 '$expect' 이 없다"
  # 데이터는 복사본(저장소 밖)의 data 에서 읽어야 한다. 서버는 Windows 에서 경로를 \ 로 찍는다.
  if ! tr '\\' '/' < "$out" | grep -iqF "데이터 읽는 중: $(native_path "$GAME")/data"; then
    fail "$name: 데이터를 복사본의 data 에서 읽지 않았다(로그: $(grep -m1 '데이터 읽는 중' "$out"))"
  fi
  console_checks "$name"
  stop_server "$name"
}

# run_server_sigterm <이름> <슬롯> : 콘솔 없이(서비스처럼) 띄우고 종료 신호로 끝낸다. 저장하고 닫고 종료 코드 0 이어야 한다.
run_server_sigterm() {
  local name="$1" slot="$2"
  local out="$WORK/logs/$name.out" err="$WORK/logs/$name.err" pid waited=0 rc
  echo "-- $name: $EXE_NAME --slot $slot --bind $BIND --port 0 (입력 없음, SIGTERM 으로 끝냄)"
  # exec 로 바꿔 띄워 $! 가 서버 프로세스 자신이 되게 한다(timeout 으로 감싸지 않는다 — 신호를 직접 받게).
  ( cd "$WORK" && exec env "${SERVER_ENV_CLEAR[@]}" "$EXE" --save-root "$SAVE" --slot "$slot" --bind "$BIND" --port 0 < /dev/null > "$out" 2> "$err" ) &
  pid=$!
  until grep -q "듣는 중: " "$out" 2>/dev/null; do
    sleep 0.3; waited=$((waited + 1))
    if ! kill -0 "$pid" 2>/dev/null || [ $waited -gt $((LIMIT * 3)) ]; then break; fi
  done
  grep -q "듣는 중: " "$out" 2>/dev/null || fail "$name: 로그에 '듣는 중:' 이 없다"
  kill -TERM "$pid" 2>/dev/null
  waited=0
  while kill -0 "$pid" 2>/dev/null; do
    sleep 0.3; waited=$((waited + 1))
    if [ $waited -gt $((LIMIT * 3)) ]; then kill -KILL "$pid" 2>/dev/null; fail "$name: SIGTERM 뒤 ${LIMIT}초 안에 끝나지 않았다"; break; fi
  done
  wait "$pid"; rc=$?
  echo "   종료 코드 $rc"
  [ "$rc" = "0" ] || fail "$name: SIGTERM 뒤 종료 코드 $rc"
  grep -qF "저장하고 닫음" "$out" || fail "$name: SIGTERM 뒤 로그에 '저장하고 닫음' 이 없다"
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
  if [ "$(uname -s)" = Linux ]; then
    run_server_sigterm sigterm verify
    grep -qF "방을 이어서 엶" "$WORK/logs/sigterm.out" || fail "sigterm: 같은 세이브인데 '방을 이어서 엶' 이 없다"
  else
    echo "-- sigterm: 건너뜀(이 플랫폼에는 종료 신호가 없다 — Linux 에서 본다)"
  fi
  # 인자 없이 켜면 빠진 값을 알리고 종료 코드 2 로 끝난다(도움말에 값 이름이 나온다)
  local help_out="$WORK/logs/no-args.out" rc
  ( cd "$WORK" && env "${SERVER_ENV_CLEAR[@]}" "${TIMEOUT_CMD[@]+"${TIMEOUT_CMD[@]}"}" "$EXE" < /dev/null > "$help_out" 2>&1 )
  rc=$?
  echo "-- no-args: 종료 코드 $rc"
  [ $rc -eq 2 ] || fail "no-args: 인자 없이 켜면 종료 코드 2 여야 한다(실제 $rc)"
  grep -qF -- "--port" "$help_out" || fail "no-args: 도움말에 --port 가 없다"
  run_sharing_check
}

# ---- 3d. 같이 쓰는 것: 배포한 서버를 돈·집·마을을 같이 쓰는 방으로 열고 손님 하나로 들어가 본다 ----
# 손님은 테스트(game/tests/Rootborn.Server.Tests 의 DeployedServerSharingTests, [Explicit])다 — 배포 클라이언트는 화면 입력 없이 가구를 놓을 수 없어서다.
# 가구 놓기가 방의 허락을 받는지(배포 폴더의 맵 데이터로 판정), 방 지갑, 손님 진행이 서버의 guests/ 에 남고 껐다 켜면 이어지는지를 본다.
# 테스트 어셈블리: VERIFY_SHARING_TEST_DLL(원격은 보낸 쪽이 빌드해 준다) 또는 저장소에서 빌드한다. 둘 다 없으면 건너뛰고 알린다.
run_sharing_check() {
  local dll="${VERIFY_SHARING_TEST_DLL:-}" dotnet="${VERIFY_DOTNET:-dotnet}" log="$WORK/logs/sharing-test.log" rc
  echo "-- sharing: 배포한 서버 --share wallet,house,town ← 손님(DeployedServerSharingTests)"
  if [ -z "$dll" ] && [ -z "$COPIED" ] && command -v "$dotnet" >/dev/null 2>&1; then
    dll="$(build_sharing_test "$WORK/sharing-test")" || { fail "sharing: 테스트를 빌드하지 못했다(로그: $(native_path "$WORK/sharing-test.build.log"))"; return; }
  fi
  if [ -z "$dll" ] || [ ! -f "$dll" ]; then
    echo "   건너뜀: 테스트 어셈블리가 없다(VERIFY_SHARING_TEST_DLL, 또는 저장소와 dotnet 이 있는 곳에서 돌린다)"
    SHARING_SKIPPED=1
    return
  fi
  cp -r "$GAME" "$WORK/share-server"
  ROOTBORN_DEPLOYED_SERVER="$(native_path "$WORK/share-server")" "$dotnet" test "$(native_path "$dll")" --filter "FullyQualifiedName~DeployedServerSharingTests" \
    > "$log" 2>&1
  rc=$?
  if [ $rc -eq 0 ] && grep -qE "(통과|Passed)[!]? +- " "$log"; then
    echo "   통과: $(grep -m1 -E '(통과|Passed)[!]? +- ' "$log" | sed 's/^ *//')"
  else
    fail "sharing: 테스트 실패(종료 코드 $rc, 로그 $(native_path "$log"))"
    grep -E "실패 |Failed |오류 메시지|Error Message" -A4 "$log" | head -30 | sed 's/^/     /'
  fi
}

# build_sharing_test <출력 폴더> : 저장소의 Rootborn.Server.Tests 를 빌드해 그 dll 경로를 찍는다(실패하면 1).
build_sharing_test() {
  local out="$1"
  ( cd "$REPO" && MSBUILDDISABLENODEREUSE=1 DOTNET_CLI_USE_MSBUILD_SERVER=0 dotnet build game/tests/Rootborn.Server.Tests -c Release -o "$(native_path "$out")" \
      -nologo -v quiet -nodeReuse:false -p:UseSharedCompilation=false ) > "$out.build.log" 2>&1 || return 1
  [ -f "$out/Rootborn.Server.Tests.dll" ] || return 1
  printf '%s' "$out/Rootborn.Server.Tests.dll"
}

# ---- 3c. 들어가기: 배포한 서버에 배포한 클라이언트가 들어가는가(버전·데이터 지문이 맞는가) ----
# run_join_check : new-game 이 만든 slot-1 로 들어간다. 서버 배포 폴더가 없으면 건너뛴다.
run_join_check() {
  echo "== 3+. 들어가기(배포한 서버 ← 배포한 클라이언트, $BIND)"
  if [ ! -f "$SERVER_SRC/$SERVER_EXE_NAME" ]; then
    echo "  건너뜀: 서버 배포 폴더가 없다: $SERVER_SRC (Tools/publish/publish.sh --server $RID)"
    JOIN_SKIPPED=1
    return
  fi
  local server_copy="$WORK/server" out="$WORK/logs/join-server.out"
  cp -r "$SERVER_SRC" "$server_copy"
  start_server join-server "$server_copy" join-room
  if [ -n "$SERVER_PORT" ]; then
    run_case join --save-root "$SAVE" --slot slot-1 --join "$BIND:$SERVER_PORT" --lang ko
    if grep -q "들어옴: " "$out"; then echo "   서버: $(grep -m1 '들어옴: ' "$out")"; else fail "join: 서버 로그에 '들어옴:' 이 없다"; fi
    if grep -q "접속 거절" "$out"; then fail "join: 서버가 거절했다: $(grep -m1 '접속 거절' "$out")"; fi
  fi
  stop_server join-server
}

if [ $SERVER -eq 1 ]; then run_server_checks; else run_client_checks; fi

# ---- 요약 ----
echo
[ -z "${JOIN_SKIPPED:-}" ] || echo "(들어가기 확인은 서버 배포 폴더가 없어 건너뛰었다)"
[ -z "${SHARING_SKIPPED:-}" ] || echo "(같이 쓰는 것 확인은 테스트 어셈블리가 없어 건너뛰었다)"
if [ ${#failures[@]} -eq 0 ]; then
  echo "===== 통과: 배포 폴더 $SRC"
  if [ "${VERIFY_KEEP:-0}" = "1" ]; then echo "임시 폴더(스크린샷·로그): $(native_path "$WORK")"; else rm -rf "$WORK"; fi
  exit 0
fi
echo "===== 실패 ${#failures[@]}건 (임시 폴더를 남겼다: $(native_path "$WORK"))"
printf '  %s\n' "${failures[@]}"
exit 1
