# 프로젝트 1 — 규칙 옮기기 설계

- 날짜: 2026-11-11
- 상태: **사용자 승인(2026-11-11).** 같은 날 진행 방식이 바뀌었다: Unity 판을 지금 얼리고 규칙을 Core로 바로 옮긴다(사용자 결정, 속도를 위해).
- 상위 문서: `docs/superpowers/specs/2026-11-11-unity-removal-design.md`

## 1. 목적

`Assets/Scripts/Game`에 있는 게임 규칙을 엔진을 모르는 `Rootborn.Core`에 새로 쓴다. MonoGame 판이 이 Core를 쓴다.

Unity 판은 태그 `unity-playable-20261111`에 얼려 둔다. 그 뒤로 Unity 코드는 고치지 않고 읽기 전용 참고 자료로만 쓴다. Unity 쪽에 연결 코드를 쓰지 않고, Unity 테스트도 돌리지 않는다.

## 2. 조사 결과 (요약)

- 진행 상태 클래스는 이미 순수 클래스이고 엔티티를 문자열 id로 저장한다(`StudentLifeProgress` 등). Unity 의존은 `Mathf` 정도다.
- 로직을 담은 ScriptableObject가 약 156개 파일(추상 기반 57개)이다: StudentLife 83, Quests 18, DiscoveryClues 17, Careers 8, WorldState 8, Minigames 8, 기타 14.
- 규칙 코드가 `SaveService`(정적), 레지스트리, 플레이어 전역 상태를 직접 부르는 곳이 있다.
- EditMode 테스트 415개 파일 중 약 200개가 규칙 테스트 후보다(나머지는 에셋·씬 검사).
- `PlayerStatus`는 어느 씬에도 없다. Status 영역은 옮기지 않는다.

## 3. 옮기는 방식

Unity의 SO 하나가 "값 + 로직"이던 것을, Core에서는 **값을 생성자로 받는 순수 클래스**로 쓴다.

```csharp
// Unity (참고용, 고치지 않음)
public sealed class TraitDeltaActivityEffect : LifeActivityEffectBase {
    [SerializeField] private TraitDefinition _trait;
    [SerializeField] private int _delta;
    public override void Apply(StudentLifeProgress progress) => progress?.AddTrait(_trait, _delta);
}

// Rootborn.Core
public sealed class TraitDeltaEffect : ILifeActivityEffect {
    public TraitDeltaEffect(string traitId, int delta) { TraitId = traitId; Delta = delta; }
    public string TraitId { get; }
    public int Delta { get; }
    public void Apply(StudentLifeProgress progress) => progress.AddTrait(TraitId, Delta);
}
```

- 엔티티는 id 문자열로만 가리킨다. 다른 정의를 참조하던 필드는 id가 된다.
- 정의(활동, 퀘스트, 진로 등)는 Core의 불변 객체다. 실행기는 Core 정의만 받는다.
- 전략 종류마다 클래스 하나. 프로젝트 2의 변환기가 내는 JSON(`{"type": "<Unity 클래스 이름>", ...}`)에서 이 객체를 만드는 로더는 이 프로젝트에서 영역별로 함께 쓴다.
- 엔티티 데이터 드리븐 원칙은 그대로다. 엔티티 id로 분기하는 코드를 쓰지 않는다.
- 동작은 바꾸지 않는다. Unity 코드와 다르게 동작해야 할 이유가 보이면 옮기지 말고 기록해 사용자에게 묻는다.

바깥 연결은 Core 인터페이스로 둔다: 저장소(슬롯·키로 읽기·쓰기), 정의 조회(id → 정의), 기록. 시간은 값으로 받는다. 세이브 데이터 모델은 Core에 두고 기존 파일 형식(JsonUtility가 쓴 JSON)과 같은 필드 이름을 유지한다.

## 4. 범위

**옮긴다:** StudentLife(진행, 활동, 일과, 하루 끝, 하루 이벤트, 캠페인, 진로 후보·관심, 장소 상태, 학교 밖 활동, 알바), Quests(로그, 체인, 조건, 보상, 완료 효과), Careers, Minigames 판정·정산, WorldState, DiscoveryClues, Encyclopedia 해금, Dialogue 조건·NPC 일정, 세이브 데이터 모델, 가구 배치 판정(격자 인터페이스), 인벤토리.

**옮기지 않는다:** World·Player·Interiors·Lighting·Bootstrap(프로젝트 3에서 MonoGame용으로 새로 씀), UI·Network(프로젝트 4·5), Status, 레거시(지금 게임이 안 쓰는 Family·Heir·Lineage·Crafting·Tools·Resources·Knowledge·VerticalSlice의 부분 — 영역을 옮길 때 실제 사용 여부를 보고 정한다).

## 5. 순서

영역 하나를 한 묶음으로 한다. 묶음마다 테스트와 함께 커밋한다.

1. ~~게임 시계~~ (끝: `DayClock`, `DayParts`, `CoreMath`)
2. StudentLife 중심부: 진행 상태, 활동 실행, 요구 조건·효과, 일과, 하루 끝
3. StudentLife 나머지: 하루 이벤트, 캠페인, 장소 방문·상태, 학교 밖 활동, 알바
4. Quests
5. Careers·Minigames
6. WorldState·DiscoveryClues·Encyclopedia·Dialogue
7. 세이브 모델·가구 배치 판정·인벤토리 나머지

## 6. 검증

- `dotnet test Tools/Rootborn.Core.Tests`만 쓴다. 실패 테스트 먼저, 구현 다음.
- 기존 EditMode 규칙 테스트의 내용을 옮겨 온다(SO 만들기 → 생성자 호출). 이것이 "Unity 판과 같은 동작"의 근거다. 옮긴 테스트와 원래 파일의 대응은 테스트 파일 머리 주석 한 줄로 남긴다.
- 세이브: 고정용 세이브 파일을 읽어 다시 쓴 결과가 같은지 확인한다. 사용자 세이브는 쓰지 않는다.
- 이 프로젝트의 통과는 전부 "자동 테스트"다. 화면을 거친 확인은 MonoGame 판이 생긴 뒤에 한다.

## 7. 제약

- Core 위치는 `Packages/com.rootborn.core/Runtime` 그대로, LangVersion 9, `UnityEngine` 참조 없음. (얼린 Unity 프로젝트가 참고용으로 계속 열릴 수 있게 한다. Unity를 저장소에서 지울 때 위치와 언어 버전을 다시 정한다.)
- 새 `.cs`에는 `.meta`를 함께 둔다(같은 이유).
- `Assets/` 아래는 고치지 않는다.
- 커밋은 한글, `[REFACTOR]`·`[TEST]` 접두어.

## 8. 끝났다는 기준

- 4절 "옮긴다"의 규칙이 Core에 있고 `dotnet test`로 돈다. 건수와 실행 시간을 보고한다.
- 옮겨 온 기존 규칙 테스트가 모두 통과한다. 옮기지 않은 테스트는 이유와 함께 목록으로 남긴다.
- 영역을 다 옮기면 그 영역에 대한 Unity 전제 규칙 조항·메모리를 그 자리에서 지운다(사용자 지시).

실패로 보는 경우: 옮기는 김에 규칙 동작을 바꾼 경우, 테스트 없이 옮긴 경우, 엔티티 id 분기를 넣은 경우.
