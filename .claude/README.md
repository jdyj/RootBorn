# .claude/ — ROOTBORN 에이전트 설정

ROOTBORN은 MonoGame 판(`game/`, `Packages/com.rootborn.core`, `data/`)이다. 최상위 규칙은 `AGENTS.md`(Claude·Codex 공통)와 이 폴더의 헌법·규칙 문서다.

## 무엇이 어디에 있나

| 위치 | 내용 |
|---|---|
| `constitution.md` | 헌법(최상위 규칙) |
| `rules/*.md` | 세부 규칙. 목록은 `AGENTS.md` "규칙 문서". 규칙이 바뀐 이력은 `rules/changelog.md` |
| `skills/` | 되풀이 절차: `port-rule-to-core`, `parallel-claude-session`, `host-play-test`, `ui-nine-slice-skin`, `game-code-review`, `feedback-constitution-update`. 앞의 네 개는 `.agents/skills/`에 같은 내용이 있다(Codex용) |
| `agents/` | `code-reviewer`, `test-designer`, `test-result-analyzer`, `bug-fixer`, `feedback-analyzer`, `constitution-updater`. 각 파일이 읽을 규칙 문서와 지킬 것을 적는다 |
| `hooks/` | `detect-feedback.js`(부정적 피드백 감지 → `feedback-constitution-update` 스킬 유도), `session-hint.sh`(세션당 1회 브랜치·마지막 커밋 표시) |
| `settings.json` | 훅 연결과 권한 목록 |
| `_workspace/` | 에이전트 중간 산출물(gitignore) |

## 정적 검사와 git 훅

- `Scripts/ci/` — dotnet 기준 정적 검사. `bash Scripts/ci/run-all.sh --checks-only`. 설명은 `Scripts/ci/README.md`.
- `.github/workflows/ci.yml` — 정적 검사와 dotnet 테스트.
- `Scripts/git-hooks/` — `install.sh`로 켜는 commit 훅.

## 피드백 → 규칙 갱신

사용자가 "잘못됐다", "그렇게 하지 마" 같은 피드백을 주면 `hooks/detect-feedback.js`가 알려 주고, `feedback-constitution-update` 스킬이 `feedback-analyzer` → `constitution-updater` 순서로 원인을 분석해 규칙 문서를 고치고 `rules/changelog.md`에 적는다. 규칙은 특정 예시가 아니라 원리 수준으로, 이유(Why)와 함께 쓴다.
