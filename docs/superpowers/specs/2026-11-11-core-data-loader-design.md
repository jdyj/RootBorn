# 데이터 로더와 세이브 저장소 — 설계

- 날짜: 2026-11-11 (main `d37565656` 기준 조사)
- 상태: **초안, 사용자 승인 대기.**
- 작성 경위: 사용자가 질문 단계를 건너뛰고 추천안으로 쓰라고 했다. 이 문서의 선택은 모두 **작성자(Claude)의 판단**이다. 사용자가 뒤집을 만한 것은 10절에 모았다.
- 위치: `docs/superpowers/specs/2026-11-11-unity-removal-design.md` 4절의 `Rootborn.Data`에 해당한다. 규칙 떼어내기(프로젝트 1)와 월드(프로젝트 3) 사이를 잇는다.

## 1. 왜 필요한가

지금 `Rootborn.Core`의 규칙 객체는 테스트가 생성자로 직접 만든 것뿐이다. `data/`의 JSON 2,289개와 이어져 있지 않아서, MonoGame 판은 실제 게임 데이터로 규칙을 돌릴 수 없다. 세이브도 마찬가지다. Core에는 세이브 모양(클래스)만 있고 파일을 읽고 쓰는 것이 없으며, 영역마다 저장소 인터페이스를 따로 두었다.

이 프로젝트가 끝나면:

- `data/`를 읽어 Core 규칙 객체를 만들 수 있다. 실제 데이터 전체가 오류 없이 읽히는지 `dotnet test`가 확인한다.
- Unity 판이 쓴 세이브 파일을 MonoGame 판이 읽는다. 이것도 `dotnet test`가 고정 파일로 확인한다.
- 새 종류(목표, 보상, 조건 등)를 더할 때 Core 클래스 하나와 등록 한 줄이면 된다.

## 2. 조사한 사실

- **JSON 모양.** 파일 하나가 엔티티 배열이다(`data/<폴더>/<Unity 클래스 이름>.json`). 엔티티마다 `type`(Unity 클래스 이름), `id`, `asset`(원본 경로)과 필드가 있다. 다른 엔티티는 `{ "ref": "<id>", "type": "<클래스>" }`로 가리킨다. 전략(효과, 조건, 목표)도 지금은 전부 `ref`다. 열거형은 숫자로 나와 있다(`"category": 0`).
- **`data/registry.json`**은 게임에 등록된 엔티티의 id 목록이다(종류별, 순서 있음).
- **Core 클래스 이름은 Unity와 다를 수 있다.** 예: JSON `TraitDeltaActivityEffect` → Core `TraitDeltaEffect`. Core는 다른 엔티티를 대부분 객체가 아니라 id 문자열로 받는다.
- **Core는 JSON 라이브러리를 쓰지 못한다.** 엔진 참조 없음, LangVersion 9, `System.Text.Json` 없음. 그래서 읽기·쓰기는 Core 밖에 둔다.
- **세이브 모양**은 `[Serializable]` 클래스의 공개 필드이고 이름이 Unity 판 파일과 같다. Unity 판은 `<저장 폴더>/<슬롯>/<파일>.json`에 `JsonUtility`로 썼다. 일부 모양에만 `Version` 필드가 있다(`AdultCareer*`, `DailyPlan`, 착용 세이브).
- **저장소 인터페이스가 영역마다 따로 있다:** `IQuestChainSaveStorage`(main), `IEncyclopediaSaveStore`·`IWorldStateSaveStore`(core-world 브랜치). 파일 이름 정리 규칙도 흩어져 있다(`SaveFileNames.Sanitize`는 core-student-b 브랜치, 도감만 정리하지 않음).
- **Unity 빌드가 쓴 QA 세이브가 남아 있다.** `output/dream-job-20261104/playthrough-20261105/saves-exe/slot-0/`(실행 파일로 플레이한 것), `output/design-comfy-20261031/saves-*/` 등. 사용자 세이브가 아니다. 다만 2026-11-05 이전 것이라 그 뒤에 생긴 필드는 들어 있지 않다.
- 풀지 못한 참조 12건은 그대로 있다(책상 가구, 오피스 타일셋, Town2 LDtk 정의). 규칙 데이터가 아니라 그림·맵 쪽이다.

## 3. 구조

```
game/Rootborn.Core/Rootborn.Core.csproj     Packages/com.rootborn.core/Runtime 소스를 그대로 묶는 프로젝트 (새 코드 없음)
game/Rootborn.Data/                          JSON 읽기, 정의 만들기, 세이브 파일 저장소. System.Text.Json 사용
game/tests/Rootborn.Data.Tests/              로더·세이브 테스트와 고정 파일
```

