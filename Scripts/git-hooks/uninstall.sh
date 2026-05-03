#!/usr/bin/env bash
# 롤백 스크립트. P3 revert 전 반드시 먼저 실행.
set -eu
cd "$(git rev-parse --show-toplevel)"
if git config --get core.hooksPath >/dev/null 2>&1; then
  git config --unset core.hooksPath
  echo "[uninstall] core.hooksPath 제거 완료"
else
  echo "[uninstall] core.hooksPath 이미 없음"
fi
echo "[uninstall] 이제 P3 commit 을 git revert 로 되돌려도 안전합니다."
