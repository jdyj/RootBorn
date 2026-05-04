# 규칙 변경 이력

`feedback-constitution-update` 스킬에 의한 헌법 및 규칙 문서 변경 이력.

형식:
```
## YYYY-MM-DD — {요약}
- 피드백: "{사용자 피드백 원문 또는 요지}"
- 원인: {왜 AI가 잘못했는지}
- 변경: {어느 파일 어떤 섹션이 어떻게 수정됐는지}
- 일반화: {오버피팅 방지를 위해 원리 수준에서 어떻게 정리했는지}
```

---

## 2026-05-04 — Addressables 도입 (SlimeMaster 패턴 차용)
- 피드백: "C:\Users\jdyj\Downloads\SlimeMaster ... addressable을 사용했는데 동일한 구조로 해볼 수 있겠어?"
- 원인: ROOTBORN이 GameDataRegistry를 `Assets/Resources/`로만 로드 → 빌드 시 메모리 적재, 핫업데이트 불가, 헌법 `path-based/assets-addressables.md` 위반
- 변경:
  - `Packages/manifest.json` — `com.unity.addressables` 2.4.6 추가
  - `Assets/Scripts/Game/Managers/Managers.cs` 신규 — 싱글톤 진입점 (`@Managers` + DontDestroyOnLoad, `BootstrapAsync()`)
  - `Assets/Scripts/Game/Managers/ResourceManager.cs` 신규 — async/await Addressables 래퍼, 캐시, Release 명시
  - `Assets/Scripts/Game/Managers/DataManager.cs` 신규 — `GameDataRegistry` + 도메인별 Dictionary lookup
  - `Assets/Scripts/Editor/Tools/AddressablesSetup.cs` 신규 — 그룹(Data/Sprites/Prefabs/Tiles) + `PreLoad` 라벨 + 자산 자동 등록
  - `GameBootstrap.Start` async, `Managers.BootstrapAsync()` 호출
  - `FarmAutoFiller` Resources.Load → `Managers.Data.Registry` 사용 (Resources fallback 유지)
  - `BuildScript` Client 빌드 시 `AddressableAssetSettings.BuildPlayerContent()` 자동 호출
  - `OneClickSetup` Step 6/6 추가 (`AddressablesSetup.WireAll`)
  - `rules/path-based/assets-addressables.md` — ROOTBORN 매니저 구조와 주소 규약 명시
- 일반화: "동적 에셋 로딩은 Addressables 일원화. SlimeMaster의 단순 콜백 래퍼 대신 async/await + Release 명시. fallback Resources.Load는 단 한 번만 허용."


## 2026-05-04 — Sprite 원본 방향 실측 (Pixelwood Side flipX 사건)
- 피드백: "왼쪽 오른쪽 뛰는게 반대야"
- 원인: Pixelwood Player Character Side.png 원본이 왼쪽을 향함. 일반 관례(원본=오른쪽) 가정으로 flipX = (input.x < 0)로 작성 → 좌우 반대 동작.
- 변경:
  - `PlayerController.cs` — flipX 조건 부호 반전 + 주석 명시
  - `rules/path-based/sprite-slicing.md` — "Sprite 원본 방향 (flipX 부호)" 섹션 추가, 원본 확인 절차 + 부호 규칙
- 일반화: "Side-view sprite의 원본 방향은 일반 관례에 의존하지 말고 PNG를 직접 보고 실측. 코드에 주석으로 원본 방향 명시."