- `Rootborn.Data`는 Core만 참조한다. MonoGame을 참조하지 않는다. Host가 Data를 참조한다.
- Core 소스 위치는 옮기지 않는다(Unity 제거 때 옮긴다). `Tools/Rootborn.Core.Tests`도 그대로 둔다.
- Core에 더하는 것은 4.4절의 저장소 인터페이스 하나와 세이브 버전 필드뿐이다.

## 4. 정의 로더

### 4.1 흐름

1. **읽기.** `data/` 아래 엔티티 파일을 모두 읽어 `(type, id)` → JSON 노드 표를 만든다. 객체는 아직 만들지 않는다.
2. **만들기.** 종류별 만들기 함수가 노드를 Core 객체로 바꾼다. 다른 엔티티가 필요하면 문맥에 요청하고, 문맥은 한 번 만든 객체를 기억해 같은 것을 돌려준다. 서로를 가리켜 끝나지 않는 참조는 오류로 낸다.
3. **묶기.** 결과를 `GameDefinitions` 하나에 담는다: `Get<T>(id)`, `TryGet<T>(id, out T)`, `All<T>()`. `All<T>()`의 순서는 `registry.json`을 따른다. Core 규칙이 요구하는 조회 인터페이스(id → 정의)는 `GameDefinitions`가 구현하거나 얇은 어댑터로 낸다.

### 4.2 종류 등록

```csharp
// game/Rootborn.Data/Builders/QuestBuilders.cs
internal static class QuestBuilders
{
    public static void Register(DefinitionBuilders b)
    {
        b.Add("TalkQuestObjective", (n, c) =>
            new TalkQuestObjective(n.Str("displayKey"), n.Int("requiredCount"), n.RefId("targetNpc")));
        b.Add("QuestDefinition", (n, c) =>
            new QuestDefinition(n.Id, n.Str("displayNameKey"), n.Str("descriptionKey"),
                c.All<QuestObjective>(n, "objectives"), c.All<QuestReward>(n, "rewards"), ...));
    }
}
```

- 등록 키는 JSON의 `type` 문자열이다. Core 클래스 이름과 달라도 된다.
- 영역마다 파일 하나(`QuestBuilders`, `StudentLifeBuilders`, …). 모든 영역을 부르는 목록은 한 곳에만 둔다.
- 필드는 만들기 함수가 손으로 읽는다. 리플렉션으로 자동 연결하지 않는다. Core 생성자가 값을 다듬는 곳(`Math.Max(1, requiredCount)` 등)을 그대로 거치게 하고, 이름이 다른 필드를 눈에 보이게 하기 위해서다.
- 노드 읽기 도우미: `Str`, `Int`, `Float`, `Bool`, `RefId`(가리키는 id만), `RefIds`. 문맥: `One<T>(n, 필드)`, `All<T>(n, 필드)`. `ref`가 아니라 엔티티 안에 바로 적힌 전략(`{ "type": ..., 필드들 }`)도 같은 만들기 함수로 만든다. JSON이 원본이 된 뒤 전략을 엔티티 안에 넣기로 한 방향에 맞춘 것이다.
- **새 종류를 더하는 일 = Core 클래스 하나 + `b.Add` 한 줄.** 엔티티 id로 분기하지 않는다.

### 4.3 빠진 것을 숨기지 않는다

- `data/`에 있는 모든 `type`은 셋 중 하나여야 한다: (1) 만들기 함수가 등록됨, (2) "규칙이 아님" 목록에 이유와 함께 있음(그림, 타일, 화면 전용 등), (3) "아직 안 옮김" 목록에 있음. 어디에도 없는 종류가 있으면 테스트가 실패한다. 영역을 옮길 때마다 (3)이 줄어드는 것이 진행 상황이다.
- 오류는 처음 하나에서 멈추지 않고 모아서 낸다. 오류마다 엔티티 id와 `asset` 경로를 붙인다. 오류 종류: 없는 필드, 형이 다른 값, 없는 참조, 기대한 종류가 아닌 참조, 끝나지 않는 참조, Core 생성자가 던진 예외.
- Host는 시작할 때 오류가 하나라도 있으면 목록을 보여 주고 멈춘다. 일부만 읽힌 채로 게임을 시작하지 않는다.
- 열거형은 지금 숫자로 나와 있다. 만들기 함수가 Core 열거형으로 바꾸고, 범위를 벗어난 숫자는 오류로 낸다. Core 열거형의 숫자가 Unity 원본과 같은지는 종류마다 테스트 하나로 고정한다(원본 소스의 순서를 테스트에 적는다).

### 4.4 성능

엔티티 2,289개를 시작할 때 한 번 읽는다. 실제 `data/` 전체를 읽고 만드는 시간을 테스트에서 재어 보고서에 적는다. 1초를 넘으면 원인을 찾는다. 게임 중에는 `GameDefinitions`의 표 조회만 하고 다시 읽지 않는다.

## 5. 세이브 저장소

