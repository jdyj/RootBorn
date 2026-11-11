#!/usr/bin/env bash
# 커밋 하나를 원격 Linux 장치(Strix Halo 등)로 보내 가상 화면(Xvfb)에서 dotnet 테스트를 돌리고 결과를 가져온다.
# 원격 실행은 연결에서 떼어 돈다: 이 PC 쪽이 끊기거나 꺼져도 끝까지 가고, 나중에 --resume으로 받는다.
#
#   bash Scripts/remote-test/run.sh [--no-wait] [--force] [<커밋 또는 브랜치>] [-- <dotnet test 인자>]
#   bash Scripts/remote-test/run.sh --resume [<실행 id>]   # 이어서 기다리고 받기(없으면 이 작업 폴더의 가장 최근 안 받은 실행)
#   bash Scripts/remote-test/run.sh --status               # 원격에서 도는 실행과 안 받아 간 실행
#   bash Scripts/remote-test/run.sh --cancel <실행 id>     # 도는 실행을 멈추고 거기까지의 결과를 받는다
#   bash Scripts/remote-test/run.sh --setup                # 원격 준비(처음 한 번, 여러 번 돌려도 된다)
#
# 접속 정보는 환경 변수로만 받는다(저장소에 적지 않는다): ROOTBORN_REMOTE_HOST, ROOTBORN_REMOTE_USER, ROOTBORN_REMOTE_KEY.
# 원격 작업 폴더는 ROOTBORN_REMOTE_DIR(원격 홈 기준 상대 경로, 기본 rootborn-test). 결과는 output/remote-test/<실행 id>/.
# 자세한 것은 Scripts/remote-test/README.md.
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo="$(git -C "$here" rev-parse --show-toplevel)"

missing=""
for v in ROOTBORN_REMOTE_HOST ROOTBORN_REMOTE_USER ROOTBORN_REMOTE_KEY; do [ -n "${!v:-}" ] || missing="$missing $v"; done
if [ -n "$missing" ]; then
  echo "원격 접속 정보가 없다:$missing" >&2
  echo "환경 변수로 준다(값은 저장소에 적지 않는다). 쓰는 법: Scripts/remote-test/README.md" >&2
  exit 2