## 2026-05-04 — Sprite sheet 셀 크기 추정 금지 (Pixelwood Idle/Down 사건)
- 피드백: "idle down은 236x49인데 16으로 잘라도 되는거야?"
- 원인: PixelwoodSliceSetup이 모든 sheet에 일괄 16×16 셀 적용. 캐릭터 sheet는 실제 59×49 셀이라 sliced sub-sprite가 잘못됨. 화면에 캐릭터가 안 보임.
- 변경:
  - `PixelwoodSliceSetup.cs` — 캐릭터 sheet 6개를 59×49 + PPU 49로 변경. SliceTarget에 PixelsPerUnit 필드 추가.
  - `FarmAutoFiller` — Player scale 4 → 1.5 (PPU 49로 1 unit 정사각형이 됨).
  - `rules/path-based/sprite-slicing.md` 신규 — Sheet별 실측 의무, PPU 통일 1 world unit 원칙, 정수 분할 검증, Pixelwood 구체 데이터 표.
- 일반화: "외부 sprite sheet 추가 시 셀 크기 추정 금지. 텍스처 픽셀 크기 + 한 프레임 셀 크기 + PPU 셋 다 실측. 파일명에 적힌 숫자도 우연일 수 있음."

## 2026-05-04 — ROOTBORN 프로젝트 분기 (coin-defense → farmer)
- 변경: coin-defense 하네스를 ROOTBORN(세대 진화 농장 생존 게임)용으로 fork
- `constitution.md` — 헤더 "ROOTBORN", 원칙 2번 "코인 데이터드리븐" → "엔티티(작물·도구·지식·특성·세대) 데이터드리븐", 네임스페이스 `Rootborn.*`, 패시브 ADR-0002 절 삭제
- `rules/path-based/assets-data.md` — 코인 절 → 엔티티 절 일반화. SO 와이어링 경로 `Assets/Data/{Crops|Tools|...}/`로 표기
- `rules/path-based/server.md` — 백엔드 미사용으로 삭제
- `rules/commit-conventions.md` — `[DB]`/`[SIM]` prefix 제거 (DB 미사용, 결정론 시뮬 불필요), 예시를 BitKnight → Wheat/Knowledge로 교체
- `Scripts/ci/check-no-coin-id-branching.sh` 삭제 → `check-no-entity-id-branching.sh` 신규 (cropId/toolId/knowledgeId/traitId 정규식 차단)
- `Scripts/ci/check-scenario-registry-sync.sh` — 시나리오 prefix MS/GS/META/PAY/NET → GEN/HEIR/TOOL/CROP/KNOW/STATUS/NET
- `.github/workflows/ci.yml` — server/admin job 제거, static-checks(entity-id 게이트) + Unity EditMode만
- `.github/workflows/unity-tests-nightly.yml` — PlayMode job 추가
- 에이전트 `web-architect/frontend-developer/backend-developer/database-architect` 4개 제거
- 스킬 `backend-web-dev` 제거

## 2026-04-12 — 초기 헌법 수립
- 하네스 최초 구축
- `constitution.md`, `rules/coding-standards.md`, `rules/unity-cli.md` 작성

## 2026-04-12 — UI 사용성 피드백 반영: 모바일 UI/UX 표준 신설
- 피드백: "X 버튼 동작 안함, 탭이 텍스트로만 보임, 슬롯 수 틀림, 텍스트 너무 작음, 가챠를 상점 내로 합쳐야 함"
- 원인: UI/UX 관련 규칙이 전무. Editor 스크립트가 SerializeField를 부분만 와이어링. 모바일 타이포/터치 기준 없음.
- 변경: `rules/ui-standards.md` 신규 생성 (모바일 타이포, 터치 타겟, 버튼 어포던스, 직렬화 완전성, 수치 추정 금지, UI IA 확인)
- 일반화: "절차적 UI 생성 시 직렬화 완전성" + "모바일 UI 최소 기준" + "게임 수치는 spec 기반" 원칙

## 2026-04-17 — 외부 Game Studios 하네스 통합 (P0~P6)

### P0 — POC 및 준비
- 피드백: "외부 Game Studios 하네스와 현 Unity 하네스를 병합하고 싶음"
- 변경: 외부 레포 clone (`.claude/_workspace/external-repo`), settings.json 백업, POC 3건 결정, .gitignore 업데이트
- POC 결정: (1) 스킬 하이픈 네임스페이스 기존 관례로 충분, (2) 공식 YAML 필드만 사용, (3) P3 수동 검증 지연 (P5 완료로 실증 확인됨)
- 일반화: "외부 하네스 도입 시 POC 선행 + 백업 필수" 원칙

