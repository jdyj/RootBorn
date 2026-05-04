# 프로젝트 헌법 — ROOTBORN (세대 진화 농장 생존 게임)

이 문서는 모든 에이전트와 개발자가 따라야 하는 최상위 규칙이다. 구체적 가이드라인은 `.claude/rules/` 하위 파일을 참조한다.

## 게임 비전 (One-liner)

> **"한 캐릭터가 아니라, 세대를 통해 문명을 키우는 농장 생존 게임"**

플레이어는 한 인물이 아니라 **가문/세대/문명**이다. 1세대는 맨손에서 시작하고, 후계자가 일부 능력·지식을 계승하며, 세대를 거듭할수록 도구·기반시설이 누적되어 문명이 발전한다.

## 핵심 메카닉

### 세대 시스템
- **세대 = 진행 단위** (레벨 ❌ / 세대 ⭕)
- 1세대 lifetimeSec(기본 1800초 ≈ 30분) 경과 시 자동 세대 교체
- 세대 교체 시 `LineageBook`에 조상 기록 + `GenerationProfile.NextGeneration`으로 전환

### 후계자 추상화 방식 (직접 육아 X)
- `HeirGenerator.Generate(parentTraits, traitPool, seed)` 결정론적 생성
- 부모의 inheritable trait를 **50% 확률로 1~2개 계승** + 랜덤 풀에서 1개 보너스
- `isRandomOnly` trait는 부모로부터 계승 불가 (랜덤 풀에만 등장)
- 동일 시드 → 동일 후계자 (테스트 결정론)

### 도구 해금 (지식 기반)
- 특정 행동 N회 반복 → 새로운 개념 발견 → 도구 제작 가능
- 예: 맨손으로 돌을 땅에서 10회 타격 → `Knowledge_StoneTool` 해금 → `Tool_StoneAxe`/`Tool_StoneHoe` 제작 가능
- `KnowledgeProgress` 액션 버스 + `KnowledgeTriggerBase[]` SO 다형 평가
- AND 조건 (모든 trigger 만족 시 해금)

### 상태 기반 제한
- 배고픔/피로/외로움 (StatusEffectDefinition SO)
- 100% 도달 시 행동 페널티 (이동 속도, 작업 실패율)
- `StatusValue.Tick`으로 시간 누적, `Restore`로 회복

### 단계 분해 제작
- 모든 행동을 세분화 (예: 도끼 = 채집→가공→건조→조립)
- `RecipeDefinition` SO + `CraftStepBase[]` 전략

### 가문 특성 / 조상 시스템 (후속)
- `LineageBook`에 누적된 조상이 영구 버프 제공 (예: 묘지 시스템)
- 가문 trait는 모든 후계자에 자동 적용

## 타겟 플랫폼

- **PC 데스크톱 1순위** (Windows 64-bit, macOS/Linux 후속)
- **기본 해상도**: 1920×1080 Borderless Fullscreen
- **종횡비**: 16:9 우선, 16:10/21:9 그레이스풀 디그레이드
- **입력**: 키보드+마우스 (WASD 이동, E/Space 채집, ESC 메뉴, F11 풀스크린)
- **멀티플레이**: Unity Netcode for GameObjects (NGO) — 싱글/호스트/클라이언트/dedicated server 4모드. `rootborn-server.exe -mode server -port 7777 -maxPlayers 4 -saveSlot myfarm` 형태 실행.

## 그래픽 자산

- **Pixelwood Valley 1.1.2** + **Icon Pack 1.0** (Unity Asset Store, 16×16 픽셀아트)
- 작물 80종, 도구 아이콘 315개, 타일 180종, 캐릭터 6방향(Idle/Walk × Down/Side/Up) × 4프레임
- Sprite sheet 슬라이스 규칙: `rules/path-based/sprite-slicing.md`
- **캐릭터 sheet는 59×49** (16×16 아님), Tile/Crops/Items만 16×16

## MVP 스코프 (현재)

1. **1세대 + 2세대 + 도구 해금** (지식 기반)
2. 맨손 농사 + 자원 채집(나무/돌)
3. 배고픔/피로/외로움 상태
4. 시간/일자 시스템 (`GameClock`)
5. 후계자 추상화 + 가문 누적
6. dedicated server 빌드 + 클라 접속

후속 (post-MVP):
- 직접 육아 옵션, 마을/NPC, 문화/신념(비 부르는 의식, 작물 숭배), 조상 묘지 버프

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
모든 게임 엔티티(작물·도구·자원·지식·특성·세대 프로필·상태이상·레시피)는 **ScriptableObject**로 정의한다. 시스템 코드는 특정 엔티티 ID에 분기하지 않는다.

**금지** (CI 게이트 `Scripts/ci/check-no-entity-id-branching.sh`로 강제):
- `if (cropId == "Wheat")`, `switch (toolId)` — 작물별/도구별 if·switch
- `enum CropId { Wheat, Carrot ... }` — 엔티티별 enum
- 작물/도구/지식별 C# 클래스 (모두 SO + 전략 SO 배열로 표현)

**허용**:
- `CropDefinition` SO + `GrowthBehaviorBase[]` (전략 SO 배열)
- `ToolDefinition` SO + `ToolEffectBase[]`
- `KnowledgeNode` SO + `KnowledgeTriggerBase[]`

**SO 와이어링 경로**: `Assets/Data/{Crops|Tools|Resources|Recipes|Knowledge|Traits|Generations|Status|Family}/`. 모든 SO는 `GameDataRegistry.asset`에 등록.

### 3. Scene은 조립, Prefab은 부품
Scene 파일에 로직을 하드코딩하지 말고 **Prefab 조립**으로 구성한다. Scene diff 충돌을 최소화한다.

### 4. 테스트는 PlayMode 우선
순수 로직은 EditMode, 게임플레이/물리/코루틴은 **PlayMode**에서 검증한다. 빌드 전 전체 테스트 통과 필수.

### 5. 증분 QA (Incremental QA)
모듈 완성 직후 즉시 QA. 전체 완성 후 일괄 QA는 금지. 경계면 버그는 조기에 잡는다.

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

- **네임스페이스**: `Rootborn.{Domain}` (예: `Rootborn.Generation`, `Rootborn.Heir`, `Rootborn.Crops`, `Rootborn.Tools`, `Rootborn.Knowledge`, `Rootborn.Network`)
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
