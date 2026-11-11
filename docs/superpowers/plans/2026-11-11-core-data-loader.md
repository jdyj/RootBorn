# 데이터 로더와 세이브 저장소 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `data/`의 JSON으로 `Rootborn.Core` 규칙 객체를 만들고, Unity 판이 쓴 세이브 파일을 읽고 쓰는 `Rootborn.Data`를 만든다.

**Architecture:** `game/Rootborn.Data`가 JSON을 `(type, id)` 표로 읽고, `type` 문자열마다 등록된 만들기 함수가 Core 생성자를 불러 정의 객체를 만든다. 세이브는 Core의 `ISaveStore` 하나 뒤에 두고 Data의 `FileSaveStore`가 `System.Text.Json`으로 구현한다. Core는 JSON도 파일도 모른다.

**Tech Stack:** .NET 8, C#(Core는 LangVersion 9), `System.Text.Json`(프레임워크 내장, 패키지 추가 없음), NUnit 3.14.0.

**Spec:** `docs/superpowers/specs/2026-11-11-core-data-loader-design.md`. 함께 읽을 것: `.claude/rules/engine-migration.md`, `.claude/rules/commit-conventions.md`.

## Global Constraints

- Core(`Packages/com.rootborn.core/Runtime`)는 `UnityEngine`, `System.Text.Json`, `System.IO`를 참조하지 않는다. LangVersion 9로 컴파일돼야 한다. 새 `.cs`와 새 폴더에는 `.meta`를 함께 만든다(기존 파일의 `.meta`를 본떠 guid만 새로).
- `Rootborn.Data`는 MonoGame을 참조하지 않는다. Core만 참조한다.
- 엔티티 id로 분기하지 않는다(`if (id == "...")`, `switch (id)` 금지). 분기는 `type` 문자열 등록으로만 한다.
- `data/`와 `Tools/Rootborn.DataExport`, `Assets/`는 고치지 않는다.
- 세이브 모양의 기존 필드 이름을 바꾸지 않는다.
- 고정 파일은 `output/`의 QA 세이브만 쓴다. 사용자 세이브 폴더(`AppData/LocalLow`)를 읽지 않는다.
- 저장 폴더 경로를 코드에 적지 않는다. 생성자 인자로 받는다.
- dotnet SDK는 `~/.dotnet`에 있다. 명령 앞에 `export PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet"`.
- 테스트 명령: `dotnet test game/Rootborn.sln`, `dotnet test Tools/Rootborn.Core.Tests`. 작업마다 둘 다 통과해야 커밋한다. 통과는 "자동 테스트"로만 보고한다.
- 커밋: 한글, prefix(`[FEATURE]`, `[REFACTOR]`, `[TEST]`), 자기 브랜치에만. 푸시와 main 병합은 하지 않는다. Unity 판과 달라지는 동작이 생기면 커밋 메시지에 적는다.
- 고칠 수 있는 곳: `game/Rootborn.Core/`, `game/Rootborn.Data/`, `game/tests/Rootborn.Data.Tests/`, `game/Rootborn.sln`, `Packages/com.rootborn.core/Runtime/Save/`, `Packages/com.rootborn.core/Runtime/Quests/QuestChainProgressStore.cs`, `Packages/com.rootborn.core/Runtime/Careers/AdultCareerSnapshotSaveData.cs`, 세이브 모양 클래스에 `Version` 필드 추가, `Tools/Rootborn.Core.Tests/`의 관련 테스트.

## Review Focus

1. **JSON 파일 하나가 깨져 있거나 배열이 아닌 경우:** 예외로 죽지 않고 그 파일 경로가 든 오류 하나로 모이고, 나머지 파일은 계속 읽힌다. (작업 1)
2. **같은 `(type, id)`가 두 파일에 있는 경우:** 뒤의 것이 조용히 덮지 않고 두 `asset` 경로가 든 오류가 난다. (작업 1)
3. **세이브 파일이 비었거나 잘린 JSON인 경우:** `TryRead`가 false를 돌려주지 않고 예외(`SaveFileCorruptException`, 경로 포함)를 던진다. 없는 파일만 false다. 깨진 파일을 "세이브 없음"으로 보고 새 게임으로 덮어쓰는 것을 막는다. (작업 4)
4. **슬롯이나 파일 이름에 `..`, 경로 구분자가 든 경우:** 저장 폴더 밖을 읽거나 쓰지 않고 `ArgumentException`. (작업 4)
5. **필드에 `null`이 든 정의 JSON**(예: `"timePolicy": null`): 참조가 없는 것으로 읽고(`RefId` → 빈 문자열, `One<T>` → null) 죽지 않는다. (작업 2)

