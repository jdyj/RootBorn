# 직업 사이 자유도 1단계(기술·일터 꾸미기·공간 이득) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 요리사가 자기 식당에 가구를 놓으면 공간 성질 점수가 오르고 요리 일의 보상·명성이 늘며, 인테리어 기술을 올리면 놓을 수 있는 수와 점수 배율이 는다. 다른 시설은 데이터만 더해 같은 것이 된다.

**Architecture:** 새 Core 영역 `Rootborn.Core.Workspaces`가 성질·점수 계산·꾸미기 규칙·일의 공간 이득·템플릿 적용을 맡는다. `AdultCareerService`는 값(`WorkExtras`)으로 이득을 받아 정산에 더하고, 값이 비면 지금과 똑같이 돈다. 일터 배치는 집 배치 코드(`HouseFurnitureLayout`)를 그대로 쓰고, 새 데이터는 `data/workspaces/`에 둔다(2026-11-12부터 `data/`가 손으로 고치는 원본이다).

**Tech Stack:** C# (Core는 엔진·파일·JSON 참조 없음), .NET, NUnit, MonoGame 3.8.6.1(Host만).

**Spec:** `docs/superpowers/specs/2026-11-12-cross-career-freedom-design.md`

## Global Constraints

- 규칙 판단은 Core에만. Game은 잇고 저장만, 화면은 `GameSession` 명령·조회만 부른다.
- Core는 `game/Rootborn.Core/src`, Core 테스트는 `game/tests/Rootborn.Core.Tests`에 있다(`.meta`는 쓰지 않는다). 문법·이름 관례는 같은 폴더의 기존 파일을 따른다. 클래스는 `sealed`.
- 엔티티 id로 분기하지 않는다(시설·일·가구·기술·성질·맵 id 모두). 종류를 `enum`·`switch`로 닫지 않는다.
- 새 데이터는 `data/workspaces/`에 쓴다(`data/README.md`의 규칙: 파일 이름 = `type`). 기존 엔티티의 수치·내용은 고치지 않는다. 데이터 폴더를 여러 개 읽는 장치는 만들지 않는다.
- 기존 세이브 모양의 필드 이름·형을 바꾸지 않는다. 새 세이브 모양은 `Version = 1`, `SaveShapeCoverage`에 등록.
- 지급·적용은 미리 확인 → 하나라도 안 되면 무변경 → 요청 id로 한 번만 → 저장·다시 읽기 뒤에도 한 번만.
- 사람이 읽는 문구는 `game/localization/ko.json`·`en.json`의 키로. 문장을 코드에서 붙이지 않는다. 글자 크기 14 미만 금지.
- 화면 코드는 스킨 이름·아이콘 키만 쓴다. 테스트는 그림 픽셀·특정 맵 좌표에 기대지 않는다.
- **수치는 전부 시험용이다.** `data/workspaces/`의 수치마다 `data/README.md`의 새 절에 "시험용, 사용자 확인 전"이라고 적는다. 실제 수치는 사용자가 정한다.
- 자유 배치는 기술로 잠그지 않는다(사용자 결정). 꾸미지 않아도 손해가 없다: 이득은 0 이상만 더한다.
- 커밋: 한글, `[FEATURE]` 등 prefix, 끝줄 `Co-Authored-By: <그 커밋을 만든 모델 이름> <noreply@anthropic.com>`. main이 아닌 작업 브랜치에 커밋하고 푸시하지 않는다.
- 명령 앞에: `export PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet"`. 커밋 전 `bash Scripts/ci/run-all.sh --checks-only`.
- 이 계획에서 하지 않는 것(스펙 1단계 범위 밖으로 미룸): 미니게임 관대함 이득, 목표 수첩 안내, 칸 위치에 기대는 점수, 의뢰·기여.

## Review Focus

1. **규칙이 없는 가구·모르는 id**: 성질 규칙이 없는 가구는 점수 0으로 놓인다. 규칙이 없는 분류·가구·성질을 가리키면 조용히 0이 아니라 읽기 오류다. → Task 1·6.
2. **남의 일터**: 자기 시설이 아닌 일터 맵에 들어가면 배치·템플릿 명령은 거절되고 기본 템플릿도 깔리지 않는다. 놓인 것은 보인다. → Task 8.
3. **한도를 넘은 옛 배치**: 수치가 줄어 이미 놓인 가구가 한도를 넘으면 놓인 것은 그대로 두고 새로 놓기만 막는다. 옮기기·치우기는 된다. → Task 8.
4. **보상 비율 합이 10000을 넘음**: 레벨 비율 + 공간 이득이 넘으면 일이 실패하는 것이 아니라 10000으로 자른다. → Task 4.
5. **예약과 정산 사이에 꾸밈**: 미니게임 일은 예약할 때의 보상 비율을 쓴다. 예약 뒤 가구를 놓아도 그 판의 보상은 바뀌지 않는다. 명성 이득은 정산 때 것을 쓴다. → Task 4·9.

---

## 파일 구조

| 파일 | 책임 |
|---|---|
| `game/Rootborn.Core/src/Workspaces/SpaceQuality.cs` | 성질 정의, `QualityScore` |
| `…/Workspaces/FurnitureQualityTable.cs` | 가구 성질 규칙과 펼친 표 |
| `…/Workspaces/SpaceScores.cs` | 점수 묶음, 점수 원천(`ISpaceQualitySource`), 가구 원천 |
| `…/Workspaces/SkillTier.cs` | 기술 단계 |
| `…/Workspaces/Workspace.cs` | 일터 정의, 꾸미기 규칙(`IDecorationRule`)과 두 구현 |
| `…/Workspaces/WorkLinks.cs` | 일의 공간 이득(`IWorkspaceBonus`)과 두 구현, 일↔이득·기술 성장 연결, 조회 표 |
| `…/Workspaces/WorkspaceTemplate.cs` | 템플릿 정의 |
| `…/Workspaces/WorkspaceProgress.cs` | 세이브 모양, 진행, 파일 읽기·쓰기 |
| `…/Workspaces/WorkspaceService.cs` | 템플릿 적용 판정·실행, 꾸미며 배우기 |
| `…/Careers/CareerContracts.cs`(수정) | `WorkExtras` 값 |
| `…/Careers/AdultCareerService.cs`, `AdultCareerProgress.cs`(수정) | 정산에 `WorkExtras` 반영 |
| `…/Placement/HouseFurnitureLayout.cs`(수정) | 받는 면 목록, 한꺼번에 바꾸기 |
| `…/Save/SaveFileNames.cs`(수정) | 일터 파일 이름 두 개 |
| `game/Rootborn.Data/Builders/WorkspaceBuilders.cs` | 새 종류 등록 |
| `game/Rootborn.Data/Builders/RootbornBuilders.cs`(수정) | 새 영역 등록 한 줄 |
| `game/Rootborn.Game/GameSession.Workspace.cs` | 일터 들어가기, 템플릿 명령, 점수·이득 조회 |
| `game/Rootborn.Game/GameSession.Furniture.cs`, `GameSession.Day.cs`(수정) | 한도 검사, 일 명령에 `WorkExtras` |
| `data/workspaces/*.json`, `data/README.md`(수정) | 요리사 한 줄 데이터(시험용 수치) |
| `game/Rootborn.Screens/…`(수정·추가) | 배치 화면 점수 줄, 템플릿 화면, 시설 화면 이득 줄 |
| `game/world/interactions.json`(수정) | 임시: 주방 맵을 요리사 일터 면으로 |
| 테스트 | `game/tests/Rootborn.Core.Tests/Workspace*.cs`, `game/tests/Rootborn.Data.Tests/WorkspaceDataTests.cs`, `game/tests/Rootborn.Game.Tests/WorkspaceTests.cs`, Screens·Host 테스트 |

