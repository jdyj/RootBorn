# NPC 이동 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** NPC가 일과표를 따라 맵을 걸어서(문을 지나) 다음 장소로 가고, 도착한 곳에서 서 있거나 어슬렁거린다. 혼자 하기와 같이 하기에서 똑같이.

**Architecture:** NPC 자리를 프레임마다 쌓지 않고 `(날, 그날의 분)`에서 계산한다. Core가 NPC마다 하루 계획(머무름·이동 구간의 줄)을 만들고 시각으로 자리를 돌려준다. World의 `NpcStage`가 지금 맵에 있는 NPC와 상호작용 후보를 고치고, Host와 창 없는 판이 같은 `NpcStage`를 쓴다. 세이브 모양과 통신 메시지는 바꾸지 않는다.

**Tech Stack:** C# (.NET), `Rootborn.Core`(엔진·파일·JSON 참조 없음), `Rootborn.Data`, `Rootborn.World`, `Rootborn.Game`, `Rootborn.Host`(MonoGame 3.8.6.1), xUnit 테스트 프로젝트(`game/tests/*`).

**Spec:** `docs/superpowers/specs/2026-11-12-npc-movement-design.md` — 실행하는 사람은 이 문서와 함께 읽는다.

## Global Constraints

- 규칙 판단은 `game/Rootborn.Core/src`에만. Core는 `System.IO`·`System.Text.Json`·엔진을 참조하지 않는다. MonoGame 참조는 Host와 Host.Tests에만.
- NPC·맵·장소 id로 분기하지 않는다(`if (npcId == "…")` 금지, CI `Scripts/ci/check-no-entity-id-branching.sh`). 종류는 클래스 하나 + 만들기 함수 `b.Add` 한 줄.
- NPC 자리를 정하는 입력은 **(데이터, 날, 그날의 분)** 뿐이다. 개인 진행, 맵에 들어온 때, 프레임 간격, `string.GetHashCode()`(실행마다 다르다), `System.Random`을 계산에 넣지 않는다.
- 세이브 모양 클래스를 바꾸지 않는다. NPC 자리는 저장하지 않는다.
- 프레임마다 도는 코드(`NpcStage.Tick`, `WorldScreen.Update`·그리기)에서 새 객체·LINQ·문자열 이어 붙이기를 만들지 않는다.
- 몸끼리 막지 않는다. NPC는 플레이어·다른 NPC·다른 참가자를 통과한다. 막힘을 위한 확장점을 만들지 않는다.
- 일과표 내용(누가 언제 어디에)은 바꾸지 않는다. 처음 넣는 수치: NPC 걷는 속도 **초당 2.5칸**, 모든 일과 줄의 머무름은 `wander` **반지름 3칸**, 쉬는 시간 **2~6초**.
- 사람이 읽는 문구를 코드에 적지 않는다. 이 계획은 새 문구를 더하지 않는다.
- 실패 테스트 먼저. 커밋 메시지는 한글, 접두어(`[FEATURE]` 등), 끝줄은 세션이 받은 `Co-Authored-By`.
- 테스트 명령 앞에: `export PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet"`. 통과 줄만 보지 말고 프로젝트별 건수가 줄지 않았는지 본다.
- `docs/art/art-requests.md`에는 사용자의 미커밋 변경이 있다. 이 파일은 고치기만 하고 **커밋하지 않는다**(Task 8).

## Review Focus

1. **시각이 뒤로 가거나 한꺼번에 뛴다**(잠, 방의 시각 따라가기, 이어 하기): 어느 시각을 주어도 `PoseOf`는 그 시각의 자리를 돌려주고 예외가 없다. 범위 밖 시각은 하루의 처음·끝으로 자른다. → Task 4
2. **자리가 막혀 있다**(플레이어가 놓은 가구가 NPC 자리나 어슬렁거릴 칸을 막음, 길 없음): NPC는 자리에 서 있고, 어슬렁거릴 칸이 없으면 서 있기와 같다. 벽·가구 칸을 지나지 않는다. → Task 2, Task 3
3. **문을 지나는 순간**: 이동 구간의 어느 시각에도 NPC는 정확히 한 맵에만 있다(두 맵에 동시에 있거나 어느 맵에도 없는 시각이 없다). → Task 4
4. **대상이던 NPC가 맵을 떠난다**: 상호작용 대상이 사라지면 `Target`이 비고 `Press()`는 null이다. 떠난 자리에서는 상호작용이 없다. → Task 6
5. **다른 실행에서도 같은 걸음**: 어슬렁거림의 씨앗이 실행·기기와 무관하다(고정한 기대 값으로 확인). → Task 3

---

## 파일 구조

| 파일 | 맡는 일 |
|---|---|
| `game/Rootborn.Core/src/Time/TimeRules.cs` (고침) | `FlowingClock.MinuteFraction` |
| `game/Rootborn.Game/GameSession.Time.cs` (고침) | `DayMinutesExact` — 분 아래까지 든 그날의 시각 |
| `game/Rootborn.Core/src/World/MapReach.cs` (새) | 한 자리에서 닿는 칸, 닿는 문 (지금 `MapChecks`에 있는 것을 옮김) |
| `game/Rootborn.Core/src/NpcMovement/NpcRoute.cs` (새) | `NpcSpot`, `NpcPose`, `NpcRouteLeg`, `NpcRoute`, `NpcRouteFinder` |
| `game/Rootborn.Core/src/NpcMovement/NpcStay.cs` (새) | `INpcStayBehavior`, `NpcStandStay`, `NpcWanderStay`, `NpcRandom`, `StableHash` |
| `game/Rootborn.Core/src/NpcMovement/NpcDayPlan.cs` (새) | `INpcAnchors`, `NpcWalkSettings`, `NpcDayPlan`, `NpcDayPlanner`, `NpcWorld` |
| `game/Rootborn.Core/src/Dialogue/NpcSchedules.cs` (고침) | `NpcScheduleEntry.Stay` |
| `game/Rootborn.Core/src/StudentLife/LocationState.cs` (고침) | `LocationStateTimeSlots.SlotStarts` |
| `game/Rootborn.Core/src/Interaction/InteractionTargets.cs` (고침) | 움직이고 꺼질 수 있는 후보 |
| `game/Rootborn.Data/Builders/DialogueBuilders.cs` (고침) | `NpcWanderStay` 등록, 일과 줄의 `stay` 읽기 |
| `game/Rootborn.Data/DataFingerprint.cs` (고침) | `game/world/*.json`을 지문에 |
| `data/npcs/NpcWanderStay.json` (새), `data/npcs/NpcScheduleDefinition.json`, `data/registry.json` (고침) | 머무는 행동 데이터 |
| `game/world/maps.json`, `game/world/interactions.json` (고침) | `npcWalker`, 마을 NPC 자리(임시) |
| `game/Rootborn.World/NpcStage.cs` (새) | 지금 맵의 NPC와 후보. Host와 창 없는 판이 함께 쓴다 |
| `game/Rootborn.World/WorldInteractionData.cs`, `MapChecks.cs`, 맵 목록 읽는 곳 (고침) | `MapAnchors : INpcAnchors`, `npcWalker` 읽기, `NpcPlacement.Place`·`PlacedNpc` 삭제 |
| `game/Rootborn.Host/World/WorldScreen.cs`, `HostGame.cs` (고침) | `NpcStage` 쓰기, NPC 그리기 |
| `game/tests/Rootborn.Simulation.Tests/Headless/HeadlessWorld.cs` (고침) | `NpcStage` 쓰기 |
| `game/tests/Rootborn.Host.Tests/PlayerDriver.cs` (고침) | `WalkToNpc` |

