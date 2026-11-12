# data/ — 게임 데이터

게임에 나오는 것(활동, 퀘스트, 가구, 장소, NPC, 아이템, 진로, 대화 등)은 모두 이 폴더의 JSON으로 정한다.
게임 코드는 특정 엔티티를 모른다. 새 활동·퀘스트·가구를 더하는 일은 JSON을 고치는 일이다.

이 문서는 MonoGame 판(`game/`)이 **지금 실제로 읽는 것**만 적는다. 규칙의 원칙은 `.claude/rules/data-driven.md`.

## 먼저 알아 둘 것: 이 폴더가 원본이다

- 이 폴더의 JSON이 게임 데이터의 원본이다. **사람이 직접 고친다.**
- 처음 내용은 Unity 에셋(`Assets/Data`)을 변환기로 바꾼 결과다. 변환기와 Unity 에셋은 Unity를 지울 때(2026-11-12) 지웠다
  — 태그 `unity-playable-20261111`에 있다. 그래서 엔티티의 `asset` 필드(옛 Unity 경로)는 이제 없는 파일을 가리킨다(아래 "엔티티의 모양").
- 문구는 `data/localization/<언어>.json`(옛 문구 표)과 `game/localization/<언어>.json`(MonoGame 판에서 더한 문구) 두 곳이다.
  게임이 두 폴더를 합쳐 읽고, 같은 키는 `game/localization`이 이긴다. 새 문구는 `game/localization`에 넣는다.
- 변환기를 다시 돌리는 단계는 없다. 고친 JSON이 그대로 다음 실행의 데이터다(빌드·변환 없이 시작할 때 읽는다).
- 수치(활동 시간 `timeCostMinutes`, 체력·집중 비용 등)를 바꿀 때는 균형 문서 `docs/superpowers/specs/2026-11-12-time-balance.md`의 원칙과 표를 함께 본다.
  균형 검사(`game/tests/Rootborn.Game.Tests/DayBalanceTests`)의 목표·가정은 `game/tests/Rootborn.Game.Tests/day-balance.json`(게임은 읽지 않는다).
  수치를 바꾸는 것은 기획 결정이라 사용자가 정한다.
- data 폴더 옆의 `game/economy.json`(통화 아이템)과 `game/time.json`(하루 시작·취침 시각·밤 체력 감소)도 규칙이다. 손으로 고치고, 게임이 읽는 값만 둔다.
- 같이 하기의 데이터 지문(`game/Rootborn.Data/DataFingerprint`)은 등록부·엔티티 파일·`game/economy.json`·`game/time.json`을 셈한다. 이 중 한 글자만
  바꿔도 바꾸기 전 배포와는 같은 방에 들어가지 못한다(문구·좌표표는 셈하지 않는다).

## 폴더와 파일

| 무엇 | 어디 | 게임이 읽는 방법 |
|---|---|---|
| 엔티티 | `data/<영역>/<종류>.json` | 맨 위가 배열이고 원소마다 `type`과 `id`가 있는 JSON은 모두 엔티티 파일이다 |
| 등록부 | `data/registry.json` | 목록마다 id의 순서, 기본값(첫 id) |
| 문구 표 | `data/localization/<언어>.json` (+ `game/localization/<언어>.json`) | 키 → 문구 |
| 그림 좌표표 | `data/atlases/<PNG 경로>.atlas.json` | PNG 안의 칸 이름 → 사각형 |

- 폴더 이름(`studentlife`, `quests`, `interiors` …)은 사람이 찾기 쉽게 나눈 것이다. 게임은 `data/` 아래 모든 `*.json`을 읽으므로 폴더가 달라도 된다.
- 파일 이름은 그 안의 `type`과 같게 한다(`QuestDefinition.json` 안에는 `"type": "QuestDefinition"`만). 같은 종류가 두 폴더에 있어도 된다
  (예: `TraitDeltaActivityEffect.json`이 `careers/`와 `studentlife/`에 있다). 몇몇 테스트가 파일 이름으로 종류를 찾는다.
- 파일 안 순서는 의미가 있다: 등록부에 없는 엔티티는 (파일 경로, 배열 순서)대로 놓인다.
- 문법이 깨진 파일은 오류 하나로 모이고 게임은 시작하지 않는다.

## 엔티티의 모양

