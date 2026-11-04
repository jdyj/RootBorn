# 승인된 도시형 UI 적용 계획

> Execution: use superpowers:subagent-driven-development. Root alone calls Unity, imports staged C# and runs tests. Workers stage `.cs.txt` files outside Assets; no commits or changes to unrelated files in this shared live Unity project.

**Goal:** 승인된 `output/design-comfy-20261028/` 아이콘과 UI 6종을 기존 실제 플레이 화면에 연결한다.

**Architecture:** 기존 `UrbanUiThemeDefinition`과 단일 Addressables 소유자를 확장한다. 256px 아이콘 아틀라스는 theme의 직렬화된 12개 sprite 참조로 로드한다. `UrbanUiIcon`은 준비 이벤트/활성화 때만 그림을 갱신하며 장식 이미지는 raycast를 받지 않는다. 기존 패널과 데이터 공급·선택·저장·입력 동작을 보존하고 시안의 배치와 스킨을 적용한다. 직업/활동 그림은 theme의 SO 참조별 presentation entry로 정의하며 엔티티 ID 분기를 추가하지 않는다.

**Tech:** Unity 6000.3, uGUI, Addressables, NUnit, native Game View input.

## 1. 공유 에셋과 아이콘

- [x] RED: `UrbanLifeIconAssetsTests`로 저장된 12개 sprite/rect/pivot/Point/무압축/로컬 Addressables/기존 theme 참조를 검증한다.
- [x] RED: `UrbanUiIconTests`로 늦은 theme 준비, 비활성화/파괴, 재사용, raycast 해제를 검증한다.
- [x] theme에 serialized icon entries와 SO presentation entries를 추가한다. `GetIcon(string)` 및 `ResolveIcon(UnityEngine.Object,string)`으로 조회한다. `UrbanUiIcon.Configure(string)` / `ConfigureFor(UnityEngine.Object,string)`을 제공한다.
- [x] 원본 `urban-life-icons-atlas.png`를 한 장만 `Assets/Art/UI/UrbanMolding/`에 복사하고 Editor importer에서 manifest 좌표를 변환해 저장한다. 기존 theme에 등록한다.

## 2. 메뉴와 저장 화면

- [x] RED 후 `ModeSelectPanel`, `SaveSlotSelectPanel`의 기존 버튼과 저장 서비스 동작을 보존하며 몰딩 패널·버튼·학교 그림과 저장 카드/뒤로가기를 적용한다.
- [x] 기존 캐릭터 생성·로드 경로는 그대로 연결하고 새 캐릭터 이미지는 만들지 않는다. 실제 화면에 없는 설정 기능은 가짜 버튼으로 추가하지 않는다. 삭제에는 실제 확인 경로를 둔다.

## 3. 목표 수첩·하루 계획·관심사

- [x] RED 후 `ObjectiveJournalPanel`의 분류/목록/상세/추적을 유지하며 아이콘, 여백, 닫기, 읽기 쉬운 글자와 스크롤을 적용한다.
- [x] RED 후 `DailyPlanPanel`의 실제 시간표/지난 날 기록/경로 선택을 유지하며 계획 아이콘과 카드·버튼 스킨을 적용한다.
- [x] RED 후 `CareerInterestPanel`의 실제 관심사 선택·조건·연결 퀘스트를 보존하며 4개 카드와 SO 기반 아이콘을 적용한다. 항목 수가 늘어나면 목록/스크롤을 사용한다.

## 4. HUD

- [x] RED 후 `TownTimeHudRuntimeInstaller`, `TrackedObjectiveHud`에 시간/수첩 아이콘, 몰딩, 충분한 inset을 적용한다.
- [x] 상시 목표는 기존 `TrackedObjectiveHud` 한 개만 유지한다. 새 per-frame 검색이나 UI 텍스트 재조립을 추가하지 않는다. 존재하지 않는 컨디션/돈 값을 장식 숫자로 표시하지 않는다.

## 5. 완료 검증

- [x] worker 결과를 spec/품질 검토하고 root가 순차 import한다. 해당 영역 회귀와 asset serialization을 확인한다.
- [ ] Boot → 싱글 → 분리된 테스트 저장 로드, Tab 수첩 분류/선택/추적/닫기, 시간표와 관심사 보드를 실제 입력으로 확인하고 스크린샷을 남긴다.
- [ ] 저장 화면 뒤로가기 및 취소, 작은 Game View에서 잘림, theme 준비 후 재개방·중복 장식/이벤트를 확인한다.
- [x] Windows 클라이언트/Addressables 빌드 후 새 아틀라스가 번들에 포함됐는지 확인한다. 측정하지 않은 성능 수치는 주장하지 않는다.


2026-10-28 execution evidence: `output/urban-ui-20261028-integration/integration-verification.json`. Final EditMode: 130/130 passed. Addressables and Windows build succeeded. Six native Game View captures are in `evidence/`. Native delete cancellation, journal track/close, board route selection/scroll/close and interest entry/card detail were checked with isolated saves. Some additional native plan checks (save Back/creator cancel and every journal category) remain unexecuted; those controls have automated coverage.
