# 프로젝트 헌법 — ROOTBORN (도시 생활 성장 시뮬레이션)

이 문서는 모든 에이전트와 개발자가 따라야 하는 최상위 규칙이다. 구체적 가이드라인은 `.claude/rules/` 하위 파일을 참조한다.

## 게임 비전 (One-liner)

> **"도시에서 살아가는 한 사람의 생활 선택이 성격, 관계, 진로, 평판을 바꾸는 생활 성장 시뮬레이션"**

플레이어는 우선 **도시에서 살아가는 개인 또는 가구**다. 핵심 판타지는 농장 경영이나 세대 교체가 아니라, 집·거리·상점·학교·알바·직장·이웃 사이에서 시간을 쓰고 선택을 반복하며 **특성, 기술, 관계, 돈, 평판, 진로 가능성**을 키우는 것이다.

세대·가문·계승은 1차 코어 루프가 아니다. 기존 Generation/Family 구현은 레거시 또는 장기 메타 시스템으로 보존할 수 있지만, 신규 기능은 기본적으로 Town/StudentLife/LifeActivity 중심으로 설계한다.

## 핵심 메카닉

### 생활 활동과 선택
- 플레이어는 학교, 알바, 취미, 심부름, 휴식, 관계, 자기계발 같은 `LifeActivityDefinition`을 수행한다.
- 활동은 시간·돈·체력·불안·관계·평판을 비용 또는 보상으로 사용한다.
- 선택지는 `LifeChoiceDefinition` 또는 동등한 SO 데이터로 정의하고, 결과는 trait/skill/career hint/status/reward에 반영한다.
- 활동 완료와 선택 결과는 중복 적용되지 않도록 request id 또는 명시 상태로 멱등 처리한다.

### 자유 경로 성장
- 학생 생활은 학교 수업·등교 루트 하나만 정답인 선형 진행으로 설계하지 않는다.
- 플레이어는 학교를 중간에 나가거나, 마을 활동, 관계, 알바, 탐험, 제작, 도움 행동, 독학, 휴식, 이벤트 선택 같은 다양한 경로로 특성·성향·스킬·진로 힌트·관계·컨디션을 성장시킬 수 있어야 한다.
- 동일한 핵심 성장 목표에는 가능한 한 2개 이상의 대체 경로를 제공한다. 예: 성실함은 수업 참여뿐 아니라 도서관 독학, NPC 도움, 반복 작업으로도 성장할 수 있다.
- 학교 밖 활동은 보조 보상 전용이 아니라 핵심 성장·진로·관계 진행에 참여할 수 있어야 한다.
- 모든 성장 경로는 ScriptableObject 데이터와 전략 배열로 표현하며, 특정 활동/장소/퀘스트 ID별 C# 분기로 진행 경로를 제한하지 않는다.

### 특성·기술·진로 성장
- 특성은 단순 스탯 보너스가 아니라 플레이 스타일과 선택 경향을 표현한다.
- 기술은 활동 성공률, 선택지 접근성, 보상 품질, 진로 힌트 해금에 영향을 준다.
- 진로는 고정 직업 트리가 아니라 반복 활동과 선택의 누적으로 드러나는 후보군이다.

### 지식과 정보 해금
- 특정 행동 N회 반복 또는 생활 활동 완료 → 새로운 생활 정보, 지역 정보, 직무 힌트, 관계 팁 발견.
- `KnowledgeProgress` 액션 버스 + `KnowledgeTriggerBase[]` SO 다형 평가.
- AND 조건은 모든 trigger 만족 시 해금한다.

### 상태 기반 제한
- 체력/피로/불안/집중/외로움/돈 압박 같은 `StatusEffectDefinition` SO.
- 임계 도달 시 행동 페널티, 선택지 제한, 활동 실패율, 관계 반응에 영향을 준다.
- `StatusValue.Tick`으로 시간 누적, `Restore`로 회복

### 단계 분해 행동
- 중요한 생활 행동은 세분화한다. 예: 알바 준비 = 이동→복장/도구 확인→업무 수행→정산.
- `RecipeDefinition` SO + `CraftStepBase[]` 전략

