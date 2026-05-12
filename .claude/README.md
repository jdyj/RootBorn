# ROOTBORN Harness — 도시 생활 성장 시뮬레이션

Unity 6000.3.13f1 (URP 2D) + Unity Netcode for GameObjects 기반 ROOTBORN 프로젝트용 에이전트 하네스. 게임 개발, 빌드·배포, 자동화 테스트, 개발 반복 루프, 피드백 기반 헌법 갱신, 코드 리뷰까지 커버한다. (백엔드/관리자 웹은 미사용.)

## 구성 요약

- **헌법**: `constitution.md` (최상위 규칙), `rules/*.md` (세부 가이드)
- **훅**: `settings.json` + `hooks/detect-feedback.js` (부정적 피드백 감지 → 헌법 갱신 스킬 유도)
- **에이전트**: `agents/` 14개 + 3-tier prefix 6개 (총 20개)
- **스킬(오케스트레이터 6 + 헬퍼 2)**: `skills/` 8개 + Unity MCP 도구 스킬 90+개

## 6개 오케스트레이터 스킬

| 스킬 | 용도 | 실행 모드 |
|------|------|----------|
| [unity-game-dev](skills/unity-game-dev/skill.md) | 2D 게임 기능 신규 개발 (설계→구현→Scene→테스트→QA→리뷰) | 에이전트 팀 |
| [unity-automated-test](skills/unity-automated-test/skill.md) | EditMode/PlayMode 테스트 작성·실행·분석 | 에이전트 팀 |
| [unity-build-deploy](skills/unity-build-deploy/skill.md) | CLI 빌드 + 배포 (클라/dedicated server) | 에이전트 팀 |
| **[dev-iteration-loop](skills/dev-iteration-loop/skill.md)** ★ | 개발→빌드→테스트→분석→수정 자동 반복 (핵심) | 에이전트 팀 |
| [feedback-constitution-update](skills/feedback-constitution-update/skill.md) | 부정적 피드백 → 헌법/규칙 자동 갱신 | 서브 에이전트 |
| [game-code-review](skills/game-code-review/skill.md) | 품질+정합성+테스트커버리지 다각도 리뷰 | 에이전트 팀 |

## 2개 헬퍼 스킬

| 스킬 | 용도 |
|------|------|
| [unity-cli-reference](skills/unity-cli-reference/skill.md) | Unity batchmode CLI 호출 표준 (빌드/테스트/로그 파싱) |
| [unity-test-framework](skills/unity-test-framework/skill.md) | Unity Test Framework 작성 패턴 (asmdef, EditMode/PlayMode, UnityTest) |

## 14개 에이전트 (+ 3-tier 6개)

**Unity 개발 (9)**
- `unity-architect` — 시스템 설계, ScriptableObject, asmdef 경계
- `csharp-developer` — MonoBehaviour/SO 구현
- `scene-builder` — Prefab/Scene 구성 절차 또는 Editor 자동화
- `unity-builder` — CLI batchmode 빌드 실행
- `unity-test-runner` — EditMode/PlayMode 실행
- `test-designer` — NUnit + UnityTest 테스트 작성
- `test-result-analyzer` — JUnit XML 파싱, 근본 원인 가설
- `bug-fixer` — 가설 기반 최소 침습적 수정
- `deploy-manager` — 클라/dedicated server 배포

**개발 루프 (1)**
- `iteration-coordinator` — 반복 루프 리더, 종료/에스컬레이션 판단

**피드백 (2)**
- `feedback-analyzer` — 피드백 원인 분석, 일반화된 교훈 추출
- `constitution-updater` — `.claude/constitution.md` / `rules/*.md` 실제 수정

**공통 (2)**
- `code-reviewer` — 품질/SOLID/성능/보안 리뷰
- `qa-inspector` — 통합 정합성(경계면) 교차 검증, "양쪽 동시 읽기"

**3-tier 추가 (6, 정보성 prefix)**
- `t1-technical-director` (전략·게이트 승인)
- `t2-game-designer`, `t2-qa-lead`, `t2-release-manager`
- `t3-gameplay-programmer`, `t3-unity-specialist`

## 테스트 자동화 (사용자 핵심 요구)

### 로컬 / CLI
- `unity-automated-test` 스킬: `test-designer` → `unity-test-runner` → `test-result-analyzer` 체인
- CLI 표준은 `rules/unity-cli.md` 참조 (`-runTests -testPlatform editmode|playmode`)
- `dev-iteration-loop` 스킬이 빌드→테스트→분석→수정을 최대 5회 자동 반복

