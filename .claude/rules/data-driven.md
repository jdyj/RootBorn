# 데이터 드리븐 — 엔티티는 데이터, 종류는 클래스

게임 엔티티(활동, 선택지, 특성, 기술, 진로, 알바, 관계, 장소, 퀘스트, 캠페인, 마일스톤, 아이템, 가구, NPC, 맵 지점 등)는 모두 데이터로 정의한다. 코드는 특정 엔티티를 모른다.
왜: 엔티티를 더하거나 바꿀 때 코드를 건드리지 않아야 회귀가 없고, 기획이 바뀌어도 시스템과 테스트가 그대로 산다.

## 흐름

```
data/<영역>/<종류>.json  →  Rootborn.Data 만들기 함수(b.Add 한 줄)  →  Core 정의 객체  →  GameDefinitions
```

- 파일 하나가 엔티티 배열이다. 엔티티마다 `type`, `id`와 필드가 있고, 다른 엔티티는 `{ "ref": "<id>", "type": "<종류>" }`로 가리킨다. `data/registry.json`이 등록 순서다.
- 만들기 함수는 `game/Rootborn.Data/Builders/<영역>Builders.cs`에 둔다. 모든 영역을 부르는 곳은 `RootbornBuilders.Register` 하나다.
- 필드는 만들기 함수가 손으로 읽는다(리플렉션 자동 연결 없음). 왜: Core 생성자의 값 다듬기를 그대로 거치고, 이름이 다른 필드가 보이게.
- 게임은 시작할 때 한 번 읽고 `GameDefinitions.Get/TryGet/All<T>()` 표 조회만 한다. 읽기 오류가 하나라도 있으면 목록을 보이고 시작하지 않는다.
- `data/`의 모든 `type`은 등록됐거나 `NotRules`(규칙이 아님)·`NotUsedByGame`(게임이 안 씀)·`NotPortedYet`(아직 안 옮김)에 이유와 함께 있어야 한다. 아니면 `RealDataTests`가 실패한다. 왜: 빠진 종류가 조용히 버려지지 않게.

데이터 원본이 어디인지(규칙 데이터 `data/`, 규칙 설정 `game/economy.json`·`game/time.json`, 문구 `game/localization`, 맵 `game/world`, 테마 `game/themes` — 모두 손으로 고친다)는 `rules/engine-migration.md` "지금 상태".

## 금지

- 엔티티 id로 분기: `if (activityId == "…")`, `switch (questId)`, `x.Id.Equals("…")`, `is "…"`. 맵 id·NPC id도 같다.
- 엔티티 id를 `enum`으로 두기(`enum CareerId { … }`).
- 엔티티마다 클래스 만들기(`class ChefCareer`).
- CI `check-no-entity-id-branching.sh`가 Core 런타임과 `game/`(tests 제외)에서 막는다. 테스트는 실제 데이터의 id로 확인해도 된다.

## 새 것을 더하는 법

- **새 엔티티** = JSON 한 덩어리. 코드는 고치지 않는다.
- **새 전략 종류**(조건, 효과, 목표, 보상, 결과) = Core 클래스 하나 + 만들기 함수 `b.Add` 한 줄. 종류를 `enum`·`switch`로 닫지 않는다.
- 엔티티 하나가 전략 여러 개를 배열로 가질 수 있다. 평가 순서는 배열 순서다.
- 화면을 여는 상호작용, 수첩의 모으는 쪽, 미니게임 도전 그리기도 같다: 종류 문자열이나 클래스 하나 + 등록 한 줄.

## 보상과 인벤토리

퀘스트, 캠페인, 마일스톤, 탐색 결과, 활동 보상처럼 인벤토리나 진행을 바꾸는 모든 지급에 적용한다. 왜: 아이템 복사·삭제와 이중 지급은 되돌리기 어렵다.

