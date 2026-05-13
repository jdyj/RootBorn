# Objective Journal UI Design

Date: 2026-05-13
Status: Draft for review

## Goal

ROOTBORN의 목표 관련 UI를 즉흥적으로 화면에 붙이는 방식에서 벗어나, `Tab` 키로 여는 통합 `Objective Journal` 기준을 세운다.

플레이 중 화면에는 현재 추적 중인 목표 1개만 간이 HUD로 보여주고, 목표/퀘스트/캠페인/진로 힌트의 전체 목록과 상세 정보는 `Objective Journal`에서 관리한다. 이 문서는 첫 구현 전에 UI 소유권, 레이아웃 템플릿, 데이터 공급 경계, 테스트 기준을 고정하기 위한 설계다.

## Current Problem

현재 목표성 UI는 각 기능이 자신의 위치와 크기를 직접 정한다.

- `Assets/Scripts/UI/StudentLife/MilestoneHudPanel.cs`는 좌상단 고정 좌표에 `Goals` 패널을 만든다.
- `Assets/Scripts/UI/StudentLife/CampaignHudPanel.cs`는 별도 좌표와 별도 텍스트 구조로 `Campaign` HUD를 만든다.
- `Assets/Scripts/UI/Quests/QuestLogPanel.cs`는 퀘스트 전용 구조를 직접 생성하지만, 통합 목표 화면의 슬롯 규칙과 연결되어 있지 않다.
- `Assets/Scripts/UI/Modern/ModernUiPanelInputRouter.cs`에서 `Tab`은 현재 status 패널 토글로 쓰이고 있어, 목표 UI의 주 입력으로 정리되어 있지 않다.
- `CampaignRuntimeInstaller` 같은 런타임 인스톨러가 Canvas/EventSystem 생성과 UI 배치를 직접 처리해 UI 정책이 분산되어 있다.

결과적으로 새 목표성 기능이 추가될 때마다 중앙, 좌상단, 우측 등 임의 위치의 패널이 늘어나는 구조가 된다. 이 방식은 화면 충돌, 중복 입력, 중복 Canvas, 테스트 누락, Style2 패널 규약 위반 가능성을 키운다.

## Decisions

### 1. Tab Opens Objective Journal

`Tab`은 목표성 정보를 보는 전용 통합 패널인 `Objective Journal`을 연다.

기존 status/book UI는 별도 단축키나 journal 내부 보조 탭으로 재배치할 수 있지만, 첫 설계 기준에서는 `Tab = Objective Journal`로 고정한다. 이유는 플레이어가 `Tab`을 눌렀을 때 가장 자주 확인해야 하는 정보가 "지금 무엇을 하면 되는가"이기 때문이다.

### 2. Journal Uses Left Rail, List, Detail

`Objective Journal`의 기본 템플릿은 다음 3영역이다.

1. 왼쪽 카테고리 레일: `Goals`, `Quests`, `Campaign`, `Hints`
2. 가운데 목록: 선택된 카테고리의 항목 리스트
3. 오른쪽 상세: 선택 항목의 설명, 진행도, 보상/결과, 액션

이 구조는 모든 목표성 도메인에 같은 배치 규칙을 제공한다. 퀘스트가 늘어나거나 캠페인 단계가 많아져도 패널 자체의 좌표를 새로 만들지 않고 목록과 상세 모델만 갱신한다.

### 3. HUD Shows Only One Tracked Objective

플레이 화면의 HUD는 전체 goal/campaign/quest 목록을 노출하지 않는다. `TrackedObjectiveHud`는 플레이어가 추적 중인 1개 목표만 간단히 보여준다.

HUD 내용은 다음으로 제한한다.

- 목표 이름
- 현재 진행도 1줄
- 다음 행동 힌트 1줄
- 완료/보상 가능 상태 아이콘 또는 짧은 텍스트

상세 설명, 전체 목록, 보상 수령, 캠페인 경로 비교는 `Objective Journal`로 이동한다.

## Architecture

### UI Shell

`Rootborn.UI.Objectives.ObjectiveJournalPanel`이 통합 화면의 소유자다.

책임:

- Style2 `ModernUiRecipes.CommonPanel` 기반 루트 패널 생성
- 카테고리 레일 생성
- 목록 영역 생성과 선택 상태 관리
- 상세 영역 생성
- `Track`, `Claim`, `Navigate` 같은 공통 액션 버튼 슬롯 제공
- 항목 수가 많을 때 스크롤/가상화 또는 페이지 단위 표시 지원
- 패널 열림/닫힘과 입력 포커스 관리

