# ADR-0001: SPUM 캐릭터 통합 방식

## Status

Proposed

## Date

2026-05-14

## Last Verified

2026-05-14

## Decision Makers

ROOTBORN 개발자, AI 코드 에이전트

## Summary

SPUM 1.8.8 캐릭터를 ROOTBORN에 직접 `Resources` 기반 런타임 의존성으로 넣지 않고, Addressables로 이관된 외관 데이터와 Player/NPC visual adapter 뒤에서 사용한다. Player 이동/상호작용/도구 타이밍은 기존 ROOTBORN 시스템이 계속 소유한다.

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6000.3.13f1 |
| **Domain** | Animation / Character Visuals / Addressables / Input |
| **Knowledge Risk** | MEDIUM - Unity Asset Store EULA와 SPUM 마켓 표시 버전은 변경 가능 |
| **References Consulted** | `docs/research/spum-integration-feasibility.md`, Unity Asset Store Terms and EULA, SPUM local README |
| **Post-Cutoff APIs Used** | None in this ADR |
| **Verification Required** | Addressables load, PlayMode keyboard/mouse path, SPUM Animator state mapping, PPU/scale screenshot comparison |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | None |
| **Enables** | EPIC-001 |
| **Blocks** | SPUM Player/NPC implementation until Accepted |
| **Ordering Note** | 조사 문서와 PoC 테스트 설계 승인 후 코드 작업을 시작한다. |

## Traceability

| Field | Value |
|-------|-------|
| **TR-ID** | TR-0001 |
| **Registry** | `docs/architecture/tr-registry.yaml` |

## Context

### Problem Statement

ROOTBORN은 도시 생활 게임으로 전환 중이며, 캐릭터 외관을 Pixelwood 단일/레이어드 sprite에서 SPUM(Soonsoon Pixel Unit Maker) 캐릭터로 확장할 가능성을 검토한다. SPUM은 조립형 픽셀 캐릭터와 Animator 기반 애니메이션을 제공하지만, 저장/로드 방식이 `Resources`와 SPUM 전용 Editor 흐름에 강하게 결합되어 있다.

### Current State

현재 Player는 `PlayerController`가 New Input System 키보드/마우스 입력, flipX, 공격 타이밍을 관리한다. `GatherInteractor`는 Control/E/Space 및 좌클릭 공격 종료 시점의 `TriggerInteract`로 상호작용을 처리한다. 도구 외관은 Addressables sub-sprite와 `ToolDefinition.CharacterPartAnimationClip`로 연결된다.

SPUM은 `SPUM_Prefabs`가 `AnimatorOverrideController`를 만들고 `PlayerState`(`IDLE`, `MOVE`, `ATTACK` 등)에 대응하는 AnimationClip을 오버라이드한다. 저장 유닛과 애니메이션/스프라이트 로딩은 `Resources.Load*`를 사용한다.

### Constraints

- ROOTBORN 런타임 신규 `Resources.Load*` 사용 금지.
- 모든 게임 엔티티와 외관 선택은 ScriptableObject 데이터로 표현해야 하며, entity ID별 C# 분기 금지.
- `Assets/**/*.cs` 직접 디스크 쓰기 금지.
- Player/NPC 유저 여정은 실제 입력, 이동, 트리거, UI 경로 PlayMode 테스트로 검증해야 한다.
- Pixelwood Player는 PPU 49, SPUM 유닛 파츠는 주로 PPU 32로 확인되어 크기/피벗 검증이 필요하다.
- SPUM Standard Unity Asset Store EULA와 SPUM README의 NFT 금지 조건을 준수해야 한다.

### Requirements

- Player root의 `PlayerController`, `GatherInteractor`, `Rigidbody2D`, inventory, network input gate 계약을 유지한다.
- SPUM visual은 Addressables 또는 직렬화 참조로 로드한다.
- Player와 NPC 모두 같은 appearance data interface를 통해 외관을 적용한다.
- 도구 장착 외관과 공격 애니메이션은 `ToolDefinition`/외관 SO 매핑으로 표현한다.
- 구현 전 실패 테스트를 작성하고, PlayMode 테스트는 실제 입력 경로를 사용한다.

## Decision

SPUM은 ROOTBORN 런타임의 직접 의존성이 아니라 **이관된 외관 데이터와 visual adapter**로 통합한다. SPUM Editor/Manager는 제작 도구로만 취급하고, 게임 런타임은 `Resources.Load*`를 호출하지 않는다.

### Architecture