---

### Task 1: 성질, 가구 성질 표, 공간 점수

**Files:**
- Create: `game/Rootborn.Core/src/Workspaces/SpaceQuality.cs`, `FurnitureQualityTable.cs`, `SpaceScores.cs`
- Test: `game/tests/Rootborn.Core.Tests/WorkspaceScoreTests.cs`

**Interfaces:**
- Consumes: `Rootborn.Core.Placement.FurnitureDefinition`(`StableId`, `Category`)
- Produces:
  ```csharp
  namespace Rootborn.Core.Workspaces
  public sealed class SpaceQuality { public SpaceQuality(string id, string displayNameKey, string iconKey); public string Id, DisplayNameKey, IconKey { get; } }
  public readonly struct QualityScore { public QualityScore(string qualityId, int score); public string QualityId { get; } public int Score { get; } }
  // category와 furnitureId 중 정확히 하나만 채운다. score는 0 이상.
  public sealed class FurnitureQualityRule { public FurnitureQualityRule(string id, string category, string furnitureId, string qualityId, int score); public bool IsValid { get; } … }
  public sealed class FurnitureQualityTable {
      public FurnitureQualityTable(IEnumerable<FurnitureQualityRule> rules, IEnumerable<FurnitureDefinition> furniture);
      public IReadOnlyList<QualityScore> ScoresOf(string furnitureId);   // 없으면 빈 목록
      public IReadOnlyList<string> Problems { get; }                     // 모르는 가구 id를 가리킨 규칙 등
  }
  public sealed class SpaceScores { public static readonly SpaceScores Empty; public int Get(string qualityId); public IReadOnlyList<QualityScore> All { get; } }
  public interface ISpaceQualitySource { void AddTo(Dictionary<string, long> totals); }
  public sealed class FurnitureQualitySource : ISpaceQualitySource { public FurnitureQualitySource(IReadOnlyList<string> placedFurnitureIds, FurnitureQualityTable table, int repeatPercent); }
  public static class SpaceScoreCalculator { public static SpaceScores Calculate(IReadOnlyList<ISpaceQualitySource> sources, int scorePercent); }
  ```
- 규칙: 같은 가구·같은 성질에 가구 규칙과 분류 규칙이 둘 다 있으면 가구 규칙이 이긴다(더하지 않는다). 같은 가구의 n번째(1부터)는 `score × repeatPercent^(n-1) / 100^(n-1)`(정수 내림, n마다 차례로 곱해 내림). `scorePercent`는 합에 한 번 곱한다(`합 × scorePercent / 100`). 결과는 `int.MaxValue`로 자른다. `All`은 성질 id 순.

- [ ] **Step 1: 실패 테스트 쓰기** — `WorkspaceScoreTests`:
  - `CategoryRule_GivesScoreToEveryFurnitureOfThatCategory`: 분류 "Kitchen"에 `cozy` 3, 가구 a·b(둘 다 Kitchen) 놓음, repeat 100, percent 100 → `Get("cozy") == 6`.
  - `FurnitureRule_OverridesCategoryRule`: 위에 가구 a에 `cozy` 10 → a 하나 놓으면 10.
  - `RepeatedFurniture_Falls`: a(`cozy` 10) 세 개, repeat 50 → 10 + 5 + 2 = 17.
  - `ScorePercent_MultipliesTotal`: 합 10, percent 150 → 15.
  - `FurnitureWithoutRule_ScoresZero`와 `UnknownQuality_ReturnsZero`.
  - `RuleForUnknownFurniture_IsReportedAsProblem`: `Problems.Count == 1`.
  - `RuleWithBothCategoryAndFurniture_IsInvalid`.
- [ ] **Step 2: 실패 확인** — `dotnet test game/tests/Rootborn.Core.Tests --filter WorkspaceScoreTests` → 컴파일 오류.
- [ ] **Step 3: 구현** — 표는 생성자에서 가구 id → `QualityScore[]`로 한 번 펼친다(조회 때 목록을 훑지 않는다).
- [ ] **Step 4: 통과 확인** — 같은 명령, 8건 통과.
- [ ] **Step 5: 커밋** — `[FEATURE] 공간 성질 점수 — 가구 성질 표와 되풀이 줄이기 계산`

### Task 2: 기술 단계와 일터 정의

**Files:**
- Create: `…/Workspaces/SkillTier.cs`, `Workspace.cs`
- Test: `game/tests/Rootborn.Core.Tests/WorkspaceDefinitionTests.cs`

**Interfaces:**
- Consumes: `StudentLifeProgress.GetSkillValue(string)`
- Produces:
  ```csharp
  public sealed class SkillTier { public SkillTier(string id, string skillId, int minimumValue, string displayNameKey); public bool IsReached(StudentLifeProgress student); … }
  public interface IDecorationRule { int CapacityBonus(StudentLifeProgress student); int ScorePercentBonus(StudentLifeProgress student); }
  public sealed class SkillTierCapacityRule : IDecorationRule { public SkillTierCapacityRule(SkillTier tier, int capacityBonus); }
  public sealed class SkillTierScoreRule : IDecorationRule { public SkillTierScoreRule(SkillTier tier, int scorePercentBonus); }
  public sealed class Workspace {
      public Workspace(string id, string facilityId, string surfaceId, string[] acceptsSurfaceIds, int[] capacityByLevel, int repeatPercent, IDecorationRule[] rules, string defaultTemplateId);
      public string Id, FacilityId, SurfaceId, DefaultTemplateId { get; } public IReadOnlyList<string> AcceptsSurfaceIds { get; } public int RepeatPercent { get; }
      public bool IsValid { get; }                                  // id·시설·면이 있고 capacityByLevel 길이 3, 값 0 이상, repeatPercent 0~100
      public int Capacity(int facilityLevel, StudentLifeProgress student);   // 레벨 1~3 밖이면 0. 레벨 값 + 닿은 규칙의 보너스 합
      public int ScorePercent(StudentLifeProgress student);                  // 100 + 닿은 규칙의 보너스 합
      public bool CanAdd(int placedCount, int facilityLevel, StudentLifeProgress student);  // placedCount < Capacity
  }
  ```
