#!/usr/bin/env bash
# 로컬에서 CI와 같은 검사를 한 번에 돌린다.
#   bash Scripts/ci/run-all.sh                 검사 스크립트 + dotnet 테스트 전부 (Host 테스트는 실제 창을 띄운다)
#   bash Scripts/ci/run-all.sh --checks-only   검사 스크립트만 (몇 초)
#   bash Scripts/ci/run-all.sh --skip-host     dotnet 테스트에서 Host 테스트(창)를 뺀다
# 하나가 실패해도 끝까지 돌리고, 마지막에 요약과 종료 코드(실패가 있으면 1)를 낸다.
set -uo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/lib/common.sh"

checks_only=0
skip_host=0
for a in "$@"; do
  case "$a" in
    --checks-only) checks_only=1 ;;
    --skip-host) skip_host=1 ;;
    *) echo "모르는 인자: $a" >&2; exit 2 ;;
  esac
done

export PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"

results=()
failed=0
run_step() { # run_step <이름> <명령...>
  local name="$1"; shift
  echo
  echo "=== $name"
  if "$@"; then
    results+=("통과  $name")
  else
    results+=("실패  $name")
    failed=$((failed + 1))
  fi
}

CHECKS=(core-references monogame-references no-entity-id-branching no-hardcoded-text localization-keys meta-files host-test-shards)

if [ "$checks_only" -eq 0 ]; then
  run_step "검사 스크립트 자체 시험" bash "$CI_DIR/tests/run-tests.sh"
fi
for c in "${CHECKS[@]}"; do
  run_step "check-$c" bash "$CI_DIR/check-$c.sh"
done

if [ "$checks_only" -eq 0 ]; then
  if ! command -v dotnet >/dev/null 2>&1; then
    echo "FAIL: dotnet이 없다 (PATH 또는 ~/.dotnet 확인)" >&2
    exit 2
  fi
  run_step "dotnet test Tools/Rootborn.Core.Tests" dotnet test Tools/Rootborn.Core.Tests
  if [ "$skip_host" -eq 1 ]; then
    while IFS= read -r proj; do
      run_step "dotnet test $proj" dotnet test "$proj"
    done < <(find game/tests -name '*.csproj' -not -path '*/obj/*' -not -name 'Rootborn.Host.Tests.csproj' | sort)
  else
    run_step "dotnet test game/Rootborn.sln (Host 테스트가 창을 띄운다)" dotnet test game/Rootborn.sln
  fi
fi

echo
echo "===== 요약"
printf '%s\n' "${results[@]}"
if [ "$failed" -gt 0 ]; then
  echo "실패 ${failed}건" >&2
  exit 1
fi
echo "모두 통과"
