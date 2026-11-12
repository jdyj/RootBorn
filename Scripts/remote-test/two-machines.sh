#!/usr/bin/env bash
# 같이 하기를 두 기계로 확인한다: 이 PC(Windows)의 게임과 원격 Linux 장치(Strix Halo)의 데디케이티드 서버·게임이 집 안 네트워크(LAN)로 만난다.
#
#   bash Scripts/remote-test/two-machines.sh                  # 준비 → 두 기계 테스트 전부(Category=TwoMachines) → 증거 받기 → 원격 뒷정리와 확인
#   bash Scripts/remote-test/two-machines.sh --filter <식>    # 그중 일부(dotnet test --filter 에 "&" 로 더한다. 예: "FullyQualifiedName~TWO_MACHINES_001")
#   bash Scripts/remote-test/two-machines.sh --no-publish     # output/publish 의 linux-x64·server-linux-x64 를 그대로 쓴다(이 스크립트가 이 커밋에서 만든 것일 때만 — 아니면 다시 만든다)
#   bash Scripts/remote-test/two-machines.sh --check          # 원격에 두 기계 확인이 남긴 것(프로세스·듣는 포트·폴더)만 본다
#
# 접속 값은 Scripts/remote-test 와 같은 환경 변수로만 받는다(저장소에 적지 않는다): ROOTBORN_REMOTE_HOST·USER·KEY, 작업 폴더 ROOTBORN_REMOTE_DIR(기본 rootborn-test).
# 값이 없으면 아무것도 하지 않고 건너뛰었다고 알린다(종료 코드 0). CI에는 넣지 않는다(두 기계가 필요하다).
# 원격에서는 <작업 폴더>/two-machines/<실행 id>/ 안에만 쓰고 sudo 를 쓰지 않는다. 서버는 원격 장치의 LAN 주소 하나에, 테스트하는 동안만 듣는다.
# 끝나면(실패·중단 포함) 이 실행이 띄운 원격 프로세스를 끝내고 폴더를 지운 뒤, 남은 것이 없는지 확인한 출력을 보인다.
# 다른 커밋의 서버(버전 거절 확인): TWO_MACHINES_OTHER_COMMIT(기본 HEAD~1)의 소스를 git archive 로 저장소 밖에 풀어 게시한다.
# 결과: output/two-machines/<실행 id>/ — trx, 이 PC 스크린샷(two-machines-*.png), 원격 스크린샷(remote-evidence/), 재기 표(two-machines-measurements.md), check.txt.
# 자세한 것은 Scripts/remote-test/README.md "두 기계로 같이 하기".
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo="$(git -C "$here" rev-parse --show-toplevel)"
cd "$repo"

filter=""; publish=1; check_only=0
while [ $# -gt 0 ]; do
  case "$1" in
    --filter) filter="$2"; shift 2 ;;
    --no-publish) publish=0; shift ;;
    --check) check_only=1; shift ;;
    *) echo "모르는 인자: $1" >&2; exit 2 ;;
  esac
done

missing=""
for v in ROOTBORN_REMOTE_HOST ROOTBORN_REMOTE_USER ROOTBORN_REMOTE_KEY; do [ -n "${!v:-}" ] || missing="$missing $v"; done
if [ -n "$missing" ]; then
  echo "두 기계 확인을 건너뛰었다: 원격 접속 정보가 없다:$missing (쓰는 법: Scripts/remote-test/README.md)"
  exit 0