시각의 단위: **그날의 분**(`double dayMinutes`). `StudentLifeProgress.TimeMinutes`와 같은 축이다 — 하루는 `TimeRules.DayStartMinutes`(420)에서 `TimeRules.DayEndMinutes`(1860)까지이고 자정을 넘긴 02:00은 1560이다. 걷는 속도는 `칸/분 = 초당 칸 × (RealSecondsPerGameDay / 1440)`.

---

### Task 1: 분 아래까지 든 시각

**Files:**
- Modify: `game/Rootborn.Core/src/Time/TimeRules.cs` (`FlowingClock`, 68-90행)
- Modify: `game/Rootborn.Game/GameSession.Time.cs` (`AdvanceTime` 56행 부근)
- Test: `game/tests/Rootborn.Core.Tests`(FlowingClock을 시험하는 기존 파일), `game/tests/Rootborn.Game.Tests/NpcTimeTests.cs` (새)

**Interfaces:**
- Produces:
  - `public double FlowingClock.MinuteFraction { get; }` — 다음 분까지 흐른 비율, `[0, 1)`.
  - `public double GameSession.DayMinutesExact { get; }` — `Student.TimeMinutes + 조각`. 혼자 하기: 조각은 `_flow.MinuteFraction`. 같이 하기: `Student.TimeMinutes`가 마지막으로 바뀐 뒤 `AdvanceTime`에 들어온 실제 초를 `초/분`으로 나눈 값, `0.999`에서 자른다. 분이 바뀌면 0부터.

- [ ] **Step 1: 실패 테스트 — Core**

```csharp
[Fact] public void MinuteFraction_은_다음_분까지_흐른_비율이다()
{
    var clock = new FlowingClock(1440f);          // 게임 1분 = 실제 1초
    Assert.Equal(0, clock.Advance(0.25f, false));
    Assert.Equal(0.25, clock.MinuteFraction, 3);
    Assert.Equal(1, clock.Advance(1.0f, false));
    Assert.Equal(0.25, clock.MinuteFraction, 3);
    clock.Advance(0.5f, true);                     // 멈춤: 그대로
    Assert.Equal(0.25, clock.MinuteFraction, 3);
}
```

- [ ] **Step 2: 실패 테스트 — Game** (`NpcTimeTests`, 기존 Game 테스트의 `GameSession` 만들기 도우미를 쓴다)

```csharp
[Fact] public void DayMinutesExact_는_분_사이를_메운다()
{
    // 혼자 하는 방. 실제 초를 조금씩 보낸다.
    double before = session.DayMinutesExact;
    session.AdvanceTime(secondsPerMinute * 0.5f, false);
    Assert.InRange(session.DayMinutesExact - before, 0.45, 0.55);
    Assert.Equal(session.Student.TimeMinutes, (int)Math.Floor(session.DayMinutesExact));
}
[Fact] public void DayMinutesExact_는_패널이_열려_멈추면_그대로다()   // AdvanceTime(…, menuOpen: true)
[Fact] public void 같이_하는_방에서는_방의_분을_따르고_조각은_1분을_넘지_않는다()
// 방의 분을 그대로 두고 AdvanceTime을 여러 번: DayMinutesExact < TimeMinutes + 1. 방의 분을 +1: floor가 새 분, 조각 0에서 다시.
```

- [ ] **Step 3: 실패 확인** — `dotnet test game/tests/Rootborn.Core.Tests --filter MinuteFraction` / `dotnet test game/tests/Rootborn.Game.Tests --filter NpcTimeTests` → 컴파일 오류(멤버 없음).
- [ ] **Step 4: 구현.** `MinuteFraction => _pendingSeconds / _secondsPerMinute`(1 미만으로 자른다). `GameSession`에 같이 하기용 `double _sinceMinuteSeconds`와 `int _lastSeenMinutes`를 두고 `AdvanceTime`에서 갱신한다.
- [ ] **Step 5: 통과 확인** — 위 두 명령 PASS, `dotnet test game/tests/Rootborn.Game.Tests` 전체 건수가 줄지 않음.
- [ ] **Step 6: 커밋** — `[FEATURE] 분 아래까지 든 시각 조회 — NPC 걷기가 분마다 튀지 않게`

---

### Task 2: 길 — 같은 맵과 문을 지나는 길

**Files:**
- Create: `game/Rootborn.Core/src/World/MapReach.cs`, `game/Rootborn.Core/src/NpcMovement/NpcRoute.cs`
- Modify: `game/Rootborn.World/MapChecks.cs` (117-175행: `Reachable`·`Touches`가 Core 것을 부르게), `game/Rootborn.Core/src/World/WorldRules.cs` (6행 주석에서 "테스트 드라이버" 문구를 고침)
- Test: `game/tests/Rootborn.Core.Tests/NpcMovement/NpcRouteTests.cs`

**Interfaces:**
- Consumes: `GridPath.Find`, `CollisionGrid`, `WorldMap`, `IWorldMapSource`, `DoorArea.Destination`(`TravelTarget.MapId/SpawnId`), `CellPos.Containing/Center`.
- Produces:

