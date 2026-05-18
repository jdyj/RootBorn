# EPIC-001: SPUM 캐릭터 통합

## 상태

Draft

## 추적

- ADR: `docs/architecture/adr-0001-spum-character-integration.md`
- TR-ID: `TR-0001`
- 조사: `docs/research/spum-integration-feasibility.md`

## 목표

SPUM 1.8.8 캐릭터를 ROOTBORN Player/NPC 외관으로 통합할 수 있도록 Addressables 이관, 데이터 기반 외관 정의, Player visual adapter, 실제 플레이 경로 테스트를 구축한다.

## 배경

SPUM은 조립형 픽셀 캐릭터와 Animator 기반 애니메이션을 제공하지만 `Resources.Load*`와 SPUM 전용 저장 경로에 의존한다. ROOTBORN은 Addressables, ScriptableObject 데이터, Player 대리 PlayMode 테스트를 헌법 규칙으로 삼고 있으므로, SPUM 원본 흐름을 그대로 런타임에 넣을 수 없다.

## 범위

포함:

- SPUM 출력 prefab/clip/sprite의 Addressables 이관 설계와 자동화.
- Player root를 유지한 SPUM visual child adapter.
- `NpcDefinition` 기반 SPUM NPC 외관 연결.
- `ToolDefinition` 또는 외관 mapping SO 기반 도구 슬롯/공격 애니메이션 연결.
- PPU/scale 기준 검증.
- EditMode/PlayMode 회귀 테스트.

제외:

- SPUM Manager UI 전체를 ROOTBORN 런타임 UI로 편입.
- SPUM 원본 `Resources.Load*`를 ROOTBORN 런타임 예외로 허용.
- NFT, asset pack 재배포, AI/ML 학습 데이터 사용.

## 성공 기준

- ROOTBORN 런타임에 신규 `Resources.Load*` 호출이 없다.
- SPUM Player visual이 실제 키보드 이동, 좌클릭 공격, Control/E/Space 상호작용 흐름을 깨지 않는다.
- StoneAxe/StonePickaxe 장착 외관이 데이터 매핑으로 반영된다.
- `NpcDefinition` 외관 데이터만 바꿔 SPUM NPC를 생성할 수 있다.
- 기존 CharacterPart/Player/Town 핵심 PlayMode 회귀 테스트가 통과한다.
- PPU/scale 비교 스크린샷 근거가 남는다.

## 스토리 초안

### STORY-001: SPUM 출력물 Addressables 이관

SPUM 원본 패키지에서 저장된 캐릭터 prefab, AnimationClip, sprite 파츠를 ROOTBORN 관리 경로로 복사하고 Addressables 그룹에 등록한다.

검증:

- EditMode: 이관 결과가 `CharacterVisuals` 그룹에 등록된다.
- EditMode: ROOTBORN 런타임 코드에 신규 `Resources.Load*`가 없다.
- EditMode: 이관된 외관 SO가 registry 또는 명시적 Addressables address를 가진다.

### STORY-002: Player SPUM visual adapter

기존 Player root와 `PlayerController`/`GatherInteractor`를 유지하고, SPUM visual child가 이동/방향/공격/도구 외관만 반영하게 한다.

검증:

- PlayMode: 실제 키보드 입력으로 Player가 이동하고 SPUM visual이 MOVE/flip 상태를 반영한다.
- PlayMode: 실제 좌클릭 공격이 SPUM ATTACK을 재생하고 `GatherInteractor` 상호작용을 1회 발생시킨다.
- PlayMode: StoneAxe/StonePickaxe 장착 변경이 SPUM 무기 슬롯에 반영된다.

### STORY-003: NPC SPUM 외관 데이터화

`NpcDefinition`이 외관 SO를 참조하고, SPUM 외관도 Pixelwood 외관과 같은 데이터 경계로 생성되게 한다.

검증:

- EditMode: NPC ID별 C# 분기 없이 외관 SO 참조만 존재한다.
- PlayMode: SPUM NPC가 Town에서 생성되고 대화/상호작용 경로가 기존과 동일하게 작동한다.

## 리스크

| 리스크 | 대응 |
|---|---|
| SPUM Animator state와 ROOTBORN motion/action 불일치 | adapter 단위 테스트와 실제 입력 PlayMode 테스트 작성 |
| PPU 32와 Pixelwood PPU 49 크기 차이 | visual child scale PoC와 importer PPU 비교 후 기준 확정 |
| Resources 예외 확산 | CI/테스트 gate로 런타임 `Resources.Load*` 차단 |
| 도구/NPC별 코드 분기 | SO mapping과 entity branching CI 유지 |
| 에셋 라이선스 오해 | README/EULA 링크와 금지 용도 문서화 |

## 테스트 계획

- EditMode: Resources usage gate, Addressables registration, visual mapping SO validation.
- EditMode: Animator state mapping과 tool visual mapping 단위 테스트.
- PlayMode: Player keyboard move, mouse attack, keyboard interact, equipped tool switch.
- PlayMode: SPUM NPC spawn + dialogue interaction.
- Visual evidence: Player/NPC isolated screenshot 또는 PlayMode screenshot으로 크기/피벗 검증.

## 성능 고려

SPUM은 다중 SpriteRenderer와 AnimatorOverrideController를 사용할 수 있으므로, 대량 NPC 적용 전 캐릭터 수별 렌더러 수, Animator 수, Addressables preload 메모리를 측정해야 한다. Town 화면에 여러 NPC가 나올 경우 visual pooling, Addressables group preload, inactive visual reuse를 우선 검토한다.