### 5.1 Core 쪽: 인터페이스 하나

```csharp
namespace Rootborn.Core.Save
{
    public interface ISaveStore
    {
        bool TryRead<T>(string saveSlot, string fileName, out T data) where T : class, new();
        void Write<T>(string saveSlot, string fileName, T data) where T : class;
        void Delete(string saveSlot, string fileName);
    }
}
```

- 영역별 저장소 인터페이스(`IQuestChainSaveStorage`, `IEncyclopediaSaveStore`, `IWorldStateSaveStore`)를 이것으로 바꾼다. 영역 코드는 어느 파일에 무엇을 쓰는지만 안다.
- 파일 이름은 Core의 `SaveFileNames` 한 곳에서 만든다(플레이어 id 정리 포함). 도감 파일은 Unity 판이 id를 정리하지 않고 썼으므로, 새 이름으로 쓰되 읽을 때는 옛 이름도 찾는다.
- 테스트용 메모리 저장소(`InMemorySaveStore`)를 Core 테스트에 둔다.

### 5.2 Data 쪽: 파일 저장소

- `FileSaveStore(rootDirectory)`: `<root>/<슬롯>/<파일>`. Unity 판과 같은 배치다. 저장 폴더는 Host가 인자로 넘긴다(코드에 경로를 적지 않는다).
- `System.Text.Json`, 공개 필드 포함, 이름 그대로, 들여쓰기. 쓰기는 임시 파일에 쓴 뒤 바꿔치기해서 쓰다 꺼져도 파일이 깨지지 않게 한다.
- **옛 파일 읽기 규칙:** 파일에 없는 필드는 클래스의 기본값을 그대로 둔다. 문자열 `null`과 `""`를 같게 본다. `JsonUtility`가 빈 객체를 기본값으로 채워 쓴 "빈 껍데기"는 Core 세이브 모양이 이미 다루는 대로 둔다. 모양에 없는 필드는 읽을 때 버린다.
- 새로 쓴 파일이 Unity 판이 쓴 것과 글자까지 같을 필요는 없다. 필요한 것은 옛 파일을 읽는 것과, 읽어서 다시 쓴 것을 다시 읽으면 같은 것이다.

### 5.3 형식 버전

- 파일마다 맨 위 모양에 `Version`(정수)을 둔다. 이미 있는 모양은 그 필드를 그대로 쓰고, 없는 모양에 더한다. 파일에 없으면 0으로 읽히고, 0은 "Unity 판이 쓴 파일"이다.
- 읽을 때 파일의 버전이 프로그램이 아는 것보다 높으면 읽기를 거절하고 파일을 덮어쓰지 않는다. 새 버전의 세이브를 옛 프로그램이 망가뜨리지 않게 하기 위해서다.
- 옛 버전을 새 모양으로 바꾸는 단계는 **처음 필요해질 때** 만든다. 지금은 필드만 두고 바꾸는 장치는 만들지 않는다.

### 5.4 고정 파일과 테스트

- QA 세이브를 `game/tests/Rootborn.Data.Tests/Fixtures/saves/<출처>/`로 복사해 커밋한다. 출처 폴더 이름과 날짜를 README 한 줄로 남긴다. 사용자 세이브는 쓰지 않는다.
- 파일마다:
  1. Core 세이브 모양으로 읽힌다.
  2. 진행 객체에 넣었다가 다시 세이브 모양으로 꺼내 쓴 뒤 다시 읽으면 같다.
  3. 파일에 있는 필드 중 모양에 없는 것이 없다. 있으면 실패한다(데이터를 조용히 잃지 않기 위해서). 의도해서 버리는 필드는 이유와 함께 목록에 적는다.
- 아직 Core에 모양이 없는 파일(`metadata.json`, `world-progress.json`, `milestone-progress.json` 등)은 "아직 안 옮김" 목록에 둔다. 4.3과 같은 방식으로, 목록에 없는 파일이 고정 폴더에 있으면 실패한다.

## 6. 만드는 순서

묶음마다 테스트와 함께 커밋한다. 실패 테스트 먼저.

1. `game/Rootborn.Core` 프로젝트와 `Rootborn.Data` 뼈대. 엔티티 파일 읽기와 `(type, id)` 표, 오류 모으기.
2. 등록 방식과 문맥(`DefinitionBuilders`, 참조 풀기, 끝나지 않는 참조 검출). 가짜 종류 둘로 테스트.
3. main에 있는 영역의 만들기 함수: 아이템·인벤토리, 퀘스트·체인, 학생 생활 중심부, 일과, 진로, 미니게임, 성인 직업. "모든 종류가 세 목록 중 하나에 있다"와 "실제 `data/` 전체가 오류 없이 만들어진다" 테스트.
4. `ISaveStore`, `SaveFileNames`, `FileSaveStore`. `QuestChainProgressStore`를 새 인터페이스로 바꾼다.
5. 고정 파일 복사와 5.4의 테스트. 버전 필드와 "더 높은 버전 거절".
6. 다른 브랜치가 합쳐진 뒤: 월드 상태·도감·하루 이벤트·캠페인 등의 만들기 함수와 저장소 교체. 그때마다 "아직 안 옮김" 목록에서 뺀다.

