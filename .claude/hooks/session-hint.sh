#!/usr/bin/env bash
# 한국어 요약: UserPromptSubmit 훅. 세션당 1회만 경로별 파이프라인 힌트 출력.
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

# 현재 git 브랜치 / 최근 커밋 / 변경 영역 힌트
BRANCH=$(cd "$PROJECT_ROOT" && git rev-parse --abbrev-ref HEAD 2>/dev/null || echo "?")
LAST=$(cd "$PROJECT_ROOT" && git log -1 --pretty=format:'%h %s' 2>/dev/null || echo "-")

CHANGED=$(cd "$PROJECT_ROOT" && git status --porcelain 2>/dev/null | awk '{print $2}')
AREA="?"
if echo "$CHANGED" | grep -q "^Assets/"; then
  AREA="game (Assets/)"
elif echo "$CHANGED" | grep -q "^server/"; then
  AREA="server (server/)"
fi

cat <<HINT
[session-hint] branch=$BRANCH  last=$LAST  area=$AREA
  game   → /gs-start → /gs-brainstorm → ... → /gs-story-done
  server → speckit.specify → .plan → .tasks → .implement → .analyze
HINT

exit 0
