# Day End Rule Registry Goal Prompt

아래 프롬프트를 goal 세션에 그대로 붙여 넣고 진행한다.

```text
ROOTBORN Unity 프로젝트에서 "하루 종료/회복/다음 날 진입 규칙을 런타임 생성 의존에서 실제 ScriptableObject 에셋 + GameDataRegistry 기반 데이터 구조로 승격"해줘.

배경:
- 현재 학생 하루 루프는 실제 이동/상호작용/결과 UI/다음 날 시작 E2E가 동작한다.
- 다만 DayEndRuleDefinition과 회복 전략이 런타임 설치 성격으로 생성되어, AGENTS.md의 SO 와이어링 규약과 장기 튜닝 구조에는 아직 약하다.
- 다음 작업에서 퀘스트/인벤토리/튜토리얼 하루 결과를 붙이기 전에 하루 종료 규칙의 데이터 진입점을 먼저 안정화한다.

최우선 검증 원칙:
- 규칙이 실제 에셋과 Registry에서 로드되는지 테스트로 증명한다.
- 테스트에서 DayEndRuleDefinition을 직접 new/CreateInstance로만 만들어 통과시키고 완료 보고하지 않는다.
- 실제 Town 하루 종료 상호작용 E2E가 새 Registry 기반 규칙을 사용해야 한다.

절대 규칙:
- AGENTS.md와 `.claude/rules/*`를 따른다.
- `Assets/**/*.cs`는 디스크 직접 쓰기 금지. C# 신규/수정은 반드시 Unity MCP `script-update-or-create` 또는 Unity Editor 안전 경로를 사용한다.
- 모든 게임 엔티티와 튜닝 단위는 ScriptableObject 데이터 기반이어야 한다.
- dayRuleId, activityId, sceneName별 `if/switch` 분기, enum 기반 엔티티 분기, 엔티티별 C# 클래스 생성은 금지한다.
- TDD로 진행한다. 먼저 실패하는 EditMode/PlayMode 테스트를 만들고 실패를 확인한 뒤 구현한다.
- 기존 dirty 변경 되돌리기 금지.

목표:
1. 하루 종료 규칙 에셋을 `Assets/Data/StudentLife/` 또는 명확한 하위 폴더에 생성한다.
2. `GameDataRegistry.asset`에 하루 종료 규칙 컬렉션을 등록한다.
3. Town의 하루 종료 지점은 런타임 하드코딩 대신 Registry에서 기본 하루 종료 규칙을 가져온다.
4. 에너지 회복, 집중도 회복, 다음 날 진입점 같은 값은 에셋에서 튜닝 가능해야 한다.
5. 저장/로드 후 같은 날 결과를 다시 열어도 중복 정산되지 않아야 한다.
6. 기존 학생 하루 루프 PlayMode E2E와 포탈/퀘스트/인벤토리 E2E가 깨지지 않아야 한다.

먼저 확인할 파일:
- `Assets/Scripts/Game/StudentLife/StudentLifeCore.cs`
- `Assets/Scripts/UI/StudentLife/StudentDayRuntimeInstaller.cs`
- `Assets/Scripts/Game/Data/GameDataRegistry.cs` 또는 Registry 관련 클래스
- `Assets/Data/Registry/GameDataRegistry.asset`
- `Assets/Data/StudentLife/`
- `Assets/Tests/EditMode/StudentLife/StudentDayProgressTests.cs`
- `Assets/Tests/PlayMode/EndToEnd/StudentDayResultLoopE2EScenarioTests.cs`

필수 테스트:
- EditMode: Registry에 등록된 기본 DayEndRuleDefinition을 찾을 수 있다.
- EditMode: Registry 기반 DayEndRuleDefinition이 에너지/집중도/다음 날 진입점을 적용한다.
- EditMode: DayEndRuleDefinition이 없을 때 명확한 실패 또는 안전한 비활성 상태가 된다.
- PlayMode: 저장 슬롯 UI로 Town 진입 → 실제 이동 → 학생 활동 상호작용 → 실제 이동 → 하루 종료 상호작용 → 결과 UI → 다음 날 버튼 흐름이 Registry 규칙으로 통과한다.
- PlayMode: 같은 슬롯 재로드 후 ResultReady 상태에서 하루 종료 지점 재상호작용 시 중복 회복/로그 증가가 발생하지 않는다.

완료 조건:
- 하루 종료 규칙이 실제 SO 에셋과 GameDataRegistry를 통해 와이어링된다.
- Town 런타임 설치 코드는 엔티티별 분기 없이 Registry 기본 규칙을 사용한다.
- 관련 EditMode/PlayMode 테스트가 통과한다.
- `Scripts/ci/check-no-entity-id-branching.sh`가 통과한다.
- 최종 보고에는 수정 파일, 생성/수정한 에셋, 실행한 테스트, 통과/실패 결과, 남은 리스크를 포함한다.
```

