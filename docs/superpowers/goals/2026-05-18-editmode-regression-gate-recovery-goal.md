# EditMode Regression Gate Recovery Goal Prompt

아래 프롬프트를 goal 세션에 그대로 붙여 넣고 진행한다.

```text
ROOTBORN Unity 프로젝트에서 현재 브랜치의 전체 EditMode 회귀 게이트를 복구해줘.

이번 goal의 목적:
- `MainMenu` 실제 포인터 입력, Boot, Town, House, SPUM 주요 PlayMode 축은 최근 보강되었다.
- 다음 병합 리스크는 전체 EditMode 스위트의 실패다.
- 최신 상태에서 전체 EditMode 테스트를 다시 실행하고, 실패를 분류한 뒤, 이번 브랜치 병합을 막는 회귀/누락/게이트 실패를 선별적으로 복구한다.

핵심 성공 기준:
- 최신 전체 EditMode 결과를 먼저 RED로 재현하고 실패 목록을 실제 출력 기준으로 분류한다.
- 실패를 원인군별로 나눈다:
  - Modern UI sprite/subsprite/addressable 누락
  - Modern Interiors asset/slice 누락
  - Registry 후보/데이터 등록 누락
  - static runtime usage gate 위반
  - default flow/source audit 실패
  - 이번 goal과 무관한 기존 dirty/untracked 상태에서 비롯된 실패
- 이번 goal 범위에 해당하는 실패는 실제 원인을 고쳐 GREEN으로 만든다.
- 프록시 신호만으로 완료 처리하지 않는다. 전체 EditMode 재실행 또는 명확한 필터 재실행으로 실패가 사라졌는지 증명한다.

필수 사전 조사:
- `Assets/Tests/EditMode/`
- `Assets/Tests/EditMode/TownConcept/TownDefaultFlowSourceAuditTests.cs`
- `Assets/Tests/EditMode/UI/Modern/`
- `Assets/Tests/EditMode/StudentLife/*Registry*Tests.cs`
- `Assets/Tests/EditMode/*Static*Tests.cs`
- `Assets/AddressableAssetsData/`
- `Assets/Data/Registry/GameDataRegistry.asset`
- `Assets/Data/{StudentLife|Careers|Quests|DiscoveryClues|WorldState|Housing}/`
- `Assets/modernuserinterface-win/`
- `Assets/moderninteriors-win/`
- `.claude/rules/testing-discipline.md`
- `.claude/rules/ui-standards.md`
- `.claude/rules/path-based/assets-data.md`

진행 규칙:
- AGENTS.md와 `.claude/rules/*`를 따른다.
- `Assets/**/*.cs`는 shell, echo, cat, apply_patch 같은 외부 디스크 직접 쓰기로 수정하지 않는다. C# 신규/수정은 반드시 Unity MCP `script-update-or-create` 또는 Unity Editor 안전 경로를 사용한다.
- 기존 dirty 변경을 되돌리지 않는다. 이번 goal과 직접 관련 없는 변경은 건드리지 않는다.
- TDD/RED-GREEN 원칙을 따른다. 먼저 실패를 재현하고, 원인을 좁힌 뒤 수정한다.
- 모든 게임 엔티티는 ScriptableObject 데이터 기반을 유지한다. entity id별 `if/switch`, enum 기반 엔티티 분기, 엔티티별 C# 클래스 추가는 금지한다.
- UI/Modern UI 관련 수정은 Style2 공통 패널 규약을 깨지 않는다.
- Addressables/asset/meta 변경은 `.meta`와 함께 선별 stage/commit한다.

우선 재현:
- Unity MCP `tests-run`으로 전체 EditMode를 실행한다.
- 가능하면 CLI batchmode EditMode도 실행한다:
  - `-runTests -testPlatform editmode`
- 실패 개수, 테스트 클래스, 메시지, stack trace 핵심 라인을 문서나 최종 보고에 정리한다.

권장 처리 순서:
1. 전체 EditMode 실패 최신화
   - 이전에 관찰된 `769 passed / 19 failed`는 참고값일 뿐이다.
   - 반드시 현재 브랜치에서 다시 실행해 최신 실패 목록을 만든다.

2. 실패 분류
   - 에셋 누락
   - Registry/SO wiring 누락
   - 코드 source audit/gate 위반
   - 테스트 자체가 현재 실제 흐름을 잘못 가정한 경우
   - 이번 goal 밖의 대규모 기존 dirty/untracked 변경 영향

3. 빠른 차단 실패부터 복구
   - Modern UI sprite/subsprite/addressable missing
   - Modern Interiors asset/slice missing
   - Registry 후보 누락
   - 단순 누락이 명확한 `.meta`/Addressable/Registry 연결

4. 코드 게이트 실패 복구
   - static runtime usage gate
   - default flow/source audit
   - entity-id branching gate와 충돌하지 않도록 데이터 기반으로 수정

5. 회귀 재실행
   - 고친 테스트 클래스 단위 GREEN 확인
   - 최종 전체 EditMode 재실행
   - `Scripts/ci/check-no-entity-id-branching.sh` 실행

필수 테스트/게이트:
- 전체 EditMode 테스트
- 수정한 영역의 개별 EditMode 테스트
- 필요 시 관련 PlayMode smoke:
  - `MainMenuPointerInputE2ETests`
  - `BootToGameEndToEndTests`
- `Scripts/ci/check-no-entity-id-branching.sh`

완료 조건:
- 최신 전체 EditMode 실패 목록을 근거로 한 분류표가 있다.
- 이번 goal 범위의 실패가 GREEN으로 전환되었다.
- 전체 EditMode가 PASS하거나, 아직 남은 실패가 있다면 goal 밖의 이유와 파일/테스트명을 명확히 분리해 보고한다.
- Unity Console 관련 Error/Exception을 확인한다.
- 변경 파일만 선별 stage/commit한다.
- 커밋 제목은 한글 본문과 허용 prefix를 사용한다. 예: `[BUGFIX][TEST] EditMode 회귀 게이트 복구`
- 원격 푸시 여부를 최종 보고에 포함한다.

최종 보고에 반드시 포함할 것:
- 수정 파일
- 생성/수정된 asset/meta/Addressables/Registry 항목
- 실행한 테스트와 PASS/FAIL
- 전체 EditMode 최종 상태
- 남은 실패가 있다면 제외 근거와 다음 goal 후보
- branch/push 상태

주의:
- 전체 EditMode 실패가 많다고 무작정 광범위 리팩터링하지 않는다.
- 이번 goal은 “현재 브랜치 병합을 막는 EditMode 회귀 게이트 복구”다.
- 전체 PlayMode batchmode 게이트 구축은 별도 goal로 남긴다.
- main에 직접 push한다고 가정하지 않는다. 현재 브랜치와 원격 상태를 먼저 확인한다.
```