- 닿지 않은 단계의 규칙은 0을 준다. 보너스는 음수일 수 없다(음수 데이터는 0으로 다듬는다).

- [ ] **Step 1: 실패 테스트** — `Capacity_IsLevelValuePlusReachedTierBonuses`(레벨 값 {4,6,8}, 단계(기술 20)+2, 기술 0 → 레벨 2에서 6, 기술 20 → 8), `ScorePercent_AddsOnlyReachedTiers`(기술 20 단계 +25 → 125, 못 닿으면 100), `CanAdd_FalseAtCapacity`, `Capacity_ZeroOutsideLevels`, `NegativeBonus_IsTreatedAsZero`, `Workspace_InvalidWithoutThreeLevels`.
- [ ] **Step 2: 실패 확인** — `dotnet test game/tests/Rootborn.Core.Tests --filter WorkspaceDefinitionTests`
- [ ] **Step 3: 구현**
- [ ] **Step 4: 통과 확인**
- [ ] **Step 5: 커밋** — `[FEATURE] 일터 정의와 기술 단계 — 기술이 가구 한도와 점수 배율을 올린다`

### Task 3: 일의 공간 이득과 일 연결 표

**Files:**
- Create: `…/Workspaces/WorkLinks.cs`
- Modify: `…/Careers/CareerContracts.cs` — `WorkExtras` 더하기
- Test: `game/tests/Rootborn.Core.Tests/WorkLinksTests.cs`

**Interfaces:**
- Consumes: `SpaceScores`(Task 1)
- Produces:
  ```csharp
  namespace Rootborn.Core.Careers
  public readonly struct SkillGain { public SkillGain(string skillId, int amount); … }
  // default(WorkExtras)는 아무것도 더하지 않는다.
  public readonly struct WorkExtras { public WorkExtras(int rewardPercentBonus, int reputationBonus, IReadOnlyList<SkillGain> skillGains); public int RewardPercentBonus, ReputationBonus { get; } public IReadOnlyList<SkillGain> SkillGains { get; } /* null이면 빈 목록 */ }

  namespace Rootborn.Core.Workspaces
  public interface IWorkspaceBonus { int RewardPercent(SpaceScores scores); int Reputation(SpaceScores scores); }
  // 점수 scorePerStep마다 amountPerStep, 위 한도 maximum. scorePerStep <= 0이면 0.
  public sealed class QualityRewardPercentBonus : IWorkspaceBonus { public QualityRewardPercentBonus(string qualityId, int scorePerStep, int percentPerStep, int maximumPercent); }
  public sealed class QualityReputationBonus : IWorkspaceBonus { public QualityReputationBonus(string qualityId, int scorePerStep, int reputationPerStep, int maximumReputation); }
  public sealed class WorkBonusBinding { public WorkBonusBinding(string id, string workId, IWorkspaceBonus[] bonuses); … }
  public sealed class WorkSkillGrowth { public WorkSkillGrowth(string id, string workId, string skillId, int amount); … }
  public sealed class WorkLinks {
      public WorkLinks(IEnumerable<WorkBonusBinding> bonuses, IEnumerable<WorkSkillGrowth> growth);
      public WorkExtras ExtrasFor(string workId, SpaceScores scores);   // 연결이 없으면 default
  }
  ```

- [ ] **Step 1: 실패 테스트** — `RewardBonus_StepsAndCaps`(점수 25, 10마다 5%, 한도 8 → 8; 점수 9 → 0), `ReputationBonus_StepsAndCaps`, `ExtrasFor_SumsAllBindingsOfThatWork`, `ExtrasFor_UnlinkedWork_IsDefault`, `ExtrasFor_IncludesSkillGains`.
- [ ] **Step 2: 실패 확인** — `--filter WorkLinksTests`
- [ ] **Step 3: 구현** — `WorkLinks`는 생성자에서 일 id → 목록 사전을 만든다.
- [ ] **Step 4: 통과 확인**
- [ ] **Step 5: 커밋** — `[FEATURE] 일의 공간 이득 — 성질 점수가 보상 비율과 명성을 더한다`

### Task 4: 일 정산에 `WorkExtras` 반영

**Files:**
- Modify: `…/Careers/AdultCareerService.cs`(`TryCompleteWork`, `TryReserveWork`, `TrySettleReservedWork`, `CheckCompleteWork`), `…/Careers/AdultCareerProgress.cs`(`CompleteWork`)
- Test: `game/tests/Rootborn.Core.Tests/AdultCareerTests.cs`에 더하기

**Interfaces:**
- Consumes: `WorkExtras`(Task 3)
- Produces: 세 메서드 끝에 선택 인자 `WorkExtras extras = default`.
  - `TryCompleteWork(…, bool autoSkip, WorkExtras extras = default)`: 보상 비율 = `min(10000, level.RewardPercent + extras.RewardPercentBonus)`. 성공(점수 ≥ 최소 성공 점수)이면 명성에 `extras.ReputationBonus`를 더하고 `extras.SkillGains`마다 `student.AddSkill`.
  - `TryReserveWork(…, int seed, WorkExtras extras = default)`: 예약에 저장하는 `RewardPercent`가 위 합(자른 값).
  - `TrySettleReservedWork(…, int score, WorkExtras extras = default)`: 보상 비율은 예약 값. 성공이면 명성 보너스와 기술 성장 적용.
  - `AdultCareerProgress.CompleteWork(FacilityWork work, string request, bool success, int reputationBonus = 0)`.
- 음수 보너스는 0으로 본다.

