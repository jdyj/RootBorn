#!/usr/bin/env bash
# 두 기계 확인(two-machines.sh)의 원격 쪽. 이 PC의 two-machines.sh가 실행 폴더의 bin/에 올리고, 이 PC의 테스트(TwoMachinesHostTests)가
# ssh로 부른다. 혼자 쓰지 않는다. 자세한 것은 Scripts/remote-test/README.md "두 기계로 같이 하기".
#
#   bash two-machines-remote.sh server <실행 폴더> <공유> <슬롯> [<서버 폴더>]  # 데디케이티드 서버를 이 장치의 LAN 주소 하나에 묶어 띄운다(포트는 운영체제가 준 빈 포트).
#                                                                  서버 폴더는 실행 폴더 안의 이름(기본 server, 다른 커밋의 서버는 server-other)
#   bash two-machines-remote.sh peer <실행 폴더>                    # 가상 화면에서 두 번째 게임(Rootborn.Host.Tests --two-games-peer)
#   bash two-machines-remote.sh lan-address                         # 이 장치의 LAN 주소(ssh로 들어온 주소)
#   bash two-machines-remote.sh client-address                      # ssh를 건 쪽(이 PC)의 주소 — 이 PC에서 방을 열 때 묶을 주소
#   bash two-machines-remote.sh home                                # 원격 홈의 절대 경로(게임 인자에 넣을 경로를 만든다)
#   bash two-machines-remote.sh guest-files <실행 폴더>             # 서버 세이브의 손님 폴더(guests/) 파일 목록
#   bash two-machines-remote.sh cleanup <실행 폴더>                 # 이 실행이 띄운 프로세스를 끝내고 폴더를 지운다
#   bash two-machines-remote.sh check <작업 폴더>                   # 두 기계 확인이 남긴 프로세스·듣는 포트·폴더가 있는지
#
# 실행 폴더·작업 폴더는 원격 홈 기준 상대 경로다. 이 스크립트는 그 폴더 안에만 쓴다(sudo 없음).
# 띄우는 프로세스에는 ROOTBORN_TWO_MACHINES_RUN=<실행 폴더 이름> 표시를 붙이고, 끝낼 때는 그 표시가 붙은 것만 끝낸다.
# 표준 입력이 닫히면(이 PC 쪽 테스트나 ssh가 끝났다) 띄운 것을 끝낸다. 오래 남지 않게 시간 한도(TWO_MACHINES_LIMIT, 기본 2h)도 건다.
set -uo pipefail

cmd="${1:-}"; shift || true
cd "$HOME" || exit 2

base_env() {
  local work="$1"
  [ -f "$work/env.sh" ] || { echo "원격에 $work/env.sh 가 없다(먼저 bash Scripts/remote-test/run.sh --setup)" >&2; exit 2; }
  # shellcheck disable=SC1090
  . "$work/env.sh"
  export DOTNET_ROOT="$(dirname "$ROOTBORN_TEST_DOTNET")"
  export PATH="$DOTNET_ROOT:$PATH"
  export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
}

