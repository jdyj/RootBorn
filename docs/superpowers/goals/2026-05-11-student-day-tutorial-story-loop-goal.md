# Student Day Tutorial Story Loop Goal Prompt

아래 프롬프트를 goal 세션에 그대로 붙여 넣고 진행한다.

```text
ROOTBORN Unity 프로젝트에서 "하루 루프를 튜토리얼/스토리 단계와 연결해 1일차 활동과 하루 종료가 2일차 안내, NPC 대사, 다음 목표 해금으로 이어지는 초반 진행 구조"를 설계/구현/검증해줘.

배경:
- 학생 하루 루프와 하루 결과 UI는 실제 플레이 경로로 동작한다.
- DayEndRuleDefinition은 Registry 기반 데이터 구조로 승격되어 있어야 한다.
- 하루 결과 UI는 학생 활동, 퀘스트, 보상, 인벤토리 변화를 표시할 수 있어야 한다.
- 이제 하루가 지났다는 사실이 다음 날의 안내/대사/목표 변화로 이어져야 플레이 동기가 생긴다.

최우선 검증 원칙:
- 튜토리얼/스토리 단계 변경은 실제 플레이 경로로 검증한다.
- 테스트에서 TutorialStageId나 StoryStageId를 직접 세팅하고 UI만 확인하면 실패다.
- NPC 대사/목표/활동 해금은 데이터 기반이어야 하며 stageId별 C# 분기는 금지한다.

절대 규칙:
- AGENTS.md와 `.claude/rules/*`를 따른다.
- `Assets/**/*.cs`는 디스크 직접 쓰기 금지. C# 신규/수정은 반드시 Unity MCP `script-update-or-create` 또는 Unity Editor 안전 경로를 사용한다.
- tutorialStageId, storyStageId, npcId, activityId, questId별 `if/switch` 분기, enum 기반 엔티티 분기, 엔티티별 C# 클래스 생성은 금지한다.
- 튜토리얼/스토리 단계, 대사 조건, 다음 목표 해금은 ScriptableObject 데이터와 전략 배열로 표현한다.
- TDD로 진행한다. 먼저 실패하는 테스트를 만들고 실패를 확인한 뒤 구현한다.
- 기존 dirty 변경 되돌리기 금지.

목표 플레이 흐름:
1. 저장 슬롯 UI로 새 게임을 시작해 Town에 진입한다.
2. 1일차 안내 또는 NPC 대사가 현재 튜토리얼/스토리 단계 기준으로 표시된다.
3. 플레이어가 실제 이동/상호작용으로 학생 활동 또는 퀘스트 목표를 수행한다.
4. 실제 이동으로 하루 종료 지점에 접근해 상호작용 입력으로 하루를 종료한다.
5. 하루 결과 UI에 튜토리얼/스토리 단계 변화 또는 다음 목표 안내가 표시된다.
6. 결과 UI 버튼으로 다음 날을 시작한다.
7. 2일차 Town에서 NPC 대사, 안내 UI, 해금된 활동/목표가 새 단계 기준으로 바뀐다.
8. 저장 슬롯 UI로 같은 슬롯을 다시 로드해도 튜토리얼/스토리 단계와 2일차 안내가 유지된다.
9. 같은 날 결과를 다시 열거나 저장/로드 후 재호출해도 단계가 중복 상승하지 않는다.

권장 구조:
- `TutorialStageDefinition` 또는 기존 스토리/대화 SO 구조를 먼저 확인하고 재사용한다.
- 단계 전이는 `TutorialStageAdvanceRule`, `StoryStageAdvanceRule`, `DialogueCondition` 같은 일반 전략으로 표현한다.
- 하루 종료 규칙 또는 결과 요약 규칙에 단계 전이 전략을 배열로 연결한다.
- NPC/안내 UI는 현재 stageId를 직접 분기하지 말고 조건을 만족하는 SO 데이터를 선택한다.
- 결과 UI는 "다음 목표"와 "새 안내"를 도메인 요약 모델로만 표시한다.

먼저 확인할 파일:
- `Assets/Scripts/Game/StudentLife/StudentLifeCore.cs`
- `Assets/Scripts/UI/StudentLife/StudentDayResultPanel.cs`
- `Assets/Scripts/UI/StudentLife/StudentDayRuntimeInstaller.cs`
- `Assets/Scripts/Game/Quests/`
- `Assets/Scripts/Game/World/`
- `Assets/Scripts/UI/Quests/`
- `Assets/Data/StudentLife/`
- `Assets/Data/Dialogue/`
- `Assets/Data/Story/`
- `Assets/Data/NPCs/`
- `Assets/Tests/PlayMode/EndToEnd/StudentDayResultLoopE2EScenarioTests.cs`
- `Assets/Tests/PlayMode/EndToEnd/StudentFullProgressionE2EScenarioTests.cs`

필수 테스트:
- EditMode: 단계 전이 조건과 효과가 SO 전략 배열로 실행되고, stageId별 C# 분기가 없다.
- EditMode: 같은 하루 결과를 재적용해도 튜토리얼/스토리 단계가 중복 상승하지 않는다.
- PlayMode: 저장 슬롯 UI → 1일차 Town 안내 확인 → 실제 이동/활동/퀘스트 상호작용 → 하루 종료 → 결과 UI에서 다음 목표 확인 → 다음 날 버튼 → 2일차 안내/NPC 대사/목표 변화 확인.
- PlayMode: 같은 슬롯 재로드 후 2일차 안내와 단계 상태가 유지된다.
- PlayMode: 기존 학생 하루 루프 및 포탈/퀘스트/인벤토리 E2E가 계속 통과한다.

완료 조건:
- 하루 종료와 다음 날 시작이 튜토리얼/스토리 단계 전이와 연결된다.
- 2일차에 플레이어가 보는 안내, NPC 대사, 목표 또는 활동 해금이 실제로 달라진다.
- 단계 전이는 데이터/전략 기반이며 엔티티 ID별 분기가 없다.
- 저장/로드 후 단계와 안내가 유지되고 중복 전이가 발생하지 않는다.
- 관련 EditMode/PlayMode 테스트가 통과한다.
- `Scripts/ci/check-no-entity-id-branching.sh`가 통과한다.
- 최종 보고에는 수정 파일, 생성/수정한 SO 에셋, 실행한 테스트, 실제 플레이 검증 흐름, 통과/실패 결과, 남은 리스크를 포함한다.
```

