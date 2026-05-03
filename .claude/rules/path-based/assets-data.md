# Assets/Data/** (ScriptableObject 데이터) 규칙

> **한국어 요약**: 게임 엔티티(작물·도구·자원·지식·특성·세대·상태)는 모두 SO. 코드 수정 없이 디자이너가 조정 가능해야 한다.

## 적용 경로
- `Assets/Data/**`
- `Assets/ScriptableObjects/**`

## 필수
- `[CreateAssetMenu(fileName, menuName = "Rootborn/...")]`
- `[SerializeField] private` + public getter
- 엔티티 상수는 systems-index.md 및 entities.yaml 과 일치 (/gs-consistency-check 검증)
- 변경 시 `/gs-balance-check` 로 밸런스 영향 검증
- 모든 SO는 `Assets/Data/Registry/GameDataRegistry.asset`에 등록 (Resources.Load 대용)

## 금지
- SO 안에서 MonoBehaviour 참조 (직렬화 깨짐 + 순환 의존)
- 런타임에 SO 필드 변경 (Immutable)

## 스토리 타입 매핑
- SO 수치만 변경 → Config/Data (/gs-smoke-check 만, 테스트 불필요)
- SO 스키마 변경 → Logic (유닛 테스트 필요)

## 엔티티 데이터-드리븐 절대 원칙

ROOTBORN의 **모든 게임 엔티티는 100% 데이터로 정의**된다. 시스템 코드는 절대 특정 엔티티를 알지 못해야 한다.

### 적용 대상
작물(Crop), 도구(Tool), 자원 노드(Resource), 레시피(Recipe), 지식 노드(Knowledge), 후계자 특성(Trait), 세대 프로필(Generation), 상태이상(Status), 가문 특성(FamilyTrait).

### 금지
- `if (crop.id == "Wheat") { ... }` — 특정 엔티티 ID 분기
- `switch (toolId)` / `switch (knowledgeId)` — 엔티티별 switch
- `class WheatController : CropController` — 엔티티별 C# 클래스
- `enum CropId { Wheat, Carrot ... }` / `enum ToolId { ... }` — 엔티티 ID enum (enum 값은 코드 파일이라 핫스왑 불가)

### 허용
- `[CreateAssetMenu] CropDefinition` SO + `GrowthBehaviorBase[]` 전략 SO 배열
- `ToolDefinition` SO + `ToolEffectBase[]` 전략 SO 배열
- `KnowledgeNode` SO + `KnowledgeTriggerBase[]` 전략 SO 배열
- 메카닉은 SO 필드(growthCurve, powerMultiplier, decayPerSecond 등)로 표현
- 신규 엔티티 추가 = SO 1개 생성 + GameDataRegistry 등록 (코드 X)

### 검증 (CI 게이트)
- 정적 분석: `Game/`, `Network/`, `UI/` 디렉토리에서 `cropId == "..."`, `switch(tool.id)`, `enum CropId` 같은 패턴 검출 시 PR 차단 (`Scripts/ci/check-no-entity-id-branching.sh`)
- 시나리오 테스트는 엔티티 ID로 검증해도 OK (테스트는 데이터 의존)

### 이유
- 엔티티 디자인이 후반에 바뀌어도 시스템·테스트 영향 0
- AI가 신규 엔티티 추가 시 코드 안 건드림 → 회귀 위험 0
- 출시 후 라이브 운영에서 신규 엔티티 = SO/Addressables 핫업데이트로 클라 업데이트 없이 배포 가능

## 엔티티 SO 와이어링 규약

### SO 인스턴스 = 튜닝 단위
- 클래스 = `Assets/Scripts/Game/{Domain}/{ClassName}.cs` (코드)
- 인스턴스 = `Assets/Data/{Domain}/{Type}_{Name}.asset` (튜닝 데이터)
- 같은 클래스의 다른 .asset이 **인스턴스별 다른 수치**를 가진다 (Inspector 입력만으로).

### 작명 규약 (예시)
- 작물: `Crop_Wheat.asset`, `Crop_Carrot.asset`
- 도구: `Tool_BareHand.asset`, `Tool_StoneAxe.asset`, `Tool_StoneHoe.asset`
- 지식: `Knowledge_StoneTool.asset`, `Knowledge_Fire.asset`
- 지식 트리거: `Knowledge/Triggers/Trigger_HitGroundWithRock.asset`
- 특성: `Trait_Hardy.asset`, `Trait_GreenThumb.asset`, `Trait_QuickLearner.asset`
- 세대: `Gen_Default.asset`, `Gen_StoneAge.asset`
- 상태: `Status_Hunger.asset`, `Status_Fatigue.asset`, `Status_Loneliness.asset`

### 와이어링 절차
1. Inspector 우클릭 → `Create → Rootborn/{Domain}/{Type}` → 새 .asset 생성 (또는 Editor 자동화 스크립트).
2. 수치 입력.
3. 상위 SO(예: `KnowledgeNode`)의 배열 필드에 드래그.
4. `GameDataRegistry.asset`에 새 엔티티 등록.
5. 일괄 와이어링은 `SerializedObject` 기반 Editor 스크립트 권장 (YAML 직접 편집 회피).

### 엔티티당 다중 전략
- `KnowledgeNode._triggers : KnowledgeTriggerBase[]` — 모두 만족 시 해금 (AND).
- `ToolDefinition._effects : ToolEffectBase[]` — 모두 적용 (체이닝).
- 같은 클래스 두 인스턴스는 평가 순서대로 합산.
- 누적 효과는 SO 필드 자체로 모델링 (예: `_powerMultiplier`를 더 높게 설정).
