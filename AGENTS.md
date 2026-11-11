# Repository Guidelines

ROOTBORN은 도시 생활 성장 시뮬레이션이다. 2026-11-11부터 Unity에서 MonoGame으로 옮겼고, Unity 판은 태그 `unity-playable-20261111`에 얼려 두었다. `Assets/`의 Unity 코드와 씬은 고치지 않는다(지울 예정). 최상위 규칙은 `.claude/constitution.md`, 자세한 규칙은 `.claude/rules/`에 있다. Claude와 Codex 모두 따른다.

## 절대 원칙

1. **엔티티는 데이터로, id로 분기하지 않는다.** 활동, 선택지, 특성, 기술, 진로, 관계, 장소, 퀘스트, 가구 같은 엔티티는 `data/` JSON으로 정의한다. `if (id == "X")`, `switch (id)`, 엔티티 id enum, 엔티티마다 C# 클래스를 두지 않는다. 새 종류는 클래스 하나와 등록 한 줄로 더한다. (`.claude/rules/data-driven.md`, CI `Scripts/ci/check-no-entity-id-branching.sh`)
2. **규칙은 `Rootborn.Core`에만.** Core는 엔진, 파일, JSON 라이브러리를 참조하지 않는다. 화면은 `GameSession`의 명령과 조회만 부르고, MonoGame 참조는 `Rootborn.Host`와 그 테스트에만 둔다. (`.claude/rules/coding-standards.md`)
3. **실패 테스트 먼저.** AI가 쓴 코드도 같다. 사용자 흐름(걷기, 문, 상호작용, 버튼, 이어 하기)은 입력으로 그 흐름을 지나는 테스트로 확인한다. 사용자가 "걸어가서", "버튼을 눌러서"라고 정한 길을 내부 호출이나 상태 강제 설정으로 대신하지 않는다. (`.claude/rules/testing-discipline.md`)
4. **"확인했다"는 실제 근거로만.** 보고할 때 자동 테스트, 입력 이벤트를 넣은 테스트(OS 입력 계층 미확인), 실제 입력, 직접 본 화면을 나눠 쓴다. 테스트 통과만으로 "화면에 보인다"고 하지 않는다. 보이는 것을 바꿨으면 스크린샷을 직접 열어 본 뒤에 끝났다고 한다. (`.claude/rules/core-logic-and-play-evidence.md`)
5. **보상은 한 번만, 전부 아니면 전무.** 보상을 주기 전에 받을 공간이 있는지와 이미 받았는지를 확인한다. 실패하면 인벤토리와 진행을 바꾸지 않는다. 저장하고 다시 읽은 뒤 다시 요청해도 한 번만 준다. (`.claude/rules/data-driven.md`)
6. **자유 경로 성장.** 학교 수업 하나만 정답인 구조로 만들지 않는다. 같은 핵심 성장 목표에는 가능하면 길을 두 개 이상 둔다. (`.claude/rules/game-design.md`)
7. **성능을 처음부터 같이 설계한다.** 긴 목록은 보이는 줄만 그리고, 매 프레임 상태를 다시 읽거나 계산하지 않는다. 완료 보고에 성능을 어디까지 확인했는지 적는다. (`.claude/rules/game-design.md`, `.claude/rules/coding-standards.md`)
8. **목표성 UI는 목표 수첩이 맡는다.** "지금 무엇을 하면 되는가"를 알려 주는 UI는 Tab으로 여는 목표 수첩에 넣고, 늘 떠 있는 것은 추적 목표 HUD 하나뿐이다. (`.claude/rules/objective-journal-ui.md`)
9. **그림과 맵은 갈아 끼울 수 있게.** 그림과 맵은 전부 새로 만들 예정이다. 화면 코드는 스킨 이름과 아이콘 키만 쓰고, 코드가 특정 맵의 이름·크기·좌표에 기대지 않는다. 테스트는 그림의 픽셀에 기대지 않는다. (`.claude/rules/engine-migration.md`, `.claude/rules/ui-standards.md`)
10. **여러 언어.** 사람이 읽는 문구를 코드에 적지 않고 현지화 키로 받는다. 문장을 코드에서 이어 붙이지 않는다. (`.claude/rules/localization.md`)
11. **기획은 사용자에게 묻는다.** 수치, 콘텐츠, 진행 순서, 화면 구성, 스토리를 바꾸는 결정은 묻는다. 옛 세이브는 계속 읽혀야 한다. Unity 판과 달라지는 동작은 테스트로 고정하고 `docs/superpowers/specs/2026-11-11-core-rules-extraction-design.md` 9절에 적는다.