금지:

- 특정 questId, campaignId, milestoneId별 분기
- 특정 도메인 데이터의 런타임 규칙 직접 평가
- 개별 도메인 UI 패널을 좌표째로 자식 삽입하는 래핑 방식

### Provider Boundary

각 도메인은 `ObjectiveJournalPanel`에 표시할 행과 상세 정보를 제공하는 provider를 둔다.

개념 인터페이스:

```text
IObjectiveJournalProvider
- CategoryId
- DisplayNameKey
- BuildItems(context)
- BuildDetail(itemId, context)
- TryExecuteAction(actionId, itemId, context)
```

첫 구현 provider:

- `GoalObjectiveProvider`: milestone/goal 진행 정보를 journal item으로 변환
- `QuestObjectiveProvider`: `QuestLog`와 `QuestDefinition[]`를 journal item으로 변환
- `CampaignObjectiveProvider`: `CampaignProgress`와 `CampaignDefinition`을 journal item으로 변환
- `HintObjectiveProvider`: 진로 힌트, 다음 추천 행동, 발견된 경로를 journal item으로 변환

provider는 entity ID별 C# 분기를 하지 않는다. 표시와 액션 가능 여부는 ScriptableObject 정의와 진행 상태에서 계산한다.

### View Models

UI Shell은 도메인 타입에 직접 의존하지 않고 작은 표시 모델을 받는다.

```text
ObjectiveJournalItem
- StableKey
- CategoryId
- Title
- Subtitle
- State
- ProgressText
- IsTracked
- SortPriority

ObjectiveJournalDetail
- Title
- Body
- ProgressRows
- RewardRows
- ActionRows
- EmptyStateText
```

문자열은 첫 단계에서 기존 display key와 fallback text를 함께 제공하고, fallback은 테스트 데이터와 미등록 localization key 상태에서만 사용한다. 최종 표시 문구는 localization key 기반으로 정리한다.

### Input Routing

`ModernUiPanelInputRouter`는 여러 패널을 직접 필드로 들고 토글하는 방식에서 `UI panel registry/router` 성격으로 확장한다.

첫 implementation slice에서는 최소 변경으로 다음만 보장한다.

- `Tab`은 `ObjectiveJournalPanel`을 토글한다.
- journal이 열리면 inventory/status/settings 같은 다른 full panel은 닫힌다.
- `Escape`는 열린 journal을 닫거나 settings 우선순위와 충돌하지 않도록 명시한다.
- 패널이 열려 있을 때 플레이어 이동/상호작용 입력 차단 여부는 기존 input gate 규칙과 맞춘다.

### Runtime Installation

Canvas와 EventSystem 생성은 각 기능 installer가 반복하지 않는다.

권장 흐름:

1. 공통 UI installer가 씬 Canvas를 찾거나 생성한다.
2. `ObjectiveJournalPanel`과 `TrackedObjectiveHud` host를 같은 Canvas 아래 생성한다.
3. quest/campaign/goal provider는 runner가 scene/player/registry 상태를 찾아 panel에 bind한다.
4. 기존 `CampaignHudPanel`, `MilestoneHudPanel`은 새 HUD/provider로 역할을 옮긴 뒤 제거하거나 compatibility shim으로 축소한다.

## UI Layout Contract

기준 해상도는 기존 규칙과 같이 1920x1080 Canvas Scaler다.

`ObjectiveJournalPanel`:

- 화면 중앙 정렬
- 권장 크기: 1180 x 720
- 왼쪽 category rail: 약 180 px
- 가운데 list: 약 360 px
- 오른쪽 detail: 나머지 영역
- 패널 배경은 `ModernUiTileImage + ModernUiRecipes.CommonPanel`
- 패널 내부 repeated item도 Style2 recipe 기반으로 만든다.

`TrackedObjectiveHud`:

- 화면 우측 또는 좌측 중 한 슬롯만 사용한다. 최종 구현 전 PlayMode 스크린샷으로 기존 HUD와 충돌 위치를 확정한다.
- 권장 크기: 360 x 96 이하
- 항상 1개 항목만 표시한다.
- 추적 목표가 없으면 숨긴다.

## Migration Plan

