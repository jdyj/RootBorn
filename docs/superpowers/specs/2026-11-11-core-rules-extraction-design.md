# 프로젝트 1 — 규칙 떼어내기 설계 초안

- 날짜: 2026-11-11 (main `097d76b9d` 기준 조사)
- 상태: **사용자 승인(2026-11-11).** 9절의 기본값도 승인됐다.
- 상위 문서: `docs/superpowers/specs/2026-11-11-unity-removal-design.md` (6절의 프로젝트 1)
- 작성 경위: 질문 단계 없이 조사 후 작성했다. 아래 선택은 따로 표시하지 않으면 작성자(Claude) 판단이다. 사용자가 정해야 할 것은 9절에 모았다.

## 1. 목적

`Rootborn.Game`에 있는 게임 규칙을 엔진을 모르는 `Rootborn.Core`로 옮긴다. Unity 판은 그대로 플레이 가능해야 한다.

얻는 것:

- 옮긴 규칙은 Unity 없이 `dotnet test`로 수 초 안에 검증된다.
- MonoGame 판(프로젝트 3~4)이 같은 Core를 그대로 쓴다. 규칙을 두 번 쓰지 않는다.
- 프로젝트 2(데이터 내보내기)가 JSON에서 만들어야 할 대상(Core 정의 객체)이 정해진다.

## 2. 조사 결과

- `Rootborn.Core`는 `Packages/com.rootborn.core`에 있고 `noEngineReferences: true`, 지금 13개 파일이다. `Rootborn.Game`, UI, Network, Editor가 이미 참조한다. 테스트는 `Tools/Rootborn.Core.Tests`(net8.0, LangVersion 9).
- **이미 쓰는 경계 방식이 있다.** 퀘스트 목표는 SO가 순수 데이터인 `QuestObjectiveSpec`을 만들고, 판정은 Core의 `QuestObjectiveMatcher`가 한다. 이 방식을 전체로 넓힌다.
- **진행 상태는 이미 순수 클래스다.** `StudentLifeProgress`는 엔티티를 SO 참조가 아니라 **문자열 id**로 저장한다(`Dictionary<string,int>`). Unity 의존은 `Mathf`뿐이다. `CampaignProgress`·`CampaignRunner`도 같다.
- **로직을 담은 SO가 많다.** 추상 SO 기반 클래스 57개, 가상·추상 메서드를 가진 SO 파일 약 156개.

| 폴더 | 로직 SO 파일 | 전체 파일 |
|---|---|---|
| StudentLife | 83 | 163 |
| Quests | 18 | 46 |
| DiscoveryClues | 17 | 31 |
| Careers | 8 | 18 |
| WorldState | 8 | 22 |
| Minigames | 8 | 10 |
| Dialogue, Knowledge, Crafting, Encyclopedia, Tools, Characters | 14 | — |

- 영역끼리 서로 참조한다(StudentLife ↔ Quests, Careers → StudentLife 등). Core는 어셈블리 하나라 문제되지 않는다.
- 규칙 코드가 `SaveService`(정적), `Common`(레지스트리·인벤토리), `Player` 전역 상태를 직접 부르는 곳이 있다. 이 연결을 끊는 것이 실제 작업의 어려운 부분이다.
- EditMode 테스트 415개 파일 중 211개가 `AssetDatabase`·`EditorSceneManager`·`UnityEditor`를 쓴다(에셋·씬 검사). 나머지 약 200개가 규칙 테스트 후보다. 정확한 분류는 하지 않았다.

## 3. 옮기는 방식

### 한 전략이 옮겨지는 모습

지금:

```csharp
// Rootborn.Game — SO가 데이터와 로직을 함께 가진다
public sealed class TraitDeltaActivityEffect : LifeActivityEffectBase {
    [SerializeField] private TraitDefinition _trait;
    [SerializeField] private int _delta;
    public override void Apply(StudentLifeProgress progress) => progress?.AddTrait(_trait, _delta);
}
```

옮긴 뒤:

```csharp
// Rootborn.Core — 로직. id만 안다. dotnet test로 검증한다.
public sealed class TraitDeltaEffect : ILifeActivityEffect {
    public TraitDeltaEffect(string traitId, int delta) { TraitId = traitId; Delta = delta; }
    public string TraitId { get; }
    public int Delta { get; }
    public void Apply(StudentLifeProgress progress) => progress.AddTrait(TraitId, Delta);
}

// Rootborn.Game — SO는 직렬화된 값과 "Core 객체 만들기"만 남는다
public sealed class TraitDeltaActivityEffect : LifeActivityEffectBase {
    [SerializeField] private TraitDefinition _trait;
    [SerializeField] private int _delta;
    public override ILifeActivityEffect Build() => new TraitDeltaEffect(_trait != null ? _trait.Id : null, _delta);
}
```

- 정의 SO(`LifeActivityDefinition` 등)도 같은 식으로 Core 정의(`LifeActivity`: id, 비용, 요구 조건 목록, 효과 목록)를 만든다. 실행기(`LifeActivityRunner`)는 Core로 옮겨 Core 정의만 받는다.
- SO에서 Core 정의로 바꾸는 일은 한 번 하고 SO 인스턴스에 기억해 둔다. 테스트용 `ConfigureFor*`가 값을 바꾸면 기억을 지운다.
- 프로젝트 2의 JSON 로더는 같은 Core 객체를 만든다. `{"type":"traitDelta","traitId":"...","delta":1}` ↔ `TraitDeltaEffect`.
- 엔티티 데이터 드리븐 원칙은 그대로다. Core에도 엔티티 id로 분기하는 코드를 쓰지 않는다. 전략 **종류**(`traitDelta`)로 나뉘는 것은 지금의 SO 클래스 구분과 같은 것이다.

### 기존 코드가 깨지지 않게

- UI와 MonoBehaviour는 **고치지 않는 것을 원칙**으로 한다. 프로젝트 4에서 다시 쓸 코드라 여기서 다듬지 않는다.
- 옮긴 클래스의 네임스페이스는 `Rootborn.Core.<영역>`이 된다. 쓰는 쪽에는 `using` 한 줄을 기계적으로 더한다.
- SO를 받던 메서드(`progress.AddTrait(TraitDefinition, int)`)는 `Rootborn.Game`에 확장 메서드로 남겨 기존 호출이 그대로 컴파일되게 한다.
- SO 클래스 이름, 직렬화 필드 이름, `.meta` guid는 바꾸지 않는다. 에셋 3,806개와 씬을 건드리지 않기 위해서다.

### 바깥으로 나가는 연결 (포트)

규칙이 엔진이나 전역 상태에 닿던 곳은 Core의 인터페이스로 바꾸고, Unity 쪽이 구현을 넣어 준다.

| 지금 | Core에서 |
|---|---|
| `Mathf.Max/Min/Clamp` | `System.Math`. 반올림처럼 동작이 다른 것은 한 곳에 모아 테스트로 고정 |
| `Debug.Log*` | 기록 인터페이스. 기본은 아무것도 하지 않음 |
| `SaveService` 정적 호출 | 저장소 인터페이스(슬롯·키로 읽기·쓰기). Unity 쪽은 기존 `SaveService`로 구현 |
| `JsonUtility` | 세이브 데이터 모델은 Core에 두고, JSON으로 바꾸는 일은 인터페이스 뒤. Unity는 `JsonUtility`, MonoGame 판은 나중에 같은 파일을 읽는 구현 |
| `GameClock.Instance`, `Time.*` | 시간 판정은 값(일차, 시각)을 인자로 받는 순수 함수 |
| `Managers.Data.Registry` | id로 정의를 찾는 조회 인터페이스 |
| 정적 이벤트 | 가능한 한 실행기의 결과값으로 돌려주고, 필요한 것만 Core 이벤트로 |

세이브 파일 형식은 바꾸지 않는다(상위 문서 결정: 기존 세이브 호환 유지). 고정용 세이브 파일로 "옮기기 전후에 같은 내용을 읽고 같은 바이트를 쓴다"를 확인한다. 사용자 세이브는 쓰지 않는다.

## 4. 범위

