# Tile Placement Quest Growth Goal Prompt

아래 프롬프트를 새 goal 세션에 그대로 붙여 넣고 진행한다.

```text
ROOTBORN Unity 프로젝트에서 "유저가 직접 타일을 선택하고 배치하며, 배치 결과가 저장되고, 퀘스트 완료와 성향/특성 성장으로 이어지는 실제 플레이 루프"를 구현해줘.

핵심 목표:
1. Town 씬에서 플레이어가 실제 UI/입력으로 타일 배치 모드에 진입할 수 있어야 한다.
2. 현재는 임시로 가능한 모든 타일을 선택 가능하게 한다. 타일 후보 큐레이션은 나중에 하며, 1차 구현은 프로젝트 내 등록/탐색 가능한 TileBase 또는 배치 가능한 타일 정의 전체를 노출한다.
3. 플레이어는 타일 팔레트 UI에서 타일을 선택하고, 월드 그리드 셀을 클릭/입력해서 타일을 배치할 수 있어야 한다.
4. 배치된 타일은 저장/로드 후에도 유지되어야 한다.
5. 타일 배치는 퀘스트 목표로 연결되어야 한다. 퀘스트 수주 후 타일을 1회 배치하면 목표가 완료되고, 완료/보상 수령 시 특성/성향 또는 스킬이 상승해야 한다.
6. 타일 배치 콘텐츠는 기존 직업 설계 중 인테리어/공간 디자이너 루트와 우선 연결한다.
   - 기존 확인 대상: `career.interior`, `practice.interior`
   - 관련 성장: `trait.creativity`, `trait.planning`, `skill.space-layout`, `skill.color-harmony`, `skill.budget-sense`
7. 타일 배치 결과는 코드 내부 상태뿐 아니라 유저가 볼 수 있는 UI에서 확인 가능해야 한다.
8. 모든 테스트는 "유저가 직접 플레이하는 것처럼" 씬 로드, 이동/상호작용, 버튼 클릭, 타일 선택, 월드 클릭, 퀘스트 완료 확인 순서로 작성한다. 내부 API 직접 호출만으로 통과하는 테스트는 완료 근거로 쓰지 않는다.

현재 확인된 직업/성향 맥락:
1. 기존 학생 진로 실습 루트는 4개다.
   - `career.chef`
   - `career.interior`
   - `career.soldier`
   - `career.emergency-care`
2. 이번 타일 배치 기능은 인테리어/공간 디자이너 루트와 가장 직접적으로 맞는다.
3. 이미 `Assets/Data/StudentLife/CareerPractices/` 아래에 인테리어 계열 Career/Practice/Activity 데이터가 있다.
4. 이미 `Assets/Data/Quests/CareerRoutes/Quest_CareerRoute_Interior.asset`와 `Effect_CareerTrait_Interior.asset` 계열 퀘스트/완료 효과 데이터가 있다.
5. 새 기능은 이 흐름을 재사용하되, "대화만으로 완료"가 아니라 "실제 타일 배치 1회"를 퀘스트 목표로 삼아야 한다.

절대 규칙:
- AGENTS.md와 `.claude/rules/*`를 따른다.
- `Assets/**/*.cs`는 외부 디스크 직접 쓰기 금지. 신규/수정 C#은 반드시 Unity MCP `script-update-or-create` 또는 Unity Editor 안전 경로를 사용한다.
- TDD로 진행한다. 먼저 실패하는 PlayMode 시나리오 테스트를 작성하고 실패를 확인한 뒤 구현한다.
- 타일, 퀘스트, 목표, 보상/완료 효과, 성향/특성, 스킬, 직업 힌트는 ScriptableObject 데이터 기반이어야 한다.
- tileId/questId/careerId/traitId별 `if/switch` 분기, enum 분기, 타일별 C# 클래스는 금지한다.
- 배치/퀘스트/보상은 중복 request id 또는 저장/로드 재호출에도 중복 적용되지 않아야 한다.
- 보상 지급은 지급 전 인벤토리 수용 가능 여부와 중복 수령 여부를 검증한다. 실패 시 인벤토리와 퀘스트 상태가 바뀌면 안 된다.
- 기존 사용자 변경이나 dirty 파일을 되돌리지 않는다.

먼저 확인할 기존 파일:
- `docs/art/town-tilemap-placement-guide.md`
- `docs/superpowers/goals/2026-05-08-student-four-career-practice-goal.md`
- `docs/superpowers/goals/2026-05-10-strict-quest-playflow-goal.md`
- `Assets/Scripts/Game/StudentLife/`
- `Assets/Scripts/Game/Quests/`
- `Assets/Scripts/Game/Bootstrap/TownStudentLifeRuntimeInstaller.cs`
- `Assets/Scripts/Game/Bootstrap/TownPlayableBaselineRuntimeInstaller.cs`
- `Assets/Scripts/UI/Quests/`
- `Assets/Data/Tiles/`
- `Assets/Data/StudentLife/CareerPractices/`
- `Assets/Data/Quests/CareerRoutes/`
- `Assets/Data/Registry/GameDataRegistry.asset`
- `Assets/Scenes/Town.unity`
- `Assets/Tests/PlayMode/`
- `Assets/Tests/EditMode/`

권장 설계:
1. `TownGrid/TownGroundTilemap`, `TownDecorationTilemap`, `TownCollisionTilemap` 구조를 유지한다.
2. 새 데이터가 필요하면 `PlaceableTileDefinition` 같은 SO를 추가하고 `Assets/Data/Tiles/`에 둔다.
3. 1차 구현은 배치 가능 타일 전체를 자동 수집/등록하되, 런타임 로직은 타일별 하드코딩 없이 정의 배열을 순회한다.
4. 타일 배치 이벤트는 퀘스트 시스템에 데이터 기반으로 연결한다. 필요하면 `QuestEventKind.TilePlaced` 또는 동등한 일반화 이벤트와 `TilePlacedQuestObjective`를 추가하되, 특정 tileId 분기는 금지한다.
5. 퀘스트 완료 효과는 기존 `TraitDeltaCompletionEffect` 또는 유사 구조를 재사용하고, 필요하면 스킬 상승/진로 힌트 효과를 SO 전략으로 추가한다.
6. 저장은 현재 SaveService/슬롯 구조를 따르고, 배치 좌표, 타일 정의 참조, 타일맵 레이어를 저장한다.
7. UI는 Town의 기존 Modern UI/Quest UI 구조를 재사용한다. 새 패널을 만들 경우 Modern UI Style2 공통 패널 규약을 지킨다.

필수 실제 플레이 시나리오:
- TILE-PLACE-QUEST-001: Town 씬 로드 후 플레이어가 타일 배치 안내 NPC/보드에 접근하고 실제 상호작용 입력으로 퀘스트를 수락한다.
- TILE-PLACE-QUEST-002: 타일 팔레트 UI가 열리고, 가능한 모든 타일 후보가 버튼/슬롯으로 표시된다.
- TILE-PLACE-QUEST-003: 유저 입력 경로로 타일 하나를 선택하고 월드 그리드 셀에 배치하면 실제 Tilemap에 타일이 표시된다.
- TILE-PLACE-QUEST-004: 퀘스트 진행도 UI가 0/1에서 1/1로 갱신되고 QuestLog 상태가 Completed가 된다.
- TILE-PLACE-QUEST-005: 완료/보상 수령 버튼을 실제 UI 클릭으로 누르면 인테리어 계열 성향/스킬 또는 진로 힌트가 증가하고 UI에서 확인된다.
- TILE-PLACE-QUEST-006: 저장 후 씬을 다시 로드해도 배치한 타일과 퀘스트/보상 상태가 유지된다.
- TILE-PLACE-QUEST-007: 이미 수령한 보상은 재대화/재로드/재클릭으로 중복 지급되지 않는다.
- TILE-PLACE-QUEST-008: 배치 로직과 퀘스트 목표 로직에 tileId/questId/careerId별 if/switch 분기가 없다.
- TILE-PLACE-QUEST-NET-001: 멀티플레이어에서는 Player 1의 타일 배치 퀘스트 성장/보상이 Player 2의 성향/스킬 상태에 섞이지 않는다.

테스트 기준:
- 반드시 PlayMode 테스트로 실제 씬을 로드한다.
- 실제 GameObject, Grid, Tilemap, PlayerInteractionRouter, DialoguePanel 또는 타일 배치 보드 UI, QuestLogPanel, EventSystem, Button click 또는 실제 입력 경로를 사용한다.
- 내부 도메인 API 직접 호출 테스트는 보조 테스트로만 인정한다.
- 유저가 보는 GUI를 검증해야 한다. 타일 팔레트 표시, 선택 상태, 퀘스트 제목, 목표 진행도, 완료 상태, 보상/성향 증가 표시를 Assert한다.
- 타일 배치는 QuestLog에 이벤트를 직접 넣지 말고, 실제 월드 그리드 클릭/입력 경로를 통해 발생시킨다.
- 임의 WaitForSeconds에 의존하지 말고 오브젝트 생성, 바인딩, UI 갱신 완료 조건을 명확히 기다린다.
- 테스트 이름에는 위 시나리오 ID를 포함한다.

작업 순서:
1. git status로 현재 dirty 상태를 확인하고 기존 변경은 되돌리지 않는다.
2. 기존 Town 타일맵, QuestLog, Dialogue/Quest UI, StudentLife/CareerPractice 구조를 읽고 재사용 경계를 요약한다.
3. 실제 플레이와 동떨어진 테스트, 즉 내부 API만 직접 호출하는 테스트와 누락된 GUI 검증을 식별한다.
4. 위 PlayMode 실패 테스트를 먼저 작성하고 실패를 확인한다.
5. 필요한 SO 모델과 데이터 에셋을 추가하고 GameDataRegistry에 등록한다.
6. 실제 UI/입력 기반 타일 선택/배치/저장/로드를 구현한다.
7. 타일 배치 이벤트를 퀘스트 목표와 연결한다.
8. 퀘스트 완료 효과로 인테리어 계열 특성/성향/스킬/진로 힌트를 증가시킨다.
9. 관련 EditMode 테스트를 실행한다.
10. 관련 PlayMode 테스트를 실행한다.
11. entity ID 분기 금지 CI를 실행한다: `Scripts/ci/check-no-entity-id-branching.sh`

완료 조건:
- Town에서 실제 유저 조작으로 퀘스트 수주 -> 타일 선택 -> 타일 배치 -> 퀘스트 완료 -> 보상/성향 상승 확인이 가능하다.
- 배치 타일이 저장/로드된다.
- 가능한 모든 타일을 임시 팔레트로 선택할 수 있다.
- 인테리어/공간 디자이너 루트와 연결된다.
- 내부 API 직접 호출만이 아니라 실제 버튼/입력/Tilemap/UI 검증 PlayMode 테스트가 통과한다.
- 데이터 기반 원칙과 entity ID 분기 금지 CI가 통과한다.
- 최종 보고에는 확인한 기존 직업 설계, 수정 파일, 테스트 결과, 남은 리스크를 포함한다.
```

## Design Notes

이번 기능은 기존 4개 진로 루트 중 `career.interior` / `practice.interior`에 우선 연결한다. 타일 배치는 공간 배치, 색 조합, 예산 감각 같은 인테리어 계열 성향/스킬과 자연스럽게 맞기 때문이다.

1차 구현은 복잡한 하우징 시스템이 아니라 "타일 팔레트에서 선택 -> Town 타일맵에 1회 배치 -> 퀘스트 목표 완료 -> 인테리어 계열 성장"의 닫힌 플레이 루프를 만드는 데 집중한다. 후속 goal에서 타일 카테고리, 배치 비용, 배치 제한, 공간 평가, 장식 점수, 직업별 고급 실습으로 확장한다.
