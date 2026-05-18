# MainMenu Pointer Input Recovery Goal Prompt

아래 프롬프트를 goal 세션에 그대로 붙여 넣고 진행한다.

```text
ROOTBORN Unity 프로젝트에서 MainMenu의 실제 포인터 입력 경로를 복구해줘.

이번 goal의 목적:
- `Boot -> MainMenu -> SinglePlayButton 실제 마우스/포인터 클릭 -> SaveSlotSelectPanel 표시` 흐름을 player-facing 방식으로 통과시키는 것이다.
- 현재 `MainMenuPointerInputE2ETests.BOOT_E2E_003_MainMenuSinglePlayButtonRespondsToRealPointerClick`가 `SinglePlayButton left-click did not open SaveSlotSelectPanel.`로 실패한다.
- 기존 `BootToGameEndToEndTests`는 `ExecuteEvents` 직접 호출로 버튼 동작을 확인하지만, 이번 goal은 실제 InputSystem Mouse 이벤트, EventSystem, GraphicRaycaster, Button click 경로가 연결되어 있는지 검증해야 한다.

핵심 성공 기준:
- `SinglePlayButton`이 실제 포인터 클릭으로 `ModeSelectPanel.OnSingle()`을 실행한다.
- `SaveSlotSelectPanel.EnsureInScene().Show()` 결과로 SaveSlot UI가 실제 씬/UI 계층에 표시된다.
- 테스트가 내부 메서드 직접 호출, 강제 `ExecuteEvents.Execute`, 강제 `SaveSlotSelectPanel.Show()`로 통과하지 않는다.
- `BootToGameEndToEndTests` 기존 SaveSlot/NewGame/SPUM/Town 경로가 회귀하지 않는다.

필수 사전 조사:
- `Assets/Tests/PlayMode/EndToEnd/MainMenuPointerInputE2ETests.cs`
- `Assets/Scripts/UI/MainMenu/ModeSelectPanel.cs`
- `Assets/Scripts/UI/MainMenu/SaveSlotSelectPanel.cs`
- `Assets/Scripts/Editor/Tools/SceneSetup.cs`
- `Assets/Scenes/Boot.unity`
- `Assets/Scenes/MainMenu.unity`
- EventSystem 구성, `BaseInputModule`, `InputSystemUIInputModule`, `GraphicRaycaster`, Canvas sorting/render mode, Button/Image/Text raycastTarget 상태

진행 규칙:
- AGENTS.md와 `.claude/rules/*`를 따른다.
- `Assets/**/*.cs`는 shell, echo, cat, apply_patch 같은 외부 디스크 직접 쓰기로 수정하지 않는다. C# 신규/수정은 반드시 Unity MCP `script-update-or-create` 또는 Unity Editor 안전 경로를 사용한다.
- 기존 dirty 변경을 되돌리지 않는다. 이번 goal과 관련 없는 변경은 건드리지 않는다.
- TDD로 진행한다. 먼저 실패 테스트를 실행해서 현재 RED를 확인하고, 원인을 좁힌 뒤 구현한다.
- 테스트를 우회하지 않는다. 실제 포인터 클릭 검증이 목적이므로 `ExecuteEvents.Execute`로 테스트를 통과시키는 방향은 금지한다.
- 단순히 테스트 좌표만 맞추는 변경은 실제 Game View 클릭 경로가 맞다는 증거가 있을 때만 허용한다.

우선 검증해야 할 가설:
1. MainMenu 씬의 EventSystem에 실제 InputSystem 기반 입력 모듈이 붙어 있는가?
2. 테스트의 Mouse 이벤트가 현재 EventSystem 입력 모듈이 읽는 포인터로 전달되는가?
3. `SinglePlayButton`의 `Button` 컴포넌트와 `ModeSelectPanel._singleButton` serialized reference가 실제 씬에서 연결되어 있는가?
4. Button 또는 하위 Label의 raycastTarget/GraphicRaycaster/Canvas 설정 때문에 클릭이 막히는가?
5. Boot에서 MainMenu로 넘어간 뒤 EventSystem 또는 input module이 중복/교체/비활성화되는가?
6. `SaveSlotSelectPanel`이 생성되지만 비활성/다른 씬/DontDestroyOnLoad/Canvas 계층 문제로 탐지되지 않는가?

권장 해결 방향:
- 먼저 원인을 좁힌다. 테스트에서 raycast 결과, top hit, EventSystem current, input module type, button interactable, button onClick listener count, SaveSlotSelectPanel 생성 여부를 임시 로그나 디버그 검사로 확인한다.
- 실제 문제가 MainMenu 씬 wiring이면 SceneSetup 또는 씬 serialized reference를 수정한다.
- 실제 문제가 InputSystem UI module 설정이면 `UiInputModuleInstaller` 또는 EventSystem 구성 경로를 수정한다.
- 실제 문제가 테스트가 새 InputSystem 포인터를 충분히 프레임 처리하지 못하는 것이라면, 테스트 헬퍼를 실제 사용자 입력에 더 가깝게 보정한다. 단, `Button.onClick.Invoke()`나 `ExecuteEvents.Execute` 직접 호출은 사용하지 않는다.
- 라벨 raycastTarget 처리처럼 기존 `ModeSelectPanel.DisableButtonLabelRaycasts` 의도와 맞는 작은 수정은 허용한다.

필수 PlayMode 테스트:
- RED 확인:
  - `MainMenuPointerInputE2ETests.BOOT_E2E_003_MainMenuSinglePlayButtonRespondsToRealPointerClick`
- GREEN 확인:
  - `MainMenuPointerInputE2ETests`
  - `BootToGameEndToEndTests`
- 회귀 확인 권장:
  - `SpumCharacterCreatorNewGameE2ETests`
  - `IntegratedVerticalSliceFoundationE2ETests.TOWN_FLOW_001_003_NewGameTownObjectiveJournalDayResultReloadAndDedupe`

필수 시각/실제 흐름 검증:
- PlayMode에서 Boot 또는 MainMenu 진입 후 Game View/Camera screenshot을 캡처하거나, 동등한 player-facing UI 상태를 검사한다.
- `SinglePlayButton` 클릭 후 SaveSlot UI가 화면에 보이는지 확인한다.
- visual/player-facing 확인이 막히면 완료라고 말하지 말고, 무엇이 검증되었고 무엇이 미검증인지 명시한다.

완료 조건:
- `MainMenuPointerInputE2ETests`가 실제 Mouse/InputSystem 포인터 경로로 PASS한다.
- `BootToGameEndToEndTests`가 PASS한다.
- 관련 Unity Console Error/Exception이 없다.
- `Scripts/ci/check-no-entity-id-branching.sh`가 PASS한다.
- 변경 파일과 테스트 결과를 커밋한다. 커밋 제목은 한글 본문과 허용 prefix를 사용한다. 예: `[BUGFIX][TEST] MainMenu 실제 포인터 입력 경로 복구`
- 최종 보고에는 수정 파일, 실행한 테스트, PASS/FAIL, 남은 미검증 범위, branch/push 상태를 포함한다.

주의할 점:
- 현재 저장소에는 많은 dirty/untracked 변경이 있을 수 있다. 이번 goal의 파일만 선별해서 stage/commit한다.
- main에 직접 push한다고 가정하지 않는다. 현재 브랜치와 원격 상태를 먼저 확인한다.
- 전체 EditMode 19개 실패는 별도 goal이다. 이번 goal은 MainMenu 실제 포인터 클릭 회복에 집중한다.
```

