#!/usr/bin/env bash
# Soft-gate: testing-discipline.md에 명시된 시나리오 ID들이
# Assets/Tests/PlayMode/Scenarios/ScenarioId.cs 또는 테스트 파일에서 참조되는지 검증.
# 농장 게임용 prefix: GEN/HEIR/TOOL/CROP/KNOW/STATUS/NET

set -euo pipefail

ROOT="$(git rev-parse --show-toplevel)"
cd "$ROOT"

DOC=".claude/rules/testing-discipline.md"
REGISTRY_CS="Assets/Tests/PlayMode/Scenarios/ScenarioId.cs"
TEST_DIRS=(
  "Assets/Tests/PlayMode/Scenarios"
  "Assets/Tests/PlayMode"
  "Assets/Tests/EditMode"
)

if [ ! -f "$DOC" ]; then
  echo "WARN: $DOC not found, skipping" >&2
  exit 0
fi

ids=$(grep -oE '\b(GEN|HEIR|TOOL|CROP|KNOW|STATUS|NET)-[0-9]{3}\b' "$DOC" | sort -u)
if [ -z "$ids" ]; then
  echo "WARN: no farmer scenario IDs found in $DOC (looking for GEN/HEIR/TOOL/CROP/KNOW/STATUS/NET-XXX)" >&2
  exit 0
fi

registered_ids=""
if [ -f "$REGISTRY_CS" ]; then
  registered_ids=$(grep -E '\b(GEN|HEIR|TOOL|CROP|KNOW|STATUS|NET)-[0-9]{3}\b' "$REGISTRY_CS" \
    | grep -v '\[PENDING' \
    | grep -oE '\b(GEN|HEIR|TOOL|CROP|KNOW|STATUS|NET)-[0-9]{3}\b' \
    | sort -u)
fi

test_referenced_ids=""
for d in "${TEST_DIRS[@]}"; do
  [ -d "$d" ] || continue
  found=$(grep -RhoE '\b(GEN|HEIR|TOOL|CROP|KNOW|STATUS|NET)[-_][0-9]{3}\b' "$d" 2>/dev/null \
    | tr '_' '-' \
    | sort -u || true)
  test_referenced_ids="$test_referenced_ids
$found"
done
test_referenced_ids=$(echo "$test_referenced_ids" | sort -u | grep -v '^$' || true)

missing=0
for id in $ids; do
  if echo "$registered_ids" | grep -qxF "$id"; then
    continue
  fi
  if echo "$test_referenced_ids" | grep -qxF "$id"; then
    continue
  fi
  echo "::warning::scenario $id is in $DOC but no test references it (and not [PENDING] in $REGISTRY_CS)"
  missing=$((missing + 1))
done

if [ "$missing" -gt 0 ]; then
  echo "NOTE: $missing scenario(s) doc-only — track in E expansion." >&2
fi

echo "OK: scenario registry check complete."