### P1 — 게임 기획 템플릿 15종 도입
- 변경: `.claude/docs/templates/` 15개 (GDD/ADR/Epic/Story/... 외부 MIT 원본 + 한국어 헤더)
- 변경: `.claude/docs/workflow-catalog.yaml` (경로별 파이프라인 카탈로그)
- 일반화: "산출물 템플릿은 `.claude/docs/templates/` 에 집중, 외부 출처 명시"

### P2 — 경로 기반 rules + 게임 도메인 규약
- 변경: `rules/path-based/` 5개 (UI/gameplay/data/addressables/server), `rules/game-design.md`, `rules/localization.md`
- 기존 coding-standards/ui-standards 상단에 path-based 링크만 추가 (본문 무변)
- 일반화: "경로별 코딩 표준은 `rules/path-based/*.md` 에 배치"

### P3 — 훅 분리 (Claude Code + git)
- 변경: `.claude/hooks/session-hint.sh` (UserPromptSubmit 세션 1회 힌트), `scripts/git-hooks/pre-commit`/`pre-push`/`install.sh`/`uninstall.sh` (core.hooksPath 방식), `.gitattributes` (LF 강제)
- settings.json: UserPromptSubmit 배열에 session-hint 추가
- 일반화: "Claude Code 훅은 `.claude/hooks/`, git 훅은 `scripts/git-hooks/` + `core.hooksPath` 로 분리"

### P4a — 핵심 게임 특화 에이전트 6개
- 변경: `t1-technical-director` (unity-architect 섹션 인용), `t2-game-designer`, `t2-qa-lead`, `t2-release-manager`, `t3-gameplay-programmer`, `t3-unity-specialist`
- 기존 18개 에이전트 무변경 (병합은 본문 인용 방식)
- 일반화: "신규 에이전트는 평면 구조 + 파일명 prefix(t1-/t2-/t3-), YAML frontmatter 는 공식 필드만, 메타는 HTML 주석"

### P5 — 게임 개발 파이프라인 스킬 16개
- 변경: `.claude/skills/gs-*/skill.md` 16개
- 래퍼 구조: /gs-code-review → game-code-review, /gs-release-checklist → unity-build-deploy, /gs-dev-story → dev-iteration-loop
- 실증 확인: available skills 목록에 16개 모두 등록됨 (하이픈 네임스페이스 동작)
- 일반화: "스킬 네임스페이스는 `gs-*` 하이픈, 디렉토리명 = 호출명, 파일명 lowercase `skill.md`"

### P4b — 보조 에이전트 (조건부 후속)
- 결정 연기: P5 관통 테스트에서 사용 빈도 확인 후 추가.
- 파킹 대상: t1-creative-director, t1-producer, t2-economy-designer, t2-localization-lead, t3-ui-programmer, t3-accessibility-specialist.
- 일반화: "보조 에이전트는 실제 사용 빈도 확인 후 추가 (YAGNI)"

### P6 — 헌법·피드백 루프 업데이트
- 변경: `constitution.md` 에 게임 개발 파이프라인 섹션 추가 (경로 분기 / GDD-ADR-TR 체인 / Addressables / 3-tier 관례)
- `detect-feedback.js`: 기존 트리거 배열이 이미 한·영 포괄적이어서 변경 없음
- 일반화: "파이프라인 전환은 헌법에 반영, 트리거는 한·영 양방향 지원"

