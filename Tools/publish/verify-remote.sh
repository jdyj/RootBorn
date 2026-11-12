# verify.sh --remote 의 몸통. verify.sh 가 1번(목록 대조) 뒤에 읽어 run_remote 를 부른다(혼자 실행하지 않는다).
# 배포 폴더를 원격 Linux 장치로 보내 거기서 verify.sh --copied 로 2~4번을 돌리고, 스크린샷·로그를 가져온 뒤 원격 폴더를 지운다.
# 쓰는 값(verify.sh 에서): SRC SERVER SERVER_SRC RID HERE REPO BIND FRAMES LIMIT failures
# 접속 값은 Scripts/remote-test 와 같은 환경 변수로만 받는다(저장소에 적지 않는다).

run_remote() {
  local missing="" v
  for v in ROOTBORN_REMOTE_HOST ROOTBORN_REMOTE_USER ROOTBORN_REMOTE_KEY; do [ -n "${!v:-}" ] || missing="$missing $v"; done
  if [ -n "$missing" ]; then
    echo
    echo "== 2~4. 원격 실행을 건너뛰었다: 접속 정보가 없다:$missing (쓰는 법: Scripts/remote-test/README.md)"
    if [ ${#failures[@]} -eq 0 ]; then echo "===== 통과(목록 대조만, 실행은 하지 않았다): 배포 폴더 $SRC"; return 0; fi
    echo "===== 실패 ${#failures[@]}건"; printf '  %s\n' "${failures[@]}"; return 1
  fi
  [ -f "$ROOTBORN_REMOTE_KEY" ] || { echo "ROOTBORN_REMOTE_KEY 파일이 없다: $ROOTBORN_REMOTE_KEY" >&2; return 2; }
  case "$RID" in linux-*) ;; *) echo "--remote 는 linux 플랫폼만 돌린다: $RID" >&2; return 2 ;; esac
  if [ ${#failures[@]} -gt 0 ]; then
    echo "===== 1번에서 실패 ${#failures[@]}건 — 원격으로 보내지 않는다"; printf '  %s\n' "${failures[@]}"; return 1
  fi
  local dir_name="${ROOTBORN_REMOTE_DIR:-rootborn-test}"
  case "$dir_name" in /*|*..*|"") echo "ROOTBORN_REMOTE_DIR는 원격 홈 아래 상대 경로여야 한다: $dir_name" >&2; return 2 ;; esac

  local ssh_opts=(-i "$ROOTBORN_REMOTE_KEY" -o IdentitiesOnly=yes -o BatchMode=yes -o StrictHostKeyChecking=yes -o ServerAliveInterval=30)
  local target="$ROOTBORN_REMOTE_USER@$ROOTBORN_REMOTE_HOST"
  local run_id; run_id="$(date +%Y%m%d-%H%M%S)-$$-$RANDOM"
  local dep="$dir_name/deploy/$run_id" dep_q out="$REPO/output/publish-verify/$run_id" status
  dep_q="$(printf '%q' "$dep")"
  mkdir -p "$out"
  remote() { ssh "${ssh_opts[@]}" "$target" "$@"; }

  echo "== 2. 원격으로 보내기: ~/$dep (실행 $run_id)"
  remote "test -f $(printf '%q' "$dir_name/env.sh")" \
    || { echo "원격에 $dir_name/env.sh 가 없다(먼저 bash Scripts/remote-test/run.sh --setup)" >&2; return 2; }
  remote "mkdir -p $dep_q/pkg" || return 2
  tar -C "$SRC" -cf - . | remote "tar -xf - -C $dep_q/pkg" || { echo "배포 폴더를 보내지 못했다" >&2; remote "rm -rf $dep_q"; return 2; }
  local extra_args=()
  if [ $SERVER -eq 1 ]; then
    extra_args+=(--server)
    # 같이 쓰는 것 확인(verify.sh run_sharing_check)의 손님 테스트를 이 PC 에서 빌드해 보낸다(원격에는 저장소가 없다). 어셈블리는 플랫폼과 무관하다.
    local test_out; test_out="$(mktemp -d)/sharing-test"
    if ( cd "$REPO" && MSBUILDDISABLENODEREUSE=1 DOTNET_CLI_USE_MSBUILD_SERVER=0 dotnet build game/tests/Rootborn.Server.Tests -c Release -o "$(native_path "$test_out")" \
           -nologo -v quiet -nodeReuse:false -p:UseSharedCompilation=false ) > "$out/sharing-test.build.log" 2>&1 \
       && [ -f "$test_out/Rootborn.Server.Tests.dll" ]; then
      remote "mkdir -p $dep_q/sharing-test" && tar -C "$test_out" -cf - . | remote "tar -xf - -C $dep_q/sharing-test" \
        || echo "  같이 쓰는 것 확인 테스트를 보내지 못했다 — 원격에서 건너뛴다" >&2
    else
      echo "  같이 쓰는 것 확인 테스트를 빌드하지 못했다($(native_path "$out/sharing-test.build.log")) — 원격에서 건너뛴다" >&2
    fi
    rm -rf "$(dirname "$test_out")"
  elif [ -f "$SERVER_SRC/Rootborn.Server" ]; then
    remote "mkdir -p $dep_q/server" && tar -C "$SERVER_SRC" -cf - . | remote "tar -xf - -C $dep_q/server" \
      || { echo "서버 배포 폴더를 보내지 못했다" >&2; remote "rm -rf $dep_q"; return 2; }
  else
    echo "  서버 배포 폴더가 없다($SERVER_SRC) — 들어가기 확인은 건너뛴다"
  fi
  tar -C "$HERE" -cf - verify.sh | remote "tar -xf - -C $dep_q" || { remote "rm -rf $dep_q"; return 2; }

  set +e
  remote "bash -s -- $(printf '%q ' "$dir_name" "$dep" "$RID" "$BIND" "$FRAMES" "$LIMIT" "${extra_args[@]+"${extra_args[@]}"}")" <<'REMOTE' 2>&1 | tee "$out/remote.log"
set -uo pipefail
BASE="$HOME/$1"; DEP="$HOME/$2"; RID="$3"; BIND="$4"; FRAMES="$5"; LIMIT="$6"; shift 6
# shellcheck source=/dev/null
. "$BASE/env.sh"
chmod +x "$DEP/pkg/Rootborn.Host" "$DEP/pkg/Rootborn.Server" "$DEP/server/Rootborn.Server" 2>/dev/null
mkdir -p "$DEP/tmp" "$DEP/home"
XVFB_PID=""
cleanup() {
  [ -z "$XVFB_PID" ] || kill "$XVFB_PID" 2>/dev/null
  pkill -TERM -f "$DEP/tmp/" 2>/dev/null
  sleep 1
  pkill -KILL -f "$DEP/tmp/" 2>/dev/null
  true
}
trap cleanup EXIT
if [ "${1:-}" != "--server" ]; then
  # 가상 화면: 원격 테스트(Scripts/remote-test)의 범위와 겹치지 않는 번호에서 빈 것을 고른다. 떠 있는 화면은 건드리지 않는다.
  for d in $(seq "${VERIFY_DISPLAY_FIRST:-200}" "${VERIFY_DISPLAY_LAST:-219}"); do
    [ -e "/tmp/.X$d-lock" ] || [ -e "/tmp/.X11-unix/X$d" ] && continue
    LD_LIBRARY_PATH="$ROOTBORN_TEST_XLIB" "$ROOTBORN_TEST_XVFB" ":$d" -screen 0 1920x1080x24 -nolisten tcp -noreset \
      -xkbdir "$ROOTBORN_TEST_XKBDIR" > "$DEP/tmp/xvfb.log" 2>&1 &
    XVFB_PID=$!
    for _ in $(seq 1 50); do [ -e "/tmp/.X11-unix/X$d" ] && break; kill -0 "$XVFB_PID" 2>/dev/null || break; sleep 0.1; done
    if kill -0 "$XVFB_PID" 2>/dev/null && [ -e "/tmp/.X11-unix/X$d" ]; then export DISPLAY=":$d"; break; fi
    XVFB_PID=""
  done
  [ -n "$XVFB_PID" ] || { echo "빈 가상 화면 번호를 찾지 못했다" >&2; exit 6; }
  echo "가상 화면 $DISPLAY, 글꼴 ${ROOTBORN_FONT:-없음}"
  export LIBGL_ALWAYS_SOFTWARE=1 SDL_AUDIODRIVER=dummy
fi
# 임시 폴더와 홈(글꼴 캐시 등)을 실행 폴더 안에 둔다 — 원격 작업 폴더 밖에 쓰지 않는다.
export TMPDIR="$DEP/tmp" HOME="$DEP/home" XDG_CACHE_HOME="$DEP/home/.cache" XDG_CONFIG_HOME="$DEP/home/.config" XDG_DATA_HOME="$DEP/home/.local/share"
export VERIFY_KEEP=1 VERIFY_SERVER_SRC="$DEP/server" VERIFY_BIND="$BIND" VERIFY_FRAMES="$FRAMES" VERIFY_TIMEOUT="$LIMIT"
if [ -f "$DEP/sharing-test/Rootborn.Server.Tests.dll" ] && [ -x "${ROOTBORN_TEST_DOTNET:-}" ]; then
  # 원격 테스트가 준비한 dotnet(env.sh). 첫 실행 기록·NuGet 은 실행 폴더 안의 HOME 에 쓴다.
  export VERIFY_SHARING_TEST_DLL="$DEP/sharing-test/Rootborn.Server.Tests.dll" VERIFY_DOTNET="$ROOTBORN_TEST_DOTNET"
  export DOTNET_ROOT="$(dirname "$ROOTBORN_TEST_DOTNET")" DOTNET_CLI_HOME="$DEP/home" DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
fi
bash "$DEP/verify.sh" --copied "$DEP/pkg" "$@" "$RID"
REMOTE
  status=${PIPESTATUS[0]}

  echo "== 원격 결과 가져오기: $(native_path "$out")"
  remote "tar -C $dep_q/tmp --exclude='./tmp.*/game' --exclude='./tmp.*/server' --exclude='./tmp.*/share-server' -cf - ." | tar -xf - -C "$out" \
    || echo "  결과를 가져오지 못했다" >&2
  remote "rm -rf $dep_q" || echo "  원격 폴더를 지우지 못했다: ~/$dep" >&2
  remote "test ! -e $dep_q" && echo "  원격 실행 폴더를 지웠다: ~/$dep"
  echo "원격 종료 코드 $status. 스크린샷·로그: $(native_path "$out")"
  return "$status"
}