---

### Task 1: 프로젝트 뼈대와 엔티티 파일 읽기

**Files:**
- Create: `game/Rootborn.Core/Rootborn.Core.csproj` — `net8.0`, `LangVersion 9.0`, `Nullable disable`, `EnableDefaultCompileItems false`, `<Compile Include="../../Packages/com.rootborn.core/Runtime/**/*.cs" />`. 코드 파일 없음.
- Create: `game/Rootborn.Data/Rootborn.Data.csproj` — `net8.0`, `Nullable disable`, Core 프로젝트 참조.
- Create: `game/Rootborn.Data/LoadError.cs`, `game/Rootborn.Data/EntityNode.cs`, `game/Rootborn.Data/EntityTable.cs`
- Create: `game/tests/Rootborn.Data.Tests/Rootborn.Data.Tests.csproj` — `Rootborn.Ui.Tests.csproj`와 같은 패키지 버전, Data 참조.
- Create: `game/tests/Rootborn.Data.Tests/EntityTableTests.cs`, `game/tests/Rootborn.Data.Tests/RepoPaths.cs`
- Modify: `game/Rootborn.sln` — 세 프로젝트 추가(`dotnet sln add`).

**Interfaces:**
- Produces:
  - `sealed class LoadError { string Kind; string Type; string Id; string Source; string Message; }` — `Source`는 엔티티의 `asset` 값, 없으면 JSON 파일 경로. `Kind` 값: `"parse"`, `"duplicate"`, `"missing-field"`, `"wrong-type"`, `"missing-ref"`, `"wrong-ref-type"`, `"cycle"`, `"unknown-type"`, `"enum-range"`, `"constructor"`.
  - `sealed class EntityNode { string Type; string Id; string Asset; JsonElement Json; }`
  - `sealed class EntityTable { static EntityTable ReadDirectory(string dataDirectory, List<LoadError> errors); bool TryGet(string type, string id, out EntityNode node); IEnumerable<EntityNode> All; IReadOnlyCollection<string> Types; IReadOnlyList<string> RegistryIds(string registryKey); }`
  - `static class RepoPaths { static string DataDirectory; static string Root; }` — 테스트 실행 폴더에서 위로 올라가며 `data/registry.json`이 있는 폴더를 찾는다.

엔티티 파일은 `data/<폴더>/<이름>.json` 중 **맨 위가 배열이고 원소가 `type`과 `id`를 가진 것**이다. `data/registry.json`, `data/export-report.json`, `data/atlases/`, `data/localization/`, `data/tiles/`는 엔티티 파일이 아니므로 건너뛴다(폴더 이름으로 고정하지 말고, 배열이 아니거나 원소에 `type`·`id`가 없는 파일은 엔티티 파일이 아닌 것으로 판정한다. 단 JSON 문법이 깨진 파일은 `parse` 오류다).

- [ ] **Step 1: 실패 테스트** (`EntityTableTests.cs`, 임시 폴더에 작은 JSON을 써서)

```csharp
[Test] public void ReadsEntitiesByTypeAndId()          // 두 파일, 세 엔티티 → TryGet 성공, All.Count()==3, Asset 값 일치
[Test] public void BrokenFile_IsOneParseError_AndOthersStillLoad()   // errors.Single().Kind=="parse", Source가 그 파일 경로로 끝남
[Test] public void SameTypeAndIdTwice_IsADuplicateError()            // Kind=="duplicate", Message에 두 asset 경로 모두 포함
[Test] public void NonEntityJson_IsSkippedWithoutError()             // 객체가 맨 위인 파일, 문자열 배열 파일 → 오류 0
[Test] public void RealData_HasNoParseOrDuplicateErrors()            // RepoPaths.DataDirectory → errors 비어 있음, All.Count()==2289
[Test] public void RegistryIds_ReadTheRealRegistryInOrder()          // RegistryIds("quests")[0]=="quest.gatherWood", RegistryIds("없는키") 는 빈 목록
```