1. 새 `ObjectiveJournalPanel` shell과 표시 모델 테스트를 먼저 작성한다.
2. `Tab` 입력이 journal을 열고 다른 full panel을 닫는 router 테스트를 작성한다.
3. `TrackedObjectiveHud`가 1개 목표만 표시하고 목표가 없으면 숨는 테스트를 작성한다.
4. `GoalObjectiveProvider`를 `MilestoneHudPanel`의 포맷 로직에서 분리한다.
5. `QuestObjectiveProvider`를 `QuestLogPanel`의 핵심 표시 정보와 연결한다.
6. `CampaignObjectiveProvider`를 `CampaignHudPanel`의 summary builder 결과와 연결한다.
7. 기존 좌상단 `MilestoneHudPanel`과 별도 `CampaignHudPanel` 자동 노출을 끄고, 필요하면 provider-backed compatibility 경로만 남긴다.
8. PlayMode에서 실제 `Tab` 입력으로 journal이 열리고, 카테고리 선택과 tracking 선택이 동작하는 사용자 흐름을 검증한다.

## Testing Strategy

TDD를 따른다. 신규 C#은 실패 테스트를 먼저 만들고, Unity MCP `script-update-or-create` 또는 Unity Editor 경유로 작성한다.

EditMode:

- `ObjectiveJournalPanel`이 category/list/detail 슬롯을 생성한다.
- Style2 common panel recipe를 사용한다.
- provider가 null/empty 데이터를 빈 상태로 반환한다.
- router가 `Tab`을 journal 토글로 노출하고 full panel stacking을 막는다.
- provider는 entity ID별 branching 없이 SO/진행 상태 기반으로 item을 만든다. 기존 `check-no-entity-id-branching.sh` CI를 통과해야 한다.

PlayMode:

- 실제 키 입력 `Tab`으로 journal을 연다.
- 카테고리 선택, 항목 선택, 추적 버튼 클릭을 실제 UI 클릭 경로로 검증한다.
- 추적 선택 후 journal을 닫으면 `TrackedObjectiveHud`가 1개 목표만 보여준다.
- 기존 플레이 HUD와 텍스트/패널이 겹치지 않는지 스크린샷 또는 RectTransform bounds 기반으로 검증한다.

## Performance Notes

목표/퀘스트/캠페인은 장기적으로 수가 늘어난다. 첫 구현부터 다음 원칙을 지킨다.

- 매 프레임 전체 provider rebuild 금지
- quest/campaign/goal 상태 변경 이벤트 또는 panel open 시점에만 refresh
- 목록 행은 필요 시 pooling 또는 페이지 단위 갱신 가능하게 설계
- 문자열 조립은 refresh 시점으로 제한
- provider 결과는 context version이 바뀔 때만 갱신 가능하게 확장 여지를 둔다.

## Non-Goals

- 이번 설계에서 모든 goal/quest/campaign 데이터를 완전한 최종 UI로 polish하지 않는다.
- 보상/퀘스트/캠페인 런타임 규칙 자체를 재설계하지 않는다.
- 기존 status, inventory, settings 전체 UX를 새로 설계하지 않는다.
- 개별 entity별 C# UI 컴포넌트를 만들지 않는다.

## Open Implementation Questions

다음 계획 단계에서 확정한다.

- `Tab`을 기존 status 패널에서 완전히 회수할지, status는 다른 단축키로 이동할지
- `TrackedObjectiveHud`의 최종 위치: 좌측 상단 기존 goal 영역 대체 또는 우측 미니 tracker
- provider context를 scene runner가 구성할지, 공통 player UI context service를 둘지
- 상세 패널의 `Claim` 액션을 첫 slice에 포함할지, quest reward button은 다음 slice로 미룰지

## Acceptance Criteria

- 목표성 UI를 새로 추가할 때 화면 좌표를 개별 패널에 직접 박지 않는다.
- `Tab`으로 열리는 단일 Objective Journal에서 Goals, Quests, Campaign, Hints를 볼 수 있다.
- 플레이 HUD에는 추적 중인 1개 목표만 표시된다.
- 기존 `CampaignHudPanel`/`MilestoneHudPanel`식 상시 노출 패널은 제거되거나 새 provider/HUD 구조 아래로 이동한다.
- Style2 common panel 규약을 지킨다.
- 실제 사용자 입력과 UI 클릭을 통한 PlayMode 검증이 존재한다.