- 지급 **전에** 모든 보상이 들어가는지와 이미 받았는지를 확인한다.
- 하나라도 안 되면 **아무것도 바꾸지 않는다.** 일부만 주고 받은 것으로 기록하지 않는다. 주고 나서 실패하면 되돌리는 흐름을 쓰지 않는다.
- 받은 상태(받음, 요청 id)를 세이브에 남겨, 같은 요청을 다시 하거나 저장·다시 읽기 뒤에 해도 두 번 주지 않는다. 버튼을 끄는 것만으로 막지 않는다.
- 이 원칙에 맞추려고 Unity 판과 동작을 바꾼 곳은 `docs/superpowers/specs/2026-11-11-core-rules-extraction-design.md` "Unity 판과 달라진 동작"에 있다.

## 세이브

- Core 영역은 `ISaveStore`(`TryRead`/`Write`/`Delete`)만 알고, 파일 이름은 `SaveFileNames` 한 곳에서 만든다. 파일 쓰기는 `game/Rootborn.Data`의 `FileSaveStore`.
- 세이브 모양 클래스의 필드 이름과 형은 바꾸지 않는다. 형식이 바뀌면 `Version`을 올린다. 더 높은 버전의 파일도 읽고 플레이한다: 모르는 필드는 저장소가 쓸 때 그대로 다시 쓴다(목록 기록·사전 안은 못 지킨다). 읽지 않는 것은 파일이 `ReadableFrom`으로 더 새 프로그램을 요구할 때뿐이고, 그 파일은 덮어쓰지 않는다.
- 새 세이브 모양은 `game/tests/Rootborn.Data.Tests/SaveShapeCoverage.cs`에 등록한다. 게임 한 판이 쓴 파일이 등록에서 빠지면 Game `SavedFilesAreRegisteredTests`가 실패한다.
- 기존 세이브는 계속 읽혀야 한다. 확인은 테스트 고정 파일로 한다(`game/tests/Rootborn.Data.Tests/Fixtures/saves/`). 사용자 세이브 폴더는 쓰지 않는다. **있는 고정 폴더는 고치지 않는다** — 형식을 바꾼 뒤에 새 날짜 폴더를 더한다(그 폴더의 `README.md`).
- 게임 진행을 바꾸는 새 세이브 파일은 `GameSession.Save()`의 묶음 안에서 쓴다(`SaveHouseLayoutIfDirty(store)`처럼). 따로 쓰면 도중에 꺼졌을 때 "받음" 기록과 받은 것이 어긋난다.
- 세이브에 있는데 지금 데이터에 없는 id(아이템, 퀘스트, 체인, 가구)의 기록은 버리지 않는다. 규칙에는 보이지 않게 들고 있다가 그대로 다시 쓴다. 왜: 데이터를 고칠 때마다 옛 세이브의 진행이 조용히 사라지지 않게.
- 파일에 숫자로 쓰이는 enum은 값을 명시하고 바꾸지 않는다(새 값은 끝에, Core `SavedEnumValuesTests`). 다른 모양 안에 든 모양의 `ReadableFrom`도 읽을 때 검사된다.
- 세이브에 닿는 것을 고칠 때는 스킬 `save-compat-change`의 순서를 따른다. 하위호환과 상위호환(새 세이브가 옛 빌드에서도 플레이된다)은 예외 없이 지킨다(사용자 지시 2026-11-12, 11-13).
- 스냅샷과 늘 함께 쓰는 영역 파일이 빠지면 시작을 거절한다(`GameSession.Load`의 `Require`). 늘 쓰는 파일을 새로 더해도 그 목록에는 "그 파일을 쓰기 시작한 뒤의 세이브"를 가릴 근거가 있을 때만 넣는다 — 아니면 옛 세이브가 안 열린다.

## 그림 좌표

테마 `rect`, 좌표표(`data/atlases/*.atlas.json`), LDtk 좌표는 모두 **PNG 왼쪽 위 기준 픽셀**이고 PNG를 그대로 읽는다. 칸 크기와 좌표는 PNG를 직접 재서 정한다 — 파일 이름이나 16px 격자로 추정하지 않는다. 자세한 것은 `game/themes/README.md`, `game/world/README.md`.
