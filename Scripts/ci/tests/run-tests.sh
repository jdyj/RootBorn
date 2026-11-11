#!/usr/bin/env bash
# 검사 스크립트 자체 시험을 모두 돌린다(작은 임시 폴더에서 통과·위반 예시로).
set -uo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"
failed=0
for t in test-*.sh; do
  bash "$t" || failed=$((failed + 1))
done
if [ "$failed" -gt 0 ]; then
  echo "FAIL: 자체 시험 파일 ${failed}개 실패" >&2
  exit 1
fi
echo "OK: 검사 스크립트 자체 시험 통과"
