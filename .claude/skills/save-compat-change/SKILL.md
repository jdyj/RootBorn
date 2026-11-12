---
name: save-compat-change
description: "ROOTBORN 세이브에 닿는 것을 고칠 때 하위호환(옛 세이브가 새 프로그램에서 열리고 진행이 남는다)과 상위호환(새 세이브를 옛 프로그램이 망가뜨리지 않는다)을 지키는 절차. 세이브 모양 클래스·필드·enum을 더하거나 바꿀 때, 새 세이브 파일을 더할 때, 세이브가 가리키는 data/ id(아이템·퀘스트·체인·가구·맵·도착 지점)를 지우거나 이름을 바꿀 때, maxStack·칸 수를 줄일 때, '세이브 형식 변경', '데이터 수정', '하위호환', '상위호환', '옛 세이브' 요청에 사용."
---

# 세이브에 닿는 것을 고칠 때 — 하위호환과 상위호환

사용자 지시(2026-11-12): 데이터를 더 고쳐야 할 때 하위호환과 상위호환은 **무조건** 되어야 한다. 예외를 두려면 먼저 사용자에게 묻는다.
규칙 원문은 `.claude/rules/data-driven.md` "세이브". 이 스킬은 그 규칙을 지키며 고치는 순서다.

## 두 약속

| 약속 | 뜻 | 깨지면 |
|---|---|---|
| 하위호환 | 옛 프로그램이 쓴 세이브가 새 프로그램에서 **열리고**, 다시 저장해도 **진행이 빠지지 않는다** | 업데이트 뒤 슬롯이 안 열리거나 진행이 조용히 사라진다 |
| 상위호환 | 새 프로그램이 쓴 세이브를 옛 프로그램이 만나면 **읽지 않고, 덮어쓰지 않고, 지우지 않는다**. 프로그램이 모르는 기록은 **그대로 들고 있다가 다시 쓴다** | 옛 빌드로 한 번 열었다고 새 진행이 지워진다 |

상위호환은 "옛 빌드로 새 세이브를 플레이한다"가 아니다. "옛 빌드가 새 세이브를 상하게 하지 않는다"다.

## 먼저: 이 변경이 세이브에 닿는가

하나라도 맞으면 아래 순서를 따른다.

- 세이브 모양 클래스(`*SaveData`, `IVersionedSave`)의 필드를 더하거나 바꾼다.
- 파일에 숫자로 쓰이는 enum을 고친다.
- 새 세이브 파일을 더한다(`SaveFileNames`).
- `data/`·`game/world`에서 세이브가 가리킬 수 있는 id를 지우거나 이름을 바꾼다(아이템, 퀘스트, 체인, 가구, 맵, 도착 지점).
- 저장된 양을 줄이는 수치를 고친다(`maxStack`, 가방 칸 수, 목록 길이 제한).

## 변경 종류마다 할 일

| 변경 | 하위호환 | 상위호환 |
|---|---|---|
| 필드 더하기 | 필드가 없는 옛 파일이 기본값으로 읽히고 그 기본값이 "옛 동작"과 같아야 한다 | 옛 프로그램은 모르는 필드를 읽을 때 버리고 다시 쓸 때 지운다. 잃으면 안 되는 필드(진행, 받음 기록, 돈)면 **`KnownVersion`을 올린다** — 옛 프로그램이 그 파일을 거절하게 |
| 필드 이름·형 바꾸기, 지우기 | **하지 않는다.** 옛 필드는 그대로 두고 새 필드를 더한 뒤, 읽을 때 옛 필드에서 새 필드를 채운다 | `KnownVersion`을 올린다 |
| 뜻이 바뀜(같은 필드, 다른 해석) | 읽을 때 `Version`으로 갈라 옛 값을 새 뜻으로 옮긴다 | `KnownVersion`을 올린다 |
| 숫자 enum | 있는 값의 숫자를 바꾸지 않는다. 새 값은 끝에, 숫자를 명시한다(Core `SavedEnumValuesTests`에 더한다) | 옛 프로그램이 모르는 값을 만나는 파일이면 `KnownVersion`을 올린다 |
| 다른 모양 안에 든 모양 | 위와 같다 | 안쪽 모양도 `IVersionedSave`로 두면 읽을 때 같이 검사된다(`FileSaveStore.RefuseNewer`) |
| 새 세이브 파일 | 그 파일이 없는 옛 슬롯이 열려야 한다. `GameSession.Load`의 `Require` 목록에는 "그 파일을 쓰기 시작한 뒤의 세이브"를 가릴 근거가 있을 때만 넣는다 | 모양을 `IVersionedSave`로 만든다. 진행을 바꾸는 파일은 `GameSession.Save()` 묶음 안에서 쓴다. `SaveShapeCoverage.cs`에 등록한다 |
| data id 지우기·이름 바꾸기 | 그 id의 기록을 버리지도, 슬롯을 거절하지도 않는다. 규칙에는 안 보이게 들고 있다가 그대로 다시 쓴다(`ItemInventory._kept`, `QuestLog`·`QuestChainLog`의 `KeepUnknown`, 가구의 풀리지 않은 기록). 새 종류의 id를 세이브에 넣으면 같은 방식으로 만든다 | 같은 장치가 "새 프로그램이 쓴 모르는 id"도 지킨다 |
| 맵·도착 지점 지우기 | 저장된 자리가 목록에 없으면 새 게임 자리에서 시작한다(`WorldSlotStart.MovedFrom`). 시작을 거절하지 않는다 | — |
| 양 줄이기(`maxStack`, 칸 수) | 넘친 만큼을 버리지 않고 보존한다(`ItemInventory.Keep`) | — |

