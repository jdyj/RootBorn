# Multiplayer Direct Play Validation Goal Prompt

아래 프롬프트를 goal 세션에 그대로 붙여 넣고 진행한다.

```text
ROOTBORN Unity 프로젝트에서 "현재 멀티플레이가 실제 플레이 가능한 수준으로 동작하는지 Host/Client 또는 Dedicated Server/Client를 직접 실행해 검증하고, 학생 하루 루프/퀘스트/인벤토리/관계/컨디션 상태가 플레이어별로 분리·동기화되는지 확인"해줘.

핵심 질문:
- 지금 멀티플레이가 실제로 잘 되는가?
- 자동화 테스트만 있는가, 아니면 Host와 Client를 직접 실행해서 플레이 흐름을 검증했는가?
- 서버 권한, 클라이언트별 개인 상태 분리, 공유 월드 상태 동기화, 저장/로드/재접속이 실제 플레이 경로에서 깨지지 않는가?
- 두 명 이상의 플레이어가 같은 Town 씬에 동시에 존재하고, 각 플레이어의 인벤토리/퀘스트/학생 성장/관계/컨디션 상태가 독립적으로 움직이는가?

최우선 검증 원칙:
- 이 goal은 "직접 멀티플레이로 실행해 플레이하듯 검증"하지 않으면 완료로 인정하지 않는다.
- EditMode, 단일 프로세스 PlayMode, 도메인 API 직접 호출, NetworkObject 상태 강제 세팅만으로는 완료 근거가 될 수 없다.
- 최소 1개 이상의 검증은 실제 Host/Client 또는 Dedicated Server/Client 구성을 실행하고, 두 플레이어가 실제 입력/상호작용 경로를 거쳐야 한다.
- 자동화가 가능하면 멀티 프로세스 실행 테스트를 작성한다. 자동화가 어렵다면 수동 직접 플레이 검증 절차와 실행 로그/스크린샷/저장 파일/콘솔 로그를 남긴다.
- 최종 보고에는 "직접 플레이 검증을 했는지", "어떤 구성으로 실행했는지", "어떤 플레이어가 어떤 행동을 했는지", "서버/클라이언트 양쪽에서 무엇을 확인했는지"를 반드시 적는다.

금지:
- Network 테스트 유틸리티만 통과시키고 실제 Host/Client 실행 검증 없이 완료 보고 금지.
- Player 1/Player 2 상태를 테스트 코드에서 직접 주입한 뒤 동기화 성공으로 보고 금지.
- QuestLog, Inventory, StudentLifeProgress, RelationshipProgress, StatusProgress를 내부 API로 직접 변경하고 멀티플레이 성공으로 보고 금지.
- 씬을 강제 로드하거나 플레이어 Transform을 목적지로 직접 이동시킨 뒤 "실제 플레이 이동"으로 보고 금지.
- 서버와 클라이언트가 같은 저장 슬롯/플레이어 identity를 덮어쓰는 상태를 무시하고 완료 보고 금지.
- 실행 불가 또는 환경 문제를 숨기고 "테스트됨"이라고 보고 금지.

절대 규칙:
- AGENTS.md와 `.claude/rules/*`를 따른다.
- `Assets/**/*.cs`는 디스크 직접 쓰기 금지. C# 신규/수정은 반드시 Unity MCP `script-update-or-create` 또는 Unity Editor 안전 경로를 사용한다.
- 모든 게임 엔티티는 100% ScriptableObject 데이터 기반이어야 한다.
- playerId, clientId, questId, itemId, activityId, npcId, statusId별 `if/switch` 분기, enum 기반 엔티티 분기, 엔티티별 C# 클래스 생성은 금지한다.
- 멀티플레이 상태 변경은 서버 권한을 기준으로 확정하고, 개인 상태와 공유 월드 상태를 분리한다.
- 보상/인벤토리/퀘스트/관계/컨디션 변화는 저장/로드 및 재접속 후 재호출해도 중복 적용되면 안 된다.
- TDD로 진행한다. 먼저 실패하는 멀티플레이 검증 테스트 또는 재현 절차를 만들고 실패를 확인한 뒤 구현/수정한다.
- 기존 dirty 변경 되돌리기 금지.

검증 대상:
1. 접속
   - Host + Client 접속
   - 가능하면 Dedicated Server + 2 Clients 접속
   - 접속 실패, 타임아웃, scene sync 실패, NetworkObject spawn 실패 여부 확인
2. 플레이어 분리
   - Player 1과 Player 2가 같은 Town 씬에 동시에 보이는 별도 Player GameObject로 스폰된다.
   - Player 1과 Player 2가 별도 NetworkObject ownership, PlayerIdentity, save state를 가진다.
   - 각 플레이어는 독립된 Inventory, QuestLog, StudentLifeProgress, RelationshipProgress, StatusProgress를 가진다.
   - 한 플레이어의 학생 성장, 퀘스트, 인벤토리, 관계, 컨디션 변화가 다른 플레이어 개인 상태에 섞이지 않는다.
   - Client 1이 아이템을 얻거나 퀘스트를 수락/진행/완료/보상 수령해도 Client 2의 인벤토리/퀘스트/성장 상태는 변하지 않는다.
   - Client 2가 별도 행동을 수행하면 Client 2 상태만 변하고 Client 1의 개인 상태는 변하지 않는다.
3. 공유 월드
   - NPC, 자원, 포탈, 하루 종료 지점, 활동 오브젝트가 양쪽 클라이언트에서 일관되게 보인다.
   - 공유되어야 하는 월드 변화와 개인화되어야 하는 진행 상태가 구분된다.
   - 같은 자원 오브젝트를 공유 소모로 처리할지, 플레이어별 개인 진행으로 처리할지 데이터/규칙으로 명확히 정의하고 테스트에서 기대 동작을 확인한다.
4. 서버 권한
   - 클라이언트가 활동/퀘스트/보상/관계/컨디션 변경을 요청하면 서버가 검증하고 확정한다.
   - 클라이언트 로컬 조작만으로 보상이나 성장 상태를 확정할 수 없다.
5. UI 동기화
   - QuestLog UI, Inventory UI, StudentDayResultPanel, 관계/컨디션 UI 또는 결과 텍스트가 각 클라이언트의 상태를 올바르게 보여준다.
6. 저장/로드/재접속
   - Host/Server 종료 후 같은 saveSlot로 재시작했을 때 각 플레이어 상태가 복원된다.
   - Client 재접속 후 개인 진행 상태와 공유 월드 상태가 일관된다.

직접 플레이 검증 시나리오:
1. 서버 또는 Host를 실행한다.
2. Client 1을 실행해 접속한다.
3. Client 2를 실행해 접속한다.
4. 두 클라이언트가 같은 Town에 진입했는지 화면과 로그로 확인한다.
5. 두 클라이언트 화면에서 Player 1과 Player 2가 동시에 보이고 서로 다른 Player GameObject로 움직이는지 확인한다.
6. Client 1이 실제 이동 입력으로 NPC 또는 활동 오브젝트에 접근한다.
7. Client 1이 실제 상호작용 입력/UI 버튼으로 학생 활동, 퀘스트 수락, 자원 획득, 보상 수령 중 하나 이상을 수행한다.
8. Client 1의 UI와 도메인 상태에서 성장/퀘스트/인벤토리 변화가 반영되는지 확인한다.
9. Client 2의 UI와 도메인 상태에서 Client 1의 개인 성장/퀘스트/인벤토리 변화가 섞이지 않았는지 확인한다.
10. Client 2가 실제 이동 입력으로 별도 활동 또는 NPC 상호작용을 수행한다.
11. Client 2의 인벤토리/퀘스트/성장 변화는 Client 2에만 반영되고, Client 1 개인 상태는 변하지 않는지 확인한다.
12. Client 1이 실제 이동으로 하루 종료 지점에 접근해 하루 종료를 실행한다.
13. Client 1의 하루 결과 UI에 본인 활동/퀘스트/인벤토리/관계/컨디션 변화만 표시되는지 확인한다.
14. Client 2는 Client 1의 하루 결과 UI나 개인 정산을 강제로 공유받지 않는지 확인한다.
15. Client 1이 결과 UI 버튼으로 다음 날을 시작한다.
16. 서버/Host를 종료하고 같은 saveSlot로 재시작한다.
17. 두 클라이언트가 재접속했을 때 각자의 일차, 성장, 퀘스트, 인벤토리, 관계, 컨디션 상태가 올바르게 복원되는지 확인한다.

권장 자동화:
- PlayMode 또는 Unity Test Framework에서 NGO host/client 구성을 띄우는 테스트를 추가한다.
- 가능하면 별도 프로세스 기반 smoke test 스크립트를 만든다.
  - Host 또는 dedicated server 프로세스 실행
  - Client 2개 실행
  - 로그에서 접속, scene sync, player spawn, RPC/NetworkVariable 오류 여부 확인
  - 테스트 종료 시 프로세스 정리
- 자동화 테스트가 실제 입력 경로를 완전히 재현하지 못하면, 자동화는 보조 검증으로 두고 직접 플레이 체크리스트를 최종 근거로 포함한다.

먼저 확인할 파일:
- `docs/superpowers/goals/2026-05-08-student-career-life-multiplayer-goal.md`
- `docs/superpowers/specs/2026-05-08-student-career-life-design.md`
- `Assets/Scripts/Network/`
- `Assets/Scripts/Network/StudentLife/`
- `Assets/Scripts/Network/Quests/`
- `Assets/Scripts/Game/StudentLife/`
- `Assets/Scripts/Game/Quests/`
- `Assets/Scripts/Game/Player/`
- `Assets/Scripts/Game/Save/`
- `Assets/Scripts/Game/World/WorldPortal.cs`
- `Assets/Scripts/UI/MainMenu/SaveSlotSelectPanel.cs`
- `Assets/Scripts/UI/StudentLife/`
- `Assets/Scripts/UI/Quests/`
- `Assets/Tests/EditMode/StudentLife/`
- `Assets/Tests/EditMode/Quests/`
- `Assets/Tests/PlayMode/`
- `Assets/Tests/PlayMode/EndToEnd/`
- `Assets/Tests/PlayMode/Quests/`
- `Assets/Tests/PlayMode/TownConcept/`
- `ProjectSettings/`

필수 테스트/검증 항목:
- MULTI-DIRECT-001: Host + Client 접속이 실제 실행 구성에서 성공한다.
- MULTI-DIRECT-002: 가능하면 Dedicated Server + 2 Clients 접속이 실제 실행 구성에서 성공한다.
- MULTI-DIRECT-003: 두 클라이언트가 같은 Town 씬에 들어가고 각자 Player GameObject를 가진다.
- MULTI-DIRECT-003A: 두 클라이언트 화면에서 Player 1과 Player 2가 동시에 보이며, 각 플레이어는 별도 NetworkObject ownership과 PlayerIdentity를 가진다.
- MULTI-DIRECT-004: Client 1의 실제 NPC/활동/퀘스트 상호작용 결과가 서버 권한으로 확정된다.
- MULTI-DIRECT-005: Client 1의 학생 성장/퀘스트/인벤토리/관계/컨디션 상태가 Client 2 개인 상태에 섞이지 않는다.
- MULTI-DIRECT-006: Client 2의 별도 상호작용도 Client 1 개인 상태를 오염시키지 않는다.
- MULTI-DIRECT-006A: Client 1이 인벤토리 아이템을 얻거나 퀘스트 보상을 받아도 Client 2의 Inventory UI와 QuestLog UI는 변하지 않는다.
- MULTI-DIRECT-006B: Client 2가 별도 아이템 획득/퀘스트 진행을 수행하면 Client 2의 Inventory UI와 QuestLog UI만 변한다.
- MULTI-DIRECT-007: 공유 월드 상태는 필요한 범위에서 양쪽 클라이언트에 일관되게 보인다.
- MULTI-DIRECT-008: Client 1 하루 종료 결과 UI는 Client 1의 하루 결과만 표시한다.
- MULTI-DIRECT-009: Client 2는 Client 1의 하루 결과 정산을 강제로 받지 않는다.
- MULTI-DIRECT-010: 서버/Host 재시작 및 Client 재접속 후 각 플레이어 상태가 복원된다.
- MULTI-DIRECT-011: 보상/인벤토리/퀘스트/관계/컨디션 변화가 재접속 후 중복 적용되지 않는다.
- MULTI-DIRECT-012: 기존 단일 플레이 학생 하루 루프, 퀘스트/인벤토리/포탈 E2E가 계속 통과한다.

테스트 작성 기준:
- 멀티플레이 직접 검증은 실제 실행 명령, 포트, saveSlot, 접속 IP, 플레이어 수를 기록한다.
- 각 클라이언트의 화면/UI 관찰 결과와 서버 로그를 함께 확인한다.
- 실패 시 "접속 실패", "씬 동기화 실패", "Player spawn 실패", "Client 1 상태가 Client 2에 섞임", "서버 권한 없이 클라이언트가 상태 확정", "재접속 후 상태 손실", "중복 보상 지급"처럼 어느 단계가 끊겼는지 알 수 있게 보고한다.
- 자동화 테스트는 가능한 한 실제 입력/상호작용 경로를 사용한다.
- 내부 API 직접 호출 테스트는 보조 검증으로만 인정한다.

완료 조건:
- 실제 Host/Client 또는 Dedicated Server/Client 구성으로 직접 실행한 멀티플레이 검증 결과가 있다.
- 최종 보고에 "직접 플레이 검증 완료/미완료"가 명시되어 있다.
- 직접 플레이 검증을 완료했다면 실행 구성, 명령, saveSlot, 포트, 접속 순서, 수행 행동, 서버/클라이언트 확인 결과를 보고한다.
- 직접 플레이 검증을 완료하지 못했다면 완료로 보고하지 않고, 실패한 단계와 남은 리스크를 명시한다.
- 서버 권한, 플레이어별 상태 분리, 공유 월드 동기화, UI 표시, 저장/로드/재접속이 검증된다.
- 관련 EditMode/PlayMode/Network 테스트가 통과한다.
- 단일 플레이 학생 하루 루프와 기존 퀘스트/인벤토리/포탈 E2E가 깨지지 않는다.
- `Scripts/ci/check-no-entity-id-branching.sh`가 통과한다.
- 최종 보고에는 수정 파일, 실행한 테스트, 직접 플레이 검증 로그/증거, 통과/실패 결과, 남은 리스크를 포함한다.
```