- [ ] **Step 1: 실패 테스트** — 기존 `AdultCareerTests`의 준비 도우미를 써서:
  - `CompleteWork_WithRewardBonus_PaysMore`: 레벨 100%, 최대 보상 30, 점수 100, 보너스 50 → 45.
  - `CompleteWork_RewardPercentIsCappedAt10000`: 보너스 20000 → 결과 Applied, 보상 = 최대 × 100배.
  - `CompleteWork_Success_AddsReputationBonusAndSkill`: 명성 = `ReputationPerSuccess` + 3, `student.GetSkillValue("skill.test") == 2`.
  - `CompleteWork_BelowSuccessScore_NoBonusNoSkill`.
  - `ReserveThenSettle_UsesBonusFromReservation`: 예약 보너스 50, 정산 때 보너스 0 → 보상은 150% 기준.
  - `DuplicateRequest_DoesNotAddSkillTwice`.
  - `DefaultExtras_SameAsBefore`: 기존 기대값 그대로(기존 테스트가 모두 그대로 통과하는 것으로 갈음해도 된다).
- [ ] **Step 2: 실패 확인** — `dotnet test game/tests/Rootborn.Core.Tests --filter AdultCareerTests`
- [ ] **Step 3: 구현** — 기술 성장은 인벤토리 교환의 commit 안에서 한다(보상이 안 들어가면 기술도 안 오른다).
- [ ] **Step 4: 통과 확인** — 기존 건수 + 새 건수.
- [ ] **Step 5: 커밋** — `[FEATURE] 시설 일 정산이 공간 이득과 기술 성장을 받는다 — 값이 없으면 전과 같다`

### Task 5: 배치가 받는 면 목록과 한꺼번에 바꾸기

**Files:**
- Modify: `…/Placement/HouseFurnitureLayout.cs`
- Test: `game/tests/Rootborn.Core.Tests`의 기존 배치 테스트 파일에 더하기(이름은 그 폴더에서 `HouseFurnitureLayout`을 쓰는 파일)

**Interfaces:**
- Produces:
  ```csharp
  public readonly struct FurniturePlacementRequest { public FurniturePlacementRequest(FurnitureDefinition definition, GridPoint anchor, FurniturePlacementDirection direction); … }
  // 생성자 끝에 선택 인자. 가구가 SurfaceId 또는 acceptedSurfaceIds 중 하나를 허용하면 놓을 수 있다.
  public HouseFurnitureLayout(HousePlacementModel model, string surfaceId, Func<FurniturePlacementSaveData[], bool> commit = null, IReadOnlyList<string> acceptedSurfaceIds = null);
  public bool CanReplaceUserFurniture(IReadOnlyList<FurniturePlacementRequest> items);   // 빈 방에 차례로 놓을 수 있는지. 아무것도 바꾸지 않는다
  public bool TryReplaceUserFurniture(IReadOnlyList<FurniturePlacementRequest> items);   // 전부 놓이면 한 번만 commit. 아니면 무변경. PlacementCommitted는 내지 않는다
  ```
- `AllowsSurface(SurfaceId)`를 보는 모든 자리(`CanPlace`, `Restore`, 옮기기)가 받는 면 목록도 본다. 저장 기록의 `SurfaceId`는 이 배치의 `SurfaceId`로 쓴다. 놓지 못했던 기록(`_unresolved`)은 바꾼 뒤에도 남긴다.

- [ ] **Step 1: 실패 테스트** — `AcceptedSurface_AllowsHouseFurnitureOnOtherSurface`, `WithoutAcceptedSurface_StillRefused`, `ReplaceAll_AllOrNothing`(둘째가 겹치면 false이고 원래 배치와 commit 횟수 그대로), `ReplaceAll_CommitsOnce`, `ReplaceAll_CommitFails_NothingChanges`, `ReplaceAll_KeepsUnresolvedRecords`.
- [ ] **Step 2: 실패 확인**
- [ ] **Step 3: 구현**
- [ ] **Step 4: 통과 확인**
- [ ] **Step 5: 커밋** — `[FEATURE] 가구 배치가 다른 면의 가구를 받고 한꺼번에 바꾼다 — 일터와 템플릿의 바탕`

### Task 6: 템플릿, 일터 진행 세이브, 템플릿 적용과 꾸미며 배우기

**Files:**
- Create: `…/Workspaces/WorkspaceTemplate.cs`, `WorkspaceProgress.cs`, `WorkspaceService.cs`
- Modify: `…/Save/SaveFileNames.cs`
- Test: `game/tests/Rootborn.Core.Tests/WorkspaceTemplateTests.cs`

**Interfaces:**
- Consumes: Task 2 `Workspace`, Task 5 `HouseFurnitureLayout.CanReplaceUserFurniture/TryReplaceUserFurniture`, `ICareerInventory`, `ISaveStore`
- Produces:
  ```csharp
  public readonly struct TemplatePlacement { public TemplatePlacement(string furnitureId, GridPoint anchor, FurniturePlacementDirection direction); … }
  public sealed class WorkspaceTemplate { public WorkspaceTemplate(string id, string workspaceId, string displayNameKey, ItemAmount<string>[] costs, TemplatePlacement[] placements); public IReadOnlyList<string> FurnitureIds { get; } … }
  // 놓으면 기술이 자란다. 하루에 dailyLimit번까지만 센다.
  public sealed class PlacementSkillGrowth { public PlacementSkillGrowth(string id, string surfaceId, string skillId, int amount, int dailyLimit); … }

  public sealed class WorkspaceProgressSaveData : IVersionedSave {   // Version 1
      public int Version; public string SaveSlot; public string PlayerId;
      public string[] AppliedRequestIds; public string[] SeededWorkspaceIds;
      public int GrowthDay; public int GrowthCountToday;
  }
  public sealed class WorkspaceProgress { public WorkspaceProgress(string saveSlot, string playerId); bool HasApplied(string requestId); bool IsSeeded(string workspaceId); int GrowthLeftToday(int day, int dailyLimit); … ToSaveData()/FromSaveData }
  public static class WorkspaceSaveFiles { LoadOrCreate(ISaveStore, saveSlot, playerId); Save(ISaveStore, WorkspaceProgress); LoadLayout(ISaveStore, surfaceId, playerId) /* 없으면 null */; SaveLayout(…records) }
  // SaveFileNames
  public static string WorkspaceProgress(string playerId) => PerPlayer("workspace-progress-", playerId);
  public static string WorkspaceLayout(string surfaceId, string playerId) => "workspace-layout-" + Sanitize(surfaceId) + "-" + Sanitize(playerId) + ".json";

  public enum WorkspaceResultKind { Applied, DuplicateRequest, InvalidRequest, InvalidDefinition, NotOwner, InsufficientResources, DoesNotFit, OverCapacity }
  public sealed class WorkspaceService {
      // catalog: 가구 id → 정의. 자기 시설인지와 레벨은 호출하는 쪽이 값으로 넘긴다.
      public WorkspaceResultKind CanApplyTemplate(WorkspaceTemplate template, Workspace workspace, HouseFurnitureLayout layout, IReadOnlyDictionary<string, FurnitureDefinition> catalog,
          ICareerInventory inventory, WorkspaceProgress progress, bool ownsFacility, int capacity, string requestId, bool free);
      public WorkspaceResultKind TryApplyTemplate(/* 같은 인자 */);
      public bool TryGrowByPlacing(PlacementSkillGrowth rule, StudentLifeProgress student, WorkspaceProgress progress);   // 오늘 한도 안이면 기술을 올리고 센다
  }
  ```
