#!/usr/bin/env bash
# 공용 도구 cs_scan.py(주석·문자열 분리)의 시험.
set -euo pipefail
T_NAME=cs-scan
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
new_root
source "$CI_DIR/lib/common.sh"   # PY 불러오기(ROOT는 CI_ROOT)

scan() { printf '%s\n' "f.cs" |"$PY" "$CI_DIR/lib/cs_scan.py" "$1" -; }
# check <이름> <모드> <있음|없음> <조각>
check() {
  local name="$1" mode="$2" want="$3" frag="$4" out
  T_COUNT=$((T_COUNT + 1))
  out="$(scan "$mode")"
  if { [ "$want" = 있음 ] && printf '%s' "$out" | grep -qF -- "$frag"; } || { [ "$want" = 없음 ] && ! printf '%s' "$out" | grep -qF -- "$frag"; }; then
    echo "  ok   $T_NAME: $name"
  else
    echo "  FAIL $T_NAME: $name"; printf '%s\n' "$out" | sed 's/^/       | /'; T_FAILS=$((T_FAILS + 1))
  fi
}

cat > "$T_ROOT/f.cs" <<'CSEOF'
int a; // 줄 주석 AAA
int b; /* 블록
주석 BBB */ int c;
string s = "// not comment CCC";
string v = @"a ""q"" DDD
next";
string i = $"x{1} EEE";
char q = '"'; int z; // FFF
CSEOF
check "줄 주석 제거" code 없음 AAA
check "블록 주석 제거(여러 줄)" code 없음 BBB
check "code 모드는 문자열 내용을 비운다" code 없음 CCC
check "nocomment 모드는 문자열 안의 // 를 유지" nocomment 있음 "not comment CCC"
check "verbatim 문자열(이중 따옴표, 여러 줄)을 한 덩어리로" nocomment 있음 DDD
check "보간 문자열 인식" nocomment 있음 EEE
check "문자 리터럴 뒤의 주석 제거" code 없음 FFF
check "문자 리터럴 뒤의 코드는 남는다" code 있음 "int z;"
check "줄 번호가 원본과 같다" code 있음 "f.cs:3: int c;"
finish