## 2026-04-28 — 코인 데이터-드리븐 절대 원칙 추가 + 디자인 후반 이연
- 피드백: "유닛은 바로 디자인 시작하기 보다 일단 기반기능부터 다 구현하고 추후 별도로 구현하거나 쉽게 변경 될 수 있으면 좋을 듯"
- 원인: 기존 플랜은 M1에서 CoinPenny, M2에서 12종 모두 작성하도록 잡혀 있어, 시스템 안정 전에 코인 디자인이 강제됨. 디자인 변경 시 시스템·테스트 회귀 위험.
- 변경: `rules/path-based/assets-data.md` 끝에 "코인(유닛) 데이터-드리븐 절대 원칙" 섹션 추가. 플랜 Task 0.9는 placeholder archetype 5종 작성 + 코인 디자인 M4 EPIC-023b로 이연. M2 EPIC-007은 archetype mechanic coverage로 변경.
- 일반화: "도메인 컨텐츠는 시스템·아키텍처가 안정된 후 데이터로 채운다. 시스템 코드는 컨텐츠 ID에 의존하지 않는다(static 분석으로 강제)."

## 2026-04-28 — testing-discipline.md 신설 (시나리오 100% 커버 의무)
- 피드백: "각 테스크 및 코어루프마다 자동화 테스트 / 유닛 테스트 필수로 구축. 자동화 테스트는 시나리오 테스트로 코어 루프 100% (최대한 100%) 커버"
- 원인: `coding-standards.md` 와 `path-based/*` 가 코드 스타일 위주로 짜여 있고, 테스트 강제력이 약했음. AI가 작성한 코드가 테스트 없이 머지될 가능성 존재.
- 변경: `rules/testing-discipline.md` 신규 생성 — TDD 의무, Tier 1~4 (단위/통합/시나리오/패리티), 코어 루프 시나리오 카탈로그 36개 명명, 커버리지 임계값 + CI 게이트, 시나리오 추가 의무
- 일반화: "테스트 디시플린은 헌법급 우선 규칙, 모든 신규 코드(특히 AI 생성)는 테스트 동시 작성 의무, 코어 루프는 명명된 시나리오 100% 커버"

## 2026-04-29 (P3) — 패시브 라이브러리 + 와이어링 + CI 워크플로우
- 추가: 8 패시브 SO 클래스 (CritBuff, SlowDebuff, DotApply, BounceAttack, PierceAttack, RootChance, InstantKillChance, RampOnSameTarget) + 8 .asset + 8 코인 와이어링 (DogWifChef/HederaHash/FrogPepe/XRipple/TonRocket/WLDOrb/MoneroMask/BitKnight) — 합계 10 코인 (BNBancer/ChainLinker 포함)
- 추가: 5 신규 EditMode 테스트 클래스 (PassiveLibrary 8 + Roster 4 + Synergy 3 + AutoPlayBot 1) → Coin 네임스페이스 76 통과
- 추가: `.github/workflows/unity-tests-nightly.yml` — game-ci 기반 야간 + PR EditMode 테스트 + DataDrivenScanner gate
- 검증: SynergyMatrix CSV 재생성 후 의도 시너지 페어 +14~18% 안착, DataDrivenScanner 위반 0건
- 일반화: "패시브는 SO 클래스 + .asset + 코인 와이어링 3-요소. 코인은 데이터로만 연결, 코드 분기 없음."

## 2026-04-29 (P2) — Unity .cs 신규 생성: 디스크 직접 쓰기 금지
- 피드백: 사용자가 MCP 사용 종용 ("mcp 연결 안되어 있음? 직접 해") 후 테스트 실행이 컴파일 에러로 차단됨. 원인 추적 결과, `Write` 로 디스크에 직접 쓴 `Assets/Scripts/Game/Coin/StarRank/UnitDpsCalculator.cs` 가 MonoScript 자산으로는 등록되었지만 `CompilationPipeline.GetAssemblies().sourceFiles` 에 누락되어 컴파일 어셈블리에 클래스가 반영되지 않음. 같은 폴더 다른 신규 파일 일부도 동일 증상.
- 원인: Unity 외부에서 디스크에 직접 .cs 를 쓰면 .meta 는 Unity 가 자동 생성하지만 Library/Bee 캐시·.csproj 가 즉시 갱신되지 않는 경합 윈도우가 존재. 본 프로젝트 헌법에 신규 스크립트 생성 표준 부재.
- 변경: `rules/unity-cli.md` 상단에 "Assets/**/*.cs 직접 디스크 쓰기 금지" 절대 원칙 신설. `script-update-or-create` MCP tool 또는 Editor 메뉴 경유 의무. 검증·복구 절차 명시.
- 일반화: "Unity 자산은 Unity API 경유 생성. 외부 도구가 메타 동시 생성을 보장하지 않는 자산 타입(.cs/.shader/.unity 등)은 모두 동일."