- 판정 순서: 빈 요청 id → `InvalidRequest`; 이미 적용 → `DuplicateRequest`; 템플릿·일터 불일치나 모르는 가구 id → `InvalidDefinition`; `!ownsFacility` → `NotOwner`; 가구 수 > capacity → `OverCapacity`; `!layout.CanReplaceUserFurniture` → `DoesNotFit`; `!free && !inventory.CanExchange(costs, null)` → `InsufficientResources`.
- 실행: `inventory.TryExchange(free ? null : costs, null, commit)`의 commit 안에서 `layout.TryReplaceUserFurniture`와 `progress`에 요청 id 기록. 배치 저장이 실패하면 예외를 던져 교환을 되돌린다(`AdultCareerService`의 "Prevalidated … changed" 방식). `free`는 기본 템플릿 깔기용이고 요청 id는 일터 id를 쓴다(`SeededWorkspaceIds`에도 적는다).

- [ ] **Step 1: 실패 테스트** — 보상 원칙 다섯 경우와 나머지:
  - `Apply_PaysAndPlaces`(돈 10 → 0, 놓인 수 = 템플릿 수).
  - `Apply_NotEnoughMoney_NothingChanges`(돈·배치·진행 그대로).
  - `Apply_OnePlacementDoesNotFit_NothingChanges`.
  - `Apply_SameRequestTwice_PaysOnce`(둘째 `DuplicateRequest`).
  - `Apply_AfterSaveAndReload_StillOnce`(진행을 `ToSaveData`→`FromSaveData` 한 뒤 같은 요청 → `DuplicateRequest`).
  - `Apply_NotOwner_Refused`, `Apply_OverCapacity_Refused`, `Apply_UnknownFurniture_InvalidDefinition`.
  - `Apply_LayoutSaveFails_MoneyUnchanged`.
  - `FreeSeed_MarksSeeded_AndCostsNothing`.
  - `GrowByPlacing_StopsAtDailyLimit_AndResetsNextDay`(한도 2, 같은 날 세 번 → 기술 +2×amount, 다음 날 다시 오른다).
  - `SaveData_HigherVersion_IsNotRead`(기존 세이브 모양들의 관례를 따른다).
- [ ] **Step 2: 실패 확인** — `--filter WorkspaceTemplateTests`
- [ ] **Step 3: 구현**
- [ ] **Step 4: 통과 확인**
- [ ] **Step 5: 커밋** — `[FEATURE] 일터 템플릿 적용 — 전부 아니면 전무, 한 번만, 꾸미며 배우는 기술`

### Task 7: 데이터 폴더 여러 개, 새 종류 등록, 요리사 한 줄 데이터

**Files:**
- Modify: `game/Rootborn.Data/Builders/RootbornBuilders.cs`
- (로더·`StartFromData`·Host·Server의 데이터 폴더 인자는 고치지 않는다. `data/` 아래 모든 `*.json`을 이미 읽는다.)
- Create: `game/Rootborn.Data/Builders/WorkspaceBuilders.cs`, `data/workspaces/{SkillDefinition,SkillTierDefinition,SpaceQualityDefinition,FurnitureQualityRule,WorkspaceDefinition,SkillTierCapacityRule,SkillTierScoreRule,QualityRewardPercentBonus,QualityReputationBonus,WorkBonusBinding,WorkSkillGrowth,PlacementSkillGrowth,WorkspaceTemplate}.json`
- Modify: `game/tests/Rootborn.Data.Tests/SaveShapeCoverage.cs`, `game/localization/ko.json`·`en.json`, `data/README.md`(새 절: `workspaces/`의 종류 표와 "수치는 시험용, 사용자 확인 전")
- Test: `game/tests/Rootborn.Data.Tests/WorkspaceDataTests.cs`

**Interfaces:**
- Produces:
  ```csharp
  // 새 로더 API는 없다. 등록은 WorkspaceBuilders.Register(b) 하나이고 RootbornBuilders.Register가 부른다.
  // 다른 작업은 definitions.Get/TryGet/All<T>()로 Workspace, WorkspaceTemplate, SkillTier, PlacementSkillGrowth 등을 꺼낸다.
  ```
  - 같은 `(type, id)`가 두 폴더에 있으면 읽기 오류다(덮어쓰지 않는다).
  - `data/registry.json`에 올려야 하는지는 기존 종류의 관례를 따른다(등록부에 없는 엔티티는 파일·배열 순서대로 놓인다).
  - `DataFingerprint`는 `data/` 엔티티 파일을 이미 셈하므로 고치지 않는다.
- 등록(`WorkspaceBuilders.Register`, `RootbornBuilders`에 한 줄). JSON 필드 이름:

  | type | 필드 |
  |---|---|
  | `SkillTierDefinition` | `skill`(ref), `minimumValue`, `displayNameKey` |
  | `SpaceQualityDefinition` | `displayNameKey`, `iconKey` |
  | `FurnitureQualityRule` | `category` 또는 `furniture`(가구 `stableId` 문자열), `quality`(ref), `score` |
  | `SkillTierCapacityRule` | `tier`(ref), `capacityBonus` |
  | `SkillTierScoreRule` | `tier`(ref), `scorePercentBonus` |
  | `WorkspaceDefinition` | `facility`(ref), `surfaceId`, `acceptsSurfaceIds`, `capacityByLevel`(수 3개), `repeatPercent`, `rules`(ref들), `defaultTemplate`(ref) |
  | `QualityRewardPercentBonus` | `quality`(ref), `scorePerStep`, `percentPerStep`, `maximumPercent` |
  | `QualityReputationBonus` | `quality`(ref), `scorePerStep`, `reputationPerStep`, `maximumReputation` |
  | `WorkBonusBinding` | `work`(ref), `bonuses`(ref들) |
  | `WorkSkillGrowth` | `work`(ref), `skill`(ref), `amount` |
  | `PlacementSkillGrowth` | `surfaceId`, `skill`(ref), `amount`, `dailyLimit` |
  | `WorkspaceTemplate` | `workspace`(ref), `displayNameKey`, `costItems`(ref들), `costCounts`(수들), `placements`([{`furniture`, `cell`{x,y}, `direction`}]) |

  - `FurnitureQualityTable.Problems`와 올바르지 않은 `Workspace`·규칙은 `LoadError`로 낸다. 표는 로더가 한 번 만들어 `GameDefinitions`에서 꺼낼 수 있게 한다(`definitions.Get<FurnitureQualityTable>` 방식이든 속성이든 기존 `Currency`의 관례를 따른다).