- [ ] **Step 2:** `dotnet test game/Rootborn.sln --filter EntityTableTests` → 컴파일 실패 확인.
- [ ] **Step 3:** 위 타입 구현. `JsonDocument`는 표가 살아 있는 동안 유지한다(`JsonElement.Clone()`으로 떼어 내 문서를 닫아도 된다).
- [ ] **Step 4:** 테스트 통과 확인. `RealData_...`의 2289가 다르면 `data/export-report.json`의 `summary.entities`와 비교해 차이의 원인을 찾고, 숫자를 고치기 전에 보고에 적는다.
- [ ] **Step 5:** 커밋 `[FEATURE] Rootborn.Data 뼈대 — 엔티티 JSON을 (type, id) 표로 읽기`

---

### Task 2: 종류 등록과 정의 만들기

**Files:**
- Create: `game/Rootborn.Data/NodeReader.cs`, `game/Rootborn.Data/DefinitionBuilders.cs`, `game/Rootborn.Data/BuildContext.cs`, `game/Rootborn.Data/GameDefinitions.cs`, `game/Rootborn.Data/GameDataLoader.cs`
- Test: `game/tests/Rootborn.Data.Tests/DefinitionBuildTests.cs`

**Interfaces:**
- Consumes: Task 1의 `EntityTable`, `EntityNode`, `LoadError`.
- Produces:
  - `readonly struct NodeReader` — `string Id; string Type; string Str(string field); int Int(string field); float Float(string field); bool Bool(string field); string RefId(string field); string[] RefIds(string field); TEnum Enum<TEnum>(string field) where TEnum : struct, Enum; bool Has(string field); IEnumerable<NodeReader> Items(string field);`
    - 없는 필드: `missing-field` 오류를 남기고 기본값. `null` 값: 오류 없이 기본값(`RefId` → `""`).
    - `Enum<T>`: 숫자를 `T`로 바꾸고 `Enum.IsDefined`가 아니면 `enum-range` 오류.
  - `sealed class DefinitionBuilders { void Add<T>(string jsonType, Func<NodeReader, BuildContext, T> build) where T : class; void NotARule(string jsonType, string reason); void NotPortedYet(string jsonType, string waitingFor); }` — 같은 `jsonType`을 두 번 넣으면 `ArgumentException`.
  - `sealed class BuildContext { T One<T>(NodeReader node, string field) where T : class; List<T> All<T>(NodeReader node, string field) where T : class; T ById<T>(string jsonType, string id) where T : class; }`
    - 필드 값이 `{ "ref", "type" }`이면 표에서 찾아 만들고, `ref` 없이 `type`만 있는 객체면 그 자리에서 만든다(id 없음, 기억하지 않음).
    - 한 번 만든 `(type, id)`는 같은 객체를 돌려준다. 만드는 중인 것을 다시 요청하면 `cycle` 오류와 null.
    - 만든 객체가 `T`가 아니면 `wrong-ref-type`. 표에 없으면 `missing-ref`. 등록되지 않은 종류면 `unknown-type`. 만들기 함수가 던진 예외는 `constructor` 오류(메시지 포함)로 바꾼다.
  - `sealed class GameDefinitions { T Get<T>(string id) where T : class; bool TryGet<T>(string id, out T definition) where T : class; IReadOnlyList<T> All<T>() where T : class; }` — `T`로 받을 수 있는 모든 객체가 대상(기반 클래스·인터페이스로도 조회). `Get`은 없으면 `KeyNotFoundException`(id와 `T` 이름 포함). `All<T>()` 순서: 그 종류가 `registry.json`의 목록에 있으면 그 순서, 없으면 `(파일 경로, 배열 순서)`.
  - `static class GameDataLoader { static LoadResult Load(string dataDirectory, Action<DefinitionBuilders> register); }`, `sealed class LoadResult { GameDefinitions Definitions; IReadOnlyList<LoadError> Errors; IReadOnlyList<string> UnclassifiedTypes; bool Ok; }` — `Ok`는 `Errors`와 `UnclassifiedTypes`가 모두 비었을 때만 true. `UnclassifiedTypes`는 표에 있지만 `Add`/`NotARule`/`NotPortedYet` 어디에도 없는 종류.
  - `sealed class GameDataException : Exception` — `LoadResult.ThrowIfFailed()`가 던진다. 메시지에 오류 수와 처음 20건.

