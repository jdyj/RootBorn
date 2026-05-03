#!/usr/bin/env bash
# 농장 게임 데이터-드리븐 원칙 강제 (.claude/rules/path-based/assets-data.md):
# 시스템 코드는 특정 엔티티(Crop/Tool/Knowledge/Trait/Generation) ID에 분기 금지.
#
# 허용: 테스트 코드 / Editor 스크립트 / Data SO 내부에서 ID 비교는 OK.
# 금지: 런타임 게임플레이/네트워크 코드.

set -euo pipefail

ROOT="$(git rev-parse --show-toplevel)"
cd "$ROOT"

SCAN_GLOBS=(
  "Assets/Scripts/Game"
  "Assets/Scripts/Network"
  "Assets/Scripts/UI"
)

PATTERNS=(
  'cropId\s*==\s*"'
  'crop\.id\s*==\s*"'
  '\.CropId\s*==\s*"'
  'switch\s*\(\s*crop(Id|\.id|Definition\.id)\s*\)'
  'enum\s+CropId\b'

  'toolId\s*==\s*"'
  'tool\.id\s*==\s*"'
  '\.ToolId\s*==\s*"'
  'switch\s*\(\s*tool(Id|\.id|Definition\.id)\s*\)'
  'enum\s+ToolId\b'

  'knowledgeId\s*==\s*"'
  'knowledge\.id\s*==\s*"'
  'switch\s*\(\s*knowledge(Id|\.id|Node\.id)\s*\)'
  'enum\s+KnowledgeId\b'

  'traitId\s*==\s*"'
  'trait\.id\s*==\s*"'
  'switch\s*\(\s*trait(Id|\.id)\s*\)'
  'enum\s+TraitId\b'
)

violations=0
for path in "${SCAN_GLOBS[@]}"; do
  [ -d "$path" ] || continue
  for pat in "${PATTERNS[@]}"; do
    if matches=$(grep -RInE \
        --include='*.cs' \
        --exclude-dir='Tests' \
        --exclude-dir='tests' \
        --exclude-dir='Editor' \
        "$pat" "$path" 2>/dev/null); then
      echo "::error::data-driven violation (pattern: $pat)"
      echo "$matches"
      echo
      violations=$((violations + 1))
    fi
  done
done

if [ "$violations" -gt 0 ]; then
  echo "FAIL: $violations data-driven principle violation(s) found." >&2
  echo "See .claude/rules/path-based/assets-data.md (엔티티 데이터-드리븐 절대 원칙)." >&2
  exit 1
fi

echo "OK: no entity-id branching in system code."