## 규칙 문서 (`.claude/rules/`)

- `engine-migration.md` — 옮기는 동안의 규칙, 지금 상태, 그림과 맵 교체 대비
- `coding-standards.md` — 프로젝트 나눔과 참조 방향, Core 제약, 성능
- `data-driven.md` — 데이터에서 Core 정의까지, id 분기 금지, 보상·인벤토리·세이브
- `testing-discipline.md` — 어느 테스트 프로젝트에 무엇을, 명령, 시나리오 id
- `core-logic-and-play-evidence.md` — 근거의 종류와 보고 규칙
- `ui-standards.md` — 화면 구조, 화면 쌓기, 키 배정, 테마, 배치 수치
- `objective-journal-ui.md` — 목표 수첩과 추적 목표 HUD
- `localization.md` — 다국어
- `game-design.md` — 자유 경로 성장, 성능 동시 설계
- `environment-config.md` — 환경에 따라 달라지는 값
- `commit-conventions.md` — 커밋 메시지
- `changelog.md` — 규칙이 바뀐 이력

## 저장소 구조

- `Packages/com.rootborn.core/Runtime` — `Rootborn.Core`: 게임 규칙, 진행 상태, 세이브 모양, 월드 판정
- `game/` — MonoGame 판: `Rootborn.Data`(데이터 읽기, 세이브 파일), `Rootborn.Game`(게임 한 판 `GameSession`), `Rootborn.World`(맵), `Rootborn.Ui`(UI 부품), `Rootborn.Screens`(화면), `Rootborn.Net`(멀티플레이 바탕), `Rootborn.Host`(실행 프로그램), `tests/`, `world/`(맵 목록과 상호작용 지점), `themes/`(UI 그림), `localization/`(더한 문구)
- `data/` — 게임 데이터 JSON. 지금은 Unity 에셋에서 변환한 결과다(`Tools/Rootborn.DataExport`). 고치는 법은 `data/README.md`
- `Tools/` — Core 테스트, 변환기, 배포 빌드(`Tools/publish`)
- `Scripts/ci` — 정적 검사
- `docs/superpowers/specs` — 설계 문서
- `Assets/` — 얼린 Unity 판. 게임이 읽는 그림과 LDtk 맵이 아직 이 아래에 있다

자세한 설명과 실행 인자는 `game/README.md`.

## 명령

```bash
export PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet"
dotnet test Tools/Rootborn.Core.Tests          # 규칙
dotnet test game/Rootborn.sln                  # 데이터, 맵, 게임 진행, 화면, 실제 창
bash Scripts/ci/run-all.sh --checks-only       # 정적 검사
dotnet run --project game/Rootborn.Host        # 실행(슬롯 선택부터)
```

평소에는 고친 프로젝트의 테스트만 돌리고, 커밋 전에는 전부 돌린다. 통과 줄만 보지 말고 프로젝트별 건수가 줄지 않았는지 본다.

## 커밋과 병합

- 커밋 제목과 본문은 한글, 제목 앞에 `[FEATURE]`, `[BUGFIX]`, `[REFACTOR]`, `[UI]`, `[ASSET]`, `[BALANCE]`, `[TEST]`, `[DOCS]`, `[CHORE]`, `[NETWORK]`, `[META]` 중 하나. (`.claude/rules/commit-conventions.md`)
- 작은 단위로 나누고, 단위마다 테스트와 함께 커밋한다.
- 여러 세션이 나눠 일할 때는 작업 폴더 하나에 쓰는 세션 하나, 자기 브랜치에만 커밋한다. main에 합치는 일은 조정 담당이 한다(스킬 `parallel-claude-session`).

## 보안과 설정

비밀값은 커밋하지 않는다. 주소, 포트, 저장 폴더, 글꼴·테마·맵 경로는 실행 인자나 환경 변수로 받는다. (`.claude/rules/environment-config.md`)