```text
SPUM package/editor output
        |
        v
SPUM migration tool (Editor, future)
        |
        +--> Addressables: CharacterVisuals group
        +--> ScriptableObjects: SpumAppearanceDefinition / tool visual mapping
        |
        v
ROOTBORN runtime
  PlayerController / GatherInteractor / PlayerInventory
        |
        v
  ICharacterVisualView
        |
        v
  SpumCharacterVisualView
        |
        v
  SPUM visual child + AnimatorOverrideController
```

### Key Interfaces

```csharp
public interface ICharacterVisualView
{
    void ApplyAppearance(CharacterAppearanceDefinition appearance);
    void SetMotion(Vector2 motion, Vector2 facing);
    void PlayAction(CharacterActionDefinition action);
    void ApplyEquippedToolVisual(ToolDefinition tool);
}
```

구현 시 실제 타입명은 ROOTBORN 네임스페이스와 기존 `CharacterPartComposer` 패턴에 맞춘다. 이 ADR은 인터페이스 경계를 설명하는 초안이며, 코드 작성은 별도 TDD 계획에서 확정한다.

### Implementation Guidelines

- Player root scale은 변경하지 않는다.
- SPUM `PlayerObj` 이동 로직은 사용하지 않는다.
- SPUM visual child의 scale 또는 importer PPU로 Pixelwood 기준 높이를 맞춘다.
- `PlayerController`의 공격 완료 시점과 `GatherInteractor.TriggerInteract` 계약을 유지한다.
- SPUM saved prefab 이름, `_code`, NPC ID, tool ID로 C# 분기하지 않는다.
- SPUM 원본 `Resources` 폴더는 런타임 빌드 의존성에서 제거하거나, 제작 전용 경로로 격리한다.

## Alternatives Considered

### Alternative 1: SPUM Resources 폴더 그대로 유지

- **Description**: SPUM 패키지를 프로젝트에 넣고 `Resources.Load*` 예외를 둔다.
- **Pros**: 초기 작업량이 가장 작다. SPUM 샘플 코드를 거의 그대로 쓸 수 있다.
- **Cons**: ROOTBORN Addressables 규칙 위반. 빌드 크기/메모리/로드 경로 추적이 어려워진다. 이후 NPC 확장 때 예외가 반복된다.
- **Estimated Effort**: 낮음.
- **Rejection Reason**: 헌법 준수도가 낮고 장기 유지보수 리스크가 크다.

### Alternative 2: Addressables 이관 레이어

- **Description**: SPUM 출력 prefab/clip/sprite를 ROOTBORN 관리 경로로 복사/등록하고 런타임은 Addressables와 SO만 사용한다.
- **Pros**: ROOTBORN 로딩 규칙, 데이터 드리븐 규칙, 테스트 전략과 맞는다. Player/NPC 공통 외관 경계를 만들 수 있다.
- **Cons**: Editor 마이그레이션 도구와 테스트가 필요하다. SPUM 업데이트 때 재이관 절차가 필요하다.
- **Estimated Effort**: 중간~높음.
- **Selection Reason**: 장기 통합에 필요한 규칙 준수와 확장성을 동시에 만족한다.

### Alternative 3: Player만 SPUM, NPC는 Pixelwood 유지

- **Description**: Player 외관만 SPUM으로 교체하고 NPC는 기존 Pixelwood/ROOTBORN 체계를 유지한다.
- **Pros**: 범위를 줄인 PoC로 빠르게 시각 검증할 수 있다.
- **Cons**: 스타일 혼재가 발생한다. Player 전용 예외가 생기기 쉽고 NPC 외관 데이터 모델을 미루게 된다.
- **Estimated Effort**: 중간.
- **Rejection Reason**: PoC 단계로는 가능하지만 최종 아키텍처로는 불충분하다.

## Consequences

### Positive

- ROOTBORN의 Addressables, SO 데이터, Player 입력/상호작용 계약을 유지한다.
- SPUM Player와 SPUM NPC를 같은 외관 데이터 경계로 확장할 수 있다.
- SPUM 원본 도구 업데이트와 ROOTBORN 런타임 코드를 분리한다.

### Negative

- 이관 도구와 visual adapter 작성 비용이 추가된다.
- SPUM Animator state와 ROOTBORN action/motion mapping을 별도로 테스트해야 한다.
- PPU/scale/피벗 검증을 시각 테스트로 반복해야 한다.

### Neutral

- 기존 Pixelwood 캐릭터는 즉시 제거하지 않고 회귀 비교 기준으로 남긴다.
- SPUM 원본 README/라이선스 파일은 법무/출처 추적용으로 보관한다.

