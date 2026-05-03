#!/usr/bin/env bash
# 팀원 온보딩 스크립트.
set -eu
cd "$(git rev-parse --show-toplevel)"
git config core.hooksPath scripts/git-hooks
chmod +x scripts/git-hooks/pre-commit scripts/git-hooks/pre-push 2>/dev/null || true
echo "[install] core.hooksPath = $(git config --get core.hooksPath)"
echo "[install] git 훅이 scripts/git-hooks/ 로 설정되었습니다."