- 데이터 내용(시험용 수치, README에 표시):
  - 기술 `skill.cooking`, `skill.interior`; 단계는 인테리어 2개(값 10, 30).
  - 성질 `quality.cozy`, `quality.hygiene`, `quality.flow`, `quality.safety`, `quality.focus`.
  - 성질 규칙은 분류 단위로: `Kitchen`→위생 2, `LivingRoom`→아늑함 2, `Bedroom`→아늑함 1, `Bathroom`→위생 1, `Plant`·`Decor`→아늑함 3.
  - 일터 `workspace.chef`: 시설 `facility.chef`, 면 `workspace.chef`, 받는 면 `["house"]`, 한도 {6, 10, 14}, 되풀이 70, 규칙: 인테리어 단계 1 → 한도 +2, 단계 2 → 한도 +4와 점수 +25%.
  - 이득: `work.chef.level1~3`에 아늑함 10마다 보상 +5%(한도 30), 위생 10마다 명성 +1(한도 3).
  - 기술 성장: `work.chef.level1~3` → 요리 +1. `PlacementSkillGrowth`: 면 `house`와 `workspace.chef` → 인테리어 +1, 하루 3번.
  - 템플릿 2개: `template.chef.basic`(값 없음, 기본), `template.chef.cozy`(용돈 20). 칸 좌표는 여기서는 서로 겹치지 않게만 적고, Task 11에서 고른 맵의 빈 바닥에 맞춰 고친다(맵에 맞는지는 Task 11의 World 테스트가 본다).

- [ ] **Step 1: 실패 테스트** — `WorkspaceDataTests`:
  - `RealData_LoadsWithoutErrors_WithWorkspaces`(`data`, `result.Ok`, 일터 1개 이상).
  - `DuplicateWorkspaceId_IsError`(임시 폴더, 같은 `(type, id)` 둘 — 기존 로더가 이미 막으면 그 동작을 고정만 한다).
  - `InvalidWorkspace_IsLoadError`(임시 폴더, `capacityByLevel` 2개).
  - `RealData_ChefWorkspace_IsValidAndLinked`(실제 데이터 id로: 일터가 올바르고, `WorkLinks.ExtrasFor("work.chef.level1", …)`가 기술 성장 1개를 준다, 기본 템플릿이 있다).
  - `EveryWorkspaceTemplate_FurnitureExists`.
  - `QualityRule_UnknownFurniture_IsLoadError`.
  - `WorkspaceProgressSaveData`를 `SaveShapeCoverage`에 등록(등록 안 하면 기존 검사가 실패하는지 먼저 본다).
  - `EveryWorkspaceType_IsRegistered`(`data/workspaces`의 모든 `type`이 등록됐다 — 기존 `RealDataTests` 분류 검사가 이미 본다면 그것으로 갈음).
- [ ] **Step 2: 실패 확인** — `dotnet test game/tests/Rootborn.Data.Tests --filter WorkspaceDataTests`
- [ ] **Step 3: 구현과 데이터 쓰기** — 문구 키(성질 5, 단계 2, 기술 2, 템플릿 2)를 `ko.json`·`en.json`에 넣는다.
- [ ] **Step 4: 통과 확인** — `dotnet test game/tests/Rootborn.Data.Tests` 전체, 건수가 줄지 않았는지.
- [ ] **Step 5: 커밋** — `[FEATURE] 일터 데이터와 새 종류 등록 — 요리사 식당 한 줄(시험용 수치)`

### Task 8: GameSession — 일터 들어가기, 한도, 템플릿, 점수 조회

**Files:**
- Create: `game/Rootborn.Game/GameSession.Workspace.cs`
- Modify: `game/Rootborn.Game/GameSession.Furniture.cs`(`EnterHouse`를 면 dispatch로, `PlaceFurniture` 한도, `CommitHouseLayout` 파일 고르기), `GameSession.cs`(읽기·저장에 `WorkspaceProgress`), `GameSession.Changes.cs`(주제 `GameChangeTopics.Workspace`)
- Test: `game/tests/Rootborn.Game.Tests/WorkspaceTests.cs`

**Interfaces:**
- Consumes: Task 1~7 전부
- Produces:
  ```csharp
  // 가구를 놓는 맵에 들어온다. 그 면의 일터가 데이터에 있으면 일터로, 없으면 지금처럼 집으로 연다. Host는 이것만 부른다.
  public void EnterPlacementSurface(IPlacementGrid grid, string surfaceId);   // EnterHouse는 이것을 부르는 얇은 이름으로 남긴다
  public Workspace ActiveWorkspace { get; }          // 집이면 null. House는 "지금 열린 배치"다(집이든 일터든)
  public bool OwnsActiveWorkspace { get; }           // Adult.FacilityId == ActiveWorkspace.FacilityId
  public sealed class WorkspaceStatus { Workspace Workspace; int Placed; int Capacity; int ScorePercent; SpaceScores Scores; bool Owned; }
  public WorkspaceStatus WorkspaceOf(string facilityId);      // 일터가 없으면 null. 배치가 열려 있지 않아도 저장된 기록(없으면 기본 템플릿의 가구)으로 점수를 낸다
  public WorkExtras WorkExtrasFor(string workId);             // 자기 시설의 일이고 일터가 있으면 이득, 아니면 default
  public IReadOnlyList<WorkspaceTemplate> TemplatesForActiveWorkspace();
  public WorkspaceResultKind CanApplyTemplate(string templateId);
  public WorkspaceResultKind ApplyTemplate(string templateId, string requestId);
  ```
- 동작:
  - 일터로 열 때: 배치 파일이 없고 깔린 적이 없고 자기 시설이면 기본 템플릿을 값 없이 깐다(안 맞으면 빈 채로 두고 깔린 것으로 적지 않는다). 남의 일터면 깔지 않는다.
  - `PlaceFurniture`: 일터이면 자기 시설이어야 하고 `Workspace.CanAdd(House.Placed.Count, Adult.FacilityLevel, Student)`여야 한다. 옮기기·치우기는 자기 시설이면 한도와 무관하게 된다. 남의 일터에서는 모두 false.
  - 놓기에 성공하면 그 면의 `PlacementSkillGrowth`가 있을 때 `TryGrowByPlacing`(집도 같다).
  - 점수는 면마다 기억하고 `Furniture`·`Workspace` 변화 때만 버린다. 일 명령마다 가구를 훑지 않는다.
  - 가구 퀘스트 이벤트(`AnnouncePlacement`, `ReplayPlacedFurniture`)는 집일 때만 낸다.