```csharp
namespace Rootborn.Core.World {
  public static class MapReach {
    public static HashSet<CellPos> From(CollisionGrid grid, CellPos from);            // from 포함, 막혔으면 빈 집합
    public static bool Touches(HashSet<CellPos> reached, AreaRect area, int cellSize); // MapChecks.Touches 그대로(1픽셀 넓혀 본다)
  }
}
namespace Rootborn.Core.NpcMovement {
  public readonly struct NpcSpot { public readonly string MapId; public readonly float X, Y; public NpcSpot(string mapId, float x, float y); }
  public readonly struct NpcPose {                      // Present가 false면 어느 맵에도 없다
    public readonly bool Present; public readonly string MapId; public readonly float X, Y;
    public readonly Facing Facing; public readonly bool Moving;
    public static NpcPose Absent { get; }
    public static NpcPose At(string mapId, float x, float y, Facing facing, bool moving);
  }
  public sealed class NpcRouteLeg { public string MapId { get; } public int CellSize { get; } public IReadOnlyList<CellPos> Cells { get; } } // Cells[0] = 그 맵에서의 출발 칸
  public sealed class NpcRoute {
    public IReadOnlyList<NpcRouteLeg> Legs { get; }
    public float TotalCells { get; }                    // 각 다리의 (Cells.Count - 1)의 합. 문을 지나는 데는 0칸
    public NpcPose PoseAt(float cellsTravelled);        // 0..TotalCells로 자른다. 칸 가운데 사이를 곧게 잇는다. 끝에서는 Moving=false
  }
  public sealed class NpcRouteFinder {
    public NpcRouteFinder(IWorldMapSource maps);
    public NpcRoute Find(NpcSpot from, NpcSpot to);     // 길이 없으면 null. 같은 (from 칸, to 칸)은 기억한 것을 돌려준다
    public void Forget(string mapId);                   // 그 맵을 지나는 기억한 길을 버린다(가구가 바뀜)
  }
}
```

- 문을 지나는 길: 맵을 마디로, "닿는 문 → 목적지 도착 지점"을 이음으로 하는 너비 우선 탐색으로 **문을 가장 적게 지나는 길**을 고른다. 한 다리는 출발 칸에서 그 문 상자에 닿는 가장 가까운 칸까지이고(`MapReach.Touches`와 같은 기준), 다음 다리는 목적지 도착 지점이 든 칸에서 시작한다. 목적지 칸이 막혀 있으면(자리가 가구 위) 그 칸의 둘레 8칸 중 닿는 칸으로 간다 — 그것도 없으면 null.
- `Facing`은 마지막으로 움직인 방향. 한 번도 안 움직였으면 `Facing.Down`.

- [ ] **Step 1: 실패 테스트** (메모리에 만든 작은 맵: `CollisionGrid` + `WorldMap` + 가짜 `IWorldMapSource`)

```csharp
[Fact] public void 같은_맵에서_막힌_칸을_돌아간다()            // 벽 한 줄. route.Legs.Count == 1, 지나는 칸 중 IsBlocked가 없다
[Fact] public void PoseAt은_칸_가운데를_곧게_잇는다()          // 0칸 → 출발 칸 가운데, 0.5칸 → 두 가운데의 중점·Moving, TotalCells → 도착 칸 가운데·!Moving
[Fact] public void 문을_지나_다른_맵으로_간다()                // A의 문 → B 도착 지점. Legs.Count == 2, Legs[1].Cells[0] == B 도착 칸
[Fact] public void 다리_사이에서도_정확히_한_맵에_있다()        // 0..TotalCells를 0.1칸씩: 모든 pose.Present, MapId는 A였다가 B로 한 번만 바뀐다
[Fact] public void 길이_없으면_null이다()                     // 벽으로 둘러싼 목적지, 문이 없는 다른 맵
[Fact] public void 목적지_칸이_막혔으면_옆_칸으로_간다()
[Fact] public void Forget_뒤에는_새_격자로_다시_찾는다()        // 길을 찾고, 격자에 막힌 칸을 더하고, Forget(mapId) → 새 길이 그 칸을 지나지 않는다
```

- [ ] **Step 2: 실패 확인** — `dotnet test game/tests/Rootborn.Core.Tests --filter NpcRouteTests` → 컴파일 오류.
- [ ] **Step 3: 구현.** `MapChecks.Reachable`·`Touches`의 몸은 `MapReach`로 옮기고 `MapChecks`는 그것을 부른다(같은 것을 두 벌 두지 않는다).
- [ ] **Step 4: 통과 확인** — 위 필터 PASS, `dotnet test game/tests/Rootborn.World.Tests` 건수·결과 그대로.
- [ ] **Step 5: 커밋** — `[FEATURE] NPC 길 찾기 — 같은 맵과 문을 지나는 길`

---

### Task 3: 머무는 행동 — 서 있기와 어슬렁거리기

**Files:**
- Create: `game/Rootborn.Core/src/NpcMovement/NpcStay.cs`
- Modify: `game/Rootborn.Core/src/Dialogue/NpcSchedules.cs` (`NpcScheduleEntry`, 51-85행)
- Test: `game/tests/Rootborn.Core.Tests/NpcMovement/NpcStayTests.cs`

**Interfaces:**
- Consumes: Task 2의 `NpcSpot`, `NpcPose`, `GridPath.Find`.
- Produces:

```csharp
public static class StableHash { public static uint Of(string text); }      // FNV-1a 32비트, UTF-16 코드 단위마다. Of("npc.teacher")의 값을 테스트에 고정한다
public struct NpcRandom {                                                     // xorshift32. 씨앗 0은 1로 바꾼다
  public NpcRandom(uint seed); public int Next(int exclusiveMax); public float Range(float min, float max);
}
public readonly struct NpcStayContext {
  public readonly NpcSpot Anchor; public readonly CollisionGrid Grid;
  public readonly uint Seed; public readonly double CellsPerMinute, SecondsPerMinute;
}
public interface INpcStayBehavior {
  /// 머무름이 시작된 뒤 minutesIntoStay(>= 0)가 지났을 때의 자리. state는 이 머무름 하나에 딸린 것으로, 처음에는 null을 넘긴다.
  NpcPose PoseAt(in NpcStayContext context, double minutesIntoStay, ref object state);
}
public sealed class NpcStandStay : INpcStayBehavior { public static NpcStandStay Instance { get; } }   // 자리 그대로, Facing.Down, !Moving
public sealed class NpcWanderStay : INpcStayBehavior {
  public NpcWanderStay(float radiusCells, float pauseMinSeconds, float pauseMaxSeconds);  // 음수는 0, max < min이면 max = min
  public float RadiusCells { get; } public float PauseMinSeconds { get; } public float PauseMaxSeconds { get; }
}
// NpcScheduleEntry: 생성자 끝에 `INpcStayBehavior stay = null` 을 더하고 `public INpcStayBehavior Stay { get; }` (null이면 NpcStandStay.Instance)
```

