# 장래희망과 알바 게임 반영 계획

**목표:** 게임에서 마을 비밀 줄거리를 빼고, 장래희망을 고른 뒤 원하는 알바로 특성을 올리는 흐름으로 바꾼다.

**기준 문서:** `docs/story/rootborn-city-life-story-direction.md` 6~8절, `docs/story/rootborn-career-growth-design.md` 2절. 서고 가구 `Archive30`은 일반 도서관 가구로 유지한다(사용자 결정).

## 작업 제약 (사용자 지시, 2026-11-04)

- **Unity를 실행하지 않는다.** 에디터, batchmode, MCP 모두 금지다. 사용자가 다시 말할 때까지 유지한다.
- 그래서 Unity 테스트, 메뉴 빌더, 씬 수정, 화면 검증은 2단계로 미룬다. 테스트는 작성만 하고 "미실행"으로 보고한다.
- 헌법상 **새 `.cs`를 `Assets/`에 디스크로 쓰지 않는다.** 새 스크립트는 `output/dream-job-20261104/staging/`에 두고 2단계에서 등록한다. 기존 `.cs` 수정은 디스크로 해도 된다.
- 이 체크아웃의 쓰기 담당은 구현 에이전트 1개다. git 쓰기는 하지 않는다. 기존 미커밋 변경을 되돌리지 않는다.
- 엔티티 ID 분기 금지, 보상 멱등성, 자유 경로 등 `AGENTS.md`를 따른다.

## 1단계: Unity 없이 (지금)

### 1. 메인 체인과 Day 3
- `Assets/Data/StudentLife/MainStory/Steps/MainQuest_DustySecrets.asset`을 MainQuest_MyDream으로 바꾼다. 파일과 `.meta`를 함께 옮겨 GUID를 유지한다. 체인 순서는 `BackToSchool, AfterSchoolTour, FirstPartTimeJob, MyDream, MyOwnSpace`다.
- 활동 2개도 GUID를 유지해 용도를 바꾼다.
  - `LocationActivity_InspectArchiveDoor` → 도서관에서 직업 책 읽기
  - `LocationActivity_ReadRepairLedger` → 공방에서 일하는 모습 보기
  - 관련 Objective, `Campaigns/Route_Archive`, `Route_Ledger`, `Objective_Archive`, `Objective_Ledger`, `Day_3`도 맞춘다. 학교 밖 완료 경로를 유지한다.
- MyDream의 목표는 1단계에서 위 두 활동 중 하나로 둔다. 장래희망 선택을 요구하는 목표는 2단계다.
- **옛 세이브:** `QuestChainCore.cs:239`는 저장된 `CurrentStepId`를 그대로 쓴다. `mainquest.DustySecrets`에 멈춘 세이브가 막히지 않게 데이터 기반으로 처리하고 회귀 테스트를 쓴다. 활동 로그와 World State의 옛 ID도 확인한다.
- `Assets/Scripts/Editor/StudentLife/MainStoryOpeningDataBuilder.cs`를 같은 내용으로 고친다. 다시 실행해도 옛 줄거리가 돌아오면 안 된다.

### 2. 문구
- `Assets/Localization/GameTextCatalog.asset`의 **기존 행은 고치지 않는다.** 앞 593행은 `ExplorationTextLocalizationTests`가 해시로 고정한다. 새 키를 파일 끝에 추가하고 에셋이 새 키를 보게 한다.
- 그 테스트는 593행 뒤에 `ui.wardrobe.*`만 허용한다. 새 키 접두사를 허용하도록 고치되 해시 검사는 그대로 둔다.
- 옛 키가 안 쓰이게 되면 남겨 두고 목록만 보고한다.
- 플레이어에게 보이는 곳에서 숨겨진 과거, 비밀, 단서 추적을 암시하는 문구를 찾아 바꾼다. 후보는 다음과 같다.
  - `opening.*`, `opening.dialogue.*`
  - `life.library.archive-*`, `life.location-state.library.*`, `life.location-state.record.hidden`
  - `world.library.archive-open.*`, `world.workshop.repair-ledger-open.*`
  - `ui.encyclopedia.entry.town-archive.*`, `daily.route.library.archive-research`, `daily.route.workshop.repair-ledger`
  - `Assets/Data/DiscoveryClues`의 LibraryArchive 계열