```json
{
  "type": "TalkQuestObjective",
  "id": "Objective_CareerTalk_Chef",
  "asset": "Assets/Data/Quests/CareerRoutes/Objectives/Objective_CareerTalk_Chef.asset",
  "displayKey": "",
  "requiredCount": 1,
  "targetNpc": { "ref": "npc.first-guide", "type": "NpcDefinition" }
}
```

- **`type`**: 종류. 게임 코드에 이 종류의 만들기 함수가 등록돼 있어야 한다(아래 "새 종류를 더할 때").
- **`id`**: 이름. 같은 `type` 안에서 겹치면 안 된다(`duplicate` 오류). 다른 종류와는 같은 id를 써도 되지만(예: 아이템과 도구 `StoneAxe`)
  헷갈리므로 새 엔티티는 `quest.…`, `furniture.…`처럼 영역을 앞에 붙인다.
- **`asset`**: 옛 Unity 원본 경로(그 파일은 이제 없다). 오류 메시지에 출처로만 쓴다. **새 엔티티는 적지 않는다**(없으면 오류 위치가 JSON 파일 경로가 된다).
- **다른 엔티티 가리키기**: `{ "ref": "<id>", "type": "<종류>" }`. 가리키지 않으면 `null`. 배열이면 `[ {…}, {…} ]`.
  다른 엔티티 안에만 쓰이는 작은 전략(효과·조건 등)은 `ref` 없이 `{ "type": "…", 필드… }`로 그 자리에 바로 적을 수도 있다.
- **필드는 빠짐없이**: 만들기 함수가 읽는 필드가 파일에 없으면 `missing-field` 오류다(옛 에셋 때문에 일부만 생략을 허락한다).
  새 엔티티는 **같은 종류의 다른 엔티티를 복사해** 값을 고친다.
- **숫자로 적힌 선택지**: `"category": 0`처럼 숫자인 것은 Core의 열거형 순서다(예: `LifeActivityCategory`). 정의되지 않은 숫자는 `enum-range` 오류.
  Unity가 숫자 배열을 16진 문자열로 적은 곳(`"badges": "0200000004000000"`)도 있다. 새로 쓸 때는 숫자 배열 `[2, 4]`도 읽는다.
- **문구는 키로**: `displayNameKey`, `descriptionKey`처럼 이름이 `Key`로 끝나는 필드(그리고 `lineKeys` 같은 `Keys` 배열)에는 문구가 아니라
  문구 표의 키를 적는다. `iconKey`는 문구가 아니라 UI 테마의 아이콘 이름이다.
- **그림**: `{ "sprite": "<PNG 경로>", "name": "<칸 이름>" }`. PNG는 저장소 뿌리 기준 경로이고, 칸은 `data/atlases/<PNG 경로>.atlas.json`의
  `sprites`에 있어야 한다(아래 "그림 좌표표").

## 등록부 `registry.json`

`{ "목록 이름": [ id, … ], "기본값 이름": "id" }`. 게임은 **등록부에 없어도 data/의 모든 엔티티를 만든다.** 등록부는

- 종류별 목록의 **순서**를 정하고(`GameDefinitions.All<T>()`은 그 종류의 id를 가장 많이 가진 목록의 순서를 따른다),
- **기본값**을 정하고(`studentLifeCalendar`, `defaultDayEndRule`, `defaultTutorialStage`, `defaultStudentCampaign` 등의 첫 id),
- 몇몇 화면이 **목록 그대로** 쓴다(`careerInterests`, `studentConditionStatuses`).

그래서 새 엔티티는 같은 종류가 들어 있는 목록의 알맞은 자리에 id를 더한다. 다른 엔티티 안에서만 쓰이는 전략(효과·조건·목표 등)도
같은 종류가 목록에 있으면(`questObjectives` 등) 함께 더한다. 등록부의 id는 모두 data/에 있어야 한다(검사가 본다).

## 예 1: 활동 하나 더하기

`data/studentlife/LifeActivityDefinition.json` 배열 끝에 더한다(`practice.chef`에서 따와 줄였다).

```json
{
  "type": "LifeActivityDefinition",
  "id": "activity.night-reading",
  "displayNameKey": "activity.night-reading",
  "category": 0,
  "timePolicy": { "ref": "paid-activity", "type": "ActivityTimePolicyDefinition" },
  "timeCostMinutes": 45,
  "energyCost": 1,
  "focusCost": 1,
  "stressDelta": 0,
  "requirements": [],
  "effects": [ { "ref": "Effect_NightReading_Focus", "type": "TraitDeltaActivityEffect" } ],
  "choices": []
}
```