- 어슬렁거림의 걸음(이것이 결정적이어야 하므로 그대로 따른다):
  1. 후보 칸 = 자리 칸에서 걸어서 닿고 자리 칸과의 가로·세로 차가 모두 `RadiusCells` 이하인 칸. 순서는 (Y, X) 오름차순. 후보가 자리 칸뿐이면 서 있기와 같다.
  2. 걸음 하나 = [쉼: `Range(PauseMin, PauseMax)`초] → [후보에서 `Next(count)`로 고른 칸까지 `GridPath.Find`로 걷기]. 고른 칸이 지금 칸이면 걷기 길이는 0이다.
  3. 시각 0에서 자리 칸에 서서 쉼부터 시작한다. 난수는 걸음마다 쉼 → 칸 순서로 뽑는다.
  4. `state`에는 "지금 걸음의 시작 분, 지금 칸, 난수 상태, 지금 길"을 둔다. `minutesIntoStay`가 지금 걸음의 시작보다 앞이면 처음부터 다시 따라간다. 같은 `state`로 시각이 앞으로만 가는 동안에는 걸음이 넘어갈 때만 새 객체를 만든다.

- [ ] **Step 1: 실패 테스트**

```csharp
[Fact] public void StableHash는_실행과_무관하다()               // Assert.Equal(<고정 값>, StableHash.Of("npc.teacher")) — 값은 FNV-1a로 손으로 계산해 적는다
[Fact] public void 서_있기는_자리에_그대로다()
[Fact] public void 어슬렁거림은_반지름_안의_열린_칸에만_있다()    // 0..600분을 0.25분씩: 칸이 막히지 않았고 |dx|,|dy| <= 3
[Fact] public void 같은_씨앗이면_같은_걸음이다()                 // state를 따로 둔 두 번의 PoseAt(…, 137.5) 가 같다
[Fact] public void 이어_간_것과_처음부터_계산한_것이_같다()       // state 하나로 0→300분을 0.2분씩 간 마지막 자리 == 새 state로 PoseAt(300) (Review Focus 5·1)
[Fact] public void 시각이_뒤로_가도_맞는_자리다()               // 같은 state로 PoseAt(300) 뒤 PoseAt(40) == 새 state의 PoseAt(40)
[Fact] public void 갈_칸이_없으면_서_있는다()                   // 자리 둘레가 모두 막힘 (Review Focus 2)
[Fact] public void 씨앗이_다르면_걸음이_다르다()
```

- [ ] **Step 2: 실패 확인** — `dotnet test game/tests/Rootborn.Core.Tests --filter NpcStayTests` → 컴파일 오류.
- [ ] **Step 3: 구현.**
- [ ] **Step 4: 통과 확인** — 위 필터 PASS, `dotnet test game/tests/Rootborn.Core.Tests` 전체 통과(`NpcScheduleEntry` 기존 테스트 포함).
- [ ] **Step 5: 커밋** — `[FEATURE] NPC 머무는 행동 — 서 있기와 어슬렁거리기(시각으로 계산)`

---

### Task 4: 하루 계획과 `NpcWorld`

**Files:**
- Create: `game/Rootborn.Core/src/NpcMovement/NpcDayPlan.cs`
- Modify: `game/Rootborn.Core/src/StudentLife/LocationState.cs` (`LocationStateTimeSlots`, 49-64행)
- Test: `game/tests/Rootborn.Core.Tests/NpcMovement/NpcDayPlanTests.cs`

**Interfaces:**
- Consumes: Task 2 `NpcRouteFinder`, Task 3 `INpcStayBehavior`·`StableHash`, `NpcScheduleLookupCache.TryGetByNpcId`, `NpcScheduleResolver.Resolve`, `NpcScheduleContext`, `TimeRules`(`DayStartMinutes`, `DayEndMinutes`, `RealSecondsPerGameDay`).
- Produces:

```csharp
// LocationStateTimeSlots
public static void SlotStarts(int dayStartMinutes, int dayEndMinutes, List<(int Minutes, string SlotId)> into);
// 그날의 분 축에서 시간대가 시작하는 때를 순서대로. FromMinutes와 같은 경계를 쓴다(12:00, 17:00, 21:00, 자정 뒤 새벽은 밤).
// 하루 시작 07:00이면: (420, morning), (720, afternoon), (1020, evening), (1260, night)

public interface INpcAnchors {
  /// 그 장소의 slot번째 자리(0부터). 어느 맵에도 없으면 false.
  bool TryGet(string locationId, int slot, out NpcSpot spot);
}
public sealed class NpcWalkSettings { public NpcWalkSettings(float speedCellsPerSecond); public float SpeedCellsPerSecond { get; } }  // 0 이하는 예외

public sealed class NpcDayPlan {
  public string NpcId { get; }
  public NpcPose PoseAt(double dayMinutes);     // 하루 밖의 시각은 처음·끝으로 자른다
  public int SegmentCount { get; }              // 테스트·측정용
}
public sealed class NpcWorld {
  public NpcWorld(IReadOnlyList<string> npcIdsInOrder, NpcScheduleLookupCache schedules, INpcAnchors anchors,
                  IWorldMapSource maps, NpcWalkSettings walk, TimeRules rules);
  public int Count { get; }
  public string NpcId(int index);
  public void Prepare(int day);                 // 날이 바뀌었거나 ForgetMap 뒤에만 계획을 다시 만든다
  public NpcPose PoseOf(int index, double dayMinutes);   // Prepare한 날 기준
  public void ForgetMap(string mapId);          // 가구가 바뀐 맵: 길 기억과 계획을 버린다
}
```

- 계획 만드는 법(설계 5절):
  1. `SlotStarts`의 시간대마다 NPC 모두의 장소를 `Resolve(schedule, new NpcScheduleContext(day, 0, slotId, null))`로 얻는다. 일정이 없는 NPC는 하루 종일 `Absent`.
  2. 시간대마다 자리 번호: `npcIdsInOrder` 순서로, 같은 장소에 앞서 배정된 NPC 수(지금 `NpcPlacement.Place`와 같은 규칙). `anchors.TryGet`이 false면 그 시간대에 `Absent`.
  3. 첫 시간대는 그 자리에서의 머무름으로 시작한다(전날에서 걸어오지 않는다).
  4. 다음 시간대의 자리가 다르면 **경계 시각에 출발하는** 이동 구간. 출발점은 경계 시각의 자리(머무는 중이면 그때의 어슬렁 자리, 아직 이동 중이면 그 길 위의 칸 가운데). 걸리는 분 = `route.TotalCells / CellsPerMinute`. 도착하면 다음 경계까지 그 시간대 줄의 `Stay`로 머문다.
  5. 길이 null이면 이동하지 않고 출발 자리에서 서 있는다(다음 경계에 다시 시도).
  6. 앞 시간대가 `Absent`였다가 자리가 생기면 걸어오지 않고 그 자리에 나타난다. 자리가 있다가 `Absent`가 되면 경계에 사라진다.
  7. 자리가 같고 줄(`NpcScheduleEntry`)만 바뀌면 머무름이 이어진다(새 머무름으로 끊지 않는다 — 어슬렁 자리가 튀지 않게).
  8. 머무름의 씨앗 = `StableHash.Of(npcId) ^ (uint)(day * 2654435761) ^ (uint)구간 순번`.