- [ ] **Step 1: 실패 테스트** — 실제 `data/`, 임시 저장소. 성인 요리사 준비는 `AdultCareerTests`의 준비를 도우미로 꺼내 쓴다. 집 칸은 `HouseRoom.Grid`, 일터 칸은 새 도우미 `WorkspaceRoom.GridFor(definitions, workspaceId)`를 쓴다: 그 일터 템플릿들의 가구가 차지하는 칸을 모두 덮고 둘레에 3칸 여유를 둔 빈 바닥 사각형(출발 칸은 여유 칸 한 곳). 템플릿 좌표가 Task 11에서 바뀌어도 테스트가 따라간다.
  - `WORKSPACE_001_DecoratingRaisesWorkReward`: 요리사가 일터에 들어가 아늑함을 주는 가구를 놓는다 → `WorkExtrasFor("work.chef.level1").RewardPercentBonus > 0`, 놓기 전에는 기본 템플릿 값.
  - `WORKSPACE_002_TemplateOnce`: `ApplyTemplate` 같은 요청 두 번 → 돈 한 번, 새 `GameSession`으로 다시 읽은 뒤 같은 요청 → `DuplicateRequest`, 배치 수 그대로.
  - `SKILL_001_ChefLearnsInteriorByDecorating`: 가구를 놓아 `skill.interior`가 오르고, 단계에 닿게 한 뒤(준비: 기술 값 직접 더하기) `WorkspaceOf("facility.chef").Capacity`가 는다.
  - `FirstEnter_SeedsDefaultTemplate_Once`(다시 들어와도, 다 치운 뒤 다시 들어와도 다시 깔리지 않는다).
  - `NotOwner_CannotPlaceOrApply_AndIsNotSeeded`(학생 또는 다른 시설의 성인).
  - `AtCapacity_PlaceRefused_MoveAndRemoveAllowed`.
  - `OverCapacityOldLayout_IsKept`(한도보다 많은 기록을 파일에 미리 써 두고 들어간다 → 전부 놓여 있고 새로 놓기만 거절).
  - `Layout_SurvivesNewSession`와 `HouseLayout_IsUntouchedByWorkspace`(집 파일 그대로).
  - `HousePlacement_GrowsInteriorSkill_UpToDailyLimit`(학교 밖 길).
  - `OldSave_WithoutWorkspaceFiles_Starts`(`Fixtures`의 성인 슬롯으로 시작해 `WorkspaceOf`가 기본 템플릿 점수를 준다).
- [ ] **Step 2: 실패 확인** — `dotnet test game/tests/Rootborn.Game.Tests --filter WorkspaceTests`
- [ ] **Step 3: 구현**
- [ ] **Step 4: 통과 확인** — `dotnet test game/tests/Rootborn.Game.Tests` 전체(기존 `FurnitureTests` 포함), 건수 확인.
- [ ] **Step 5: 커밋** — `[FEATURE] GameSession 일터 — 들어가기, 한도, 템플릿, 공간 점수 조회`

### Task 9: 일 명령에 공간 이득 넘기기

**Files:**
- Modify: `game/Rootborn.Game/GameSession.Day.cs`(`ReserveFacilityWork`, `SettleFacilityWork`, `CompleteFacilityWork`)
- Test: `game/tests/Rootborn.Game.Tests/WorkspaceTests.cs`에 더하기

**Interfaces:**
- Consumes: `WorkExtrasFor(workId)`(Task 8), 서비스의 `extras` 인자(Task 4)
- 세 명령이 `WorkExtrasFor`를 넘긴다. 정산은 예약에 적힌 일 id로 조회한다.

- [ ] **Step 1: 실패 테스트**
  - `CompleteWork_PaysMoreInDecoratedWorkspace`(같은 점수로 꾸미기 전·후 보상 비교, 뒤가 많다).
  - `CompleteWork_GrowsCookingSkill`.
  - `ReserveThenDecorateThenSettle_RewardUsesReservation`(Review Focus 5).
  - `UndecoratedWorkspace_NeverPaysLessThanBefore`(가구를 다 치운 뒤 보상 = 레벨 비율만의 보상).
  - `RewardOnce_StillHolds`(같은 요청 두 번 → 기술·보상 한 번).
- [ ] **Step 2: 실패 확인**
- [ ] **Step 3: 구현**
- [ ] **Step 4: 통과 확인** — `dotnet test game/tests/Rootborn.Game.Tests`, 기존 `AdultCareerTests`·`AdultAutoCompleteTests`·`RewardOnceTests`가 그대로인지.
- [ ] **Step 5: 커밋** — `[FEATURE] 시설 일이 일터의 공간 이득을 받는다 — 꾸민 식당의 요리 일 보상과 명성`

### Task 10: 화면 — 점수 줄, 템플릿 고르기, 시설 화면의 이득

**Files:**
- Modify: `game/Rootborn.Screens`의 가구 배치 화면(`FurniturePlacementScreen`과 그 배치 수치 묶음), 시설 관리 화면, `ScreenCatalog.cs`, `ThemeRequirements.cs`와 `game/themes/README.md`·모든 테마(성질 아이콘 키 5개)
- Create: `game/Rootborn.Screens/Workspaces/WorkspaceTemplateScreen.cs`(모달, 이동을 막는다), 그 표시 모델
- Modify: `game/localization/ko.json`·`en.json`
- Test: `game/tests/Rootborn.Screens.Tests`의 가구 배치·시설 화면 테스트에 더하기, `Workspaces/WorkspaceTemplateScreenTests.cs`

**Interfaces:**
- Consumes: `GameSession.ActiveWorkspace`, `WorkspaceOf`, `TemplatesForActiveWorkspace`, `CanApplyTemplate`, `ApplyTemplate`, `WorkExtrasFor`
- 화면 구성(추천안 — 스펙 12절에 없던 화면 결정이므로 커밋 메시지와 완료 보고에 "추천으로 정함"이라고 적는다):
  - 배치 화면: 일터일 때만 위쪽에 한 줄 — 성질마다 아이콘과 점수(요소 id `workspace-quality-<성질 id>`), 가구 수 `놓은 수/한도`(`workspace-capacity`, 문구 키 `ui.workspace.capacity` = `{0}/{1}`), 템플릿 단추(`workspace-templates`). 한도에 닿으면 가구 카드는 눌리지 않고 이유 키 `ui.workspace.full`. 남의 일터면 단추·카드가 꺼지고 `ui.workspace.not-owner`.
  - 템플릿 화면: 템플릿 줄(이름, 값, 적용 단추 `template-apply-<id>`, 못 하면 이유 키). 목록은 `UiScrollList`.
  - 시설 화면: 일마다 지금 이득 한 줄(`work-bonus-<일 id>`, 문구 키 `ui.facility.work-bonus` = 보상 `+{0}%`, 명성 `+{1}`). 이득이 0이면 줄을 숨긴다.