fi
[ -f "$ROOTBORN_REMOTE_KEY" ] || { echo "ROOTBORN_REMOTE_KEY 파일이 없다: $ROOTBORN_REMOTE_KEY" >&2; exit 2; }
DIR_NAME="${ROOTBORN_REMOTE_DIR:-rootborn-test}"
case "$DIR_NAME" in /*|*..*|"") echo "ROOTBORN_REMOTE_DIR는 원격 홈 아래 상대 경로여야 한다: $DIR_NAME" >&2; exit 2 ;; esac

ssh_opts=(-i "$ROOTBORN_REMOTE_KEY" -o IdentitiesOnly=yes -o BatchMode=yes -o StrictHostKeyChecking=yes -o ServerAliveInterval=30 -o ConnectTimeout=20)
target="$ROOTBORN_REMOTE_USER@$ROOTBORN_REMOTE_HOST"
remote() { ssh "${ssh_opts[@]}" "$target" "$@"; }

if [ "${1:-}" = "--setup" ]; then
  remote "bash -s -- $(printf '%q' "$DIR_NAME")" < "$here/setup-remote.sh"
  exit $?
fi

q() { printf '%q' "$1"; }
DIR_Q="$(q "$DIR_NAME")"
ctl() { remote "bash -s -- $DIR_Q $(printf '%q ' "$@")" < "$here/remote-ctl.sh"; }
# 결과를 받아 갈 쪽 = 이 작업 폴더. 원격은 받을 쪽이 모두 받아 가야 실행 폴더를 지운다.
client="$(printf '%s' "$repo" | sha1sum | cut -c1-12)"
client_name="$(basename "$repo")"
local_root="$repo/output/remote-test"

# 원격 실행 하나를 끝날 때까지 기다린 뒤 결과를 받고(원격 폴더는 받을 쪽이 다 받으면 지운다) 요약을 보인다.
wait_and_fetch() {
  local id="$1" out="$local_root/$1" st="" last_shown="" fails=0 line state elapsed rexit progress lastlog
  mkdir -p "$out"; echo "$id" > "$out/run-id"
  trap 'echo; echo "기다리기를 멈췄다. 원격은 계속 돈다 — 이어 받기: bash Scripts/remote-test/run.sh --resume $id" >&2; exit 130' INT TERM
  while :; do
    if ! line="$(ctl state "$id" 2>&1)"; then
      fails=$((fails + 1))
      echo "원격 상태를 읽지 못했다($fails/10): $(echo "$line" | tail -1)" >&2
      if [ "$fails" -ge 10 ]; then echo "연결이 안 된다. 원격은 계속 돈다 — 나중에: bash Scripts/remote-test/run.sh --resume $id" >&2; exit 1; fi
      sleep 30; continue
    fi
    fails=0
    state="$(sed -n 's/^state=//p' <<<"$line")"; elapsed="$(sed -n 's/^elapsed=//p' <<<"$line")"
    rexit="$(sed -n 's/^exit=//p' <<<"$line")"; progress="$(sed -n 's/^progress=//p' <<<"$line")"
    lastlog="$(sed -n 's/^last=//p' <<<"$line")"
    case "$state" in
      done) break ;;
      missing) echo "원격에 실행 $id가 없다(이미 받아 갔거나 지워졌다)." >&2; exit 1 ;;
      dead)
        echo "원격 실행이 정리 없이 죽었다. 남은 프로세스를 끝내고 거기까지의 결과를 받는다." >&2
        ctl cancel "$id" || exit 1 ;;
      *)
        st="${state} ${progress} | ${lastlog}"
        if [ "$st" != "$last_shown" ]; then
          echo "[$(date +%H:%M:%S)] ${state/running/도는 중}${elapsed:+ ${elapsed}초}${progress:+ — 끝난 프로젝트: $progress}${lastlog:+ — $lastlog}"
          last_shown="$st"
        fi
        sleep 20 ;;
    esac
  done
  trap - INT TERM
  if ! remote "tar -C $(q "$DIR_NAME/runs/$id/results") --exclude=./.done -cf - ." | tar -xf - -C "$out"; then
    echo "결과를 가져오지 못했다. 원격 폴더는 남겼다 — 다시: bash Scripts/remote-test/run.sh --resume $id" >&2; exit 1
  fi
  date +%Y-%m-%dT%H:%M:%S > "$out/fetched"
  ctl release "$id" "$client" || true
  local status="${rexit:-1}"
  [ -f "$out/exit-code" ] && status="$(cat "$out/exit-code")"
  echo
  echo "== 결과 (원격 ${elapsed:-?}초, 원격 상태 $status) — $out"
  [ -f "$out/run.txt" ] && cat "$out/run.txt"
  [ -f "$out/summary.txt" ] && cat "$out/summary.txt"
  [ -f "$out/refused.txt" ] && cat "$out/refused.txt"
  [ -f "$out/cancelled" ] && echo "(취소된 실행 — 거기까지의 결과다)"
  local shots=0
  [ -d "$out/output" ] && shots=$(find "$out/output" -name '*.png' | wc -l)
  echo "스크린샷 ${shots}장: $out/output"
  exit "$status"
}

# 이 작업 폴더가 시작했거나 붙었는데 아직 받지 않은 실행(새 것부터).
pending_ids() {
  local d
  for d in $(ls -1d "$local_root"/*/ 2>/dev/null | sort -r); do
    [ -f "$d/run-id" ] && [ ! -f "$d/fetched" ] && cat "$d/run-id"
  done
}