1~5는 세션 하나에 맡길 수 있다. 고칠 수 있는 폴더는 `game/Rootborn.Core`, `game/Rootborn.Data`, `game/tests/Rootborn.Data.Tests`, 그리고 Core의 `Save` 폴더와 `Quests/QuestChainProgressStore.cs`다. 규칙 세션(core-student-b, core-world)과 겹치는 파일은 `QuestChainProgressStore.cs`의 저장소 인터페이스 부분뿐이다.

## 7. 검증

- `dotnet test game/Rootborn.sln`과 `dotnet test Tools/Rootborn.Core.Tests`. 모두 "자동 테스트"다.
- 실제 `data/` 전체 읽기 테스트는 `data/`를 고치지 않는다. 풀지 못한 참조 12건은 규칙 종류에 걸리지 않아야 하며, 걸리면 오류로 드러난다.
- 화면을 거친 확인은 이 프로젝트에 없다. Host가 실제 데이터와 세이브로 시작하는 확인은 화면을 옮기는 프로젝트에서 한다.

## 8. 하지 않는 것

- `data/`의 JSON 모양을 바꾸지 않는다. 변환기도 고치지 않는다(열거형을 이름으로 내는 것 포함). JSON이 원본이 되는 때에 다시 정한다.
- JSON Schema, 검증 명령(`Rootborn.Data.Validate`), 데이터 편집 화면은 만들지 않는다. 검증은 테스트가 한다.
- 그림 좌표표, 현지화, LDtk 읽기는 다루지 않는다(Host·월드 프로젝트 몫).
- 세이브 버전을 바꾸는 장치, 세이브 암호화·압축·클라우드는 만들지 않는다.
- 세션들이 남긴 임시 인터페이스 중 저장소와 파일 이름 외의 것(알바, 장소 활동, 세계 상태 조회 등)은 규칙 세션이 끝난 뒤 조정 담당이 따로 잇는다.
- 게임 중 데이터 다시 읽기(핫 리로드)는 만들지 않는다.

## 9. 실패로 보는 경우

- `data/`에 있는 규칙 종류가 등록도 목록도 없이 조용히 무시되는 경우.
- 옛 세이브 파일의 필드를 알리지 않고 버리는 경우. 옛 세이브를 못 읽게 되는 경우.
- 만들기 함수나 로더가 엔티티 id로 분기하는 경우.
- Core가 JSON 라이브러리나 파일 시스템을 참조하게 되는 경우. Data가 MonoGame을 참조하는 경우.
- 오류가 있는데 일부만 읽힌 채로 시작하는 경우.

## 10. 작성자가 정한 것 (사용자가 뒤집을 수 있다)

1. 필드는 리플렉션이 아니라 손으로 읽는다. 종류마다 몇 줄이 더 들지만, 이름이 다른 필드와 빠진 필드가 컴파일과 테스트에서 드러난다.
2. 열거형은 숫자 그대로 읽는다. 변환기를 고쳐 이름으로 내는 것은 JSON이 원본이 될 때로 미룬다.
3. 새로 쓴 세이브는 Unity 판 파일과 글자까지 같지 않다. MonoGame 판이 쓴 세이브를 Unity 판으로 되돌려 읽는 것은 목표가 아니다.
4. 세이브 모양에 없는 필드는 읽을 때 버린다. 대신 고정 파일 테스트가 그런 필드를 찾아 실패시킨다.
5. 고정 파일은 2026-11-05 이전의 QA 세이브다. 그 뒤에 생긴 필드(예: 진로 후보, 예약된 미니게임)가 든 Unity 판 세이브는 없다. 필요하면 태그 `unity-playable-20261111` 빌드로 QA 세이브를 한 번 더 만들어야 하는데, Unity를 돌리는 일이라 사용자 결정이 필요하다.
6. 도감 세이브는 새 이름(플레이어 id 정리)으로 쓰고 옛 이름도 읽는다.

## 11. 확인하지 못한 것

- `data/`의 모든 종류가 `(type, id)`로 겹치지 않는지는 변환 보고서의 "중복 id 0"만 보았다. 로더의 첫 테스트가 직접 확인한다.
- `JsonUtility`가 쓴 파일의 모든 특이점(숫자 표기, 특수문자 이스케이프)을 `System.Text.Json`이 읽는지는 고정 파일 테스트를 돌려 봐야 안다.
- core-world, core-student-b 브랜치의 세이브 모양은 읽지 않았다. 6단계에서 본다.
