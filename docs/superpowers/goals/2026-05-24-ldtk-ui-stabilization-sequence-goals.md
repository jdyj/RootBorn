# LDtk/UI Stabilization Sequential Goal Prompts

작성일: 2026-05-24
대상 프로젝트: `C:\Users\jdyj\farmer`

이 문서는 LDtk 렌더링, UI 패널, PlayMode 검증, 테스트 정리를 순서대로 진행하기 위한 goal 프롬프트 모음이다. 각 goal은 하나씩 독립 실행하고, 완료 검증 후 다음 goal로 넘어간다.

## 공통 실행 원칙

- `Assets/**/*.cs`는 디스크 직접 쓰기 금지. C# 수정은 Unity MCP `script-update-or-create` 또는 `script_execute` 경유.
- visible gameplay, UI, sprite, tilemap, placement, camera 변경은 실제 PlayMode 사용자 흐름과 Game View/Camera 스크린샷으로 검증.
- LDtk/tilemap 작업은 저장된 씬 상태와 PlayMode 런타임 상태를 모두 확인.
- 기존 사용자 변경은 되돌리지 말고, 관련 없는 dirty file은 건드리지 않는다.
- 각 goal 완료 시 자동화 테스트, PlayMode 검증 범위, 남은 리스크를 기록한다.
- 다음 goal로 넘어가기 전 `git status --short`로 이번 goal의 변경 파일을 확인한다.

## 실행 순서

1. LDtk Importer 고정화
2. LDtk Visual Diff 테스트
3. QuestLogPanel 기본 노출 제거
4. EventSystem 중복 경고 정리
5. 테스트/파일 추적 상태 정리

## Goal 1. LDtk Importer 고정화

### 목적

Town LDtk를 Unity 씬으로 다시 가져올 때 사람이 수동으로 고친 상태에 의존하지 않도록, LDtk 레이어 순서, 타일 source rect, same-cell overdraw 보존을 importer/재생성 경로에 고정한다.

### 실행 프롬프트

```text
Goal: LDtk Town importer를 고정화해서 LDtk 원본과 Unity Town 씬이 같은 레이어 순서, 같은 src tile, 같은 same-cell overdraw를 유지하게 만들어줘.

현재 확인된 기준:
- LDtk layerInstances 순서는 top-to-bottom: Door, Beach, School, Building, Police, Hospital, Shop, Fense, Props, Ground2, Ground
- Unity 렌더링은 bottom-to-top sorting order:
  Ground=0, Ground2=100, Props=200, Fense=300, Shop=400, Hospital=500, Police=600, Building=700, School=800, Beach=900
- same-cell overdraw는 별도 Tilemap으로 보존하고 overdraw sorting order는 base + 1, base + 2처럼 증가시킨다.
- 저장된 Town draw count 기준:
  Ground 5050, Ground2 96, Props 1003, Fense 97, Shop 250, Hospital 609, Police 141, Building 296, School 47, Beach 0

작업 범위:
- 기존 LDtk importer/scene rebuild/editor utility 흐름을 찾아 원인별로 고정한다.
- y source row가 +32처럼 밀리지 않도록 LDtk src 좌표를 그대로 사용하는 테스트를 유지/강화한다.
- Unity Tilemap의 one tile per cell 한계를 보완하기 위해 same-cell overdraw tilemap 생성 규칙을 importer에 넣는다.
- 레이어 sorting order가 LDtk visual stack과 일치하도록 importer에 명시한다.

성공 기준:
- 수동 수정 없이 importer/rebuild를 실행해도 Town 씬의 저장된 Tilemap 상태가 기준 draw count와 sorting order를 만족한다.
- LDtk src 좌표가 Unity Tile asset name/source metadata와 일치한다.
- same-cell overdraw tilemap이 누락되지 않는다.

필수 검증:
- 관련 EditMode 테스트를 먼저 실패시키거나 기존 회귀 테스트가 importer 미고정 상태를 잡는지 확인한다.
- 구현 후 `TownLdtkMapDefinitionTests`, `TownSavedTilemapSceneTests`, `TownBootTilemapVisualTests`, `TownDoorPortalPlayModeTests`를 실행한다.
- PlayMode에서 Town 진입 후 Game View 스크린샷을 캡처하고, LDtk와 눈에 띄는 차이가 없는지 확인한다.
```

### 다음으로 넘어가기 전 체크