- [ ] **Step 1: 실패 테스트** (메모리 맵 둘, 가짜 `INpcAnchors`, 손으로 만든 `NpcSchedule` — 오전 A맵 자리, 오후 B맵 자리, 저녁 A맵 자리. `TimeRules(1440f, 420, 1320, 0, 0)`이면 게임 1분 = 실제 1초)

```csharp
[Fact] public void SlotStarts는_FromMinutes와_같은_경계다()      // 각 (m, id): FromMinutes(m) == id, FromMinutes(m-1) != id (첫 것 빼고)
[Fact] public void 오전에는_오전_자리에_있다()                   // PoseOf(i, 500): MapId A, 자리 좌표(서 있기), !Moving
[Fact] public void 경계_시각에_출발한다()                       // PoseOf(i, 720): 아직 A맵·오전 자리. PoseOf(i, 721): Moving
[Fact] public void 걸리는_시간은_칸_수를_속도로_나눈_것이다()     // 길 20칸, 초당 2.5칸, 1분 = 1초 → 720 + 20/(2.5*1) = 728분에 도착: PoseOf(i, 729)는 B맵 자리·!Moving
[Fact] public void 이동_중_어느_시각에도_정확히_한_맵에_있다()    // 720..729를 0.05분씩: Present, MapId가 A→B 한 번만 (Review Focus 3)
[Fact] public void 시각이_한꺼번에_뛰어도_맞는_자리다()          // 새 NpcWorld로 PoseOf(900) == 500→900을 1분씩 지나온 NpcWorld의 PoseOf(900)
[Fact] public void 시각이_뒤로_가도_하루_밖이어도_예외가_없다()   // PoseOf(900) 뒤 PoseOf(430), PoseOf(-5), PoseOf(5000), NaN → 하루의 처음/끝 자리 (Review Focus 1)
[Fact] public void 길이_없으면_출발_자리에_머문다()
[Fact] public void 자리가_없는_시간대에는_어느_맵에도_없다()      // anchors가 false → !Present
[Fact] public void 같은_장소의_두_번째_NPC는_두_번째_자리다()
[Fact] public void 이동이_다음_경계까지_안_끝나면_그_자리에서_새_길을_잡는다()  // 아주 느린 속도: 1020분의 자리에서 저녁 자리로
[Fact] public void 날이_다르면_어슬렁_걸음이_다르다()            // Prepare(1) vs Prepare(2), wander 머무름
[Fact] public void Prepare는_같은_날이면_다시_만들지_않는다()     // SegmentCount 객체가 같은 인스턴스이거나 만들기 횟수 카운터가 그대로
```

- [ ] **Step 2: 실패 확인** — `dotnet test game/tests/Rootborn.Core.Tests --filter NpcDayPlanTests` → 컴파일 오류.
- [ ] **Step 3: 구현.** 구간은 `PoseAt(double dayMinutes)`를 가진 두 클래스(머무름, 이동)로 두고 계획은 시작 분으로 정렬한 배열을 이진 탐색한다.
- [ ] **Step 4: 통과 확인** — 위 필터 PASS, Core 전체 통과.
- [ ] **Step 5: 커밋** — `[FEATURE] NPC 하루 계획 — 일과표에서 머무름과 이동 구간을 만들고 시각으로 자리를 얻는다`

---

### Task 5: 데이터 — 머무는 행동, 걷는 속도, 마을 자리, 데이터 지문

**Files:**
- Create: `data/npcs/NpcWanderStay.json`
- Modify: `data/npcs/NpcScheduleDefinition.json`, `data/registry.json`, `game/world/maps.json`(20행 `walker` 다음 줄), `game/world/interactions.json`(Town 묶음), `game/world/README.md`("임시 연결 목록", NPC 자리 약속)
- Modify: `game/Rootborn.Data/Builders/DialogueBuilders.cs`(35-48행), `game/Rootborn.Data/DataFingerprint.cs`, `game/Rootborn.World/WorldInteractionData.cs`, 맵 목록의 `walker`를 읽는 곳(`grep -n "speedCellsPerSecond" game/Rootborn.World`)
- Test: `game/tests/Rootborn.Data.Tests`(RealDataTests·DataFingerprintScopeTests 곁), `game/tests/Rootborn.World.Tests/NpcRouteContractTests.cs`(새)

**Interfaces:**
- Consumes: Task 3 `NpcWanderStay`, Task 4 `INpcAnchors`·`NpcWalkSettings`·`NpcWorld`.
- Produces:
  - 데이터: `{ "type": "NpcWanderStay", "id": "Stay_Wander_Default", "radiusCells": 3, "pauseMinSeconds": 2, "pauseMaxSeconds": 6 }`. 일과표의 **모든** 줄에 `"stay": { "ref": "Stay_Wander_Default", "type": "NpcWanderStay" }`. `stay`가 없는 줄은 서 있기.
  - `maps.json`: `"npcWalker": { "speedCellsPerSecond": 2.5 }` → `MapCatalog.NpcWalker`(`NpcWalkSettings`). 없으면 읽기 오류로 알린다(기본값으로 메우지 않는다).
  - `public sealed class MapAnchors : INpcAnchors { public MapAnchors(InteractionData data, IEnumerable<string> mapIds); }` (`Rootborn.World`) — 맵 목록 순서로 찾아 처음 나오는 맵의 자리를 쓴다.
  - `DataFingerprint.Compute`가 `data` 폴더 옆 `game/world/maps.json`·`game/world/interactions.json`을 `game/time.json`과 같은 방식으로 넣는다(없으면 넣지 않는다).

- 마을 NPC 자리(임시, `"temporary"` 주석과 README 목록에 적는다). 장소 id는 일과표 데이터에 적힌 것을 그대로 쓴다(`grep -o '"ref": "location[^"]*"' data/npcs/NpcScheduleDefinition.json | sort -u`). 처음 값 — 북쪽 광장 안, 다른 임시 지점과 2칸 이상:
  - 도서관: `[1040, 368]`
  - 가게: `[1264, 368]`
  - 일터: `[1040, 240]`
  - 광장: `[1680, 240]`, `[1680, 304]` (저녁에 두 명이 겹친다)
  - 이 칸이 막혀 있거나 `MapContractTests`·`TemporaryPointsTests`가 실패하면 같은 줄의 가장 가까운 열린 칸으로 옮기고 README에 적는다.