## 2026-04-29 — 별등급 시스템 + 27 유닛 로스터 + 미스틱 Model 3 확정 (ADR-0001)
- 사용자 결정: 모델 3 (4 슬롯 × 2종 출시), MA-2 옵션 B, 선형 스탯 + 등급별 분리 비용 곡선, P2W 톤업, 캐치업 방지
- 변경:
  - `design/systems-index.md` 신규 — 별등급/가챠/풀 예산 immutable 상수
  - `docs/architecture/adr-0001-star-rank-cost-curves.md` 신규 — 곡선 결정 근거
  - `docs/architecture/tr-registry.yaml` 신규 — TR-0001/TR-0002 등록
  - `design/gdd/units.md` 신규 — 27 유닛 (4C/5R/5E/5L/StableShield/8M) × 마일스톤 ★3/5/8/12
  - `production/epics/EPIC-032-balance-metrics-pipeline.md` 신규
  - `.claude/rules/testing-discipline.md` STAR-001~015, ECON-001~006, MYT-A~D-001, BAL-001~027, SYN-* 시나리오 추가
- 일반화: "수치 곡선은 systems-index.md immutable 상수, 코인 SO는 디폴트 사용 + 예외 시만 오버라이드. 곡선 변경은 ADR 신설 + EPIC-032 시뮬 게이트 통과 의무."

## 2026-04-29 — 커밋 메시지 컨벤션 신설
- 피드백: "커밋명 규칙 추가 [FEATURE], [BUGFIX]. [UI], [DB], [ASSET] 등의 prefix 활용. 제목+내용 모두 한글로"
- 원인: 컨벤션 부재 — 영문 conventional 형식 (`feat(sim):`)이 혼용되었고 AI 에이전트가 임의 형식으로 커밋.
- 변경: `rules/commit-conventions.md` 신규 (prefix 카탈로그 13종, 제목·본문 규칙, 예시, 다중 변경 분리 원칙). `constitution.md`에 "커밋 메시지" 섹션 추가하여 최상위 규칙으로 승격.
- 일반화: "사용자가 보는 모든 산출물(커밋 메시지 포함)은 한글이 1차"; "prefix 분류는 변경 카테고리 자가 점검을 강제하여 다중 카테고리 혼합 커밋을 자연스럽게 분리".

## 2026-04-29 — 패시브 시뮬 후크 + 데이터 와이어링 + 디자인 결정 (대규모 세션)

### 시뮬 통합 (10 패시브 후크 완료)
- **AuraAttackSpeedBuff** (P0.3): 인접 코인 공속 곱연산 buff. EffectiveAttackSpeed.
- **CritBuff**: 공격 시 RNG 굴림 → 데미지 ×CritMultiplier. Crit-free 코인은 RNG 미소비 → parity vector 유지.
- **DotApply**: per-enemy DOT 스택 (FIFO 캡), TickDots 프레임당 1회. EnemyState에 List<DotStack>.
- **SlowDebuff**: per-enemy 속도 디버프, 강한 magnitude/늦은 expire 우선. EffectiveMoveSpeed.
- **AuraAttackPowerBuff**: 인접 코인 공격력 곱연산 buff. EffectiveDamage.
- **InstantKillChance**: 공격 시 RNG 굴림 → HP=0. 보스 면역(IncludeBoss=false 시 RNG 미소비).
- **RootChance**: RNG 굴림 → magnitude=1.0 슬로우. SlowMagnitude/SlowExpire 슬롯 재사용.
- **RampOnSameTarget**: 코인별 LastTargetEnemyId + RampBonusRaw. 데미지 가산.
- **BounceAttack**: 추가 적 N마리에 감쇠 데미지 체인. FindNearestEnemyExcluding.
- **PierceAttack**: 추가 적 N마리에 감쇠 없는 풀 데미지 (또는 unlimited).

