# ROOTBORN 전체 설계 구현 계획

> **For agentic workers:** Use superpowers:subagent-driven-development for independent staged changes. The root agent exclusively applies changes to the open Unity Editor and runs tests. Track completion with evidence, not file counts.

**Goal:** 2026-05-25 스토리/직업 설계의 학생 생활부터 성인기 시설 Lv.3 엔딩까지 실제 조작 가능한 흐름으로 연결한다.

**Architecture:** 기존 StudentLife, QuestChain, ObjectiveJournal, Inventory, Save, WorldState를 확장한다. 엔티티는 SO 및 전략 배열로 정의하고, 시스템별 기능을 실제 플레이와 저장 복원까지 검증한 뒤 연결한다. 기존 세이브와 공유 작업을 보존한다.

**Tech Stack:** Unity 6000.3.13f1, C#, ScriptableObject, Addressables, NUnit/EditMode/PlayMode, native input, FEVM ComfyUI.

## 기준과 결정

- 기준 문서: `docs/story/rootborn-city-life-story-direction.md`, `docs/story/rootborn-career-growth-design.md`. 이전 첫 playable slice의 범위 제한은 이번 전체 구현 요청의 범위를 축소하지 않는다.
- 새 캐릭터 이미지 제작 금지는 유지한다. 현재 인물 에셋을 재사용한다. 필요한 환경/건물 아트는 기존 Modern 에셋을 확인한 뒤 ComfyUI로 보충한다.
- 빠른 완료는 문서대로 최고 보상을 지급한다. 별도 70% 보상 규칙을 만들지 않는다.
- 시설 Lv.3은 해당 직업 엔딩이다. 4개 시설 전체 달성 진엔딩은 문서에서 미정이며 임의 확정하지 않는다.
- 2026-11-04 사용자 결정: 마을의 숨겨진 과거 줄거리를 뺀다. 어릴 때부터 장래희망을 정하고, 학교를 다니며 원하는 알바로 특성을 올리는 흐름으로 간다.
- First Guide의 가족 관계, Time Jump 시점, 구급차 액션 범위는 데이터로 분리하여 후속 조정이 가능하게 한다.
- 열린 farmer 체크아웃에서 작업한다. 다른 작업의 변경을 포함하는 광역 Git 변경/커밋/리셋은 하지 않는다.

## 구현 순서와 판정

### 1. 공통 시간과 학생 생활

- [ ] SO 달력: 월~금 2 TP, 토~일 3 TP; 학생/성인 공유.
- [ ] 활동별 시간 정책: 자유 활동 비용, 무료 평일 아침 활동의 요일·중복 제한.
- [ ] 모든 활동 진입점의 사전 검증, 실패/재요청 시 TP·성장·보상 불변.
- [ ] 일일 사용량 저장/로드, 다음 날 초기화, 구 세이브 호환.
- [ ] Daily Plan의 시간표 선택과 남은 TP 표시, 여러 성장 경로 유지.
- [ ] 실제 학교 밖 활동 → TP 소모 → 부족 안내 → 하루 정산 → 다음 날 → 재로드 검증.

### 2. 스토리와 퀘스트

- [ ] 다단계/조건부 전이, 차단 조건, 단계 보상 원자성/멱등성, 저장 복원.
- [ ] 첫 3일 도입, BackToSchool/AfterSchoolTour/FirstPartTimeJob/DustySecrets/MyOwnSpace의 실제 데이터와 상호작용 연결.
- [ ] 장래희망 선택(MainQuest_MyDream), 변경, Objective Journal 추적과 완료 반영.
- [ ] 고른 장래희망에 맞춘 후속 기록·대사·관계 변화.

### 3. 직업 체험과 미니게임