효과는 `data/studentlife/TraitDeltaActivityEffect.json`에 더한다.

```json
{ "type": "TraitDeltaActivityEffect", "id": "Effect_NightReading_Focus", "trait": { "ref": "trait.focus", "type": "TraitDefinition" }, "delta": 2 }
```

- 등록부: `"lifeActivities"` 목록에 `"activity.night-reading"`을 더한다.
- 문구: `game/localization/ko.json`과 `en.json`에 `"activity.night-reading": "…"`를 더한다(모든 언어에).
- 가리키는 `paid-activity`(시간 규칙)와 `trait.focus`(특성)는 이미 있는 엔티티다. 없는 id를 가리키면 검사가 실패한다.

## 예 2: 퀘스트 하나 더하기

`data/quests/QuestDefinition.json`에 퀘스트를, `data/quests/TalkQuestObjective.json`에 목표를 더한다(`quest.career.chef`에서 따왔다).

```json
{
  "type": "QuestDefinition",
  "id": "quest.visit-librarian",
  "displayNameKey": "quest.visit-librarian.name",
  "descriptionKey": "quest.visit-librarian.desc",
  "providerConditions": [],
  "prerequisites": [],
  "objectives": [ { "ref": "Objective_VisitLibrarian_Talk", "type": "TalkQuestObjective" } ],
  "rewards": [],
  "completionEffects": []
}
```

```json
{ "type": "TalkQuestObjective", "id": "Objective_VisitLibrarian_Talk", "displayKey": "", "requiredCount": 1,
  "targetNpc": { "ref": "npc.librarian", "type": "NpcDefinition" } }
```

- 등록부: `"quests"`에 `"quest.visit-librarian"`, `"questObjectives"`에 `"Objective_VisitLibrarian_Talk"`. 보상·완료 효과를 달면
  `"questRewards"`·`"questCompletionEffects"`에도.
- 문구: `quest.visit-librarian.name`, `quest.visit-librarian.desc`를 모든 언어에.
- 보상은 지급 전에 들어갈 수 있는지와 이미 받았는지를 Core가 확인한다(`.claude/rules/data-driven.md` "보상과 인벤토리"). 데이터는 무엇을 줄지만 적는다.

## 예 3: 가구 하나 더하기

`data/interiors/FurnitureDefinition.json`에 더한다(`story.study-desk`에서 따왔다).

```json
{
  "type": "FurnitureDefinition",
  "id": "furniture.reading-lamp",
  "stableId": "furniture.reading-lamp",
  "displayName": "Reading Lamp",
  "displayNameKey": "furniture.reading-lamp.name",
  "category": "lamp",
  "allowedSurfaceIds": [ "house" ],
  "tileParts": [
    { "localCell": { "x": 0, "y": 0 },
      "tile": { "sprite": "Assets/Art/CareerFurniture/Growth31/Growth31_assembly_desk_2x1.png", "name": "Growth31_assembly_desk_2x1" } }
  ],
  "footprintCells": [ { "x": 0, "y": 0 } ],
  "blocksMovement": 1,
  "placementPreference": 2,
  "unlockToken": "unlock.furniture.reading-lamp",
  "priceItem": { "ref": "item.allowance", "type": "ItemDefinition" },
  "price": 10,
  "purchaseLocationId": "location.corner-store"
}
```

- `stableId`는 `id`와 같게 둔다(게임 안의 가구 id는 `stableId`에서 온다). `allowedSurfaceIds`는 가구를 놓는 면 이름(`game/world/interactions.json`의 `placementSurface`, 집은 `house`).
- `footprintCells`는 차지하는 칸(규칙), `tileParts`는 그 칸에 그릴 그림(겉모습)이다. 그림을 바꿔도 칸·id·세이브는 그대로다.
- 그림 PNG가 저장소에 있고 좌표표에 그 칸이 있어야 한다. 새 PNG면 좌표표도 만든다(아래).
- 팔지 않는 가구는 `displayNameKey`, `priceItem`, `price`, `purchaseLocationId`를 빼도 된다(옛 가구 대부분이 그렇다. 예: `desk.basic`).
- 등록부: `"furnitureDefinitions"`에 id를 더한다. 문구: `furniture.reading-lamp.name`.