- 평범한 "도서관 기록실", 일반 조사 UI 문구는 그대로 둔다.
- 한국어와 영어를 모두 넣는다. 게임 안에서 아이의 말투에 맞게 쓴다.

### 3. 장래희망
- `Assets/Data/Careers/Interests`에 네 직업 Interest를 추가한다(Chef, EmergencyCare, Interior, Soldier). `Assets/Data/Careers/Facilities/<직업>/Candidate_*.asset`을 연결하고, 기존 Interest의 Requirement, Rule, Reward 구성을 따른다.
- `AfterSchoolTour`의 소개 활동으로 후보가 열려 선택 가능한지 코드로 확인한다.
- 기존 4개(`learning, technical, service, investigation`)는 레지스트리에서 뺀다. 저장된 옛 ID가 안전한지 `CareerInterestCore.cs`, `CareerInterestPanel.cs`를 읽어 확인하고, 안전하지 않으면 남긴 뒤 이유를 보고한다.
- 레지스트리는 `Assets/Data/Registry/GameDataRegistry.asset`과 `Assets/Resources/GameDataRegistry.asset` 둘 다 고친다.
- `HarvestedWheatComfyAssetTests`의 레지스트리 고정 해시가 깨지는지 확인한다. **`output/` 증거는 고치지 않는다.**

### 4. 알바
- 현재 알바는 `CornerStoreShift`, `WorkshopHelper` 2개뿐이다. 진로마다 하나씩 4개를 추가해 `_partTimeWorks`, `_workplaces`에 등록하고 `FirstPartTimeJob` 목표의 `_acceptedWorks`에도 넣는다.
  - 식당 보조: 미각 또는 섬세함
  - 진료소 보조: 구호
  - 스튜디오 보조: 미적 감각
  - 훈련장 정리: 규율 또는 체력
- 특성은 기존 `Assets/Data/StudentLife/CareerPractices/Traits`와 `Facilities/*/Effect_Trait_*.asset`을 재사용한다. 미니게임은 `Assets/Data/StudentLife/Minigames`의 기존 자산을 재사용한다. 보상은 기존 알바와 같은 규모로 맞추되, 수치 근거가 없으면 기존 값을 쓰고 보고한다.
- 소개 활동이 같은 시설에 있어도 충돌하지 않는지 확인한다.
- Addressables 등록이 필요한지 `OpeningStoryAddressableTests`를 읽고, 필요한 항목을 2단계 목록에 적는다.

### 5. 문서와 테스트
- `MainStoryOpeningAssetTests`와 Archive를 쓰는 테스트를 새 내용에 맞춘다. 고칠 때 테스트의 검사 범위를 줄이지 않는다.
- 새 동작마다 테스트를 쓴다. 예: 체인 순서, 비밀 문구 부재, 네 알바가 서로 다른 특성을 올림, 네 장래희망이 후보와 연결됨, 옛 세이브 복원.
- 에셋은 `.meta`와 함께 만들고 GUID가 겹치지 않게 한다. `output/dream-job-20261104/verify_refs.py`를 만들어, 바꾼 YAML의 guid가 모두 실제 `.meta`로 해석되는지 검사하고 결과를 남긴다.
- Unity 없이 컴파일 확인이 가능하면 시도한다(기존 `.csproj`로 `dotnet build`). 안 되면 안 됐다고 적는다.

## 2단계: 사용자가 Unity를 허용한 뒤

- 새 목표 `CareerInterestSelectedQuestObjective`와 `QuestEventKind` 추가. `output/dream-job-20261104/staging/`의 스크립트를 `script-update-or-create`로 등록하고 MyDream을 장래희망 선택 목표로 바꾼다.
- 알바 4곳을 `Assets/Scenes/Town.unity`에 배치한다. 현재 `PartTimeWorkInteractor`는 씬에 2개다.
- Addressables 등록과 번들 재빌드.
- EditMode 전체, PlayMode, 실제 플레이 흐름, Game View 화면 검증. 이 전까지 완료로 보고하지 않는다.

## 보고

`output/dream-job-20261104/report.md`에 한글로 쓴다. 바꾼 파일, 내린 판단과 그 이유, 남긴 옛 키, 미실행 테스트, 2단계 목록을 적는다. 끝에 `DONE` 한 줄을 쓴다.