### 세대·가문 시스템 (후속 메타)
- `LineageBook`, `GenerationProfile`, `HeirGenerator` 계열은 후속 메타 시스템으로 취급한다.
- 1차 MVP와 신규 생활 기능은 세대 교체를 필수 진행 단위로 요구하지 않는다.
- 세대·가문 보상은 생활 성장의 장기 기록 또는 엔딩 이후 계승으로만 도입한다.

## 타겟 플랫폼

- **PC 데스크톱 1순위** (Windows 64-bit, macOS/Linux 후속)
- **기본 해상도**: 1920×1080 Borderless Fullscreen
- **종횡비**: 16:9 우선, 16:10/21:9 그레이스풀 디그레이드
- **입력**: 키보드+마우스 (WASD 이동, E/Space 채집, ESC 메뉴, F11 풀스크린)
- **멀티플레이**: Unity Netcode for GameObjects (NGO) — 싱글/호스트/클라이언트/dedicated server 4모드. `rootborn-server.exe -mode server -port 7777 -maxPlayers 4 -saveSlot town-slot` 형태 실행.

## 그래픽 자산

- **Pixelwood Valley 1.1.2** + **Icon Pack 1.0** (Unity Asset Store, 16×16 픽셀아트)
- 타일, 실내/도시 소품, 아이콘, 캐릭터 6방향(Idle/Walk × Down/Side/Up) × 4프레임
- Sprite sheet 슬라이스 규칙: `rules/path-based/sprite-slicing.md`
- **캐릭터 sheet는 59×49** (16×16 아님), Tile/Items 계열은 자산별 PPU와 slice 규칙을 따른다.

### Modern UI Style2 공통 패널
- 공통 패널은 `Assets/modernuserinterface-win/16x16/Modern_UI_Style_2.png`의 16x16 Style2 패널 블록을 사용한다.
- 3x3 좌표는 top=`r2_c0/r2_c1/r2_c2`, middle=`r3_c0/r3_c1/r3_c2`, bottom=`r4_c0/r4_c1/r4_c2`로 고정한다.
- `r3_c1`은 패널 내부 fill tile로 반복 배치한다.
- 크기 가변 패널은 단일 `Image.sprite`가 아니라 `ModernUiTileImage + ModernUiRecipes.CommonPanel`로 생성한다.

## MVP 스코프 (현재)

1. **Town 기본 진입 흐름**: Boot/MainMenu 이후 기본 플레이 경험은 도시 생활 화면이어야 한다.
2. **학생/도시 생활 활동 루프**: 학교, 알바, 취미, 심부름, 휴식, 관계 활동.
3. **특성·기술·진로 힌트 성장**: 활동과 선택이 trait/skill/career hint에 누적된다.
4. **상태와 시간 관리**: `GameClock` 기반 일정, 체력/피로/불안/집중/돈 압박.
5. **보상·인벤토리 멱등성**: 활동/퀘스트/지식/진로 보상은 지급 전 검증과 원자적 지급을 보장한다.
6. **dedicated server 빌드 + 클라 접속**

후속 (post-MVP):
- 관계 심화, 직업 루트, 주거/소비 확장, 도시 이벤트, 세대·가문 메타, 레거시 Farm/Crop 기능 삭제 또는 격리

## 기술 스택

- **Unity 6000.3.13f1 (Unity 6)** 2D
- **Universal Render Pipeline (URP)**
- **New Input System** (`InputSystem_Actions.inputactions`)
- 언어: **C# 9.0+** (Unity 6 기준)
- 테스트: **Unity Test Framework** (EditMode + PlayMode)
- 빌드: **Unity CLI batchmode** (`-batchmode -nographics -quit`)

## 핵심 원칙

### 1. 변경 가능성 우선 (Changeability First)
게임은 반복 수정이 빈번하다. 재사용보다 **수정 용이성**이 우선이다. 이른 추상화를 피하고, 3회 이상 반복될 때만 추상화한다.

### 2. 엔티티 데이터-드리븐 (Entity Data-Driven Design)
모든 게임 엔티티(생활 활동·선택지·특성·기술·진로·직업·관계·장소·도구·자원·지식·상태이상·레시피·레거시 작물/세대 프로필)는 **ScriptableObject**로 정의한다. 시스템 코드는 특정 엔티티 ID에 분기하지 않는다.