- [ ] **Step 1: 실패 테스트** (임시 폴더, 테스트 안에서만 쓰는 가짜 클래스 `Leaf(string id, int power)`, `Branch(string id, IReadOnlyList<Leaf> leaves, string friendId)`)

```csharp
[Test] public void BuildsAnEntityAndItsReferences()            // Branch.Leaves 2개, 값 일치
[Test] public void SameReference_IsBuiltOnce()                 // 두 Branch가 같은 Leaf를 가리키면 ReferenceEquals
[Test] public void InlineStrategy_IsBuiltInPlace()             // "leaves": [{ "type":"Leaf", "power": 3 }] → Power==3
[Test] public void MissingField_IsAnError_WithTheAssetPath()   // Kind=="missing-field", Source==asset
[Test] public void NullField_IsNoReference_NotAnError()        // "friend": null → RefId=="" , 오류 0
[Test] public void MissingReference_IsAnError()                // Kind=="missing-ref"
[Test] public void ReferenceOfTheWrongKind_IsAnError()         // Leaf 자리에 Branch → "wrong-ref-type"
[Test] public void ReferencesThatNeverEnd_AreACycleError()     // A→B→A → "cycle", 무한 재귀 없음
[Test] public void ConstructorThrowing_IsCollected_NotThrown() // "constructor"
[Test] public void EnumOutOfRange_IsAnError()                  // "enum-range"
[Test] public void TypeWithNoBuilderAndNoListing_IsUnclassified() // UnclassifiedTypes == ["Mystery"], Ok==false
[Test] public void NotARule_And_NotPortedYet_AreClassified()   // Ok==true, 그 종류는 Definitions에 없음
[Test] public void All_FollowsRegistryOrder()                  // registry.json 순서가 파일 순서와 다를 때 registry 순서
[Test] public void Get_UnknownId_Throws_WithIdAndTypeName()
[Test] public void AddingTheSameTypeTwice_Throws()
```

- [ ] **Step 2:** 실패 확인. **Step 3:** 구현. 모든 엔티티를 등록된 종류에 대해 빠짐없이 만든다(참조로 닿지 않는 것도). **Step 4:** 통과 확인.
- [ ] **Step 5:** 커밋 `[FEATURE] 정의 로더 — 종류 등록, 참조 풀기, 빠진 종류와 오류 모으기`

---

### Task 3: main에 있는 영역의 만들기 함수

**Files:**
- Create: `game/Rootborn.Data/Builders/InventoryBuilders.cs`, `QuestBuilders.cs`, `StudentLifeBuilders.cs`, `DailyPlanBuilders.cs`, `CareerBuilders.cs`, `MinigameBuilders.cs`, `AdultCareerBuilders.cs`, `NotRules.cs`, `NotPortedYet.cs`, `RootbornBuilders.cs`
- Test: `game/tests/Rootborn.Data.Tests/RealDataTests.cs`, `game/tests/Rootborn.Data.Tests/EnumOrderTests.cs`

**Interfaces:**
- Consumes: Task 2 전부.
- Produces: `static class RootbornBuilders { static void Register(DefinitionBuilders b); }` — 영역 파일의 `Register`를 모두 부르는 유일한 곳. Host와 테스트는 `GameDataLoader.Load(dir, RootbornBuilders.Register)`만 쓴다.

**종류를 나누는 방법.** `data/export-report.json`의 `typeCounts`에 있는 모든 종류에 대해:
1. `Packages/com.rootborn.core/Runtime`에 대응하는 Core 클래스가 있으면 `Add`. 대응은 Core 테스트 파일 머리 주석과 원본 `Assets/Scripts/Game/**/<type>.cs`를 보고 찾는다. 이름이 다를 수 있다(예: `TraitDeltaActivityEffect` → `Rootborn.Core.StudentLife.TraitDeltaEffect`).
2. 그림·타일·화면·맵·캐릭터 겉모습 전용이면 `NotARule(type, 이유)`.
3. 규칙인데 Core에 아직 없으면 `NotPortedYet(type, 기다리는 영역)`. 영역 이름은 `core-student-b`(하루 이벤트, 캠페인, 장소, 알바, 탐색, 발견, 마일스톤), `core-world`(월드 상태, 도감, 발견 단서, 대화, NPC 일정), `legacy`(Family, Knowledge, Tools, Resources, Recipes 등 지금 게임이 쓰는지 확인되지 않은 것), `status`.

