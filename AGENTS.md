# Repository Guidelines

> **헌법급 절대 원칙 — 모든 AI 에이전트는 위반 금지.** 상세 규칙은 `.claude/rules/` 참조.

## Constitutional Rules (절대 원칙)

1. **`Assets/**/*.cs` 디스크 직접 쓰기 금지.** 신규 C# 스크립트는 반드시 `script-update-or-create` MCP tool 또는 Unity Editor 메뉴 경유. `Write`/`echo >` 등 외부 디스크 쓰기는 `CompilationPipeline.sourceFiles` 누락을 일으켜 CS0103 발생. (`.claude/rules/unity-cli.md`)

2. **엔티티 데이터-드리븐 절대 원칙.** 모든 게임 엔티티(작물·도구·자원·지식·특성·세대 프로필·상태이상·레시피)는 100% ScriptableObject로 정의. `if (cropId == "X")`, `switch(toolId)`, 작물/도구별 C# 클래스, ID enum 모두 **금지**. 메카닉은 `CropDefinition` SO + `GrowthBehaviorBase[]`, `ToolDefinition` SO + `ToolEffectBase[]`, `KnowledgeNode` SO + `KnowledgeTriggerBase[]` 처럼 SO 전략 배열로만 표현. (`.claude/rules/path-based/assets-data.md`, CI 게이트 `Scripts/ci/check-no-entity-id-branching.sh`)

3. **TDD 의무 + 시나리오 100% 커버.** 모든 신규 코드는 실패 테스트 먼저, 구현 다음. 코어 루프(생성/세대교체/도구해금/농사/상태/네트워크) 시나리오는 명명된 테스트로 ≥95% 커버. AI 생성 코드도 예외 없음. (`.claude/rules/testing-discipline.md`)

4. **엔티티 SO 와이어링 규약.** SO 클래스는 `Assets/Scripts/Game/{Domain}/`, 인스턴스는 `Assets/Data/{Crops|Tools|Resources|Recipes|Knowledge|Traits|Generations|Status|Family}/`. 모든 SO는 `Assets/Data/Registry/GameDataRegistry.asset`에 등록 (Resources.Load 대용). SO 인스턴스 = 튜닝 단위.

5. **커밋 prefix 카탈로그 (한글 본문 의무).** `[FEATURE] [BUGFIX] [REFACTOR] [UI] [ASSET] [BALANCE] [TEST] [DOCS] [CHORE] [NETWORK] [META]` — 11종. 복수 가능 (예: `[ASSET][BALANCE]`). 다중 카테고리 변경은 분리 커밋 권장. (`.claude/rules/commit-conventions.md`)

## Rules Reference (`.claude/rules/`)

- `coding-standards.md` — Unity C# 표준 (sealed, [SerializeField] private, Update 내 GetComponent 금지)
- `ui-standards.md` — PC 1920×1080 기본, Borderless Fullscreen, Canvas Scaler 1920×1080 기준
- `testing-discipline.md` — Tier 1~4 테스트, 시나리오 카탈로그 (GEN/HEIR/TOOL/CROP/KNOW/STATUS/NET)
- `path-based/assets-gameplay.md`, `assets-data.md`, `assets-ui.md`, `assets-addressables.md`
- `unity-cli.md` — batchmode 빌드/테스트
- `game-design.md` — GDD/ADR/TR/Epic/Story 경로
- `localization.md` — ko/en 키 기반
- `environment-config.md` — host/port/url/credential 환경 의존 값
- `commit-conventions.md` — 위 5번 상세

## Project Structure & Module Organization

이 저장소는 Unity 6000.3.13f1 (URP 2D) + Unity Netcode for GameObjects 기반 ROOTBORN 클라이언트와 dedicated server를 단일 프로젝트로 관리합니다 (백엔드/관리자 웹 미사용).

- Unity 소스: `Assets/Scripts/Game`, `Assets/Scripts/UI`, `Assets/Scripts/Network`, `Assets/Scripts/Editor` — 각각 `Rootborn.Game`, `Rootborn.UI`, `Rootborn.Network`, `Rootborn.Editor` asmdef 경계
- 씬: `Assets/Scenes/Boot.unity`, `MainMenu.unity`, `HostLobby.unity`, `Farm.unity`
- 게임 데이터: `Assets/Data/{Crops,Tools,Resources,Recipes,Knowledge,Traits,Generations,Status,Family,Registry}/` (모두 ScriptableObject)
- 로컬라이즈 리소스: `Assets/Localization`
- 테스트: `Assets/Tests/EditMode`, `Assets/Tests/PlayMode`
- 스프라이트 에셋: `Assets/Pixelwood Valley/Pixelwood Valley 1.1.2/`, `Assets/Pixelwood Valley Icon Pack 1.0/`