fi
[ -f "$ROOTBORN_REMOTE_KEY" ] || { echo "ROOTBORN_REMOTE_KEY 파일이 없다: $ROOTBORN_REMOTE_KEY" >&2; exit 2; }
DIR_NAME="${ROOTBORN_REMOTE_DIR:-rootborn-test}"
case "$DIR_NAME" in /*|*..*|"") echo "ROOTBORN_REMOTE_DIR는 원격 홈 아래 상대 경로여야 한다: $DIR_NAME" >&2; exit 2 ;; esac

ssh_opts=(-i "$ROOTBORN_REMOTE_KEY" -o IdentitiesOnly=yes -o BatchMode=yes -o StrictHostKeyChecking=yes -o ServerAliveInterval=30 -o ConnectTimeout=20)
target="$ROOTBORN_REMOTE_USER@$ROOTBORN_REMOTE_HOST"
remote() { ssh "${ssh_opts[@]}" "$target" "$@"; }
q() { printf '%q' "$1"; }
helper_check() { remote "bash -s -- check $(q "$DIR_NAME")" < "$here/two-machines-remote.sh"; }

if [ "$check_only" = 1 ]; then helper_check; exit $?; fi

export PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}" MSBUILDDISABLENODEREUSE=1 DOTNET_CLI_USE_MSBUILD_SERVER=0
remote "test -f $(q "$DIR_NAME/env.sh")" || { echo "원격에 $DIR_NAME/env.sh 가 없다(먼저 bash Scripts/remote-test/run.sh --setup)" >&2; exit 2; }

id="$(date +%Y%m%d-%H%M%S)-$RANDOM"
run="$DIR_NAME/two-machines/$id"
out="$repo/output/two-machines/$id"
mkdir -p "$out"
start=$(date +%s)
echo "== 두 기계 확인 $id (원격 ~/$run, 결과 $out)"
[ -z "$(git status --porcelain --untracked-files=no)" ] || echo "  경고: 커밋하지 않은 변경이 있다 — 버전은 커밋 해시로 찍혀 같은 커밋의 다른 수정은 가려내지 못한다"
echo "  커밋 $(git log --oneline -1)"

cleanup() {
  local code=$?
  trap - EXIT INT TERM
  echo "== 원격 뒷정리"
  remote "bash $(q "$run/bin/two-machines-remote.sh") cleanup $(q "$run")" 2>&1 | sed 's/^/  /' \
    || remote "rm -rf $(q "$run")" || true
  echo "== 원격에 남은 것 확인"
  helper_check 2>&1 | tee "$out/check.txt" | sed 's/^/  /'
  echo "== 끝: $(( $(date +%s) - start ))초, 결과 $out (종료 코드 $code)"
  exit "$code"
}
trap cleanup EXIT
trap 'exit 130' INT TERM

# ---- 1. 배포 폴더(Linux 서버·클라이언트), 다른 커밋의 서버, 테스트 어셈블리 ----
# 방은 같은 커밋에서 만든 것끼리만 받는다 — --no-publish 라도 지난 게시가 이 커밋의 것이 아니면 다시 만든다.
stamp=output/publish/.two-machines-commit
head="$(git rev-parse HEAD)"
if [ "$publish" = 0 ] && [ "$(cat "$stamp" 2>/dev/null)" != "$head" ]; then
  echo "  지난 게시가 이 커밋($head)의 것이 아니다 — 다시 게시한다"; publish=1
fi
if [ "$publish" = 1 ] || [ ! -d output/publish/linux-x64 ] || [ ! -d output/publish/server-linux-x64 ]; then
  echo "== 게시: linux-x64, server-linux-x64"
  rm -f "$stamp"
  bash Tools/publish/publish.sh linux-x64 > "$out/publish-client.log" 2>&1 || { tail -20 "$out/publish-client.log" >&2; exit 1; }
  bash Tools/publish/publish.sh --server linux-x64 > "$out/publish-server.log" 2>&1 || { tail -20 "$out/publish-server.log" >&2; exit 1; }
  echo "$head" > "$stamp"
fi
other_ref="${TWO_MACHINES_OTHER_COMMIT:-HEAD~1}"
other="$(git rev-parse --verify --quiet "$other_ref^{commit}" || true)"
other_dir=""
if [ -n "$other" ] && [ "$other" != "$(git rev-parse HEAD)" ]; then
  echo "== 다른 커밋의 서버: $(git log --oneline -1 "$other")"
  # 저장소 밖에 풀어야 그 커밋 해시가 찍힌다(저장소 안이면 소스 링크가 이 저장소의 HEAD를 찍는다).
  other_src="$(mktemp -d)"
  git archive "$other" | tar -x -C "$other_src"
  if (cd "$other_src" && ROOTBORN_COMMIT="$other" bash Tools/publish/publish.sh --server linux-x64) > "$out/publish-other.log" 2>&1; then
    other_dir="$other_src/output/publish/server-linux-x64"
  else
    echo "  다른 커밋의 서버를 만들지 못했다 — 버전 거절 확인은 건너뛴다(로그 $out/publish-other.log)"
  fi
else
  echo "== 다른 커밋($other_ref)이 없거나 HEAD와 같다 — 버전 거절 확인은 건너뛴다"
fi
echo "== 테스트 어셈블리 빌드(Release)"
dotnet build game/tests/Rootborn.Host.Tests -c Release -nodeReuse:false -p:UseSharedCompilation=false -v q > "$out/build.log" 2>&1 || { tail -20 "$out/build.log" >&2; exit 1; }
bin="game/tests/Rootborn.Host.Tests/bin/Release/net8.0"

# ---- 2. 원격에 올리기 ----
echo "== 원격에 올리기"
remote "mkdir -p $(q "$run/bin")"
tar -C "$here" -cf - two-machines-remote.sh | remote "tar -xf - -C $(q "$run/bin")"
send() { # <이 PC 폴더> <원격 이름>
  remote "mkdir -p $(q "$run/$2")" && tar -C "$1" -cf - . | remote "tar -xf - -C $(q "$run/$2")"
}
send "$bin" peer
send output/publish/linux-x64 client
send output/publish/server-linux-x64 server
remote "chmod +x $(q "$run/server/Rootborn.Server") $(q "$run/client/Rootborn.Host")"
if [ -n "$other_dir" ]; then
  send "$other_dir" server-other && remote "chmod +x $(q "$run/server-other/Rootborn.Server")"
  rm -rf "$other_src"
fi
remote "cd $(q "$run") && du -sh *" | sed 's/^/  /'

# ---- 3. 테스트 ----
expr="Category=TwoMachines"; [ -n "$filter" ] && expr="$expr&($filter)"
echo "== 테스트: $expr"
rm -f output/two-games-evidence/two-machines-* 2>/dev/null || true
set +e
ROOTBORN_TWO_MACHINES_RUN="$run" ROOTBORN_TWO_MACHINES_OTHER_COMMIT="${other_dir:+$other}" \
  dotnet test game/tests/Rootborn.Host.Tests -c Release --no-build --filter "$expr" \
  --logger "trx;LogFileName=two-machines.trx" --logger "console;verbosity=normal" --results-directory "$out" > "$out/test.log" 2>&1
tcode=$?
set -e
grep -E '^\s+(통과|실패|건너뜀) |^\s+(Passed|Failed|Skipped) |총 테스트|통과:|실패:|건너뜀:' "$out/test.log" | sed 's/^/  /' || true
grep -A3 '오류 메시지' "$out/test.log" | sed 's/^/  /' | head -40 || true

# ---- 4. 증거 받기 ----
cp output/two-games-evidence/two-machines-* "$out/" 2>/dev/null || true
mkdir -p "$out/remote-evidence"
remote "test -d $(q "$run/evidence") && tar -C $(q "$run/evidence") -cf - . || true" | tar -xf - -C "$out/remote-evidence" 2>/dev/null || true
remote "test -d $(q "$run/logs") && tar -C $(q "$run/logs") --exclude='console-*' -cf - . || true" | tar -xf - -C "$out/remote-evidence" 2>/dev/null || true
[ -f "$out/two-machines-measurements.md" ] && { echo "== 재기"; sed 's/^/  /' "$out/two-machines-measurements.md"; }
exit "$tcode"