**필드 대응.** JSON 필드 이름은 원본 Unity 클래스의 직렬화 필드에서 `_`를 뗀 것이다. Core 생성자 인자와의 대응은 원본 클래스가 그 필드를 어떻게 쓰는지를 보고 정한다. Core 생성자가 id 문자열을 받으면 `RefId`, 객체를 받으면 `One`/`All`. Core 생성자에 받는 곳이 없는 JSON 필드(아이콘, 그림, 화면 문구 배치 등)는 읽지 않는다. 받는 곳이 없는데 규칙에 영향을 주는 필드로 보이면 읽지 말고 보고에 적는다.

- [ ] **Step 1: 실패 테스트**

```csharp
// RealDataTests.cs — [OneTimeSetUp]에서 GameDataLoader.Load(RepoPaths.DataDirectory, RootbornBuilders.Register) 한 번
[Test] public void EveryTypeInData_IsClassified()        // result.UnclassifiedTypes 비어 있음 (실패 메시지에 목록)
[Test] public void RealData_BuildsWithoutErrors()        // result.Errors 비어 있음 (실패 메시지에 처음 20건)
[Test] public void Quests_MatchTheRegistry()             // All<QuestDefinition>()의 id 순서 == registry.json "quests"
[Test] public void AQuest_CarriesItsObjectives()         // Get<QuestDefinition>("quest.career.chef"): 목표 1개, TalkQuestObjective, TargetNpcId=="npc.first-guide", RequiredCount==1
[Test] public void AnActivity_CarriesItsCostsAndEffects()// Get<LifeActivity…>("practice.chef"): 시간 45분, 에너지 1, 집중 1, 효과 개수 == JSON의 effects 길이
[Test] public void LoadingRealData_TakesUnderOneSecond() // Stopwatch, 두 번째 호출 기준. 측정값을 TestContext.WriteLine
```

`practice.chef`의 Core 클래스 이름과 속성 이름은 Core 소스에서 확인해 쓴다. 테스트 데이터가 엔티티 id를 쓰는 것은 허용된다.

```csharp
// EnumOrderTests.cs — 만들기 함수가 Enum<T>로 읽는 Core 열거형마다 하나
[Test] public void LifeActivityCategory_HasTheUnityOrder() // Enum.GetNames(typeof(...)) == 원본 Assets 소스의 선언 순서를 그대로 적은 배열
```

- [ ] **Step 2:** 실패 확인. **Step 3:** 영역 파일 하나씩 구현하고 그때마다 `RealData_BuildsWithoutErrors`의 오류 목록이 줄어드는지 본다. **Step 4:** 전부 통과 확인.
- [ ] **Step 5:** 영역별로 커밋(예: `[FEATURE] 퀘스트·인벤토리 정의 만들기 함수`). 마지막 커밋 메시지에 `Add`/`NotARule`/`NotPortedYet` 종류 수와 측정한 읽기 시간을 적는다.

---

### Task 4: 저장소 인터페이스와 파일 저장소

**Files:**
- Create: `Packages/com.rootborn.core/Runtime/Save/ISaveStore.cs`, `SaveFileNames.cs`, `SaveVersion.cs`(+ `.meta`, 폴더 `.meta`)
- Modify: `Packages/com.rootborn.core/Runtime/Quests/QuestChainProgressStore.cs` — `IQuestChainSaveStorage`를 지우고 `ISaveStore`를 받는다. `FileNameFor`는 `SaveFileNames.QuestChain(playerId)`로 옮기고 결과 문자열은 그대로.
- Create: `Tools/Rootborn.Core.Tests/InMemorySaveStore.cs`, `Tools/Rootborn.Core.Tests/SaveFileNamesTests.cs`; Modify: `QuestChainProgressStore`를 쓰는 기존 테스트(가짜 저장소를 `InMemorySaveStore`로).
- Create: `game/Rootborn.Data/Save/FileSaveStore.cs`, `SaveFileCorruptException.cs`, `SaveFileTooNewException.cs`
- Test: `game/tests/Rootborn.Data.Tests/FileSaveStoreTests.cs`