case "${1:-}" in
  --status)
    ctl status
    p="$(pending_ids | tr '\n' ' ')"
    echo; echo "이 작업 폴더가 받을 실행: ${p:-(없음)}"
    exit 0 ;;
  --resume)
    id="${2:-}"
    if [ -z "$id" ]; then
      id="$(pending_ids | head -1)"
      [ -n "$id" ] || { echo "이 작업 폴더에 아직 받지 않은 실행이 없다." >&2; exit 1; }
    fi
    case "$id" in *[!A-Za-z0-9._-]*) echo "실행 id가 아니다: $id" >&2; exit 2 ;; esac
    echo "실행 $id 이어 받기"
    ctl claim "$id" "$client" "$client_name" || true
    wait_and_fetch "$id" ;;
  --cancel)
    id="${2:?취소할 실행 id를 준다}"
    case "$id" in *[!A-Za-z0-9._-]*) echo "실행 id가 아니다: $id" >&2; exit 2 ;; esac
    ctl cancel "$id" || exit 1
    wait_and_fetch "$id" ;;
esac

no_wait=""; force=0
while :; do
  case "${1:-}" in
    --no-wait) no_wait=1; shift ;;
    --force) force=1; shift ;;
    *) break ;;
  esac
done
rev="HEAD"
if [ $# -gt 0 ] && [ "$1" != "--" ]; then rev="$1"; shift; fi
[ "${1:-}" = "--" ] && shift
sha="$(git -C "$repo" rev-parse --verify --quiet "$rev^{commit}")" || { echo "커밋을 찾지 못했다: $rev" >&2; exit 2; }
if [ "$rev" = "HEAD" ] && [ -n "$(git -C "$repo" status --porcelain --untracked-files=no)" ]; then
  echo "주의: 커밋하지 않은 변경은 원격 실행에 들어가지 않는다(HEAD $sha 그대로 돈다)." >&2
fi

run_id="$(date +%Y%m%d-%H%M%S)-${sha:0:9}-$$-$RANDOM"
echo "커밋: $(git -C "$repo" log --oneline -1 "$sha")"

# 1) 커밋을 원격 bare 저장소의 실행 전용 ref로 밀어 넣는다(origin에는 밀지 않는다). 푸시 안 된 브랜치도 된다.
ssh_cmd="ssh"; for o in "${ssh_opts[@]}"; do ssh_cmd="$ssh_cmd $(q "$o")"; done
GIT_SSH_COMMAND="$ssh_cmd" git -C "$repo" push --quiet --no-verify "$target:$DIR_NAME/repo.git" "$sha:refs/runs/$run_id"

# 2) 스크립트를 실행 폴더에 풀고, 연결에서 뗀 원격 실행을 시작한다. 같은 커밋·같은 인자의 실행이 있으면 거기에 붙는다.
bin="$DIR_NAME/runs/$run_id/bin"
reply="$(tar -C "$here" -cf - remote-run.sh remote-ctl.sh | remote "mkdir -p $(q "$bin") && tar -xf - -C $(q "$bin") && bash $(q "$bin/remote-ctl.sh") $DIR_Q start $(printf '%q ' "$run_id" "$sha" "$client" "$client_name" "$force" "$@")")" \
  || { echo "$reply" >&2; echo "원격 실행을 시작하지 못했다." >&2; exit 1; }
grep -v '^STARTED \|^ATTACH ' <<<"$reply" || true
if attach="$(sed -n 's/^ATTACH //p' <<<"$reply")" && [ -n "$attach" ]; then
  run_id="${attach%% *}"
  echo "같은 커밋·같은 인자의 실행이 이미 있어 거기에 붙는다(${attach#* }). 새로 돌리려면 --force."
fi
mkdir -p "$local_root/$run_id"; echo "$run_id" > "$local_root/$run_id/run-id"
echo "실행 id: $run_id"
echo "이어 받기: bash Scripts/remote-test/run.sh --resume $run_id"
[ -n "$no_wait" ] && exit 0

# 3) 끝날 때까지 가볍게 기다리며 진행을 보이고 결과를 받는다. 여기서 끊기거나 꺼져도 원격은 계속 돈다.
wait_and_fetch "$run_id"
