# Modern UI Panel Reconstruction Goal

아래 프롬프트를 새 goal 세션에 그대로 사용한다.

```text
ROOTBORN Unity 프로젝트의 퀘스트, 인벤토리, 상태창 UI를 `Assets/modernuserinterface-win/16x16` 및 현재 프로젝트에 존재하는 16x16 sliced sprite 조합 방식으로 재구성해줘.

사용자가 제공한 기준 이미지:
- 상태창: https://img.itch.zone/aW1hZ2UvMTg2NTgzOS8xMDk2MjY0Ni5naWY=/original/Z9nBBy.gif
- 인벤토리창: https://img.itch.zone/aW1hZ2UvMTg2NTgzOS8xMDk2MjY0OC5naWY=/original/dztsAE.gif

기존 참고 문서:
- `docs/superpowers/goals/2026-05-07-modern-ui-sprite-reconstruction-goal.md`
- `docs/superpowers/plans/2026-05-07-modern-ui-sprite-reconstruction.md`
- `docs/art/reference-modern-ui/`
- `docs/art/modern-ui-reconstruction-manifest.json`
- `docs/art/modern-ui-reconstruction-audit.md`

핵심 문제:
- 현재 TAB 또는 I를 누르면 background panel은 나오지만, 기준 GIF처럼 완성도 있는 인벤토리/상태/퀘스트 판넬 구조가 아니다.
- 목표는 단일 16x16 sprite 확대가 아니라, 기준 이미지처럼 16x16 조각을 찾아 이어붙여 window, title bar, tab, slot, scrollbar, item popup, quest list, button, gauge를 구성하는 것이다.
- 사용자는 "16x16 픽셀로 될 때까지", "100% 동일할 때까지", "완전 동일할 때까지 goal로 반복"을 요구했다. 따라서 이 goal은 한 번에 대충 끝내지 말고, reference screenshot과 Unity screenshot을 반복 비교하면서 차이를 줄이는 검증 루프를 포함해야 한다.

절대 규칙:
- AGENTS.md 헌법을 지킨다.
- `Assets/**/*.cs` 직접 디스크 쓰기 금지. 신규/수정 C#은 반드시 Unity MCP `script-update-or-create` 또는 Unity Editor 안전 경로를 사용한다.
- 실패 테스트 먼저 작성하고 구현한다.
- UI를 임의 색상 Image, 거대한 단일 sprite scale, placeholder, SVG, 런타임 생성 Texture2D로 때우지 않는다.
- 16x16 source sprite sheet에서 slice된 sub-sprite를 찾아 조합한다.
- 기준 GIF와 다르게 보이는 부분을 "충분히 비슷함"으로 넘기지 말고 audit 항목으로 남기고 반복 수정한다.
- `FilterMode.Point`, pixel-perfect에 가까운 정수 배율, 1920x1080 기준 Canvas 배치를 유지한다.
- 기존 사용자 변경분은 되돌리지 않는다.
- 런타임 신규 `Resources.Load`, `GameObject.Find`, `FindObjectOfType` 의존을 늘리지 않는다.

우선 조사할 에셋 위치:
- `Assets/modernuserinterface-win/16x16/`
  - `Modern_UI_Style_1.png`
  - `Modern_UI_Style_2.png`
  - `Modern_UI_Gamepad.png`
  - `Animated/`
  - `Portrait_Generator/`
- `Assets/moderninteriors-win/4_User_Interface_Elements/`
- `Assets/moderninteriors-win/1_Interiors/16x16/Room_Builder_subfiles/`
- 이미 추출된 기준 이미지:
  - `docs/art/reference-modern-ui/inventory.gif`
  - `docs/art/reference-modern-ui/inventory-frame61.png`
  - `docs/art/reference-modern-ui/status.gif`
  - `docs/art/reference-modern-ui/status-frame37.png`

1차 대상 UI:
1. 인벤토리 panel
   - I 키로 열고 닫힘.
   - 기준 GIF처럼 큰 window background, 상단 title/tab, item slot grid, scrollbar, 선택 cursor, 하단 control 영역을 가진다.
   - item slot에는 실제 inventory item icon이 들어간다.
   - slot을 클릭하면 item detail popup이 뜬다.
   - popup은 item name, icon, description/localization key, count, 가능한 action 버튼을 보여준다.

2. 상태창 panel
   - TAB 또는 기존 상태창 입력 흐름과 연결한다.
   - 기준 GIF처럼 portrait/status window, gauge row, status value, icon frame, bottom button 영역을 가진다.
   - 현재 status/resource HUD와 충돌하지 않도록 overlay panel과 HUD를 분리한다.

3. 퀘스트 panel
   - 다른 UI와 동일한 16x16 background panel 조합을 사용한다.
   - quest list, selected quest detail, objective progress, reward row, claim button, scrollbar를 가진다.
   - 기존 `QuestLogPanel`, `QuestHudAutoFiller`, `QuestRewardButton`, `DialoguePanel` 흐름과 보상 트랜잭션 원칙을 유지한다.
   - 보상 수령 UI는 지급 전 인벤토리 수용 가능 여부와 중복 수령 여부를 검증해야 한다.

목표 아키텍처:
1. Sprite recipe manifest 확장
   - 기존 `modern-ui-reconstruction-manifest.json`을 확장하거나, 별도 manifest를 만든다.
   - window background, title bar, tab, slot, scrollbar, popup, quest row, reward frame, gauge, button state별로 사용한 sub-sprite row/column을 기록한다.
   - 각 recipe는 sheet address, sub-sprite name, row, column, role, intended usage를 가진다.

2. 공용 16x16 UI 조립 컴포넌트
   - 3x3 panel, tiled fill, horizontal/vertical edge repeat, slot grid, scrollbar, popup frame을 조립한다.
   - 단일 sprite를 panel 전체 크기로 늘리지 않는다.
   - corner는 원본 16x16 크기/비율을 유지하고, edge/fill은 16x16 타일 반복으로 채운다.
   - child tile 수와 위치는 deterministic해야 하며 테스트 가능해야 한다.

3. Inventory/Status/Quest panel 분리
   - UI 외형 조립 로직과 도메인 데이터 표시 로직을 분리한다.
   - Inventory panel은 inventory/equipment 데이터만 읽고, item popup state를 가진다.
   - Status panel은 status/resource/character summary 데이터만 읽는다.
   - Quest panel은 quest log/progress/reward 상태만 읽고, reward claim은 기존 원자성 원칙을 호출한다.

4. 입력과 열림 상태
   - I: Inventory panel toggle
   - TAB: Status panel toggle 또는 현재 프로젝트에서 이미 연결된 status flow 유지
   - Quest panel은 기존 퀘스트 키/버튼이 있으면 유지하고, 없으면 명확한 입력 경로를 추가한다.
   - panel이 열렸을 때 닫기 버튼, ESC 닫기, 다른 panel과의 layering 규칙을 정의한다.

5. 반복 시각 검증 루프
   - 기준 frame과 Unity screenshot을 같은 해상도 또는 정규화된 크기로 비교한다.
   - pixel similarity, non-empty, tile-count, text-bounds, layout bounds 검증을 둔다.
   - "100% 동일"은 외부 레퍼런스 GIF와 라이선스/폰트/런타임 차이 때문에 기계적으로 보장하기 어렵다. 대신 반복 기준을 명시한다:
     - panel/slot/scrollbar/popup의 sprite recipe가 기준 구조와 1:1로 매핑됨
     - corner/edge/fill/slot/button이 모두 실제 16x16 sub-sprite 조합임
     - screenshot diff에서 남은 차이가 문서화되고, 허용할 수 없는 차이는 다음 반복 task로 남김
     - 최종 보고 전 최소 3회 이상 screenshot audit를 반복하거나, 모든 blocking visual issue가 0개임을 증명

권장 접근 방식:

접근 A. 권장: 기존 Modern UI reconstruction 기반을 확장해 Inventory/Status/Quest recipe를 완성한다.
- 장점: 이미 존재하는 goal/plan/manifest/test 흐름을 재사용하고, 사용자가 준 레퍼런스와 연결된다.
- 단점: 초기에 sprite 좌표 매핑과 screenshot audit 시간이 든다.

접근 B. 각 panel을 개별 코드로 빠르게 조립한다.
- 장점: 한 화면씩 빠르게 만들 수 있다.
- 단점: panel/slot/button recipe 중복과 하드코딩이 늘고, "다른 것과 동일한 16x16 조합" 요구에 약하다.

접근 C. Unity Image sliced mode 중심으로 구현한다.
- 장점: 단순하다.
- 단점: 단일 sprite 확대 문제를 다시 만들 수 있고, 사용자가 요구한 조각 단위 재현 검증이 약해진다.

이번 goal은 접근 A를 따른다.

1차 시나리오 카탈로그:

UI-PANEL-QUEST-001:
- Quest panel background는 16x16 corner/edge/fill 조합으로 만들어진다.
- quest list row, selected detail, reward row, claim button, scrollbar가 실제 sprite recipe를 사용한다.
- reward claim은 인벤토리 수용 가능 여부와 중복 수령 여부를 먼저 검증한다.

UI-INVENTORY-DETAIL-001:
- I 키로 inventory panel이 열린다.
- inventory panel은 기준 GIF 구조에 맞는 title/tab, slot grid, scrollbar, bottom control 영역을 가진다.
- slot click 시 item detail popup이 뜬다.
- popup은 16x16 panel recipe로 구성되고 item icon/name/count/description/action button을 표시한다.
- 빈 slot click은 popup을 닫거나 빈 상태를 표시하되 예외를 내지 않는다.

UI-STATUS-PANEL-001:
- TAB으로 status panel이 열린다.
- status panel은 기준 GIF 구조에 맞는 portrait/status frame, gauge row, icon frame, bottom button 영역을 가진다.
- HUD와 overlay panel이 서로 겹쳐 읽기 어려워지지 않는다.

UI-RECIPE-QUEST-001:
- inventory/status/quest/popup/slot/scrollbar/button/gauge recipe가 manifest에 존재한다.
- manifest의 모든 sub-sprite가 실제 sliced sprite로 존재한다.
- 동일한 panel, slot, button 조각은 공용 recipe로 재사용된다.

UI-PIXEL-AUDIT-001:
- 기준 frame과 Unity screenshot 비교 결과가 저장된다.
- non-empty, expected panel bounds, tile count, text bounds, sprite scale guard가 통과한다.
- 단일 16x16 sprite를 거대한 panel로 확대하는 1차 범위 사용처가 없다.

UI-ITERATION-001:
- visual audit에서 blocking issue가 발견되면 수정 후 screenshot audit를 다시 실행한다.
- 최종 완료 전 최소 3회 audit 기록 또는 blocking issue 0개 기록을 남긴다.
- 남은 차이가 있으면 "허용된 차이"와 "다음 반복 대상"으로 분리해 보고한다.

필수 구현 방향:
- `ModernUiSpriteResolver`
  - Addressables 또는 기존 preload/cache 방식으로 `sheetAddress + subSpriteName` Sprite를 가져온다.
  - 신규 runtime `Resources.Load` 금지.
- `ModernUiTileImage` 또는 동등 컴포넌트
  - RectTransform 안에 16x16 sprite Image children을 타일 방식으로 배치한다.
  - 3x3 panel, repeated fill, slot grid, scrollbar track/thumb, popup frame을 지원한다.
- `ModernUiWindowBuilder`
  - title bar, tab row, content area, close button, button row, scrollbar를 공용으로 만든다.
- `InventoryPanel`
  - slot grid, item popup, scroll, selection state를 관리한다.
- `StatusPanel`
  - status/gauge/portrait/frame 표시를 관리한다.
- `QuestLogPanel` 개선
  - 기존 퀘스트 시스템을 유지하면서 panel recipe와 list/detail/reward 표시를 교체한다.
- `docs/art/modern-ui-panel-audit.md`
  - 기준 이미지 분석, 사용한 sub-sprite 좌표, screenshot audit 결과, 남은 차이를 기록한다.

필수 테스트:
1. EditMode
   - recipe manifest가 inventory/status/quest/popup/scrollbar/slot/button/gauge를 포함하는지 검증
   - 모든 manifest sub-sprite 존재 검증
   - panel recipe가 corner/edge/fill 역할을 모두 갖는지 검증
   - slot grid와 popup tile count 결정성 검증
   - oversized single-sprite panel 금지 source audit
   - quest reward claim 원자성/멱등성 회귀 테스트 유지
2. PlayMode
   - I 키 inventory open/close
   - slot click item popup open/close
   - TAB status panel open/close
   - quest panel open/scroll/select/claim attempt
   - screenshot capture 후 non-empty/panel-tile-count/text-bounds 검증
3. Visual audit
   - inventory/status 기준 frame과 Unity screenshot 비교
   - blocking visual mismatch list 작성
   - 반복 수정 후 audit 재실행
4. 기존 회귀
   - 변경 영역 EditMode/PlayMode
   - `Rootborn.Tests.EditMode`
   - `Rootborn.Tests.PlayMode`
   - `Scripts/ci/check-no-entity-id-branching.sh`

완료 조건:
- Inventory, Status, Quest panel이 모두 16x16 sprite 조합 방식으로 구성되어 있다.
- I/TAB 입력으로 해당 UI가 안정적으로 열리고 닫힌다.
- Inventory slot click 시 item popup이 나온다.
- Quest panel은 list/detail/reward/scroll 구조를 갖고, reward claim 원자성 원칙을 유지한다.
- 기준 GIF의 주요 panel/button/slot/scrollbar/gauge 구조가 manifest에 매핑되어 있다.
- 단일 16x16 sprite를 거대한 panel로 확대하는 1차 범위 사용처가 제거되어 있다.
- screenshot 기반 visual audit가 존재하고, blocking issue가 0개이거나 다음 반복 대상이 명확히 문서화되어 있다.
- 마지막 보고에는 아래를 포함한다:
  - 분석한 기준 이미지 URL과 local frame
  - 사용한 sheet와 sub-sprite 좌표 목록
  - 생성/수정한 recipe/manifest/assets/scripts
  - 구현한 Inventory/Status/Quest 상호작용
  - screenshot audit 반복 횟수와 결과
  - 실행한 테스트와 결과
  - 남은 visual diff와 다음 우선순위
```

## Scope Note

이번 goal은 UI를 "예쁘게" 만드는 작업이 아니라, 기준 GIF를 16x16 sliced sprite 조각 단위로 역분해해 Unity UI에서 재조립하고, screenshot audit를 반복해 차이를 줄이는 작업이다. 기존 `modern-ui-sprite-reconstruction-goal`의 후속/확장 goal로 취급한다.