check_run_dir() {
  case "$1" in /*|*..*|"") echo "실행 폴더는 원격 홈 아래 상대 경로여야 한다: $1" >&2; exit 2 ;; esac
  [ -d "$1" ] || { echo "실행 폴더가 없다: $1" >&2; exit 2; }
}

# 이 기계로 ssh가 들어온 주소 = 이 PC와 같은 LAN에서 이 장치를 가리키는 주소(SSH_CONNECTION의 셋째 칸). 스크립트에 주소를 적지 않는다.
lan_address() {
  local a; a="$(awk '{print $3}' <<<"${SSH_CONNECTION:-}")"
  [ -n "$a" ] || { echo "SSH_CONNECTION이 없다(ssh로 부를 때만 LAN 주소를 안다)" >&2; exit 2; }
  case "$a" in 127.*|::1) echo "ssh가 루프백으로 들어왔다 — LAN 주소를 알 수 없다: $a" >&2; exit 2 ;; esac
  echo "$a"
}

# 그 실행 표시가 붙은 프로세스 번호들(자기 사용자 것만 읽힌다).
marked_pids() {
  local mark="$1" d
  for d in /proc/[0-9]*; do
    { tr '\0' '\n' < "$d/environ"; } 2>/dev/null | grep -qx "ROOTBORN_TWO_MACHINES_RUN=$mark" && echo "${d#/proc/}"
  done
}

all_marked() {
  local d line
  for d in /proc/[0-9]*; do
    line="$({ tr '\0' '\n' < "$d/environ"; } 2>/dev/null | grep '^ROOTBORN_TWO_MACHINES_RUN=')" || continue
    [ -n "$line" ] && echo "${d#/proc/} ${line#ROOTBORN_TWO_MACHINES_RUN=} $(tr '\0' ' ' < "$d/cmdline" 2>/dev/null | cut -c1-120)"
  done
}

kill_marked() {
  local mark="$1" pids
  pids="$(marked_pids "$mark" | grep -vx "$$" || true)"
  [ -n "$pids" ] || return 0
  kill -TERM $pids 2>/dev/null || true
  for _ in $(seq 1 50); do
    pids="$(marked_pids "$mark" | grep -vx "$$" || true)"
    [ -n "$pids" ] || return 0
    sleep 0.2
  done
  kill -KILL $pids 2>/dev/null || true
}

case "$cmd" in
  lan-address)
    lan_address
    ;;

  client-address)
    a="$(awk '{print $1}' <<<"${SSH_CONNECTION:-}")"
    [ -n "$a" ] || { echo "SSH_CONNECTION이 없다" >&2; exit 2; }
    echo "$a"
    ;;

  home)
    pwd
    ;;

  server)
    run="$1"; share="${2:-}"; slot="${3:-room}"; sdir="${4:-server}"
    check_run_dir "$run"
    case "$sdir" in */*|*..*|"") echo "서버 폴더 이름이 이상하다: $sdir" >&2; exit 2 ;; esac
    [ -x "$run/$sdir/Rootborn.Server" ] || { echo "서버 실행 파일이 없다: $run/$sdir/Rootborn.Server" >&2; exit 2; }
    work="$(dirname "$(dirname "$run")")"
    base_env "$work"
    mark="$(basename "$run")"
    export ROOTBORN_TWO_MACHINES_RUN="$mark"
    bind="$(lan_address)" || exit 2
    mkdir -p "$run/saves" "$run/logs"
    log="$run/logs/server-$(date +%H%M%S)-$$.log"
    : > "$log"
    args=(--save-root "$run/saves" --slot "$slot" --bind "$bind" --port 0)
    [ -n "$share" ] && args+=(--share "$share")
    # 표준 입력의 줄을 서버 콘솔로 넘긴다. 입력이 닫히면 stop을 넣어 저장하고 끝내게 한다. 출력은 로그 파일 → tail로 이 연결에.
    # (서버는 콘솔 입력이 닫혀도 끝나지 않으므로 — 서비스용 — 여기서 stop을 넣는다.)
    # 배경 명령의 표준 입력은 /dev/null이 되므로 이 연결의 입력을 3번으로 넘겨 읽고, 서버 콘솔은 이름 붙은 관(fifo)으로 잇는다
    # (파이프로 이으면 서버가 끝나도 입력을 읽는 쪽이 이 연결이 닫힐 때까지 남아 끝을 알리지 못한다).
    exec 3<&0
    fifo="$run/logs/console-$$"
    rm -f "$fifo"; mkfifo "$fifo"
    timeout -s TERM "${TWO_MACHINES_LIMIT:-2h}" "$run/$sdir/Rootborn.Server" "${args[@]}" < "$fifo" >> "$log" 2>&1 &
    spid=$!
    { cat <&3; echo stop; } > "$fifo" 2>/dev/null &
    fpid=$!
    exec 3<&-
    tail -n +1 -f "$log" --pid="$spid" &
    tpid=$!
    wait "$spid"; code=$?
    sleep 0.3; kill "$tpid" 2>/dev/null; wait "$tpid" 2>/dev/null
    pkill -P "$fpid" 2>/dev/null; kill "$fpid" 2>/dev/null
    rm -f "$fifo"
    echo "@@server-exit $code"
    exit "$code"
    ;;

  peer)
    run="$1"
    check_run_dir "$run"
    work="$(dirname "$(dirname "$run")")"
    base_env "$work"
    mark="$(basename "$run")"
    export ROOTBORN_TWO_MACHINES_RUN="$mark"
    mkdir -p "$run/home" "$run/tmp" "$run/logs"
    export LD_LIBRARY_PATH="${ROOTBORN_TEST_XLIB:+$ROOTBORN_TEST_XLIB:}${LD_LIBRARY_PATH:-}"
    # 가상 화면: 원격 테스트(140~199)·배포 확인(200~219)과 겹치지 않는 범위에서 빈 번호. 떠 있는 화면은 건드리지 않는다.
    disp=""; xpid=""
    for d in $(seq "${TWO_MACHINES_DISPLAY_FIRST:-220}" "${TWO_MACHINES_DISPLAY_LAST:-239}"); do
      { [ -e "/tmp/.X$d-lock" ] || [ -e "/tmp/.X11-unix/X$d" ]; } && continue
      "$ROOTBORN_TEST_XVFB" ":$d" -screen 0 1920x1080x24 -nolisten tcp -noreset -xkbdir "$ROOTBORN_TEST_XKBDIR" > "$run/logs/xvfb-$d.log" 2>&1 < /dev/null &
      xpid=$!
      for _ in $(seq 1 50); do
        [ -e "/tmp/.X11-unix/X$d" ] && break
        kill -0 "$xpid" 2>/dev/null || break
        sleep 0.1
      done
      if kill -0 "$xpid" 2>/dev/null && [ -e "/tmp/.X11-unix/X$d" ]; then disp="$d"; break; fi
      xpid=""
    done
    [ -n "$disp" ] || { echo "@@err	빈 가상 화면 번호를 찾지 못했다"; exit 6; }
    trap '[ -n "$xpid" ] && kill "$xpid" 2>/dev/null' EXIT
    export DISPLAY=":$disp" LIBGL_ALWAYS_SOFTWARE=1 SDL_AUDIODRIVER=dummy
    export HOME="$PWD/$run/home" TMPDIR="$PWD/$run/tmp" XDG_CACHE_HOME="$PWD/$run/home/.cache" XDG_DATA_HOME="$PWD/$run/home/.local/share"
    export ROOTBORN_REPO_ROOT="$PWD/$run/client"
    unset ROOTBORN_SAVE_ROOT ROOTBORN_SAVE_SLOT ROOTBORN_JOIN ROOTBORN_HOST ROOTBORN_SHARE
    echo "[원격 게임] 가상 화면 :$disp, 루트 $run/client"
    # 표준 입력은 그대로 게임에(동사 줄). 닫히면 게임이 스스로 끝난다(PeerProgram).
    timeout -s TERM "${TWO_MACHINES_LIMIT:-2h}" "$ROOTBORN_TEST_DOTNET" "$run/peer/Rootborn.Host.Tests.dll" --two-games-peer
    exit $?
    ;;

  guest-files)
    run="$1"
    check_run_dir "$run"
    [ -d "$run/saves" ] && (cd "$run/saves" && find . -type f -printf '%P %s\n' | sort)
    ;;

  cleanup)
    run="$1"
    case "$run" in /*|*..*|"") echo "실행 폴더는 원격 홈 아래 상대 경로여야 한다: $run" >&2; exit 2 ;; esac
    mark="$(basename "$run")"
    kill_marked "$mark"
    rm -rf "$run"
    rmdir "$(dirname "$run")" 2>/dev/null || true   # 두 기계 확인 폴더가 비었으면 그것도
    left="$(marked_pids "$mark")"
    [ -z "$left" ] && [ ! -e "$run" ] && echo "지웠다: $run (남은 프로세스 없음)" || { echo "남았다: 프로세스 [$left], 폴더 $( [ -e "$run" ] && echo 있음 || echo 없음)"; exit 1; }
    ;;

  check)
    work="${1:-rootborn-test}"
    case "$work" in /*|*..*|"") echo "작업 폴더는 원격 홈 아래 상대 경로여야 한다: $work" >&2; exit 2 ;; esac
    echo "== 두 기계 확인 표시가 붙은 프로세스"
    p="$(all_marked)"; [ -n "$p" ] && echo "$p" || echo "(없음)"
    # 두 기계 확인이 띄우는 것: 서버(two-machines/ 아래 Rootborn.Server), 두 번째 게임(--two-games-peer), 그 가상 화면(220~239).
    # 다른 일(원격 테스트 runs/, VM, ComfyUI)은 여기에 잡히지 않는다.
    echo "== 두 기계 확인의 서버·게임·가상 화면(표시와 무관하게 이름으로)"
    pids=""
    for d in /proc/[0-9]*; do
      c="$(tr '\0' ' ' 2>/dev/null < "$d/cmdline")" || continue
      case "$c" in
        *two-machines/*Rootborn.Server*|*--two-games-peer*) pids="$pids ${d#/proc/}"; echo "${d#/proc/} $(cut -c1-140 <<<"$c")" ;;
        *Xvfb\ :2[23][0-9]\ *) pids="$pids ${d#/proc/}"; echo "${d#/proc/} $(cut -c1-140 <<<"$c")" ;;
      esac
    done
    [ -n "$pids" ] || echo "(없음)"
    echo "== 그 프로세스들이 듣는 포트"
    p=""
    for pid in $pids; do p="$p$(ss -H -ulnp 2>/dev/null | grep "pid=$pid,")$(ss -H -tlnp 2>/dev/null | grep "pid=$pid,")"; done
    [ -n "$p" ] && echo "$p" || echo "(없음)"
    echo "== $work/two-machines 아래 폴더"
    if [ -d "$work/two-machines" ]; then p="$(ls -A "$work/two-machines")"; [ -n "$p" ] && echo "$p" || echo "(비었다)"; else echo "(폴더 없음)"; fi
    ;;

  *)
    echo "쓰는 법: two-machines-remote.sh server|peer|lan-address|guest-files|cleanup|check ..." >&2
    exit 2
    ;;
esac
