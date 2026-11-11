#!/usr/bin/env bash
# Core 규칙(game/Rootborn.Core/src)은 엔진·파일·JSON 구현에 기대지 않는다.
# 금지: UnityEngine, System.Text.Json, System.IO (using과 완전한 이름 모두). 주석·문자열 안은 보지 않는다.
# (.claude/rules/core-logic-and-play-evidence.md, coding-standards.md)
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/lib/common.sh"

CORE="game/Rootborn.Core/src"
violations=0

if [ ! -d "$CORE" ]; then
  echo "FAIL: $CORE 폴더가 없다" >&2
  exit 1
fi

# 폴더 이름이 tests/Tests인 경로는 cs_files가 빼므로 Core 안에는 그런 폴더를 두지 않는다고 본다.
mapfile -t files < <(cs_files "$CORE")
if [ "${#files[@]}" -gt 0 ]; then
  scanned="$(printf '%s\n' "${files[@]}" | cs_scan code)"
  while IFS= read -r hit; do
    [ -n "$hit" ] || continue
    echo "$hit  <- Core가 참조하면 안 되는 이름"
    violations=$((violations + 1))
  done < <(printf '%s\n' "$scanned" | grep -E '(^|[^A-Za-z0-9_.])(UnityEngine|UnityEditor|System\.Text\.Json|System\.IO)([^A-Za-z0-9_]|$)' || true)
fi

if [ "$violations" -gt 0 ]; then
  echo "FAIL: Core 참조 위반 ${violations}건." >&2
  exit 1
fi
echo "OK: Core 런타임에 UnityEngine/System.Text.Json/System.IO 참조가 없다 (${#files[@]}개 파일)."