- importer/rebuild 재실행 후에도 `Assets/Scenes/Town.unity`가 같은 결과를 낸다.
- 테스트가 수동 씬 상태만 보는 것이 아니라 importer 회귀를 잡는다.
- screenshot 경로와 테스트 결과를 기록했다.

## Goal 2. LDtk Visual Diff 테스트

### 목적

LDtk에서 보이는 Town/House/Shop과 Unity Game View가 달라지는 문제를 조기에 잡기 위해, 기준 스크린샷 또는 구조적 렌더 검증을 자동화한다.

### 실행 프롬프트

```text
Goal: LDtk 원본과 Unity Game View 렌더링 차이를 자동으로 잡는 visual diff/structural visual 테스트를 만들어줘.

대상:
- Town
- Interiors house_1
- Interiors shop

작업 범위:
- LDtk 원본에서 레이어 순서, tile gid/src, 좌표, opacity, visibility를 읽어 Unity Tilemap/Renderer 상태와 비교하는 구조적 검증을 우선 만든다.
- 가능하면 Game View screenshot 기반 visual smoke test도 추가한다.
- 픽셀 완전 diff가 flaky하면 허용 오차가 있는 핵심 영역 diff 또는 tile occupancy/source comparison으로 대체한다.

성공 기준:
- 레이어 순서가 바뀌거나 src row가 밀리거나 same-cell overdraw가 사라지면 테스트가 실패한다.
- Town, house_1, shop 각각에 대해 저장된 씬 상태와 PlayMode 렌더 상태를 검증한다.
- 테스트 이름은 시나리오 목적이 드러나게 작성한다.

필수 검증:
- 새 테스트가 실제로 실패 가능한 조건을 갖는지 확인한다.
- EditMode 구조 검증과 PlayMode Game View 검증을 모두 실행한다.
- 테스트 결과와 캡처된 screenshot 경로를 최종 보고에 포함한다.
```

### 다음으로 넘어가기 전 체크

- house_1과 shop도 Town과 같은 방식으로 LDtk 기준 검증 대상에 포함됐다.
- screenshot만 통과하고 Tilemap 상태가 틀린 경우를 놓치지 않는다.
- Tilemap 상태만 통과하고 Game View가 틀린 경우도 놓치지 않는다.

## Goal 3. QuestLogPanel 기본 노출 제거

### 목적

Town Game View 오른쪽의 큰 빈 UI 패널이 LDtk 렌더링 문제처럼 보이지 않도록, `QuestLogPanel` 기본 표시 정책을 정리한다.

### 실행 프롬프트

```text
Goal: Town 진입 시 오른쪽에 보이는 빈 QuestLogPanel이 기본으로 노출되지 않게 정리해줘.

작업 범위:
- `QuestLogPanel`, `ObjectiveJournalPanel`, `TrackedObjectiveHud` 소유권을 확인한다.
- AGENTS.md 규칙에 맞게 목표성 UI는 Tab으로 여는 ObjectiveJournalPanel이 소유하고, 상시 HUD는 TrackedObjectiveHud 하나만 사용하도록 정리한다.
- 빈 상태 패널이 Town 기본 화면에 보이지 않도록 초기 active state, CanvasGroup, prefab/scene reference를 수정한다.
- 목표가 실제로 추적 중일 때만 HUD가 1개 표시되는지 확인한다.

성공 기준:
- Town PlayMode 기본 진입 화면에서 빈 QuestLogPanel이 보이지 않는다.
- Tab 또는 지정된 입력으로 ObjectiveJournalPanel을 열 수 있다.
- 추적 목표 HUD가 중복 생성되지 않는다.

필수 검증:
- 관련 UI EditMode/PlayMode 테스트를 추가 또는 갱신한다.
- 실제 PlayMode에서 Town 진입, 기본 화면 확인, Tab으로 저널 열기/닫기, 추적 HUD 상태 확인을 수행한다.
- Game View screenshot으로 기본 화면과 저널 열린 상태를 각각 캡처한다.
```

### 다음으로 넘어가기 전 체크

- UI 변경이 LDtk 타일을 가리는 문제를 만들지 않는다.
- default visible과 runtime spawned panel을 모두 확인했다.
- Objective Journal 규약을 위반하는 새 Canvas나 상시 패널이 없다.

## Goal 4. EventSystem 중복 경고 정리

### 목적