공통 원칙:
- **결정론**: 모든 RNG는 XorShift64 흐름. RNG 미소비 케이스 명시 (보스 면역, 미장착, chance=0).
- **Hash invariance**: opt-in segment 패턴 (`,dots:`, `,slow:`, `,ramp:`, `,star:`) — 사용 안 한 코인/적의 hash format 변경 없음. 기존 parity vector 유지.
- **HandleEnemyKill 헬퍼**: gold + boss bonus + KillCount 단일 출처. TickCombat·TickDots·Bounce·Pierce 4곳 호출.

### 데이터 와이어링 (28 코인 / 18 패시브 .asset)
- 기존 10 코인 wired (BNBancer/ChainLinker/DogWifChef/HederaHash/FrogPepe/XRipple/TonRocket/WLDOrb/MoneroMask/BitKnight)
- 신규 8 코인 wired (LightSilver/FlokiViking/TronTraffic/EthSage/SolWiz/HyperBitKing/CritLordWif/BittensorOmni) — 기존 패시브 클래스 재활용
- 잔여 10 코인 (CashFork/NearByte/MoonShibe/MemeMint/StableShield/GenesisCore/OracleEye/TerraTombstone/BitConnectMax/ChronoFetch)는 신규 패시브 클래스 필요 → ADR-0002 로드맵

### 디자인 결정 (ADR-0002 / TR-0003)
- **SO 인스턴스 = 튜닝 단위** (`Passive_<Type>_<CoinName>.asset`). 같은 클래스의 다른 .asset이 인스턴스별 다른 수치.
- **코인당 다중 패시브 허용** (서로 다른 클래스). 같은 클래스 두 인스턴스는 first-wins.
- **★별 잠금해제·곡선 — 코인별 자유**. 단일 글로벌 정책 강제하지 않음 (post-MVP `_milestones` 데이터 입력).
- **액티브 슬롯 데이터 예약** (`ActiveSkillBase[] _activeSkills` + `IActiveSkill` 추상). MVP 시뮬 미호출.

### 데이터 + 인프라
- **CoinInstance.Star** 추가 (default 1, clamp <1 to 1). FinalStateHash opt-in segment.
- **DpsDistributionRunner** (EPIC-032): N-시드 분포 측정 + JSON 산출 (overflow-safe Q16.16 평균).
- **DpsDistributionMenu**: 7 시나리오 메뉴 (synthetic + 4 solo + 2 synergy). Solo 결과 — BitKnight 42.6 / DogWifChef 15.8 / CritLordWif 117.6 / HyperBitKing 89.4 DPS. SYN-001 +46.7%, SYN-002 +94%.
- **balance-bands.json**: passive_ranges (10 클래스 × 등급 × 필드) + synergy_pairs (SYN-001..010 스켈레톤).
- **BitKnight ATK 25 → 35** (DPS 12.5 → 17.5). ECON-005 worst-case 강화 (SolWiz 대신 BitKnight로 단언).
- **밴드 좁히기**: Common ★50 max 30→18, Legendary ★1 min 6→15.