- 화면은 `OnChanged(Furniture | Workspace | Career)`에서만 값을 고친다. 요청 id는 다른 명령과 같은 방식으로 만든다.

- [ ] **Step 1: 실패 테스트**
  - `Placement_InWorkspace_ShowsQualityRowAndCapacity`, `Placement_InHouse_HidesWorkspaceRow`.
  - `Placement_AtCapacity_CardsDisabledWithReason`, `Placement_NotOwner_Disabled`.
  - `PlacingFurniture_UpdatesQualityScore`(카드를 눌러 놓은 뒤 점수 글자가 바뀐다).
  - `TemplateScreen_ListsTemplates_ApplyChangesLayout_AndClosesOrRefreshes`, `TemplateScreen_NotEnoughMoney_ShowsReason`.
  - `Facility_ShowsWorkBonus_WhenPositive`.
  - 새 화면을 기존 "모든 언어의 키", "긴 문구", `TestThemeScreensTests` 목록에 넣는다.
- [ ] **Step 2: 실패 확인** — `dotnet test game/tests/Rootborn.Screens.Tests`
- [ ] **Step 3: 구현**
- [ ] **Step 4: 통과 확인** — 전체, 건수 확인.
- [ ] **Step 5: 커밋** — `[UI] 일터 꾸미기 화면 — 성질 점수 줄, 템플릿 고르기, 시설 화면의 공간 이득`

### Task 11: Host 잇기, 임시 일터 맵, 실제 입력 테스트, 문서

**Files:**
- Modify: `game/Rootborn.Host/HostGame.cs`(`SyncHouse`가 `EnterPlacementSurface`를 부른다), `game/world/interactions.json`(주방 실내 맵에 `"placementSurface": "workspace.chef"`, `"temporary": true`와 이유: "임시: 새 맵 전까지 요리사 일터로 쓴다"), `data/workspaces/WorkspaceTemplate.json`(그 맵의 빈 바닥 칸에 맞춘 좌표)
- Modify: `game/world/README.md`(일터 면 약속 한 절), `game/tests/Rootborn.Game.Tests/SCENARIOS.md`와 `.claude/rules/testing-discipline.md` 표(`WORKSPACE-001`, `WORKSPACE-002`, `SKILL-001`), `docs/superpowers/specs/2026-11-11-core-rules-extraction-design.md` "Unity 판과 달라진 동작"(한 줄: 시설을 맡으면 기본 템플릿 배치가 생긴다)
- Test: `game/tests/Rootborn.Host.Tests/WorkspaceHostTests.cs`, `game/tests/Rootborn.World.Tests`에 템플릿 칸 검사

**Interfaces:**
- Consumes: Task 8·10
- 주방 맵이 둘(`KitchenInterior`, `Kitchen2Interior`)이다. 마을에서 걸어 들어갈 수 있고 빈 바닥이 더 넓은 쪽을 고르고, 고른 이유를 `interactions.json`의 이유 칸에 적는다. 코드는 맵 id를 모른다.

- [ ] **Step 1: 실패 테스트**
  - World: `EveryWorkspaceTemplate_FitsItsMap`(템플릿마다 그 면을 가진 맵의 방 칸에서 `CanReplaceUserFurniture`가 참 — 맵과 면은 데이터에서 찾는다).
  - Host `ChefDecoratesRestaurant`: 준비(명령으로 해도 된다)로 성인 요리사 슬롯을 만든다. 그 뒤는 `PlayerDriver`만: 마을에서 걸어 문을 지나 일터 맵에 들어간다 → 배치 화면을 연다 → 가구 카드를 눌러 놓는다 → 성질 점수 요소의 글자가 바뀐다 → 시설 지점까지 걸어가 상호작용 → 일을 한다 → 보상이 꾸미기 전 기준보다 많다. 스크린샷 3장(배치 화면, 템플릿 화면, 시설 화면)을 저장한다.
- [ ] **Step 2: 실패 확인** — `dotnet test game/tests/Rootborn.Host.Tests --filter WorkspaceHostTests`(이 PC 메모리가 모자라면 커밋 뒤 `bash Scripts/remote-test/run.sh`)
- [ ] **Step 3: 구현과 데이터·문서 고치기**
- [ ] **Step 4: 통과 확인과 화면 직접 보기** — 스크린샷 3장을 열어 겹친 글자, 잘린 패널, 빈 그림, 14 미만 글자가 없는지 본다. 결함이 있으면 고치고 다시 찍는다.
- [ ] **Step 5: 전체 확인** — `bash Scripts/ci/run-all.sh`, 프로젝트별 건수가 줄지 않았는지 적는다.
- [ ] **Step 6: 성능 측정** — Core 테스트 `SpaceScore_AtMaxCapacity_IsFast`(한도의 가장 큰 값만큼 놓고 점수 계산 1,000번, 걸린 시간을 출력만 하고 실패 조건으로 쓰지 않는다)와, 가구 1,503개에 대한 성질 표 만들기 시간을 잰다. 수치를 완료 보고에 적는다.
- [ ] **Step 7: 커밋** — `[FEATURE] 요리사가 식당을 꾸민다 — Host 연결, 임시 일터 맵, 실제 입력 테스트`

---

## 완료 보고에 적을 것

- 근거를 종류로 나눠: 자동 테스트(Core·Data·World·Game·Screens 건수), 장치 이벤트 주입(`WorkspaceHostTests`, OS 입력 계층 미확인), 직접 본 스크린샷 경로.
- 실제 플레이로는 미확인: 성인 요리사 슬롯 준비를 명령으로 한 것, 기술 단계에 닿게 하려고 기술 값을 직접 더한 것.
- 추천으로 정한 것: 화면 구성(Task 10), 임시 일터 맵 고르기, 시험용 수치 전부, 파일 이름.
- 사용자에게 확인받을 것: 실제 수치 표, 성질·기술 목록, 다른 세 시설의 데이터.
- 성능: 측정값과 남은 병목 가능성(배치 화면의 미리 보기 점수는 이 계획에서 전체 다시 계산이다 — 한도가 작아 문제가 없는지 측정값으로 판단).