- [ ] **Step 1: 실패 테스트 — Data**

```csharp
[Fact] public void 일과표의_머무는_행동을_읽는다()      // 실제 data/: 모든 NpcSchedule의 모든 Entry.Stay가 NpcWanderStay, RadiusCells == 3, 쉼 2~6
[Fact] public void stay가_없는_줄은_서_있기다()        // 작은 고정 JSON
[Fact] public void 맵_데이터가_바뀌면_지문이_바뀐다()   // 임시 폴더에 data + game/world 복사 → interactions.json의 좌표 하나를 바꾸면 Compute 결과가 다르다
```

- [ ] **Step 2: 실패 테스트 — World** (`NpcRouteContractTests`, 실제 맵·데이터. 기존 World 테스트의 로더 도우미를 쓴다)

```csharp
[Fact] public void 일과표의_이웃한_시간대_자리_사이에_길이_있다()
// 모든 NPC, SlotStarts의 이웃한 두 시간대: 둘 다 자리가 있으면 NpcRouteFinder.Find != null
[Fact] public void 모든_NPC는_하루_중_만날_수_있는_때가_있다()
// 모든 NPC: 어느 시간대엔가 자리가 있고(MapAnchors.TryGet), 그 자리가 마을 시작 도착 지점에서 MapChecks.Walk로 닿는 맵 안에 있다
[Fact] public void 하루를_10분씩_훑어도_NPC는_막힌_칸에_서지_않는다()
// NpcWorld.Prepare(1); 420..1859를 10분씩, 모든 NPC: Present면 그 맵의 CollisionGrid.IsBlocked(칸) == false
[Fact] public void npcWalker를_읽는다()               // Catalog.NpcWalker.SpeedCellsPerSecond == 2.5f
```

- [ ] **Step 3: 실패 확인** — `dotnet test game/tests/Rootborn.Data.Tests` / `dotnet test game/tests/Rootborn.World.Tests --filter NpcRouteContractTests` → FAIL.
- [ ] **Step 4: 구현과 데이터 고치기.** `DialogueBuilders`: `b.Add("NpcWanderStay", …)` 한 줄, 일과 줄에서 `c.One<INpcStayBehavior>(entry, "stay")`. `registry.json`에 새 종류를 등록한다(`RealDataTests`가 빠진 종류를 잡는다).
- [ ] **Step 5: 통과 확인** — `dotnet test game/tests/Rootborn.Data.Tests`, `dotnet test game/tests/Rootborn.World.Tests`, `dotnet test game/tests/Rootborn.Server.Tests`(지문) 모두 PASS, 건수 줄지 않음. School↔Town 임시 연결(`maps.json` 39·90행의 문 `north`와 `areas`)이 길에 잡히지 않아 첫 테스트가 실패하면, `areas`가 `WorldMap.Doors`에 들어가는지 `WorldMapLoader`에서 확인하고 보고한다(맵 데이터를 고쳐 통과시키지 않는다).
- [ ] **Step 6: 커밋** — `[FEATURE][ASSET] NPC 머무는 행동·걷는 속도 데이터, 마을 NPC 자리(임시), 맵 데이터를 데이터 지문에`

---

### Task 6: 움직이는 상호작용 후보와 `NpcStage`

**Files:**
- Modify: `game/Rootborn.Core/src/Interaction/InteractionTargets.cs`
- Create: `game/Rootborn.World/NpcStage.cs`
- Modify: `game/Rootborn.World/WorldInteractionData.cs`(`PlacedNpc`·`NpcPlacement.Place` 삭제, `Candidates`가 NPC를 받지 않게), `game/tests/Rootborn.Simulation.Tests/Headless/HeadlessWorld.cs`(42-78행)
- Test: `game/tests/Rootborn.Core.Tests`(InteractionTargets 기존 테스트 파일), `game/tests/Rootborn.World.Tests/NpcStageTests.cs`, `game/tests/Rootborn.Simulation.Tests`(NPC 관련 기존 테스트 + 새 것)

**Interfaces:**
- Consumes: Task 4 `NpcWorld`, Task 5 `MapAnchors`·`Catalog.NpcWalker`.
- Produces:

```csharp
// InteractionCandidate: X, Y를 읽기 전용 속성으로, 더해서
public bool Active { get; private set; } = true;
public void MoveTo(float x, float y);
public void SetActive(bool active);
// InteractionTargets.Update: Active가 false인 후보는 건너뛴다. 대상이던 후보가 꺼지면 다음 Update에서 Target이 바뀐다.

namespace Rootborn.World {
  public readonly struct NpcOnMap { public readonly string NpcId; public readonly NpcPose Pose; }
  public sealed class NpcStage {
    public NpcStage(NpcWorld world, InteractionData interactions);
    /// 맵에 들어섰을 때. 후보 목록을 한 번 만든다: 물건, 문, 그리고 NPC 모두(꺼진 채로).
    public List<InteractionCandidate> EnterMap(string mapId, MapInteractions here, IEnumerable<DoorArea> doors);
    /// 프레임마다. 날이 바뀌었으면 Prepare. 지금 맵에 있는 NPC의 자리와 후보를 고친다. 새 객체를 만들지 않는다.
    public void Tick(int day, double dayMinutes);
    public int Count { get; }                    // 지금 맵에 있는 NPC 수
    public NpcOnMap this[int index] { get; }     // 0..Count-1
    public bool TryPose(string npcId, out NpcPose pose);   // 어느 맵에 있든. 테스트 드라이버가 쓴다
    public void ForgetMap(string mapId);
  }
}
```

- [ ] **Step 1: 실패 테스트 — Core**

```csharp
[Fact] public void 꺼진_후보는_대상이_되지_않는다()
[Fact] public void 대상이던_후보가_꺼지면_Press는_null이다()      // Update → Target == c; c.SetActive(false); Update → Target == null; Press() == null (Review Focus 4)
[Fact] public void 후보가_움직이면_새_자리로_고른다()             // MoveTo로 반경 밖 → Target null, 반경 안 → Target
```

- [ ] **Step 2: 실패 테스트 — World** (`NpcStageTests`, 실제 데이터)