**금지** (CI 게이트 `Scripts/ci/check-no-entity-id-branching.sh`로 강제):
- `if (activityId == "Study")`, `if (cropId == "Wheat")`, `switch (toolId)` — 활동/작물/도구별 if·switch
- `enum ActivityId { Study, PartTime ... }`, `enum CropId { Wheat, Carrot ... }` — 엔티티별 enum
- 활동/선택지/작물/도구/지식별 C# 클래스 (모두 SO + 전략 SO 배열로 표현)

**허용**:
- `LifeActivityDefinition` SO + `LifeActivityEffectBase[]` 또는 `LifeActivityRequirementBase[]` (전략 SO 배열)
- `LifeChoiceDefinition` SO + `LifeChoiceOutcomeBase[]`
- `ToolDefinition` SO + `ToolEffectBase[]`
- `KnowledgeNode` SO + `KnowledgeTriggerBase[]`
- 레거시 `CropDefinition` SO + `GrowthBehaviorBase[]`

**SO 와이어링 경로**: `Assets/Data/{StudentLife|LifeActivities|Choices|Skills|Careers|Jobs|Relationships|Locations|Tools|Resources|Recipes|Knowledge|Traits|Status|Family|Generations|Crops}/`. 모든 SO는 `GameDataRegistry.asset`에 등록.

### 2-1. 자유 경로 성장 원칙 (Open-Ended Progression)
학생 생활, 퀘스트, 진로 힌트, 특성·성향·스킬 성장은 단일 필수 루트가 아니라 복수 경로로 진행 가능해야 한다. 학교 수업은 중요한 경로 중 하나지만 유일한 정답이 아니다.

**금지**:
- 특정 성장이나 퀘스트 진행을 "학교 수업 완료" 하나에만 종속시키는 설계
- 정해진 순서대로 등교→수업→퀘스트→하루 종료를 따라야만 핵심 성장이 가능한 일직선 구조
- 학교 밖 활동을 핵심 성장/진로/관계 진행에서 배제하고 보조 보상으로만 취급하는 설계
- route/activity/location/quest ID별 C# 분기로 특정 경로만 통과시키는 구현

**필수**:
- 핵심 성장 목표에는 가능한 한 2개 이상의 대체 경로를 제공한다.
- 학교, 독학, 마을 활동, 관계, 알바, 탐험, 제작, 도움 행동, 휴식, 이벤트 선택을 성장 경로로 조합할 수 있게 한다.
- 퀘스트와 활동은 조건 기반 선택지, 대체 목표, 복수 해결 방식을 지원한다.
- 신규 코어 루프 PlayMode 테스트는 최소 1개 이상의 학교 밖 경로로도 성장/퀘스트/진로 힌트가 진행되는지 실제 플레이 방식으로 검증한다.

### 3. Scene은 조립, Prefab은 부품
Scene 파일에 로직을 하드코딩하지 말고 **Prefab 조립**으로 구성한다. Scene diff 충돌을 최소화한다.

### 4. 테스트는 PlayMode 우선
순수 로직은 EditMode, 게임플레이/물리/코루틴은 **PlayMode**에서 검증한다. 빌드 전 전체 테스트 통과 필수.

### 4-0. 플레이어 대리 테스트와 사용자 지정 플레이 흐름 우회 금지
모든 유저 여정, 코어 루프, PlayMode 시나리오의 완료 판정은 에이전트가 플레이어를 대리해 실제 조작 경로를 재현하는 테스트로 증명한다. 즉 저장 슬롯 선택, 메뉴 버튼 클릭, 마우스/키보드/게임패드 입력, 캐릭터 이동, 콜라이더/트리거 진입, NPC/오브젝트 상호작용, UI 선택, 씬 전환 관찰이 검증 대상이면 테스트도 같은 경로를 통과해야 한다.

사용자가 재현 순서나 조작 경로를 지정하면 자동화 테스트는 그 경로를 그대로 검증한다. 예를 들어 "Town에서 나무 획득 → 퀘스트 진행 → 직접 포탈로 가서 Farm으로 씬 변경 → 인벤토리/퀘스트 유지 확인"은 내부 `Travel()` 직접 호출이나 `SceneManager.LoadScene()` 강제 호출이 아니라, 플레이어 이동·트리거·상호작용 라우터·UI 갱신까지 실제 플레이 경로로 증명해야 한다.