- [ ] 데이터 기반 기억/타이밍, 도구 선택/순서, 공간 배치, 표적/회피 메커니즘.
- [ ] Chef/EmergencyCare/Interior/Soldier에 해당 메커니즘과 성향 보정을 데이터로 연결.
- [ ] 성공/실패, 취소, 시간 비용, 보상, 숙련도, UI 입력과 저장 검증.
- [ ] 학교 밖 두 가지 이상 성장 경로로 각 진로에 도달 가능.

### 4. 성인기 시설 경영

- [ ] 자격을 충족한 진로 중 플레이어가 선택하는 성인기 전환; 기존 성장/관계 보존.
- [ ] 4개 시설, 각 Lv.1~3, 업무·재료/수익·명성·업그레이드·마을 변화.
- [ ] 최고 숙련도 + 업무 성공 횟수 + 시설/장비 조건으로 빠른 완료 해금.
- [ ] 최고 보상 지급, 만석/재요청/로드 후 중복 지급 금지.
- [ ] 시설 Lv.3 직업 엔딩과 이후 계속 플레이.

### 5. 월드·아트·최종 통합

- [ ] 설계상 시설/학교/집/도서관의 포탈, 상호작용, 카메라, 현재 누락 그래픽 정리.
- [ ] 새 시설 아트의 실제 ComfyUI 생성, 픽셀/투명도/pivot/충돌/가림 검증.
- [ ] Addressables 등록·번들 빌드·실제 로드 및 release.
- [ ] 사용자 세이브와 분리된 신규 플레이 및 저장 복원 시나리오, 실제 Game View 증거.
- [ ] 영향 회귀·성능·클라이언트 빌드·네트워크 권한/동기화 확인.

각 항목은 구현 파일, 데이터, 자동화 결과, 실제 조작 증거가 있을 때만 완료로 바꾼다. 전체 항목이 끝나기 전에는 전체 설계 구현 완료로 보고하지 않는다.

### 6. 줄거리 전환 (2026-11-04 결정, 게임 미반영)

문서만 먼저 바꿨다. 아래 게임 데이터에는 아직 옛 줄거리가 남아 있다.

- [ ] 메인 체인: `Assets/Data/StudentLife/MainStory/QuestChain_SchoolOpening.asset`의 `Steps/MainQuest_DustySecrets.asset`을 MainQuest_MyDream으로 교체.
- [ ] 첫 3일: `Campaigns/Route_Archive`, `Route_Ledger`, `Objective_Archive`, `Objective_Ledger`, `Objectives/Objective_ArchiveDoor`, `Activities/LocationActivity_InspectArchiveDoor`를 장래희망과 알바 경로로 교체. 빌더는 `Assets/Scripts/Editor/StudentLife/MainStoryOpeningDataBuilder.cs`.
- [ ] 비밀을 암시하는 데이터 정리: `DiscoveryClues`의 LibraryArchive 계열, `Encyclopedia/Entries/EncyclopediaEntry_LockedArchive`, `WorldState/Flags/WorldState_LibraryArchiveOpen`, `StudentLife/DailyPlans/LibraryArchiveResearchRoute`.
- [ ] 문구: `GameTextCatalog`의 `opening.quest.DustySecrets.*`, `opening.clue.*`, `opening.objective.archive`, `opening.objective.ledger`, `life.library.archive-*`, `world.library.archive-open.*`, `ui.encyclopedia.entry.town-archive.*`.
- [ ] 테스트: `MainStoryOpeningAssetTests`, `ExplorationTextLocalizationTests`의 해시 고정, `DiscoveryClueLoopTests`, `WorldStateUsageLoopTests` 등 Archive를 쓰는 테스트 갱신.
- [ ] 원하는 알바: 현재 `PartTimeWork` 데이터는 CornerStoreShift와 WorkshopHelper 2개뿐이다. 4개 진로 모두 고를 수 있는 알바가 있도록 데이터를 추가한다.
- [ ] 서고 가구 `Interiors/Furniture/SchoolPixels/Archive30`은 일반 도서관 가구로 유지할지 사용자 확인.

Evidence/staging: `output/full-design-20261027/`.