## Risks

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|-----------|
| SPUM Animator 파라미터와 ROOTBORN 모션 상태 불일치 | Medium | High | adapter 단위 테스트와 PlayMode 이동/공격 테스트 추가 |
| PPU/피벗 보정으로 애니메이션 어긋남 | Medium | Medium | visual child scale PoC와 importer PPU 비교 스크린샷 테스트 |
| Resources 예외가 런타임에 확산 | Medium | High | `Resources.Load*` usage gate 추가 |
| 도구/NPC별 C# 분기 도입 | Medium | High | SO 매핑 테스트와 CI entity ID branching gate 유지 |
| SPUM 라이선스 오해 | Low | High | README/EULA 링크 보관, NFT/AI/asset pack 재배포 금지 명시 |

## Performance Implications

| Metric | Before | Expected After | Budget |
|--------|--------|---------------|--------|
| CPU frame time | Pixelwood 단일/레이어드 SpriteRenderer | SPUM 다중 SpriteRenderer + AnimatorOverrideController | Player/NPC 밀집 씬에서 16.6ms/frame 유지 |
| Memory | Pixelwood sheets Addressables cache | SPUM prefab/clip/sprite Addressables cache 증가 | CharacterVisuals group 단위 unload 가능 |
| Load Time | Bootstrap Addressables + selected sheets | SPUM visual group preload 필요 | Town 진입 전 preload 또는 lazy load |
| Network | 외관 save metadata 중심 | appearance SO id/skin id 동기화 필요 | raw sprite/clip 네트워크 전송 금지 |

## Migration Plan

1. 조사 문서와 ADR 승인.
2. 실패 테스트 작성: Resources gate, SPUM visual adapter mapping, Player PlayMode 이동/공격.
3. SPUM 출력 prefab/clip/sprite를 Addressables 그룹으로 등록하는 Editor 마이그레이션 도구 설계.
4. Player visual adapter PoC 작성. 기존 Player root와 interaction 계약 유지.
5. NPC `NpcDefinition` 외관 SO 연결 PoC 작성.
6. Pixelwood와 SPUM visual screenshot 비교 및 크기/피벗 기준 확정.

**Rollback plan**: SPUM visual adapter를 비활성화하고 기존 Pixelwood `CharacterPartComposer`/`CharacterPartAnimator` 경로를 유지한다. Addressables 이관 에셋은 별도 그룹으로 격리해 제거 가능하게 둔다.

## Validation Criteria

- [ ] ROOTBORN 런타임 코드에 신규 `Resources.Load*`가 없다.
- [ ] SPUM Player visual이 실제 키보드 이동에서 IDLE/MOVE/flip 상태를 반영한다.
- [ ] 실제 좌클릭 공격이 SPUM 공격 애니메이션을 재생하고 `GatherInteractor` 상호작용을 1회만 발생시킨다.
- [ ] StoneAxe/StonePickaxe 도구 외관이 SO 매핑으로 반영된다.
- [ ] `NpcDefinition` 외관 SO 변경만으로 SPUM NPC가 생성된다.
- [ ] Player/NPC visual이 1 world unit 기준에서 Pixelwood 대비 허용 오차 안에 있다.

## GDD Requirements Addressed

| GDD Document | System | Requirement | How This ADR Satisfies It |
|-------------|--------|-------------|--------------------------|
| `.claude/rules/game-design.md` | Town / Character | 도시 생활의 다양한 활동과 관계 확장을 지원 | Player/NPC 외관을 데이터로 바꿔 활동/관계 시스템과 분리한다. |
| `.claude/rules/path-based/assets-data.md` | Entity Data | 모든 엔티티는 SO 데이터로 표현 | SPUM 외관과 도구/NPC 매핑을 SO로 표현한다. |
| `.claude/rules/path-based/assets-addressables.md` | Asset Loading | 동적 로딩은 Addressables 사용 | SPUM 출력물을 Addressables로 이관한다. |
| `.claude/rules/testing-discipline.md` | Player Journey | 실제 입력/이동/상호작용 경로 테스트 | PlayMode 검증 기준을 ADR validation에 포함한다. |

## Related

- `docs/research/spum-integration-feasibility.md`
- `production/epics/EPIC-001-spum-character-integration.md`
- `docs/architecture/tr-registry.yaml`
- `Assets/Scripts/Game/Player/PlayerController.cs`
- `Assets/Scripts/Game/Player/GatherInteractor.cs`
- `Assets/Scripts/Game/Managers/ResourceManager.cs`
