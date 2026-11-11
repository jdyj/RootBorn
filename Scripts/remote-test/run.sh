#!/usr/bin/env bash
# 커밋 하나를 원격 Linux 장치(Strix Halo 등)로 보내 가상 화면(Xvfb)에서 dotnet 테스트를 돌리고 결과를 가져온다.
#
#   bash Scripts/remote-test/run.sh [<커밋 또는 브랜치>] [-- <dotnet test 인자>]
#   bash Scripts/remote-test/run.sh --setup        # 원격 준비(처음 한 번, 여러 번 돌려도 된다)
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

ssh_opts=(-i "$ROOTBORN_REMOTE_KEY" -o IdentitiesOnly=yes -o BatchMode=yes -o StrictHostKeyChecking=yes -o ServerAliveInterval=30)
target="$ROOTBORN_REMOTE_USER@$ROOTBORN_REMOTE_HOST"
remote() { ssh "${ssh_opts[@]}" "$target" "$@"; }

if [ "${1:-}" = "--setup" ]; then
  remote "bash -s -- $(printf '%q' "$DIR_NAME")" < "$here/setup-remote.sh"
  exit $?
fi

rev="HEAD"
if [ $# -gt 0 ] && [ "$1" != "--" ]; then rev="$1"; shift; fi
[ "${1:-}" = "--" ] && shift
sha="$(git -C "$repo" rev-parse --verify --quiet "$rev^{commit}")" || { echo "커밋을 찾지 못했다: $rev" >&2; exit 2; }
if [ "$rev" = "HEAD" ] && [ -n "$(git -C "$repo" status --porcelain --untracked-files=no)" ]; then
  echo "주의: 커밋하지 않은 변경은 원격 실행에 들어가지 않는다(HEAD $sha 그대로 돈다)." >&2
fi

run_id="$(date +%Y%m%d-%H%M%S)-${sha:0:9}-$$-$RANDOM"
out="$repo/output/remote-test/$run_id"
mkdir -p "$out"
echo "실행 $run_id: $(git -C "$repo" log --oneline -1 "$sha")"

# 1) 커밋을 원격 bare 저장소의 실행 전용 ref로 밀어 넣는다(origin에는 밀지 않는다). 푸시 안 된 브랜치도 된다.
ssh_cmd="ssh"; for o in "${ssh_opts[@]}"; do ssh_cmd="$ssh_cmd $(printf '%q' "$o")"; done
GIT_SSH_COMMAND="$ssh_cmd" git -C "$repo" push --quiet --no-verify "$target:$DIR_NAME/repo.git" "$sha:refs/runs/$run_id"

# 2) 원격에서 체크아웃 → 가상 화면 → 테스트. 원격 스크립트는 이 PC의 것을 표준 입력으로 넘긴다.
args="$(printf '%q ' "$DIR_NAME" "$run_id" "$sha" "$@")"
start=$(date +%s)
set +e
remote "bash -s -- $args" < "$here/remote-run.sh" 2>&1 | tee "$out/remote.log"
status=${PIPESTATUS[0]}
set -e
end=$(date +%s)

# 3) 원격 실행이 끝났다는 표시(.done)를 기다린 뒤 결과를 가져오고 원격 실행 폴더를 지운다.
#    연결만 끊겼으면 원격은 아직 돌고 있다. 원격의 연결 감시가 1분 안에 그 실행을 끝내므로 조금 기다린다.
res_q="$(printf '%q' "$DIR_NAME/runs/$run_id/results")"
done_mark=""
for _ in $(seq 1 12); do
  if remote "test -f $res_q/.done"; then done_mark=1; break; fi
  echo "원격 실행이 아직 끝나지 않았다. 10초 뒤 다시 본다." >&2
  sleep 10
done
if [ -n "$done_mark" ]; then
  remote "tar -C $res_q --exclude=./.done -cf - ." | tar -xf - -C "$out" || echo "결과를 가져오지 못했다" >&2
  remote "rm -rf $(printf '%q' "$DIR_NAME/runs/$run_id")" || true
else
  echo "원격 실행 $run_id가 끝나지 않아 결과를 가져오지 않고 원격 폴더도 지우지 않았다." >&2
  echo "끝난 뒤 가져오기: ssh … \"tar -C $DIR_NAME/runs/$run_id/results -cf - .\" | tar -xf - -C $out" >&2
  [ "$status" = 0 ] && status=1
fi

echo
echo "== 결과 ($((end - start))초, 원격 상태 $status) — $out"
[ -f "$out/run.txt" ] && cat "$out/run.txt"
[ -f "$out/summary.txt" ] && cat "$out/summary.txt"
[ -f "$out/refused.txt" ] && cat "$out/refused.txt"
shots=0
[ -d "$out/output" ] && shots=$(find "$out/output" -name '*.png' | wc -l)
echo "스크린샷 ${shots}장: $out/output"
exit "$status"
