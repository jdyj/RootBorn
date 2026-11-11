#!/usr/bin/env bash
# MonoGame(Microsoft.Xna / MonoGame.Framework) 참조는 game/Rootborn.Host와 game/tests/Rootborn.Host.Tests에만 둔다.
# csproj(PackageReference/ProjectReference)와 .cs(using, 완전한 이름) 모두 본다. (.claude/rules/engine-migration.md)
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/lib/common.sh"

violations=0
# 소스 폴더만 본다. 저장소 전체를 훑으면 git 밖 폴더(output/의 스파이크 코드, Unity Library 등)가 걸리고 몇 분이 걸린다.
SOURCE_DIRS=(game Tools)
is_host_path() {
  case "$1" in
    game/Rootborn.Host/*|game/tests/Rootborn.Host.Tests/*) return 0 ;;
  esac
  return 1
}

# 1) csproj/props/targets 안의 참조
while IFS= read -r f; do
  f="${f#./}"
  is_host_path "$f" && continue
  while IFS=: read -r ln text; do
    echo "$f:$ln:$text  <- MonoGame 참조는 Rootborn.Host(와 그 테스트)에만"
    violations=$((violations + 1))
  done < <(grep -nE 'MonoGame|Microsoft\.Xna' "$f" || true)
done < <(find "${SOURCE_DIRS[@]}" \( -name '*.csproj' -o -name '*.props' -o -name '*.targets' \) -not -path '*/obj/*' -not -path '*/bin/*' | sort)

# 2) .cs 안의 using/완전한 이름 (주석·문자열은 제외)
mapfile -t files < <(find "${SOURCE_DIRS[@]}" -name '*.cs' -not -path '*/obj/*' -not -path '*/bin/*' | sort)
outside=()
for f in "${files[@]}"; do
  is_host_path "$f" || outside+=("$f")
done
if [ "${#outside[@]}" -gt 0 ]; then
  scanned="$(printf '%s\n' "${outside[@]}" | cs_scan code)"
  while IFS= read -r hit; do
    [ -n "$hit" ] || continue
    echo "$hit  <- MonoGame 참조는 Rootborn.Host(와 그 테스트)에만"
    violations=$((violations + 1))
  done < <(printf '%s\n' "$scanned" | grep -E '(^|[^A-Za-z0-9_.])(Microsoft\.Xna|MonoGame)(\.|[^A-Za-z0-9_]|$)' || true)
fi

if [ "$violations" -gt 0 ]; then
  echo "FAIL: MonoGame 참조 위반 ${violations}건." >&2
  exit 1
fi
echo "OK: MonoGame 참조가 Rootborn.Host와 Rootborn.Host.Tests 밖에 없다."