`Version` 0은 "Unity 판이 쓴 파일"이다. 저장소는 0인 것만 쓸 때 `KnownVersion`으로 채운다(`FileSaveStore.Write`).

## 순서

1. **실패 테스트 먼저.** 옛 모양의 파일(고정 파일이나 테스트가 직접 쓴 JSON)을 새 코드로 읽어 열리는지, 읽고 다시 쓴 뒤 옛 필드와 기록이 남는지 본다. 상위 쪽은 `Version`이 `KnownVersion`보다 높은 파일을 주고 **읽기가 거절되고 파일이 바뀌지 않았는지** 본다(예: Data `FileSaveStoreTests`, Screens `SettingsStoreTests`, Core `SaveKeepsUnknownRecordsTests`).
2. 고친다. **있는 고정 폴더(`game/tests/Rootborn.Data.Tests/Fixtures/saves/*`)는 고치지 않는다.** 그 폴더를 읽는 테스트가 실패하면 고칠 것은 코드다.
3. 새 `GameSession`으로 다시 읽어 같은지 본다(저장 → 새 판 → 읽기).
4. `KnownVersion`을 올렸으면 새 날짜 고정 폴더를 **더한다**:
   ```bash
   ROOTBORN_FIXTURE_OUT=<저장소>/game/tests/Rootborn.Data.Tests/Fixtures/saves/monogame-<날짜> \
     dotnet test game/tests/Rootborn.Game.Tests --filter "Name~WriteCurrentSlotFixture"
   ```
   그리고 그 폴더의 `README.md` 표에 한 줄 더한다.
5. 돌린다: `Rootborn.Core.Tests`, `Rootborn.Data.Tests`(`OldSaveTests`, `SaveShapeRegistrationTests`), `Rootborn.Game.Tests`(`SavedFilesAreRegisteredTests`, `DeletedSaveFileTests`). 합치기 전에는 전부(`Scripts/remote-test/run.sh`).
6. Unity 판과 달라진 동작이면 `docs/superpowers/specs/2026-11-11-core-rules-extraction-design.md` 9절에 한 줄.

## 하지 않는다

- 읽을 수 없는 파일(깨짐, 더 새 버전)을 "없음"으로 보고 기본값으로 덮어쓰기. 읽기 실패 뒤에는 그 파일을 쓰지 않는다.
- 모르는 id·넘친 양을 조용히 버리기, 또는 그것 때문에 슬롯 전체를 거절하기.
- 옛 세이브가 안 읽힐 때 고정 파일이나 테스트 기대값을 고쳐 통과시키기.
- 테스트에서 사용자 세이브 폴더(`AppData/LocalLow`) 쓰기.
- 두 약속 중 하나를 포기해야만 되는 변경을 묻지 않고 진행하기 — 무엇을 잃는지 적어 사용자에게 묻는다.

## 보고

"하위호환 확인"과 "상위호환 확인"을 나눠 쓰고 각각 테스트 이름을 붙인다. 모두 자동 테스트다(화면 근거 아님). 확인하지 못한 쪽은 못 했다고 쓴다.
