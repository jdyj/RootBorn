#!/usr/bin/env bash
# Unity가 패키지로 읽는 Core 런타임(Packages/com.rootborn.core/Runtime)은 .cs와 폴더마다 .meta가 있어야 한다.
# (.meta가 없으면 Unity 판이 파일을 무시하거나 GUID를 새로 만든다.)
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/lib/common.sh"

CORE="Packages/com.rootborn.core/Runtime"
if [ ! -d "$CORE" ]; then
  echo "FAIL: $CORE 폴더가 없다" >&2
  exit 1
fi

violations=0
checked=0
while IFS= read -r p; do
  checked=$((checked + 1))
  if [ ! -f "$p.meta" ]; then
    echo "$p:1: .meta 파일이 없다 ($p.meta)"
    violations=$((violations + 1))
  fi
done < <(find "$CORE" \( -type d -o -name '*.cs' \) -not -path '*/obj/*' -not -path '*/bin/*' | sort)

if [ "$violations" -gt 0 ]; then
  echo "FAIL: .meta 없는 항목 ${violations}건." >&2
  exit 1
fi
echo "OK: Core 런타임의 .cs와 폴더 ${checked}개 모두 .meta가 있다."
