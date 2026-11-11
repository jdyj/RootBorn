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
- 고칠 것은 고치고 확장하기 쉽게 만든다(사용자 지시 2026-11-11, `.claude/rules/engine-migration.md`). 구조 개선과 분명한 버그 수정은 하고, 달라진 동작은 9절에 적는다. 수치·콘텐츠·진행 순서 같은 기획은 바꾸지 않는다.

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

실패로 보는 경우: 달라진 동작을 적지 않고 바꾼 경우, 기획(수치·콘텐츠)을 묻지 않고 바꾼 경우, 테스트 없이 옮긴 경우, 엔티티 id 분기를 넣은 경우, 옛 세이브를 못 읽게 된 경우.

## 9. Unity 판과 달라진 동작

옮기면서 일부러 바꾼 것을 여기에 한 줄씩 적는다(무엇을, 왜, 어느 테스트가 고정하는지).

- 퀘스트: 개수 0 이하 이벤트는 목표를 진행하지 않고 이벤트 키도 소모하지 않는다(원본은 1로 셈). `EventsWithoutAPositiveCount_CreditNothing_AndDoNotUseUpTheirKey`
- 퀘스트: `QuestLog`는 다시 알리는 이벤트(`IsReplay`)를 세지 않는다. 체인은 그대로 따라잡는다. `QuestLog_IgnoresReplayedEvents_WhileChainsStillCatchUpOnThem`
- 퀘스트: 체인 세이브 상태는 이름만 받고 숫자는 `Available`로 읽는다. 옛 세이브는 이름이라 그대로 읽힌다. `ChainSaveState_AcceptsOnlyStateNames`
- 퀘스트: 체인 진행 저장소는 이미 가진 로그를 체인 목록 없이도 돌려준다(원본은 null). `Store_ReturnsAHeldLogEvenWithoutAChainList`
- 알바 정산: 정산 뒤 요청을 서비스가 직접 적용됨으로 표시해, 정산 쪽이 빠뜨려도 두 번 정산되지 않는다. `ChangedBehavior_SettledJobIsMarkedEvenIfPayrollForgetsTheReceipt`
- 캠페인: 진행 상태 없이 보상을 받으면 거절하고 아무것도 기록하지 않는다(원본은 받은 것으로만 기록). `RewardWithoutStudentProgressIsRejectedAndNotRecorded`
- 캠페인: 진행 적용기는 바뀐 것이 없으면 결과 id를 남기지 않는다(원본은 소모). 캠페인 테스트로 고정.
- 하루 이벤트: 실행기가 선택지 가용 규칙을 검사하고, 안 되면 무변경으로 거절한다. `ChoiceAvailabilityRuleIsCheckedAndRejectionChangesNothing`
- 탐색: 한 선택지의 물건 지급·소모를 합쳐 확인하고, 안 들어가면 아무것도 주지 않는다(원본은 일부만 지급). `SeveralItemGrantsThatDoNotFitTogetherAreRejectedWithoutPartialGrant`, `SpendAndGrantInTheSameChoiceApplyTogether`
- 탐색: 거절한 선택은 빈 기록을 세이브에 남기지 않는다. `DismissAtZeroEnergyBypassesDefaultRiskAndCreatesNoRecordActivityOrOutcome`
- 탐색: 이어지는 퀘스트 결과가 이미 시작한 퀘스트를 다시 더하지 않는다(원본은 진행을 지워 보상을 두 번 받을 수 있었다). 탐색 테스트로 고정.
- 마일스톤: 보상 물건을 합쳐 확인하고, 안 들어가면 아무것도 바꾸지 않는다(원본은 일부만 주고 받은 것으로 기록). `SeveralItemRewardsThatDoNotFitTogetherAreRejectedWithoutPartialGrant`
- 마일스톤: 대상이 정해진 학교 밖 기록 목표는 그 대상의 기록이 있을 때만 하루 결과 적용에서 채워진다(원본은 다른 대상의 기록으로도 채워졌다). 사용자 결정 2026-11-11. `ChangedBehavior_DayResultDoesNotFillATargetedObjectiveFromAnotherTargetsLog`
- 하루 요약: 관계·상태의 음수 변화 기록('+' 없음)도 읽는다. 어제의 관계·상태 변화 목록도 같다(원본은 '+'가 붙은 기록만 읽었다). 사용자 결정 2026-11-11. `ResultLogSummaryReadsPlusLogsUsesKnownNamesAndSkipsOthers`
- 장소 상태: 요약과 오늘 기록의 숨김 여부가 첫 상태의 `IsHidden`을 따른다(원본은 항상 false). 지금 데이터에는 숨은 상태가 없어 화면은 그대로다. 사용자 결정 2026-11-11. `ChangedBehavior_HiddenStateMakesTheSummaryAndTodayRecordHidden`
- 퀘스트: 받을 때 제공 조건과 선행 조건을 확인한다. 조건이 있는 퀘스트는 판정할 문맥 없이 받을 수 없다(원본은 확인하지 않았다). 지금 데이터의 퀘스트는 조건이 비어 있다. 사용자 결정 2026-11-11. `ChangedBehavior_QuestIsAcceptedOnlyWhenItsPrerequisitesHold`, `ChangedBehavior_QuestIsAcceptedOnlyWhenItsProviderConditionsHold`
- 장래희망 집중 보상: 하루 집중(하루 끝 규칙의 집중 회복 값)을 넘기지 않는다. 진행 상태를 만드는 쪽이 `ConfigureDailyFocus(dayEndRules.DailyFocus)`를 불러야 하며, 부르지 않으면 막지 않는다. 사용자 결정 2026-11-11. `ChangedBehavior_FocusRewardStopsAtTheDailyFocus`
- 고르기 도전: 다른 도전처럼 허용한 실수를 넘으면 0점으로 끝난다(원본은 끝까지 가서 맞힌 비율로 점수를 냈다). 사용자 결정 2026-11-11. `ChangedBehavior_ChoiceChallengeFailsWhenMistakesPassTheAllowance`
- 성인 직업: 하루가 끝난 뒤(결과 화면)에는 시설을 고를 수 없다. 사용자 결정 2026-11-11. `ChangedBehavior_TransitionIsRefusedAfterTheDayHasEnded`
- 장소 상태 시간대: 12:00 전은 오전이다(원본은 정확히 8:00만 오전이라 7:00과 9:00이 오후였다). 사용자 결정 2026-11-11. `ChangedBehavior_EarlyHoursAreMorning`
- 미니게임 예약: 정산하지 않은 학생 미니게임 예약은 다음 날을 시작할 때 포기 처리한다(원본은 화면을 다시 불러올 때 처리). 성인 일 예약도 앞선 날의 것은 포기 처리한다 — 다음 날을 시작할 때 `ForfeitWorkLeftFromEarlierDays`를 부르고, 새 일을 예약하거나 바로 할 때도 같은 처리를 한다(원본은 예약이 남으면 날을 넘기지 못하게 막았다). 사용자 결정 2026-11-11. `ChangedBehavior_UnsettledStudentMinigameIsForfeitedWhenTheNextDayStarts`, `ChangedBehavior_UnsettledWorkIsForfeitedWhenTheNextDayStarts`, `ChangedBehavior_WorkLeftFromAnEarlierDayDoesNotBlockANewReservation`
- 일과: 길 묶음의 `MaxSelectionsPerDay`는 "그 묶음의 길 하나를 하루에 몇 칸까지 넣을 수 있는지"다. 값은 데이터에서 바꾼다(지금 1 → 같은 길은 하루 한 번). 원본에서는 쓰이지 않던 값이라 같은 길을 여러 칸에 넣을 수 있었다. 묶음에 없는 길은 제한이 없다. 사용자 결정 2026-11-11. `ChangedBehavior_TheSameRouteFillsOnlyAsManySlotsADayAsItsSetAllows`, `TheDailyLimitOfARoute_ComesFromData`
- 세이브: 퀘스트 체인 파일이 깨져 있으면 새 기록으로 덮어쓰지 않고 예외를 낸다(원본은 덮어썼다). Data 테스트로 고정.
- 데이터 읽기: 시설 단계 비용·시설 일 보상의 아이템과 개수 짝이 맞지 않거나, 캠페인 장소 방문 목표에 `requiresSchoolClass`가 있으면 로드 오류로 드러낸다. 지금 데이터에는 없다.
- 단서 해석: 순서형 정책은 같은 그룹의 바로 앞 순서 해석이 끝나야 통과한다(원본은 같은 그룹의 아무 해석이나 끝나 있으면 통과). 해석 목록이 없으면 순서 1 이상은 막는다. 단서 해석 테스트로 고정.
- 월드 상태 사용·발견 단서·단서 해석: 이어지는 퀘스트 결과가 아직 시작하지 않은 퀘스트만 연다(원본은 매번 다시 넣어 진행도가 지워지고 보상을 다시 받을 수 있었다). `FollowUpQuests.TryStart`.
- 월드 상태 사용·발견 단서·단서 해석: 조회나 거절된 요청이 빈 기록을 세이브에 남기지 않는다.
- 기록 안의 id 목록이 넣은 순서를 지킨다(원본은 순서가 정해지지 않았다). 도감 발견 시각은 시계에서 직접 읽지 않고 문맥이나 받은 시계에서 읽는다.
- 대화: 선택지의 퀘스트 받기가 퀘스트의 제공 조건·선행 조건을 따진다(위 퀘스트 수락 규칙을 따름).
- 세이브 파일 이름: 도감·발견 단서·단서 해석·탐색 파일은 기본 플레이어가 아니면 id를 정리한 새 이름으로 쓴다(예: `encyclopedia-steam_7.json`). 읽을 때 새 이름이 없으면 Unity 판 이름을 찾는다. 기본 플레이어의 파일 이름은 그대로다. 옛 이름에 ':'가 들어가면 옛 파일이 없는 것으로 본다.
- 세이브: 있는데 읽을 수 없는 세이브 파일(월드 영역·탐색 포함)은 새 기록으로 시작하지 않고 예외를 낸다. 새로 쓴 파일 10종의 맨 위에 `"Version": 1`이 들어간다(Unity 판 파일은 0으로 읽힘).
- 월드: 맵 밖은 언제나 막힌다. 한 프레임의 시간은 0.1초까지만 쓴다. 칸 경계의 부동소수 오차는 0.001px까지 허용한다.
- 월드: 집 출구 상자 높이가 0.65칸에서 1칸으로 커졌다. 집 문짝 그림 높이는 53.33px에서 53px. 가게의 겹친 타일은 LDtk 층 순서대로 그린다(Unity는 일부 순서가 섞였다).
- 월드: 한 칸 틈으로 들어갈 때 Unity 물리의 밀어내기 대신 몸이 있는 칸의 가운데로 살짝 미는 보정을 쓴다. 문 판정 여유 0.02칸은 Unity 물리의 모양 껍질을 흉내 낸 것이다.
- 월드: 마을 바닥은 LDtk 원본 타일로 그린다. Unity 판 마을의 Ground·Ground2와 Kitchen2는 GifWorldStyle 자동 타일로 바뀌어 있어 바닥 모양이 다르다(맵 대조 결과, 사용자 결정 대기).
- 가구: `Items` 필드가 없는 배치 파일은 빈 방으로 읽지 않고 예외를 낸다. 옮긴 가구의 기록은 세이브 안에서 원래 자리를 지킨다(원본은 파일 끝으로 보냈다). 가구를 놓거나 옮길 때는 저장이 성공한 뒤에만 상태가 바뀐다.
- 채집: 자원을 부술 만큼 쳤는데 나온 물건이 가방에 다 들어가지 않으면 부수지 않고 아무것도 주지 않는다(원본은 들어가는 만큼만 넣었다).
- 레시피 공개: 깨진 레시피 진행 파일은 새 기록으로 덮어쓰지 않고 예외를 낸다. 새로 쓴 가구·레시피 파일 맨 위에 `"Version": 1`이 들어간다.
- 옮기지 않은 영역(지금 게임이 쓰지 않음, 근거는 `game/Rootborn.Data/Builders/NotUsedByGame.cs`): `StatusEffectDefinition`, `KnowledgeNode`, `HitGroundWithRockTrigger`, `ToolDefinition`, `FishingAnimationEffect`, `GatherResourceStep`, `HeirTrait`.
- 월드 2차: 읽을 수 없는 슬롯 정보는 새 게임으로 덮지 않고 시작을 거절한다(원본은 슬롯을 지우고 새로 시작). 새 게임을 시작해도 슬롯에 남은 다른 파일은 지우지 않는다. 새 게임 시드는 슬롯 id에서 만든다(만들기 화면이 생기면 다시 정한다).
- 월드 2차: 마을 낮밤은 게임 시계를 따르고 시각을 세이브에 쓰지 않는다(원본은 활동으로만 흐르는 학생 생활 시각을 따랐다 — 화면을 옮길 때 GameSession의 시각으로 잇는다). 가로등 같은 지역 조명은 옮기지 않았다.
- 월드 2차: 성장 단계는 아직 학생 진행을 읽지 않아 늘 1일차 학생이다. NPC는 정의의 맵 그림 데이터로 그린다(원본은 그 데이터를 쓰지 않았다). "[E] Talk" 글자 대신 작은 표시만 그린다(화면을 옮길 때 글자를 넣는다).
- 게임 진행(GameSession): 적용된 명령마다 스냅샷과 영역 파일을 모두 다시 쓴다(원본은 바뀐 영역 파일만, 내용은 같다). 진로 연습은 적용될 때마다 저장한다(원본은 힌트가 열릴 때만 저장한 것으로 보임).
- 게임 진행(GameSession): 하루 이벤트 진행을 아직 저장하지 않는다(파일 이름 규칙이 Core에 없다 — 잇는 일 남음). 다음 날 시작 때 "이미 놓은 가구 다시 알리기"를 아직 하지 않는다(가구 연결 남음).