테스트 통과를 위해 사용자 경로를 더 낮은 레벨의 메서드 호출, 상태 강제 세팅, 프리패스 fixture로 대체하지 않는다. 자동화가 어려운 조작은 먼저 제한을 명시하고, 최소한 PlayMode에서 물리/입력/UI/씬 전환 중 사용자가 요구한 관찰 지점을 포함하는 회귀 테스트를 작성한다.

### 4-1. Sprite 런타임 전수 검증
UI/HUD/캐릭터/타일 등 런타임에서 주소로 로드되는 sprite는 **개별 sprite 픽셀/주소 전체를 PlayMode에서 전수 검증**한다. 단순 manifest/EditMode 검증이나 대표 샘플 smoke test만으로 완료 처리하지 않는다.

필수 검증:
- 선언된 모든 sheet address와 sub-sprite name이 PlayMode에서 실제 Addressables/런타임 로더로 resolve 된다.
- resolve 된 `Sprite.name`이 선언 sub-sprite name과 일치한다.
- 각 sprite의 `textureRect` 전체 픽셀 배열을 읽어 width × height pixel count가 유효함을 확인한다.
- sprite asset 검증만으로는 부족하다. 실제 UI/Image/SpriteRenderer 같은 렌더 대상에 sprite가 할당되는지 PlayMode에서 검증한다.
- 대표 화면 또는 핵심 패널은 PlayMode에서 열어 `Image.sprite != null` 및 기대 sprite name/prefix를 확인한다.
- 신규 sprite 주소/manifest/recipe를 추가하면 대응 PlayMode 전수 테스트를 같은 변경 단위에 포함한다.

### 5. 증분 QA (Incremental QA)
모듈 완성 직후 즉시 QA. 전체 완성 후 일괄 QA는 금지. 경계면 버그는 조기에 잡는다.

### 6. 성능·최적화 동시 설계
신규 시스템은 기능 구현 후 뒤늦게 최적화하지 않는다. 기획, 데이터 구조, UI 목업, 테스트 작성 단계부터 성능 예산과 확장 비용을 함께 고려한다.

필수 원칙:
- 대량 항목 UI(도감, 인벤토리, 퀘스트, 마일스톤, 관계 목록)는 가상화, 페이징, 검색/필터 캐싱, 지연 로딩, dirty 갱신을 우선 검토한다.
- 매 프레임 전체 목록 스캔, `Update()` 기반 폴링, 런타임 문자열 조립 반복, LINQ/할당이 큰 열거, 불필요한 Instantiate/Destroy 루프를 피한다.
- ScriptableObject 데이터는 런타임 조회용 캐시/인덱스를 별도로 구성하고, 항목 해금/변경 이벤트가 발생했을 때만 UI를 갱신한다.
- Addressables/sprite 로딩은 중복 로드를 피하고, 필요한 화면/항목만 로드한다.
- 신규 goal과 최종 보고는 성능 리스크, 검증 범위, 남은 최적화 과제를 명시한다.

## 에이전트 작업 원칙

### 에이전트는 헌법을 먼저 읽는다
각 에이전트는 작업 시작 전 이 파일과 관련 `rules/*.md`를 Read로 로드한다.

### 피드백 루프
사용자가 **"잘못됐다", "틀렸다", "그렇게 하지 마", "wrong", "incorrect", "don't do that"** 류의 피드백을 주면:
1. `feedback-constitution-update` 스킬이 트리거된다
2. `feedback-analyzer`가 피드백 원인을 분석
3. `constitution-updater`가 이 파일 또는 `rules/*.md`에 항목 추가/수정
4. 변경 이력은 `.claude/rules/changelog.md`에 기록

### 산출물 경로 규약
- 에이전트 간 중간 산출물: `_workspace/{phase}_{agent}_{artifact}.{ext}`
- 최종 산출물: 프로젝트 실제 경로 (`Assets/`, `web/` 등)
- `_workspace/`는 사후 감사를 위해 보존

### 모델 설정
모든 에이전트는 `model: "opus"`를 명시한다.

## 게임 개발 파이프라인 (2026-04-17 도입)