### CI 게이트 (`Scripts/ci/`)
- `check-no-entity-id-branching.sh` — 생활 활동/선택지/작물/도구/지식/특성 ID에 시스템 코드가 분기 시 빌드 실패 (데이터-드리븐 위반 검출)
- `check-scenario-registry-sync.sh` — `testing-discipline.md`의 시나리오 ID(TOWN/LIFE/CAREER/REL/JOB/KNOW/STATUS/NET-XXX, 레거시 GEN/HEIR/CROP-XXX) ↔ `Assets/Tests/.../ScenarioId.cs` 동기화 검증

### Git 훅 (`Scripts/git-hooks/`)
- `pre-commit` — Unity .meta 동행 검증, TODO 형식, JSON 파싱
- `pre-push` — 푸시 전 추가 검증
- `install.sh` 실행으로 활성화

### GitHub Actions (`.github/workflows/`)
- `ci.yml` — push/PR마다 entity-id 게이트 + scenario sync + Unity EditMode
- `unity-tests-nightly.yml` — 매일 02:00 KST EditMode + PlayMode + Long 카테고리

## 개발 반복 루프 (핵심)

`dev-iteration-loop` 스킬이 하네스의 심장이다. 사용자 목표를 받아 다음을 반복한다:

```
iter-N: [개발/수정] → [빌드] → [테스트] + [QA(병렬)] → [분석] → [coordinator 판단]
                                                               ↓
                          성공 | 다음 iter | 재설계 호출 | 에스컬레이션
```

- 기본 최대 5회 반복 (`max_iterations`)
- `failure_trend` 단조 감소 → 계속, 정체/증가 → 개입
- 설계 결함이 근본 원인이면 `unity-architect` 재호출하여 재설계 후 재구현
- 상태는 `_workspace/loop_state.json`에 파일로 보존

## 피드백 훅 시스템

사용자가 "잘못됐다", "틀렸다", "wrong", "incorrect", "그렇게 하지 마" 등 부정적 피드백을 주면:

1. `settings.json`의 `UserPromptSubmit` 훅이 `hooks/detect-feedback.js` 실행
2. 스크립트가 키워드 감지 시 시스템 리마인더 출력
3. Claude가 `feedback-constitution-update` 스킬 호출
4. `feedback-analyzer`가 원인 분석 + 일반화된 교훈 추출
5. `constitution-updater`가 `constitution.md` 또는 `rules/*.md` 수정, `rules/changelog.md`에 이력 기록
6. 사용자 승인 후 확정
7. 다음 세션부터 모든 에이전트가 갱신된 규칙을 자동 로드

**오버피팅 회피**: 분석·갱신 모두 특정 예시가 아닌 **원리 수준**에서 수행. 규칙은 항상 **Why**(이유) + **How to apply**(적용 조건)와 함께 기록한다.

## 원칙 요약 (헌법에서 발췌)

1. **변경 가능성 우선** — 이른 추상화 금지, 3회 이상 반복될 때만
2. **엔티티 데이터-드리븐** — 생활 활동/선택지/특성/기술/진로/관계/직무/도구/지식/상태 모두 SO. 레거시 작물/세대도 SO 유지. 시스템 코드는 ID 분기 금지 (CI 게이트로 강제)
3. **Scene은 조립, Prefab은 부품** — Scene YAML 직접 수정 금지
4. **PlayMode 우선 테스트** — 물리/코루틴은 PlayMode, 순수 로직은 EditMode
5. **증분 QA** — 모듈 완성 직후 즉시 경계면 검증
6. **양쪽 동시 읽기** — Script↔Scene, NetworkVariable↔ServerLogic 교차 비교로 경계면 버그 방지
7. **Why 기반 규칙** — 규칙 뒤에는 항상 이유를 기록하여 엣지 케이스 판단 가능

## 사용법

```
사용자: "도시 생활 활동으로 특성이 오르는 시스템 만들어줘"
  → unity-game-dev 스킬 호출

사용자: "EditMode 테스트 통과할 때까지 고쳐줘"
  → dev-iteration-loop 스킬 호출 (★ 핵심)

사용자: "win64 클라/서버 빌드 만들어줘"
  → unity-build-deploy 스킬 호출

사용자: "왜 또 그렇게 했어, 안 된다고 했잖아"
  → (훅이 자동 감지) feedback-constitution-update 스킬 호출

사용자: "이 PR 리뷰해줘"
  → game-code-review 스킬 호출
```