## Build, Test, and Development Commands

### Unity 빌드 (CLI batchmode)
- 클라이언트 (Windows 64): `-executeMethod Rootborn.Editor.BuildScripts.BuildScript.BuildClientWindows64`
- Dedicated Server (Windows 64): `-executeMethod Rootborn.Editor.BuildScripts.BuildScript.BuildServerWindows64`
- Dedicated Server (Linux 64): `-executeMethod Rootborn.Editor.BuildScripts.BuildScript.BuildServerLinux64`

### 산출물 실행 (사용자 흐름)
```cmd
:: Dedicated server (옵션만 줘서 실행)
rootborn-server.exe -mode server -batchmode -nographics -port 7777 -maxPlayers 4 -saveSlot myfarm

:: Client
rootborn.exe -mode client -joinIp 192.168.0.10 -port 7777

:: Host (서버 + 자기 자신 플레이)
rootborn.exe -mode host -port 7777 -maxPlayers 4 -saveSlot localhost-farm
```

### Unity 테스트 (CLI)
```cmd
"<UnityPath>" -batchmode -nographics -projectPath "c:\Users\jdyj\farmer" ^
  -runTests -testPlatform editmode ^
  -testResults "Builds/Logs/editmode-results.xml" -logFile "Builds/Logs/editmode.log"
```

## Coding Style & Naming Conventions

C# 코드는 `Rootborn.*` 네임스페이스와 폴더별 책임을 유지합니다. 런타임 도메인 로직은 `Rootborn.Game.{Domain}`, UI는 `Rootborn.UI.{Domain}`, NGO 의존 코드는 `Rootborn.Network.{Domain}`, 에디터 자동화는 `Rootborn.Editor.{Domain}`. Unity 에셋을 추가하면 `.meta` 파일을 함께 커밋합니다. 새 코드는 의미 있는 도메인명으로 작성하고, 임시 하드코딩이나 엔티티 ID별 분기 로직은 피합니다 (CI 게이트가 차단).

## Testing Guidelines

Unity 변경은 영향 범위에 맞춰 EditMode 또는 PlayMode 테스트를 추가합니다. 테스트 파일은 대상 도메인 폴더 아래에 두고 `*Tests.cs` 패턴을 사용합니다. PR 전에는 최소한 변경 영역의 EditMode 테스트와 빌드 검증(`BuildClientWindows64`)을 실행합니다. 시나리오 ID(GEN/HEIR/TOOL/CROP/KNOW/STATUS/NET-NNN)를 testing-discipline.md에 등록한 경우 대응 테스트가 존재해야 합니다 (CI `check-scenario-registry-sync.sh`가 검증).

## Commit & Pull Request Guidelines

커밋 제목과 본문은 한글로 작성합니다. 제목에는 반드시 `[FEATURE]`, `[BUGFIX]`, `[UI]`, `[ASSET]`, `[NETWORK]`, `[TEST]`, `[DOCS]`, `[CHORE]` 같은 prefix를 붙입니다. 예: `[NETWORK] dedicated server 부트스트랩 추가`. 모든 작업은 작은 작업 단위로 나누고, 각 단위마다 자동화 테스트와 코드리뷰를 완료한 뒤 커밋합니다. PR에는 변경 목적, 테스트 결과, 코드리뷰 결과, 관련 이슈를 적고 UI 변경은 스크린샷을 첨부합니다. Unity 씬, 프리팹, 생성 에셋을 바꾼 경우 재생성 절차와 영향을 받은 씬 이름도 명시합니다.

## Branch & Merge Workflow

기능 브랜치 작업 후 `main`에 병합할 때는 먼저 최신 `main` 커밋 위로 리베이스합니다. 병합은 머지 커밋이 생기지 않도록 스쿼시 머지로 처리합니다. 예: `git fetch origin`, `git rebase origin/main`, `git checkout main`, `git merge --squash feature/name`, `git commit -m "[FEATURE] 기능 요약"`.

## Security & Configuration Tips

비밀값은 커밋하지 않습니다. 환경 의존 값(host/port/saveSlot)은 명령줄 인자 또는 `.env`로만 주입합니다. 코드 하드코딩 금지 — `.claude/rules/environment-config.md` 참조.
