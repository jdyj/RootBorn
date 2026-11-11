#!/usr/bin/env bash
# 검사 스크립트 자체 시험 도우미: 작은 임시 저장소 루트를 만들고 CI_ROOT로 스크립트를 돌려 종료 코드·출력을 본다.
CI_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
T_FAILS=0
T_COUNT=0
T_NAME="${T_NAME:-test}"

new_root() { cleanup; T_ROOT="$(mktemp -d)"; export CI_ROOT="$T_ROOT"; }
cleanup() { if [ -n "${T_ROOT:-}" ]; then rm -rf "$T_ROOT"; fi; return 0; }
trap cleanup EXIT

# put <상대경로> <내용> : 임시 루트에 파일을 만든다
put() { mkdir -p "$T_ROOT/$(dirname "$1")"; printf '%s\n' "$2" > "$T_ROOT/$1"; }

# run_check <스크립트 이름> [환경변수=값...] : OUT, CODE에 담는다
run_check() {
  local script="$1"; shift
  set +e
  OUT="$(env "$@" bash "$CI_DIR/$script" 2>&1)"
  CODE=$?
  set -e
}

# expect <이름> pass|fail [출력에 있어야 할 조각]
expect() {
  local name="$1" want="$2" frag="${3:-}" ok=1
  T_COUNT=$((T_COUNT + 1))
  if [ "$want" = pass ] && [ "$CODE" -ne 0 ]; then ok=0; fi
  if [ "$want" = fail ] && [ "$CODE" -ne 1 ]; then ok=0; fi
  if [ -n "$frag" ] && ! printf '%s' "$OUT" | grep -qF -- "$frag"; then ok=0; fi
  if [ "$ok" -eq 1 ]; then
    echo "  ok   $T_NAME: $name"
  else
    echo "  FAIL $T_NAME: $name (종료 코드 $CODE, 기대 $want ${frag:+/ 조각 '$frag'})"
    printf '%s\n' "$OUT" | sed 's/^/       | /'
    T_FAILS=$((T_FAILS + 1))
  fi
}

finish() {
  echo "$T_NAME: ${T_COUNT}개 중 실패 ${T_FAILS}개"
  [ "$T_FAILS" -eq 0 ]
}