PlayMode 검증 로그에 반복되는 `There are 2 event systems...` 경고를 제거해서 UI 입력과 테스트 신뢰도를 높인다.

### 실행 프롬프트

```text
Goal: PlayMode에서 발생하는 중복 EventSystem 경고의 원인을 찾아 하나의 EventSystem만 활성화되게 수정해줘.

작업 범위:
- Boot, Town, House, Shop, UI prefab, DontDestroyOnLoad 오브젝트에서 EventSystem 생성 경로를 조사한다.
- 씬 전환, additive load, interior transition 과정에서 EventSystem이 중복되는 조건을 재현한다.
- 하나의 책임 경로만 남기고 중복 생성은 비활성/제거/guard 처리한다.

성공 기준:
- Town 진입, house_1 전환, shop 전환, 다시 Town 복귀 시 활성 EventSystem이 1개다.
- Unity Console에 중복 EventSystem 경고가 나오지 않는다.
- UI 클릭/Tab/상호작용 입력이 정상 동작한다.

필수 검증:
- PlayMode 사용자 흐름으로 Town -> house_1 -> Town, Town -> shop -> Town을 직접 재현한다.
- 각 전환 후 runtime object state에서 활성 EventSystem 개수를 검사한다.
- 관련 PlayMode 테스트를 추가 또는 갱신한다.
- Game View screenshot과 Console 로그 확인 결과를 남긴다.
```

### 다음으로 넘어가기 전 체크

- EventSystem을 없애서 UI 입력이 죽는 방식으로 해결하지 않았다.
- 씬별 EventSystem과 persistent EventSystem의 책임이 명확하다.
- house/shop 전환 모두 확인했다.

## Goal 5. 테스트/파일 추적 상태 정리

### 목적

LDtk/UI 안정화 작업에서 생긴 테스트 파일, 씬 변경, 로그 산출물을 정리해서 다음 작업과 커밋 준비 상태를 명확히 만든다.

### 실행 프롬프트

```text
Goal: LDtk/UI 안정화 작업의 파일 상태를 정리하고, 필요한 테스트 파일이 추적되도록 정리해줘.

현재 주의할 파일:
- `Assets/Scenes/Town.unity`
- `Assets/Scripts/Game/Managers/ResourceManager.cs`
- `Assets/Tests/EditMode/Family/CharacterPartResourceManagerTests.cs`
- `Assets/Tests/EditMode/TownConcept/TownSavedTilemapSceneTests.cs`
- `Assets/Tests/PlayMode/TownBootTilemapVisualTests.cs`
- `Assets/Tests/EditMode/World/TownLdtkMapDefinitionTests.cs`가 untracked일 수 있음

작업 범위:
- `git status --short`로 관련 변경과 unrelated 변경을 분리한다.
- 새로 필요한 테스트 파일과 `.meta`가 모두 존재하고 추적 대상인지 확인한다.
- 임시 screenshot/log/cache 산출물이 커밋 대상에 섞이지 않도록 정리한다.
- 사용자가 만든 unrelated 변경은 건드리지 않는다.

성공 기준:
- 이번 LDtk/UI 안정화에 필요한 파일 목록이 명확하다.
- untracked 테스트 파일 누락이 없다.
- 임시 파일과 실제 변경 파일이 분리된다.

필수 검증:
- 전체 관련 테스트 또는 최소 변경 영역 테스트를 다시 실행한다.
- `git status --short` 결과를 기준으로 커밋 후보 파일과 제외 파일을 보고한다.
- visible 변경이 포함된 경우 최종 PlayMode Game View screenshot 검증 상태를 함께 보고한다.
```

### 다음으로 넘어가기 전 체크

- 테스트 파일 누락으로 CI에서 테스트가 빠지는 상태가 아니다.
- 커밋 후보와 제외 후보가 구분되어 있다.
- 다음 기능 작업 전에 기준 상태를 설명할 수 있다.

## 전체 완료 기준

- Town, house_1, shop이 LDtk 기준과 Unity Game View에서 일관되게 렌더링된다.
- door/interior 전환 후 카메라 줌, UI, EventSystem, Tilemap layer가 정상이다.
- 빈 UI 패널이 기본 화면을 가리지 않는다.
- 저장된 씬 상태와 PlayMode 런타임 상태가 모두 검증됐다.
- 관련 테스트와 실제 PlayMode 스크린샷 검증 결과가 남아 있다.