**옮긴다:** StudentLife(진행, 활동, 일과, 하루 끝, 하루 이벤트, 캠페인, 진로 후보·관심), Quests(목표 외 나머지: 로그, 체인, 조건, 보상, 완료 효과), Careers, Minigames의 판정·정산 규칙, WorldState, DiscoveryClues, Encyclopedia 해금, Dialogue 조건·일정 규칙, Status, 시간 판정, 세이브 데이터 모델, 가구 배치 판정(격자 인터페이스를 끼워 `Tilemap` 호출을 뗀다).

**옮기지 않는다:**

- World, Player, Interiors, Lighting, Bootstrap, Managers — 화면·물리에 묶여 있어 프로젝트 3에서 MonoGame용으로 다시 쓴다. 걷기·충돌·문 판정의 Core 구현도 프로젝트 3에서 만든다.
- UI, Network — 프로젝트 4·5.
- 레거시로 보이는 것: Family, Heir, Lineage, Crafting, Tools, Resources, Knowledge(채집 시절), VerticalSlice. 지금 게임이 실제로 쓰는지 슬라이스 0에서 확인하고, 쓰는 것만 옮긴다(9절 1번).
- 데이터 형식 변경 — 프로젝트 2.

## 5. 순서

영역 하나를 한 슬라이스로 한다. 슬라이스마다 커밋하고 검증한다(6절). 앞 슬라이스가 뒤 슬라이스의 토대다.

| # | 슬라이스 | 내용 | 크기 |
|---|---|---|---|
| 0 | 토대 | 3절의 포트(기록, 저장소, 조회), 수학 도우미, SO 껍데기 공통 패턴(Build + 기억), 이전 기간 규칙 문서, 레거시 사용 여부 확인, 규칙 테스트 분류표 | 작음 |
| 1 | Status·시간 | 가장 작은 영역으로 패턴을 한 번 끝까지 돌려 본다. 여기서 드러난 문제로 패턴을 고친다 | 작음 |
| 2 | StudentLife 중심부 | `StudentLifeProgress`, 활동 실행, 요구 조건·효과, 일과, 하루 끝 | 큼 |
| 3 | StudentLife 나머지 | 하루 이벤트, 캠페인, 장소 방문·상태, 학교 밖 활동, 알바 | 큼 |
| 4 | Quests | 로그, 체인, 조건, 보상(인벤토리 사전 검증·원자적 지급은 이미 Core), 완료 효과 | 중간 |
| 5 | Careers·Minigames | 진로 후보·관심·전환 요건, 미니게임 판정과 정산 | 중간 |
| 6 | WorldState·DiscoveryClues·Encyclopedia·Dialogue | 조건, 결과, 해금, NPC 일정 | 중간 |
| 7 | 세이브·배치 | 세이브 모델과 저장소 포트 정리, 가구 배치 판정의 격자 인터페이스 | 중간 |

슬라이스 1이 끝난 뒤 패턴이 맞는지 사용자에게 한 번 보고한다. 그 뒤로는 슬라이스 단위로 계속한다.

## 6. 검증

슬라이스마다:

1. **Core 테스트 (`dotnet test Tools/Rootborn.Core.Tests`)** — 옮긴 규칙마다 실패 테스트 먼저, 구현 다음. 기존 EditMode 규칙 테스트가 있으면 그 내용을 옮겨 온다(SO 만들기 → 생성자 호출).
2. **Unity 쪽은 FEVM에서 커밋 해시 기준으로** — EditMode와 PlayMode 기준선(2026-11-10: PlayMode 177/179, EditMode 실패 55건 기준선)에서 새 실패가 없어야 한다. Windows Unity는 켜지 않는다.
3. **빌드된 Addressables로 PlayMode 한 번 더** — 커밋 직전 규칙(`core-logic-and-play-evidence.md`).
4. **CI** — Core에 `UnityEngine` 참조가 없는지 검사(이미 있음).

보고 규칙: 이 프로젝트는 동작을 바꾸지 않는 옮기기다. 완료 보고에서 "실제 플레이로 확인"이라고 쓸 수 있는 것은 PlayMode E2E(장치 입력)가 통과한 흐름뿐이고, dotnet 테스트와 EditMode 통과는 "자동 테스트"로 적는다.