**Interfaces:**
- Produces (Core, 네임스페이스 `Rootborn.Core.Save`):

```csharp
public interface ISaveStore
{
    bool TryRead<T>(string saveSlot, string fileName, out T data) where T : class, new();
    void Write<T>(string saveSlot, string fileName, T data) where T : class;
    void Delete(string saveSlot, string fileName);
}
public interface IVersionedSave { int Version { get; set; } }   // 세이브 모양이 구현. 필드 이름은 "Version" 그대로여야 하므로 공개 필드 + 명시적 인터페이스 구현
public static class SaveVersion { public const int Unity = 0; public const int Current = 1; }
public static class SaveFileNames
{
    public static string QuestChain(string playerId);   // 지금의 QuestChainProgressStore.FileNameFor와 같은 결과
    public const string QuestLog = "quest-log.json";
    public static string AdultCareerSnapshot(string playerId); // "adult-career-snapshot-<hex>.json": 원본 Assets/Scripts/Game/Careers/AdultCareerSnapshotPersistence.cs의 규칙 그대로
}
```

  - 이미 `Version` 필드가 있는 모양(`AdultCareerProgressSaveData`, `DailyPlanProgressSaveData`, 착용 세이브)은 값과 뜻을 바꾸지 않는다. `IVersionedSave`는 **파일 맨 위 모양**에만 붙인다: `QuestLogSaveData`, `QuestChainLogSaveData`(새 필드 `public int Version;`), Task 5의 `AdultCareerSnapshotSaveData`(이미 있는 `Version`, 지금 값 2). 파일마다 "아는 최고 버전"은 그 모양의 상수로 둔다(`QuestLogSaveData.KnownVersion = 1` 등, 스냅샷은 2).
- Produces (Data): `sealed class FileSaveStore : ISaveStore { FileSaveStore(string rootDirectory); }`
  - 경로 `<root>/<saveSlot>/<fileName>`. 슬롯·파일 이름에 `/`, `\`, `..`, `:`이 있거나 비어 있으면 `ArgumentException`.
  - 옵션: `IncludeFields = true`, `WriteIndented = true`, 이름 정책 없음, `PropertyNameCaseInsensitive = false`, `NumberHandling` 기본값, `Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping`(한글을 그대로 쓴다).
  - 읽기: 파일 없음 → false. 비었거나 문법이 깨짐 → `SaveFileCorruptException`(경로). `T`가 `IVersionedSave`이고 파일의 `Version`이 그 모양의 아는 최고 버전보다 높음 → `SaveFileTooNewException`(경로, 파일 버전, 아는 버전).
  - 쓰기: `IVersionedSave`면 쓰기 전에 `Version`이 0이면 아는 최고 버전으로 올린다. 같은 폴더의 임시 파일에 쓰고 `File.Move(temp, target, overwrite: true)`. 폴더가 없으면 만든다.

- [ ] **Step 1: 실패 테스트**

```csharp
// SaveFileNamesTests.cs (Core)
[Test] public void QuestChain_DefaultPlayer_UsesTheSharedName()   // "quest-chain-progress.json"
[Test] public void QuestChain_OtherPlayer_MatchesTheOldRule()     // 기존 FileNameFor 테스트의 기대값을 그대로 옮김
[Test] public void AdultCareerSnapshot_LocalPlayer()              // "adult-career-snapshot-006c006f00630061006c002d0070006c0061007900650072.json" (playerId "local-player")

