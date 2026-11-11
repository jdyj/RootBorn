#!/usr/bin/env bash
# 검사 스크립트 공용 도우미. source로 불러 쓴다.
#   CI_ROOT : 검사할 저장소 루트(자체 시험이 임시 폴더로 바꾼다). 없으면 git 루트.

CI_LIB_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CI_DIR="$(dirname "$CI_LIB_DIR")"
ROOT="${CI_ROOT:-$(git -C "$CI_DIR" rev-parse --show-toplevel)}"
cd "$ROOT"

# 파이썬 실행 파일 이름(리눅스는 python3, 윈도우 쪽은 python일 수 있다)
if command -v python3 >/dev/null 2>&1 && python3 -c 'import sys' >/dev/null 2>&1; then
  PY=python3
elif command -v python >/dev/null 2>&1; then
  PY=python
else
  echo "FAIL: python3가 필요하다" >&2
  exit 2
fi
export PYTHONUTF8=1

# 폴더 아래 .cs 파일 목록(obj/bin 제외). 인자: 폴더들. 제외할 경로 조각은 CS_EXCLUDE(공백 구분 -path 패턴).
cs_files() {
  local d
  for d in "$@"; do
    [ -d "$d" ] || continue
    find "$d" -name '*.cs' -not -path '*/obj/*' -not -path '*/bin/*' -not -path '*/tests/*' -not -path '*/Tests/*' | sort
  done
}

# 허용 목록 파일에서 주석·빈 줄을 뺀 항목을 낸다. 형식: <경로>|<코드에 들어 있는 조각>|<이유>
allowlist_entries() {
  [ -f "$1" ] || return 0
  grep -vE '^\s*(#|$)' "$1" || true
}

# 허용 목록에 있는지: allowed <목록파일> <경로> <줄 내용>
is_allowed() {
  local list="$1" path="$2" text="$3" entry p frag
  while IFS= read -r entry; do
    p="${entry%%|*}"
    frag="${entry#*|}"; frag="${frag%%|*}"
    [ "$p" = "$path" ] || continue
    case "$text" in *"$frag"*) return 0 ;; esac
  done < <(allowlist_entries "$list")
  return 1
}

# 파일 목록(한 줄에 하나)을 표준입력으로 받아 cs_scan.py를 돌린다. 실패하면 종료한다(검사가 조용히 통과하지 않게).
# 쓰기: scanned="$(printf '%s\n' "${files[@]}" | cs_scan code)"
cs_scan() {
  "$PY" "$CI_LIB_DIR/cs_scan.py" "$1" -
}