```csharp
[Fact] public void 지금_맵에_있는_NPC만_보인다()                 // 마을에서 오후 한가운데(900분): Count > 0, 모든 Pose.MapId == "Town"
[Fact] public void 떠난_NPC의_후보는_꺼진다()                    // 학교에서 오전(500분)에 선생님 후보 Active, 오후(900분)에 Active == false
[Fact] public void 후보_좌표는_NPC_자리를_따라간다()             // Tick 두 번(이동 중): 후보의 X, Y == 그 NPC의 Pose
[Fact] public void 맵에_들어온_때가_달라도_같은_시각에는_같은_자리다()
// NpcStage 둘: 하나는 500분부터 1분씩 900분까지 Tick, 하나는 900분에 처음 Tick → 모든 NPC TryPose가 같다 (같이 하기의 약속)
[Fact] public void Tick은_새_객체를_만들지_않는다()
// 맵에 들어가 한 번 Tick한 뒤, GC.GetAllocatedBytesForCurrentThread() 차이가 0.01분씩 200번 Tick에 0 (걸음이 넘어가지 않는 짧은 구간)
```

- [ ] **Step 3: 실패 확인** — `dotnet test game/tests/Rootborn.Core.Tests --filter Interaction`, `dotnet test game/tests/Rootborn.World.Tests --filter NpcStageTests` → FAIL.
- [ ] **Step 4: 구현.** `HeadlessWorld`는 `Refresh`/`Place`의 NPC 부분을 지우고 `NpcStage`를 쓴다: 생성자가 `Func<double> dayMinutes`를 더 받고(`hour`는 다른 곳이 쓰면 남긴다), 맵이 바뀌면 `EnterMap` + `Interactions.SetCandidates`, `Update`마다 `Tick` 뒤 `Interactions.Update`. 창 없는 판의 가구 방(`HeadlessRooms`)이 가구를 바꾸면 `ForgetMap`을 부른다.
- [ ] **Step 5: 실패 테스트 — Simulation** (창 없이 하루 흘려 보내기)

```csharp
[Fact] public void 선생님이_정오에_학교를_떠나_마을_광장에_도착한다()
// 실제 데이터, 게임 한 판. 시각을 흘려 보내며 선생님 TryPose: 720분 직전 MapId School, 그 뒤 Town으로 바뀌고, 1019분 전에 광장 자리에서 반지름 3칸 안
[Fact] public void 이어_해도_같은_시각에는_같은_자리다()
// 800분에 저장 → 새 GameSession·새 HeadlessWorld로 읽기 → 모든 NPC의 TryPose가 저장 전과 칸 단위로 같다(분 아래 조각은 0에서 시작하므로 1칸 안)
```

- [ ] **Step 6: 통과 확인** — `dotnet test game/tests/Rootborn.Core.Tests`, `Rootborn.World.Tests`, `Rootborn.Simulation.Tests` PASS. Simulation의 기존 NPC 테스트가 "NPC는 자리에 서 있다"를 가정해 실패하면 `TryPose`로 NPC를 찾아가게 고친다(가정을 되살리려고 데이터를 서 있기로 바꾸지 않는다).
- [ ] **Step 7: 커밋** — `[FEATURE] 걷는 NPC를 월드에 — 움직이는 상호작용 후보, Host와 창 없는 판이 함께 쓰는 NpcStage`

---

### Task 7: Host — 그리기, 따라가 말 걸기, 메인 체인

**Files:**
- Modify: `game/Rootborn.Host/World/WorldScreen.cs`(155-215행 시간대·`RefreshInteractions`, 316-327행 `Update`, 389-405행 그리기 순서, 608-615행 `DrawNpc`), `game/Rootborn.Host/HostGame.cs`(484행 `FollowStudentTime` 부르는 곳, 가구가 바뀔 때)
- Modify: `game/tests/Rootborn.Host.Tests/PlayerDriver.cs`(311-320행 곁)
- Test: `game/tests/Rootborn.Host.Tests/NpcWalkHostTests.cs`(새), 기존 `MainChainHostTests`·`NewGameJourneyTests`

**Interfaces:**
- Consumes: Task 6 `NpcStage`, Task 1 `GameSession.DayMinutesExact`.
- Produces:
  - `WorldScreen.FollowStudentTime(Func<int> day, Func<float> hour, Func<float> lightHour = null, Func<double> dayMinutes = null)` — `dayMinutes`를 주지 않으면(`--world`로 띄운 판) 시각에서 만든다: `hour*60`, 하루 시작보다 이르면 `+1440`.
  - `public NpcStage WorldScreen.Npcs { get; }`
  - `PlayerDriver.WalkToNpc(string npcId, float withinCells = 1.5f, int maxFrames = 3600)` — 프레임마다 `Npcs.TryPose`로 자리를 다시 읽어 `WalkNear`처럼 다가간다. NPC가 다른 맵에 있거나 `maxFrames` 안에 닿지 못하면 무엇이 어디 있었는지 적은 예외.
  - `PlayerDriver.WaitUntilDayMinutes(double minutes, int maxFrames)` — 가만히 서서 프레임을 돌린다(시각을 강제로 바꾸지 않는다).

- 그리기: `DrawNpc`는 `Npcs[i].Pose`의 자리에 지금의 정지 한 칸을 그린다(발 Y 정렬 순서는 그대로, `_npcs` 목록 대신 `Npcs`를 for로 돈다). 걷는 그림 시트 연결은 이 계획에 넣지 않는다 — 그림이 오면 따로 한다(Task 8의 그림 요청).
- `RefreshInteractions`: 맵이 바뀔 때만 `EnterMap` + `SetCandidates`(지금의 `_storageCandidates` 더하기 포함). 시간대·날이 바뀔 때 후보를 다시 만들던 것을 지운다. `Update`에서 `Npcs.Tick(CurrentDay, dayMinutes())` 뒤 `Interactions.Update`.
- 집의 가구가 바뀌면(`FurnitureRoom`이 칸을 고치는 곳) `Npcs.ForgetMap(mapId)`.

- [ ] **Step 1: 실패 테스트** (`host-play-test` 스킬을 먼저 읽는다. `PlayerDriver` 동사만 쓴다)

```csharp
[Fact] public void 선생님이_학교에서_나와_마을을_걸어간다()
// 새 게임 → 마을의 학교 문 앞까지 걸어가 선다 → WaitUntilDayMinutes(725) → Npcs.TryPose(선생님): MapId == Town, Moving.
// 30프레임 뒤 자리가 달라졌다. 스크린샷 저장.
[Fact] public void 걸어가는_NPC에게_다가가_말을_건다()
// 위에 이어 WalkToNpc(선생님) → 상호작용 키 → 대화 화면이 열린다. (혼자 하기: 대화 중 NPC 자리가 그대로인지도 본다)
[Fact] public void NPC가_떠난_자리에서는_상호작용이_없다()
// 오후에 학교의 선생님 자리로 걸어가 상호작용 키 → 대화 화면이 열리지 않는다(World.Interactions.Target이 NPC가 아니다)
[Fact] public void 집의_NPC는_가구를_지나지_않는다()
// 집에서 가구가 있는 채로 게임 시간 2시간을 흘려 보내며 프레임마다 안내인 칸이 막힌 칸이 아님
```
NPC id는 테스트에서 실제 데이터 id를 써도 된다(`data/npcs/NpcDefinition.json`).