### 경로 기반 파이프라인 분기
- **`Assets/` 작업**: `/gs-*` 파이프라인 (brainstorm → design-system → architecture-decision → create-epics → create-stories → dev-story → story-done)
- 본 프로젝트는 백엔드 미사용. `server/` 파이프라인 비활성.
- 경로 판정은 `/gs-start` 스킬이 담당. 혼합·신규는 사용자에게 질의.

### GDD/ADR/TR-ID 문서 체인
- 게임 시스템 추가 시 GDD 작성 필수 — `.claude/docs/templates/game-design-document.md` 사용
- 비자명한 기술 결정은 ADR 작성 — `docs/architecture/adr-NNNN-*.md`
- 요구사항은 TR-ID 로 추적 — `docs/architecture/tr-registry.yaml`
- 세부 규약: `rules/game-design.md`

### Addressables 원칙
- 신규 코드는 **Addressables 사용 필수**. `Resources.Load` 금지.
- 기존 `Assets/Resources/` 는 마이그레이션 대상. 본 P6 이후 별도 스토리로 진행.
- 세부 규칙: `rules/path-based/assets-addressables.md`

### 에이전트 3-tier 파일명 관례 (정보성)
- Tier 1 (Director): `t1-*.md` — 전략·게이트 승인 (technical-director 등)
- Tier 2 (Lead): `t2-*.md` — 부서 단위 의사결정 (game-designer, qa-lead, release-manager)
- Tier 3 (Specialist): `t3-*.md` — 개별 전문 작업 (gameplay-programmer, unity-specialist)
- **자동 라우팅은 없음.** `/gs-*` 스킬이 명시적으로 호출.
- 기존 18개 에이전트는 prefix 없이 유지.

## 코딩 규약 (요약)

- **네임스페이스**: `Rootborn.{Domain}` (예: `Rootborn.Game.StudentLife`, `Rootborn.Game.Town`, `Rootborn.Game.Activities`, `Rootborn.Game.Knowledge`, `Rootborn.Network`; 레거시는 `Rootborn.Generation`, `Rootborn.Crops` 유지 가능)
- **MonoBehaviour**: 1파일 1클래스, `[SerializeField] private` 우선 (public 필드 금지)
- **null 체크**: `if (x == null)` 보다 `if (!x)` 는 Unity 오브젝트에만 허용 (일반 객체는 ReferenceEquals 사용)
- **GC 주의**: `Update()`에서 `new`, `foreach on Dictionary`, `string +` 금지
- **명명**: PascalCase(타입/메서드/public), camelCase(local/private), `_camelCase`(private field), `k_PascalCase`(const)
- **로그**: `Debug.Log`는 개발용, 배포 전 `[Conditional("DEVELOPMENT_BUILD")]`로 감싸거나 래퍼 사용

세부 사항은 `rules/coding-standards.md` 참조.

## 금지 사항 (Hard No)

- `GameObject.Find`, `FindObjectOfType`는 런타임에서 금지 (초기화/Editor 스크립트에서만)
- `Resources.Load`는 Addressables로 대체 (신규 코드)
- `using UnityEditor;` 를 런타임 스크립트에 노출 금지 (`#if UNITY_EDITOR` 필수)
- Scene 파일 직접 수정(loop에서 yaml patch) 금지 — Unity Editor 경유
- 민감 정보를 코드에 하드코딩 금지 — `.env`, Addressables, 또는 서버에서 제공
- 환경 의존 값(host/port/url/endpoint/credential/path)을 코드에 하드코딩 금지 — `.env` 또는 설정 파일 키로 추출. 환경 통념(예: "dev=localhost") 가정 금지, 항상 데이터로 확인. 세부: `rules/environment-config.md`.

## 커밋 메시지

- 모든 커밋은 한글 prefix 형식 (`[FEATURE]`, `[BUGFIX]`, `[REFACTOR]`, `[UI]`, `[ASSET]`, `[BALANCE]`, `[TEST]`, `[DOCS]`, `[CHORE]`, `[NETWORK]`, `[META]`) + 한글 제목 + 한글 본문.
- 세부 규약: `rules/commit-conventions.md`.
- AI 에이전트도 동일 규칙 적용.

## 변경 규칙

이 파일의 원칙은 사용자 피드백 또는 `feedback-constitution-update` 스킬을 통해서만 수정된다. 에이전트가 임의로 수정하면 안 된다.