### 일반화 (헌법급 원칙)
- "패시브·이펙트 추가 시 RNG·Hash 보존 패턴" — 사용 안 한 케이스에 비용 0 (early return), opt-in segment 로 hash 호환.
- "kill-reward 단일 출처 (HandleEnemyKill)" — 데미지 경로 추가 시 복붙 금지.
- "데이터 와이어링은 SerializedObject 기반 Editor 자동화" — YAML 직접 편집 회피.
- 437/437 EditMode 테스트 통과 (시작 점 ~360 → 종료 437).

## 2026-05-01 — 환경 의존 값 하드코딩 금지 (CoinDefense MCP host 사건)
- 피드백: "db 127.0.0.1:55432가 아니라 58.123.57.182:55432"
- 원인: AI 가 환경 통념(dev=localhost)을 데이터 검증 없이 코드에 박음. 같은 `.env.dev` 의 `DISCORD_REDIRECT_URI`/`OPS_ADMIN_PUBLIC_URL` 에 실제 dev 호스트(`58.123.57.182`)가 노출돼 있었으나 인접 단서 무시. 헌법 §"금지 사항"의 시크릿 톤 규칙은 host/port 등 비-시크릿 환경 값을 충분히 커버하지 못했고, `tools/`·`scripts/` 등 server/ 외 경로는 사실상 사각지대.
- 변경:
  - `rules/environment-config.md` 신규 생성 — Why / 금지 / 허용 패턴 / PR 검증 절차 / 인접 단서 체크리스트 / 환경 분리 작업 의무 6 섹션. 적용 범위: Unity 클라·서버·도구·스크립트·MCP·CI 전 경로.
  - `constitution.md` §"금지 사항" 에 "환경 의존 값(host/port/url/endpoint/credential/path) 하드코딩 금지" 한 줄 추가 + `rules/environment-config.md` 포인터.
  - `rules/coding-standards.md` 상단에 "전 경로 적용 — 환경 설정" 포인터 추가.
  - `rules/path-based/server.md` 상단에 "환경 의존 값 일반 규칙" 포인터 추가 (분석서가 지시한 `server-node.md` 는 디스크에 부재 — 활성 서버 규칙 파일에 추가).
- 일반화: "환경 의존 값(host/port/url/credential/path)은 환경 데이터로 정의. 환경 통념 가정 금지, 데이터로 확인. `.env`·설정 파일의 인접 키를 일관성 단서로 활용 의무. 도메인 한정(코인 데이터-드리븐) 원칙을 환경 설정으로 확장 — 모든 환경 의존 값은 코드 외부에서 주입."

## 2026-05-01 — EPIC-039 별 마일스톤 액티브/★8 v8 데이터 규약
- 피드백: "EPIC-039 Star Milestone + Active 병렬 작업에서 asset/data/docs Worker는 코드 분기 없이 안전한 와이어링과 문서 갱신만 수행"
- 원인: 액티브 스킬과 ★8 패시브 변형은 데이터 와이어링 작업이지만, 선행 C# 타입과 `StarUnlockTier` 선택 규칙이 없으면 Unity asset을 먼저 만들 수 없음. 잘못 진행하면 누락된 MonoScript 참조나 의미 없는 v8 asset이 생김.
- 변경: `docs/architecture/tr-registry.yaml` 에 TR-039 등록, `design/gdd/units.md` 에 EPIC-039 ★5 액티브 5종/★8 v8 패시브 4종/★12 데이터 규약 추가.
- 일반화: "SO asset 와이어링은 대상 ScriptableObject 타입과 선택 규칙이 존재한 뒤 수행한다. 의존 코드가 없을 때는 문서·추적성만 갱신하고 asset 생성은 보류한다."

## 2026-04-28 — server-node.md 신설, server.md(Python/Flask) deprecated
- 피드백 없음 (CoinDefense 프로젝트 시작 시점 결정)
- 변경: `rules/path-based/server-node.md` 신규 생성 (Node 20 + Fastify + TS + Drizzle + Zod + vitest 표준), `path-based/server.md` 상단에 deprecation 배너
- 일반화: "스택 변경 시 신규 규칙 + 기존 규칙 deprecation 명시 의무"