## 그림 좌표표

`data/atlases/<PNG 경로>.atlas.json` 하나가 PNG 하나다. 좌표는 **PNG 왼쪽 위 기준 픽셀**이다.

```json
{
  "image": "Assets/Art/CareerFacilities/Achievements/trophy_chef.png",
  "width": 64, "height": 64, "pixelsPerUnit": 48, "origin": "top-left",
  "sprites": { "trophy_chef": { "x": 0, "y": 0, "w": 64, "h": 64, "pivot": { "x": 0.5, "y": 0.5 } } }
}
```

- 파일 경로는 `data/atlases/` + `image` + `.atlas.json`이다. `width`·`height`는 PNG의 실제 크기와 같아야 하고 칸은 그림 안이어야 한다.
- 칸 크기와 좌표는 PNG를 직접 재서 정한다. 파일 이름이나 16px 격자로 짐작하지 않는다.

## 새 종류를 더할 때 (코드에서 할 일)

새 엔티티는 JSON만 더하면 되지만, **새 종류**(지금 없는 `type`, 예: 새 효과나 새 조건)는 코드가 필요하다.

1. Core(`game/Rootborn.Core/src`)에 그 종류의 클래스를 만든다. 엔진·파일·JSON 라이브러리를 참조하지 않는다.
2. `game/Rootborn.Data/Builders/<영역>Builders.cs`에 한 줄: `b.Add("<type>", (n, c) => new 클래스(n.Id, n.Int("delta"), c.One<…>(n, "trait")))`.
   - `n.Str/Int/Float/Bool/Enum`은 값, `c.One/All`은 다른 엔티티를 만들어 받음(없으면 `missing-ref` 오류), `n.RefId/RefIds`는 id 문자열만 받음.
   - 모든 영역을 부르는 곳은 `RootbornBuilders.Register` 하나다. 새 영역 파일을 만들면 거기에 한 줄 더한다.
3. 게임이 쓰지 않는 종류라면 만들기 함수 대신 이유를 적어 분류한다: `NotRules.cs`(규칙이 아님), `NotUsedByGame.cs`(게임이 안 씀).
   **분류되지 않은 `type`이 data/에 있으면 `RealDataTests`가 실패한다.**
4. 엔티티 id로 분기하지 않는다(`if (id == "…")` 금지, CI가 막는다). 테스트를 먼저 쓴다(`.claude/rules/testing-discipline.md`).

## 고친 뒤 돌릴 검사

```bash
export PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet"
dotnet test game/tests/Rootborn.Data.Tests          # data/ 전체 읽기 + 아래 온전성 검사
bash Scripts/ci/check-localization-keys.sh           # 모든 언어가 같은 키를 갖는지
dotnet test game/Rootborn.sln                        # 커밋 전에는 전부
```

`Rootborn.Data.Tests`가 보는 것:

| 테스트 | 보는 것 |
|---|---|
| `RealDataTests` | 모든 종류가 분류됐는지, 오류 없이 만들어지는지(필드 빠짐, 형, 없는 참조 `missing-ref`, id 겹침) |
| `Integrity/FileReferenceTests` | 그림 참조의 PNG·좌표표 칸·그림 크기, 좌표표 자체, UI 테마, 맵 목록의 LDtk·레벨·타일셋, 상호작용 지점의 정의 id |
| `Integrity/EntityReferenceTests` | 모든 `{ ref, type }`, `…Id`·`…Ids` 문자열 필드가 가리키는 엔티티, 등록부 id |
| `Integrity/LocalizationKeyTests` | `…Key`·`…Keys`의 키가 모든 언어에 있는지 |

지금 데이터에 있는 빈 곳은 고치지 않고 목록으로 고정해 두었다. 목록 밖의 새 빈 곳이 생기거나, 목록의 것이 고쳐져 사라지면 테스트가 실패한다.

- 그림·파일·참조: `game/tests/Rootborn.Data.Tests/Integrity/KnownDataGaps.cs`(무엇, 어디, 이유)
- 빠진 문구 키: `game/tests/Rootborn.Data.Tests/Fixtures/known-missing-localization-keys.json`(이유별)
- 새 `…Id` 필드는 `EntityReferenceTests`의 `IdStringFields`에 엔티티 id인지 이름인지 분류해야 통과한다.

검사가 실패하면 메시지에 파일, 엔티티(`종류/id`), 필드 경로가 나온다.
