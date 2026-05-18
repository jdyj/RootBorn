# Player Flow Scenario Simulation Design

Date: 2026-05-18
Status: Approved for implementation planning

## Goal

ROOTBORN의 핵심 플레이 검증을 `B -> C -> A` 순서로 정리하고 보강한다.

1. `B`: Town 핵심 플레이 루프를 실제 사용자 흐름으로 검증한다.
2. `C`: House/Interiors 직접 조작, 배치, 저장, 시각 상태를 검증한다.
3. `A`: 코드 전수조사 결과를 시나리오 카탈로그와 테스트 매핑으로 남긴다.

완료 기준은 단순 테스트 통과가 아니다. AGENTS.md의 Direct Visual Play Verification Gate에 따라 PlayMode에서 실제 입력, 이동, UI 클릭, 상호작용, 저장/로드, Game View 또는 Camera 스크린샷, 런타임 상태 검사를 함께 통과해야 한다.

## Current Baseline

현재 프로젝트에는 많은 테스트가 이미 존재한다.

- `Assets/Scripts` C# 파일: 527개
- `Assets/Tests/EditMode` C# 테스트: 209개
- `Assets/Tests/PlayMode` C# 테스트: 90개
- 가장 큰 런타임 도메인: `StudentLife`, `Quests`, `DiscoveryClues`, `WorldState`, `Housing`, `Interiors`

이미 존재하는 중요한 기반:

- SaveSlot New Game에서 Town으로 진입하는 PlayMode 흐름
- 키보드 이동 기반 Town E2E 테스트
- Quest, WorldStateUsage, StudentLife, DiscoveryClues, Career, Objective Journal, DayResult 관련 E2E 테스트
- House/Interiors 배치, 저장, 시각 정리, 카메라 조작 관련 PlayMode 테스트
- `DirectValidationAutoplayInstaller`와 `DirectValidationTrace`
- `ScenarioId.cs`의 일부 시나리오 상수

확인된 문제:

- `ScenarioId.cs`는 현재 확장된 `StudentLife`, `LocationState`, `DiscoveryClues`, `WorldStateUsage`, `House/Interiors`, `VerticalSlice` E2E 범위를 충분히 반영하지 못한다.
- 일부 테스트는 실제 플레이 경로를 사용하지만, 일부는 fixture 생성, 런타임 스폰, 내부 상태 검사, 소스 감사에 치우쳐 있다.
- "시뮬레이션" 개념이 하나의 공통 하네스가 아니라 PlayMode E2E, DirectValidation, 저장/로드 검증, 중복 방지 검증으로 흩어져 있다.
- 최근 작업 트리에 대규모 변경과 미추적 파일이 많으므로, 새 작업은 기존 변경을 되돌리지 않고 좁은 범위로 추가해야 한다.

## Design Approach

이 작업은 세 개의 구현 단위로 나눈다.

### Phase B: Town Core Player Flow Simulation

목표는 Town 핵심 루프를 하나의 대표 플레이어 여정으로 검증하는 것이다.

필수 사용자 흐름:

1. SaveSlot UI에서 New Game을 클릭한다.
2. SPUM 또는 캐릭터 확인 UI가 있으면 실제 버튼 클릭으로 통과한다.
3. Town 씬에 진입한다.
4. 키보드 입력으로 플레이어를 NPC, 자원, 사용 가능 오브젝트, day-end 지점까지 이동시킨다.
5. `E` 또는 현재 상호작용 입력으로 prompt를 실행한다.
6. Dialogue 또는 선택 UI가 뜨면 실제 버튼 클릭으로 선택한다.
7. 최소 두 개 이상의 도메인 상태가 바뀐다.
8. Objective Journal이 다음 행동을 표시한다.
9. DayResult UI가 결과와 다음 동기를 표시한다.
10. 저장 후 같은 save slot을 load하여 상태가 복원되는지 확인한다.
11. 반복 상호작용이 중복 지급이나 중복 진행을 만들지 않는지 확인한다.
12. Game View 스크린샷을 저장한다.

대표 도메인 변화는 다음 중 둘 이상이어야 한다.

- StudentLife progress
- QuestLog state
- Inventory reward
- WorldState flag
- WorldStateUsage personal record
- Encyclopedia unlock
- Career hint
- Objective Journal item
- DayResult guide

Phase B는 가능한 한 기존 `IntegratedVerticalSliceFoundationE2ETests`, `WorldStateUsageLoopE2EScenarioTests`, `TownLocationNpcFoundationE2EScenarioTests` 패턴을 재사용한다. 새 공통 하네스를 만들더라도 테스트가 플레이어 경로를 우회하지 않도록 이동, prompt, 클릭, 저장/로드만 추상화한다.

### Phase C: House/Interiors Player Flow Simulation

목표는 최근 House/Interiors 작업에서 반복된 시각 검증 누락을 막는 것이다.

필수 사용자 흐름:

1. 실제 플레이 경로로 House 또는 Interior 씬에 들어간다.
2. 배치 모드 또는 시공 모드를 UI로 켠다.
3. 마우스/포인터 입력으로 가구 또는 시공 타일을 배치한다.
4. 가능한 경우 이동, 회전, 삭제 중 현재 구현된 조작 하나 이상을 실제 입력으로 수행한다.
5. Tilemap, Renderer, Overlay, 선택된 asset name, cell position을 런타임에서 검사한다.
6. Game View 또는 Camera 스크린샷을 저장한다.
7. 저장 후 reload한다.
8. reload 후 배치 결과, overlay 상태, stale sample/debug tilemap 미노출을 다시 검사한다.
9. 요청 결과가 저장 asset이나 scene에 반영되어야 하는 경우, 런타임 생성 상태와 saved scene/prefab/SO 상태를 별도로 검사한다.

