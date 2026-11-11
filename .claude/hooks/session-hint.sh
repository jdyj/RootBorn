#!/usr/bin/env bash
# 한국어 요약: UserPromptSubmit 훅. 세션당 1회만 현재 브랜치와 마지막 커밋을 보인다.
# 플래그 파일: .claude/_workspace/.session-hint-shown (gitignored)

set -eu

PROJECT_ROOT="${CLAUDE_PROJECT_DIR:-$(git rev-parse --show-toplevel 2>/dev/null || pwd)}"
WS_DIR="$PROJECT_ROOT/.claude/_workspace"
FLAG="$WS_DIR/.session-hint-shown"

# 이미 이 세션에서 출력했으면 종료
if [[ -f "$FLAG" ]]; then
  exit 0
fi

mkdir -p "$WS_DIR"
touch "$FLAG"

BRANCH=$(cd "$PROJECT_ROOT" && git rev-parse --abbrev-ref HEAD 2>/dev/null || echo "?")
LAST=$(cd "$PROJECT_ROOT" && git log -1 --pretty=format:'%h %s' 2>/dev/null || echo "-")

echo "[session-hint] branch=$BRANCH  last=$LAST"

exit 0