**테스트 중복 정리:** 규칙을 Core로 옮기고 그 테스트를 dotnet으로 옮기면, 같은 내용을 검사하던 EditMode 테스트는 지운다(기존 규칙: 같은 동작을 Unity EditMode에서 중복 검증하지 않는다). SO 껍데기가 Core 객체를 제대로 만드는지는 영역마다 대표 EditMode 테스트 몇 개로만 확인한다. 에셋·씬 검사 테스트는 건드리지 않는다. 지우기 전에 어떤 테스트를 어디로 옮겼는지 표로 남긴다.

## 7. 제약

- Core는 Unity가 컴파일할 수 있어야 한다: LangVersion 9, Unity가 지원하는 .NET API만. `System.Text.Json`은 Core에서 쓰지 않는다.
- 새 `.cs`와 `.meta`는 직접 작성한다(`Packages/com.rootborn.core` 아래도 `.meta` 필요). 열린 Unity 편집기 밑에서는 쓰지 않는다.
- 사용자의 미커밋 변경을 건드리지 않는다. 지금 `AGENTS.md`, `.claude/constitution.md`, `.claude/rules/changelog.md`에 사용자 미커밋 변경이 있다. 이전 기간 규칙은 새 파일 `.claude/rules/engine-migration.md`로만 쓰고, `AGENTS.md`에 가리키는 줄을 넣는 것은 사용자 확인 뒤에 한다(9절 3번).
- 커밋은 슬라이스 안에서 작게, `[REFACTOR]`·`[TEST]` 접두어, 한글 본문.
- 성능: SO → Core 정의 변환을 매 호출마다 하지 않는다. 정의당 한 번 만들고 기억한다.

## 8. 끝났다는 기준

- 슬라이스가 끝날 때마다, 그 슬라이스로 쓸모없어진 Unity 관련 문서·규칙·메모리·테스트를 그 자리에서 지운다(사용자 지시 2026-11-11).

- 4절 "옮긴다"의 영역에서 SO 클래스에 남은 것은 직렬화 필드, 접근자, `Build`뿐이다. 판정·계산·상태 변경은 Core에 있다.
- 그 영역의 규칙 테스트는 `dotnet test`로 돈다. 실행 시간과 건수를 보고한다.
- FEVM EditMode·PlayMode가 기준선에서 새 실패 없이 통과한다. 빌드된 Addressables로도 통과한다.
- 세이브 고정 파일의 읽기·쓰기 결과가 옮기기 전과 같다.
- 에셋과 씬 파일은 바뀌지 않았다(`git diff --stat`으로 확인).

실패로 보는 경우: 옮기는 김에 규칙 동작을 바꾼 경우, UI를 다듬다 범위가 커진 경우, Core 테스트는 통과하는데 Unity 판 PlayMode 기준선이 내려간 경우.

## 9. 사용자가 정해야 할 것

1. **레거시 영역(Family, Heir, Lineage, Crafting, Tools, Resources, Knowledge, VerticalSlice).** 기본값: 지금 게임이 쓰는 것만 옮기고 나머지는 Unity 판과 함께 사라지게 둔다.
2. **옮긴 규칙의 EditMode 테스트를 dotnet으로 옮기고 Unity 쪽 것은 지운다.** 기본값: 그렇게 한다. 옮긴 표를 남긴다.
3. **`AGENTS.md`에 이전 기간 규칙을 가리키는 한 줄 추가.** 그 파일에 사용자 미커밋 변경이 있다. 기본값: 줄만 더하고 커밋에는 넣지 않은 채 알려 드린다.
4. **슬라이스가 검증되면 main에 커밋하고 푸시한다**(2026-11-07의 기존 방침). 기본값: 그대로 따른다.

## 10. 조사하지 못한 것

- 규칙 테스트 약 200개 파일의 정확한 분류와 건수.
- `Common`과 `Player`에서 규칙 코드가 실제로 쓰는 범위.
- 레거시 영역의 실제 사용 여부.
- `Mathf`와 `System.Math`의 동작 차이가 실제로 결과를 바꾸는 곳이 있는지(반올림, 부동소수 비교).
- 테스트와 Unity는 이번 조사에서 실행하지 않았다.