- [ ] **Step 2: 실패 확인** — `dotnet test game/tests/Rootborn.Host.Tests --filter NpcWalkHostTests` → FAIL. (이 PC에서 무거우면 커밋 뒤 `bash Scripts/remote-test/run.sh`.)
- [ ] **Step 3: 구현.**
- [ ] **Step 4: 메인 체인 맞추기.** `dotnet test game/tests/Rootborn.Host.Tests --filter "MainChainHostTests|NewGameJourneyTests"`. NPC 자리 좌표로 걸어가던 곳은 `WalkToNpc`로 바꾼다. NPC가 그 시각에 그 맵에 없어서 실패하는 걸음은 `WaitUntilDayMinutes`로 기다린다 — 상태 강제 설정이나 `GameSession` 직접 호출로 대신하지 않는다. 메인 체인이 "NPC가 없는 시간대에만 갈 수 있는" 걸음을 요구하면 멈추고 보고한다(일과표를 바꾸는 것은 기획이다).
- [ ] **Step 5: 통과 확인과 화면 보기** — Host 전체 PASS, 건수 줄지 않음. Step 1에서 저장한 스크린샷을 직접 열어 NPC가 벽·가구 안에 있지 않은지, 겹친 글자·빈 그림이 없는지 본다.
- [ ] **Step 6: 커밋** — `[FEATURE] NPC가 맵을 걸어 다닌다 — Host 그리기, 걸어가는 NPC에게 말 걸기, 메인 체인 테스트 맞춤`

---

### Task 8: 같이 하기 확인, 성능, 문서

**Files:**
- Test: `game/tests/Rootborn.Game.Tests/NpcTimeTests.cs`(더하기), `game/tests/Rootborn.Host.Tests/MultiplayerJoinHostTests.cs`(더하기), `game/tests/Rootborn.World.Tests/NpcStageTests.cs`(측정 하나)
- Modify: `game/world/README.md`, `game/README.md`, `game/tests/Rootborn.Game.Tests/SCENARIOS.md`, `.claude/rules/testing-discipline.md`(시나리오 표), `docs/superpowers/specs/2026-11-12-multiplayer-design.md`(NPC 자리 한 줄), `docs/art/art-requests.md`(커밋하지 않음)

**Interfaces:**
- Consumes: 앞의 모든 Task.

- [ ] **Step 1: 실패 테스트 — 같이 하기**

```csharp
// Game.Tests: 같은 방(IRoomTime 하나)을 쓰는 GameSession 둘, 한쪽은 늦게 들어온다
[Fact] public void 같은_방의_두_사람은_같은_분에_있다()
// 둘 다 AdvanceTime 뒤: floor(a.DayMinutesExact) == floor(b.DayMinutesExact) == 방의 분, 날도 같다
// Host.Tests(MultiplayerJoinHostTests 곁): 방을 연 판과 들어간 판
[Fact] public void 같이_하는_두_화면에서_NPC가_같은_자리에_있다()
// 같은 프레임에 두 판의 Npcs.TryPose(모든 NPC): MapId가 같고 자리 차이가 2칸 이하(분 아래 조각의 차이)
```

- [ ] **Step 2: 실패 확인 → 고치기 → 통과.** 실패하면 원인은 "자리 계산에 참가자마다 다른 값이 들어갔다"이다(Global Constraints 3번). 통신 메시지를 더해 맞추지 않는다.
- [ ] **Step 3: 성능 재기.** `NpcStageTests`에 측정 하나: NPC 60명(같은 일과표를 id만 달리해 메모리에서 만든 것)으로 `Prepare` 한 번의 시간과 `Tick` 10,000번의 시간·할당을 재서 테스트 출력에 남긴다. 기준: `Tick` 한 번 평균 0.05ms 아래, 걸음이 넘어가지 않는 구간에서 할당 0. 넘으면 고친다. 잰 값을 완료 보고에 적는다.
- [ ] **Step 4: 문서.**
  - `game/world/README.md`: NPC 자리 약속(자리는 걸어서 닿는 칸 옆, 같은 장소에 겹치는 NPC 수만큼), `npcWalker`, 임시 자리 목록, "NPC는 일과표의 이웃한 자리 사이를 걸을 수 있어야 한다"(`NpcRouteContractTests`).
  - `game/README.md`: NPC 이동 한 단락(자리는 시각으로 계산, 저장하지 않음).
  - `SCENARIOS.md`와 `testing-discipline.md` 표에 `NPC-001`(일과표대로 걸어 도착), `NPC-002`(자리에 없으면 만날 수 없다), `NPC-003`(같은 시각이면 같은 자리 — 이어 하기·같이 하기)과 대응 테스트.
  - `2026-11-12-multiplayer-design.md`: NPC 자리는 방의 (날, 분)에서 각자 계산, 메시지 없음, 같이 하기에서는 대화 중에도 걷는다.
  - `docs/art/art-requests.md` 끝에: NPC 다섯 명의 4방향 걷기 시트(플레이어 시트와 같은 짜임: 방향 4줄 × 6칸). **이 파일은 커밋하지 않고** 보고에 적는다.
- [ ] **Step 5: 전체 확인** — `bash Scripts/ci/run-all.sh`(또는 커밋 뒤 `bash Scripts/remote-test/run.sh`). 정적 검사 통과, 프로젝트별 건수가 시작 때보다 줄지 않음.
- [ ] **Step 6: 커밋** — `[TEST][DOCS] NPC 이동 — 같이 하기에서 같은 자리, 성능 측정, 맵 약속과 시나리오 id`

---

## 완료 보고에 적을 것

- 근거를 종류별로: 자동 테스트(Core·Data·World·Game·Simulation), 장치 이벤트 주입(Host — OS 입력 계층 미확인), 직접 본 스크린샷 경로.
- 실제 플레이로는 미확인인 것: 사람이 보는 걸음의 느낌(속도 2.5칸, 반지름 3칸, 쉼 2~6초가 어울리는지).
- 성능 측정 값(Task 8 Step 3).
- 사용자에게 남는 결정: 수치 세 개, 몸끼리 막힘, 같이 하기에서 말 거는 동안 멈출지, 길에서 마주치는 일을 늘리려면 일과표 내용(지금은 맵을 넘는 NPC가 선생님뿐), 걷는 그림.