Phase C는 기존 `Assets/Tests/PlayMode/Interiors` 및 `HousePlacementSaveLoadE2EScenarioTests` 계열을 우선 재사용한다. 새 테스트는 기존 미추적 변경과 충돌하지 않도록 별도 파일 또는 좁은 append로 작성한다.

### Phase A: Scenario Coverage Audit

목표는 전수조사 결과를 지속 가능한 카탈로그로 남기는 것이다.

산출물:

- `docs/superpowers/audits/2026-05-18-player-flow-scenario-coverage-audit.md`
- 도메인별 코드 파일 수와 테스트 파일 수 요약
- 실제 PlayMode 사용자 흐름 테스트 목록
- 내부 메서드 호출, 소스 감사, fixture 중심 테스트 목록
- `ScenarioId.cs`와 실제 테스트명 불일치 목록
- AGENTS.md 직접 시각 검증 게이트 충족 여부
- 우선 보강해야 할 누락 시나리오 목록

전수조사 대상:

- `Assets/Scripts/Game`
- `Assets/Scripts/UI`
- `Assets/Scripts/Network`
- `Assets/Tests/EditMode`
- `Assets/Tests/PlayMode`
- `.claude/rules/testing-discipline.md`
- `docs/superpowers/goals`
- `docs/superpowers/plans`
- `docs/superpowers/audits`

## Scenario Naming

새 시나리오 ID는 기존 이름을 보존하면서 부족한 범위를 확장한다.

권장 신규 prefix:

- `TOWN-FLOW-*`: Town 진입, 이동, 상호작용, Objective Journal, DayResult
- `HOUSE-FLOW-*`: House 진입, 배치, 시공, 저장/로드, 시각 검증
- `COVERAGE-*`: 전수조사와 시나리오 카탈로그 동기화

`ScenarioId.cs`는 구현 단계에서 실제 테스트와 맞춰 확장한다. 단, 단순 상수 추가만으로 완료로 보지 않는다. 각 ID는 대응 PlayMode 또는 EditMode 테스트와 문서 매핑을 가져야 한다.

## Testing Strategy

모든 신규 런타임 코드나 테스트 유틸 변경은 TDD를 따른다.

EditMode:

- 시나리오 카탈로그 파서 또는 감사 유틸이 생기면 먼저 실패 테스트를 작성한다.
- `ScenarioId.cs`와 실제 테스트명 매핑 검사를 추가한다.
- entity ID별 C# 분기 금지를 검사한다.
- 테스트 하네스가 내부 도메인 메서드를 완료 판정으로 직접 호출하지 않는지 소스 감사한다.

PlayMode:

- Phase B는 SaveSlot New Game부터 시작한다.
- Phase C는 실제 House/Interior 진입과 UI/포인터 조작을 사용한다.
- 완료 판정은 runtime state, persisted save state, UI text, screenshot evidence를 함께 사용한다.
- 시각 변경은 Game View 또는 Camera screenshot 없이는 완료로 보지 않는다.

Required verification commands:

```powershell
# Targeted EditMode through Unity MCP tests-run
# testMode=EditMode, target namespace/class selected by implementation plan

# Targeted PlayMode through Unity MCP tests-run
# testMode=PlayMode, target class selected by implementation plan

& "C:\Program Files\Git\bin\bash.exe" Scripts/ci/check-no-entity-id-branching.sh
```

## Visual Evidence

Phase B evidence should be saved under:

- `production/qa/evidence/town-core-flow-objective-journal.png`
- `production/qa/evidence/town-core-flow-day-result.png`

Phase C evidence should be saved under:

- `production/qa/evidence/house-interior-placement-before-reload.png`
- `production/qa/evidence/house-interior-placement-after-reload.png`

The audit document must record exact screenshot paths and whether screenshots came from Game View, Camera, or isolated render.

## Performance Constraints

This work must not add per-frame registry scans or broad scene searches in production runtime paths.

Allowed in tests:

- bounded polling while waiting for scene objects
- direct `Object.FindFirstObjectByType` in PlayMode tests
- filesystem inspection of test save roots

Production code changes, if any, must prefer explicit flow checkpoints:

- after New Game setup
- after interaction result
- after UI binding
- after save/load
- after placement commit

Objective Journal and audit output must be built from cached models or explicit refresh, not continuous `Update()` polling.

## Out of Scope

This spec does not require:

- rewriting all existing E2E tests
- replacing all DirectValidation code
- building a full generic simulation engine
- multiplayer host/client validation
- new gameplay content or new ScriptableObject entities beyond what tests need
- committing unrelated dirty asset changes already present in the worktree

## Completion Criteria

The implementation is complete only when all of the following are true:

- Phase B targeted EditMode and PlayMode tests pass.
- Phase C targeted EditMode and PlayMode tests pass.
- Phase A audit document exists and lists remaining gaps honestly.
- Game View or Camera screenshot evidence exists for visible Phase B and Phase C flows.
- Save/load and duplicate-action checks are covered in PlayMode.
- `ScenarioId.cs`, test names, and audit mapping agree for newly added scenarios.
- `check-no-entity-id-branching.sh` passes.
- Any unverified visual or persistence path is explicitly called out instead of reported as done.