// FileSaveStoreTests.cs (Data, 임시 폴더)
[Test] public void WriteThenRead_GivesTheSameFields()
[Test] public void MissingFile_IsFalse()
[Test] public void EmptyOrTruncatedFile_Throws_Corrupt_AndIsNotOverwritten() // 예외 뒤 파일 내용 그대로
[Test] public void FieldsMissingInTheFile_KeepTheirDefaults()     // CurrentDay 없는 파일 → 1
[Test] public void UnknownFieldsInTheFile_AreIgnored()
[Test] public void NewerVersion_Throws_TooNew()                   // Version 99
[Test] public void FileWithoutVersion_ReadsAsZero_AndIsWrittenAsKnownVersion()
[Test] public void SlotOrNameThatLeavesTheRoot_Throws()           // "..", "a/b", "a\\b", "c:x", ""
[Test] public void Write_LeavesNoTempFileBehind()
[Test] public void Korean_IsWrittenAsIs()                         // 파일 글자에 "\u" 없음
[Test] public void Delete_RemovesTheFile_AndMissingIsFine()
```

- [ ] **Step 2:** 실패 확인. **Step 3:** 구현, `QuestChainProgressStore` 교체. **Step 4:** 두 테스트 명령 모두 통과 확인(Core 테스트 수가 줄지 않았는지 본다).
- [ ] **Step 5:** 커밋 둘: `[REFACTOR] 세이브 저장소 인터페이스를 ISaveStore 하나로 — 퀘스트 체인 저장소 교체`, `[FEATURE] FileSaveStore — 옛 파일 읽기, 깨진 파일과 더 높은 버전 거절, 안전한 쓰기`

---

### Task 5: 옛 세이브 고정 파일 테스트

**Files:**
- Create: `game/tests/Rootborn.Data.Tests/Fixtures/saves/README.md` — 출처 폴더, 날짜, "QA 세이브이며 사용자 세이브가 아님".
- Create (복사): `game/tests/Rootborn.Data.Tests/Fixtures/saves/exe-20261105/slot-0/*.json` ← `output/dream-job-20261104/playthrough-20261105/saves-exe/slot-0/`; `.../editor-20261105/slot-0/*.json` ← `output/dream-job-20261104/playthrough-20261105/saves/slot-0/`. csproj에서 `CopyToOutputDirectory`.
- Create: `Packages/com.rootborn.core/Runtime/Careers/AdultCareerSnapshotSaveData.cs`(+ `.meta`) — 원본 `Assets/Scripts/Game/Careers/AdultCareerSnapshot.cs`의 세이브 모양을 옮긴다. 필드: `Version`, `Adult`(`AdultCareerProgressSaveData`), `Student`(`StudentLifeProgressSaveData`), `Inventory`(`PlayerInventorySaveData`), `Quest`(`QuestLogSaveData`), `Chains`(`QuestChainLogSaveData`). `World`, `Furniture`, `Unlocks`는 Core에 모양이 없으므로 넣지 않고 아래 목록에 적는다. 원본의 필드 형이 이와 다르면 원본을 따른다.
- Test: `game/tests/Rootborn.Data.Tests/OldSaveTests.cs`, `game/tests/Rootborn.Data.Tests/SaveShapeCoverage.cs`

**Interfaces:**
- Consumes: Task 4의 `FileSaveStore`, `SaveFileNames`.
- Produces: `static class SaveShapeCoverage` — 파일 이름 무늬 → 세이브 모양 `Type` 표, "아직 모양 없음" 파일 목록(이유 포함), "모양에 없는 필드" 허용 목록(파일 무늬, JSON 경로, 이유).
  - 모양 있음: `quest-log.json` → `QuestLogSaveData`, `adult-career-snapshot-*.json` → `AdultCareerSnapshotSaveData`.
  - 아직 모양 없음: `metadata.json`(슬롯 정보, Host 몫), `world-progress.json`(월드 프로젝트), `campaign-progress.json`·`location-state-progress.json`·`milestone-progress.json`(core-student-b 합친 뒤), `student-day-result-quest-inventory-summary.json`(화면 요약), `career-interest-progress.json`·`house-furniture-layout-stage-0.json`·`objective-journal-tracking.json`(고정 폴더에 있으면).
  - 허용 목록 초기값: 스냅샷의 `World`, `Furniture`, `Unlocks`("Core에 모양 없음, 월드·가구 영역을 옮길 때 뺀다").

- [ ] **Step 1: 실패 테스트** (`OldSaveTests.cs`, 고정 폴더의 모든 `.json`에 대해 `[TestCaseSource]`)

```csharp
[TestCaseSource(nameof(AllFixtureFiles))]
public void EveryFixtureFile_IsEitherShapedOrListed(string path)      // 표에도 "아직 모양 없음"에도 없으면 실패
[TestCaseSource(nameof(ShapedFixtureFiles))]
public void OldFile_Reads(string path)                                 // TryRead true, 예외 없음
[TestCaseSource(nameof(ShapedFixtureFiles))]
public void OldFile_HasNoFieldTheShapeDrops(string path)               // JSON의 모든 경로(배열은 원소 형으로 내려감)가 모양의 공개 필드에 있거나 허용 목록에 있음
[TestCaseSource(nameof(ShapedFixtureFiles))]
public void ReadWriteRead_IsTheSame(string path)                       // 읽기 → 임시 폴더에 쓰기 → 다시 읽기 → 두 객체를 JSON으로 직렬화한 문자열이 같음
[Test] public void Snapshot_StudentSurvivesTheProgressObject()         // exe 스냅샷의 Student → StudentLifeProgress에 싣기 → ToSaveData → Energy 11, Focus 9, CurrentDay 2, TimeMinutes 510 그대로이고 Traits·Skills 길이도 같음
[Test] public void QuestLog_SurvivesTheLogObject()                     // quest-log.json → QuestLog에 싣기 → ToSaveData → 퀘스트 id 목록과 상태가 같음
```

  `StudentLifeProgress`, `QuestLog`에 싣는 방법(`LoadFromSaveData` 등)은 Core 소스에서 확인한다. 정의 조회가 필요하면 Task 3의 실제 `GameDefinitions`를 쓴다.

- [ ] **Step 2:** 실패 확인. **Step 3:** 모양·표·복사 구현.
- [ ] **Step 4:** 통과 확인. `OldFile_HasNoFieldTheShapeDrops`가 허용 목록에 없는 필드를 찾으면: 원본 Unity 세이브 모양에 있는 필드면 Core 모양에 더하고(이름 그대로), 그 필드를 쓰는 규칙이 아직 없으면 허용 목록에 이유와 함께 적는다. 어느 쪽인지 판단이 서지 않으면 허용 목록에 넣고 보고에 적는다.
- [ ] **Step 5:** 커밋 `[TEST] Unity 판 QA 세이브 고정 파일 — 읽기, 다시 쓰기, 버려지는 필드 검사`

---

### Task 6: 마무리

**Files:**
- Modify: `game/README.md` — `Rootborn.Core`, `Rootborn.Data` 프로젝트 두 줄과 테스트 명령.
- Modify: `.claude/skills/port-rule-to-core/SKILL.md`와 `.agents/skills/port-rule-to-core/SKILL.md`(같은 내용) — 규칙을 옮긴 뒤 할 일 두 줄 추가: "`game/Rootborn.Data/Builders`에 만들기 함수를 등록하고 `NotPortedYet`에서 뺀다", "세이브 파일이 있으면 `ISaveStore`와 `SaveFileNames`를 쓰고 `SaveShapeCoverage`에 모양을 등록한다".

- [ ] **Step 1:** 두 문서 수정. 이 두 스킬 파일은 고칠 수 있는 곳의 예외로 허용한다.
- [ ] **Step 2:** `dotnet test game/Rootborn.sln`, `dotnet test Tools/Rootborn.Core.Tests` 전체 통과 확인.
- [ ] **Step 3:** 참조 검사: `grep -rn "System.Text.Json\|System.IO\|UnityEngine" Packages/com.rootborn.core/Runtime` → 0건, `grep -n "MonoGame" game/Rootborn.Data/*.csproj game/Rootborn.Core/*.csproj` → 0건. 엔티티 id 분기 검사: `grep -rnE '(Id|id) == "' game/Rootborn.Data --include=*.cs` → 0건.
- [ ] **Step 4:** 커밋 `[DOCS] 로더·세이브 저장소 사용법 — game/README와 port-rule-to-core 스킬`
- [ ] **Step 5:** 보고(한국어): (1) 만든 것과 테스트 수, (2) `Add`/`NotARule`/`NotPortedYet` 종류 수와 `NotPortedYet` 목록, (3) 읽지 않고 남긴 JSON 필드 중 규칙에 영향을 줄 것 같은 것, (4) 세이브 허용 목록에 넣은 필드, (5) Unity 판과 달라진 동작, (6) 확인하지 못한 것. 같은 요약을 `orca worktree set --worktree active --comment "..." --json`으로 남긴다.

## 이 계획에 없는 것 (설계 6절 6단계)

core-student-b, core-world가 main에 합쳐진 뒤 조정 담당이 따로 맡긴다: 그 영역의 만들기 함수, `IEncyclopediaSaveStore`·`IWorldStateSaveStore`를 `ISaveStore`로 교체, 도감 옛 파일 이름 읽기, `campaign-progress.json` 등의 모양 등록.
