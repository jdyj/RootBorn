#!/usr/bin/env bash
# 다국어 규칙(.claude/rules/localization.md): 화면 코드에 사람이 읽는 한글 문구를 적지 않는다. 문구는 현지화 키로 받는다.
# 대상: game/Rootborn.Screens 의 .cs (tests 제외). 주석은 보지 않는다.
# 예외: 예외 메시지(throw …, new …Exception(...))는 개발자용이라 보지 않는다.
# 지금 걸리는 것은 Scripts/ci/allowlists/hardcoded-text.txt 에 이유와 함께 고정한다.
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/lib/common.sh"

ALLOW="${TEXT_ALLOWLIST:-$CI_DIR/allowlists/hardcoded-text.txt}"
SCREENS="game/Rootborn.Screens"

mapfile -t files < <(cs_files "$SCREENS")
violations=0
allowed=0
if [ "${#files[@]}" -gt 0 ]; then
  # 주석을 지운 코드에서 한글이 든 줄만 본다(남은 한글은 문자열이나 이름이다).
  scanned="$(printf '%s\n' "${files[@]}" | cs_scan nocomment)"
  while IFS= read -r hit; do
    [ -n "$hit" ] || continue
    path="${hit%%:*}"
    rest="${hit#*:}"; text="${rest#*:}"
    case "$text" in *throw\ *|*Exception\(*) continue ;; esac
    if is_allowed "$ALLOW" "$path" "$text"; then
      allowed=$((allowed + 1))
      continue
    fi
    echo "$hit  <- 한글 문구를 코드에 적지 않는다(현지화 키를 쓴다)"
    violations=$((violations + 1))
  done < <(printf '%s\n' "$scanned" | LC_ALL=C.UTF-8 grep -P '[\x{AC00}-\x{D7A3}\x{3130}-\x{318F}]' || true)
fi

if [ "$violations" -gt 0 ]; then
  echo "FAIL: 한글 문구 리터럴 ${violations}건 (허용 목록 ${allowed}건 제외). .claude/rules/localization.md 참고." >&2
  exit 1
fi
echo "OK: 화면 코드에 한글 문구 리터럴이 없다 (${#files[@]}개 파일, 허용 목록 ${allowed}건)."
