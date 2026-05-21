# Recommended Goal Prompts After Long-Run Freshness Guard

작성일: 2026-05-21

이 문서는 현재 `Dedicated route relationship long-run freshness guard` 커밋 이후 바로 이어서 실행하기 좋은 goal 프롬프트를 추천 순서대로 정리한다. 우선순위는 빌드/QA 기반 안정화, 카탈로그와 증거 최신성, 그 다음 player-facing Student Life 기능 확장 순서다.

## 추천 순서 요약

1. Dedicated route relationship long-run rebuild and proof
2. Dedicated core gameplay matrix freshness parity
3. Dedicated core gameplay matrix evidence refresh
4. Career/Job Daily Plan route expansion hardening
5. Relationship activity reward and commitment route
6. Knowledge/Status activity chain

## 공통 절대 조건

- 공유 워크트리로 취급한다. 관련 없는 dirty/untracked 파일은 stage, commit, delete, move, restore 하지 않는다.
- `Assets/**/*.cs`는 shell/apply_patch로 직접 쓰지 않는다. Unity C# 파일은 MCP `script-update-or-create` 또는 Unity Editor 경유로만 수정한다.
- 신규 동작, QA runner, 저장/로드, 네트워크, UI 변경은 실패 테스트를 먼저 작성한다.
- 게임 엔티티 조건은 ScriptableObject 데이터와 전략 배열 기반으로 유지한다.
- production C#에 activity, route, location, NPC, reward, career, job, resource, recipe ID별 분기를 추가하지 않는다.
- 목표성 UI는 `ObjectiveJournalPanel`과 단일 `TrackedObjectiveHud` 소유권을 유지한다.
- visible/player-facing 변경은 실제 PlayMode 사용자 흐름, Game View 또는 Camera screenshot, runtime state, reload 검증으로 확인한다.
- `Assets/AddressableAssetsData/link.xml`과 `.meta` 삭제를 방지한다.
- 완료 전 공통 게이트를 실행한다.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

---

## Prompt 1: Dedicated Route Relationship Long-Run Rebuild and Proof

가장 먼저 붙여 넣을 프롬프트:

```text
docs/superpowers/goals/2026-05-21-dedicated-route-relationship-longrun-rebuild-proof-goal.md 진행.

목표:
`Scripts/qa/run-dedicated-route-relationship-longrun.ps1`에 freshness guard가 추가된 뒤, 현재 source 기준으로 server/client build artifact를 다시 만들고 long-run이 stale artifact 없이 통과함을 증명한다.

이유:
freshness guard는 runner script 자체도 freshness 기준으로 삼는다. guard 추가 전 빌드 산출물은 현재 runner보다 오래되었을 가능성이 높으므로, 다음 네트워크/기능 검증 전에 실제 빌드와 long-run evidence를 다시 맞춰야 한다.

필수 작업:
- 현재 워크트리 상태를 확인하고 관련 없는 변경은 건드리지 않는다.
- Unity build method로 Windows server/client artifact를 재빌드한다.
  - server: `Rootborn.Editor.BuildScripts.BuildScript.BuildServerWindows64`
  - client: `Rootborn.Editor.BuildScripts.BuildScript.BuildClientWindows64`
- build artifact timestamp가 다음 source보다 최신인지 확인한다.
  - `Assets/Scripts/Game/Common/DirectValidationRouteRelationshipAutoplayInstaller.cs`
  - `Scripts/qa/run-dedicated-route-relationship-longrun.ps1`
- long-run을 2 cycles로 실행한다.
- action cycle과 observe-only reload cycle의 server/client logs, screenshots, completion markers를 수집한다.
- audit 문서를 새로 작성한다.
  - `docs/superpowers/audits/2026-05-21-dedicated-route-relationship-longrun-rebuild-proof.md`
- catalog row가 새 evidence를 참조해야 하는지 판단하고, 필요하면 별도 TDD cycle로 분리한다.

필수 검증:
```powershell
tests_run EditMode testClass DedicatedRouteRelationshipAutoplayContractTests
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/qa/run-dedicated-route-relationship-longrun.ps1 -RunName network-dedicated-route-relationship-longrun-rebuild-proof -Cycles 2 -StartPort 7991 -WaitSeconds 100
Get-ChildItem Builds/Logs/network-dedicated-route-relationship-longrun-rebuild-proof -Recurse -File | Select-Object FullName,Length,LastWriteTime
Select-String -Path Builds/Logs/network-dedicated-route-relationship-longrun-rebuild-proof/*.log -Pattern "autoplay completed route-relationship|autoplay completed route-relationship-observe|dailyPlan=day:.*selected:|relationships="
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

완료 기준:
- long-run freshness guard가 stale artifact 없이 통과한다.
- screenshots 2개와 action/observe completion marker가 존재한다.
- server/client process leak이 없다.
- 새 audit 문서가 실제 evidence 경로, log marker, screenshot size를 기록한다.
- Addressables link.xml 보존 게이트가 통과한다.

커밋:
`[DOCS][NETWORK] Dedicated route relationship long-run rebuild 증거 갱신`
```

---

## Prompt 2: Dedicated Core Gameplay Matrix Freshness Parity

Prompt 1 커밋 후 붙여 넣을 프롬프트:

```text
docs/superpowers/goals/2026-05-21-dedicated-core-gameplay-matrix-freshness-parity-goal.md 진행.

목표:
`Scripts/qa/run-dedicated-core-gameplay-matrix.ps1`에도 dedicated smoke/long-run과 같은 pre-launch freshness guard를 적용해서 core matrix가 stale server/client build artifact로 통과하지 못하게 한다.

필수 제약:
- runtime gameplay, ScriptableObject data, scenes, prefabs, UI, save format은 변경하지 않는다.
- 이번 cycle은 QA runner와 EditMode contract만 다룬다.
- `Assets/**/*.cs` 수정이 필요하면 MCP script tool만 사용한다.

필수 작업:
- core matrix runner contract test를 RED로 추가하거나 강화한다.
- runner가 다음 artifact를 launch 전에 검증하도록 한다.
  - server/client executable
  - server/client `Rootborn.Game.dll`
  - server/client Addressables runtime settings
- runner가 source freshness를 다음 기준으로 비교하도록 한다.
  - matrix runner script
  - matrix가 의존하는 autoplay installer/source files
  - 필요하면 포함된 slice runner scripts
- stale failure message에 server/client build remediation method를 명시한다.
- 기존 slice orchestration과 evidence output은 유지한다.

필수 TDD:
RED:
- matrix runner에 `Assert-FreshBuildArtifacts` 또는 동등 guard가 없으면 실패하는 EditMode contract test.
- required artifact path와 remediation text가 없으면 실패하는 test.

GREEN:
- freshness guard를 추가한다.
- contract test와 PowerShell parser check를 통과시킨다.

필수 검증:
```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Network
powershell.exe -NoProfile -Command "`$errors = `$null; `$tokens = `$null; `$null = [System.Management.Automation.Language.Parser]::ParseFile('Scripts/qa/run-dedicated-core-gameplay-matrix.ps1', [ref]`$tokens, [ref]`$errors); if (`$errors -and `$errors.Count -gt 0) { `$errors | Format-List; exit 1 }"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

완료 기준:
- core matrix runner가 stale/missing build artifact를 process launch 전에 fail closed 처리한다.
- failure message가 build remediation을 명확히 안내한다.
- gameplay behavior는 변하지 않는다.

커밋:
`[NETWORK][TEST] Dedicated core gameplay matrix freshness guard 추가`
```

---

## Prompt 3: Dedicated Core Gameplay Matrix Evidence Refresh

Prompt 2 커밋 후 붙여 넣을 프롬프트:

```text
docs/superpowers/goals/2026-05-21-dedicated-core-gameplay-matrix-evidence-refresh-goal.md 진행.

목표:
freshness guard가 적용된 현재 build/runner 기준으로 dedicated core gameplay matrix를 다시 실행하고, action/reload/screenshot/evidence가 최신 source와 일치함을 audit에 기록한다.

필수 작업:
- Prompt 2에서 runner freshness guard가 추가되었다면 server/client build artifact를 먼저 재빌드한다.
- `Scripts/qa/run-dedicated-core-gameplay-matrix.ps1`를 실행한다.
- route/relationship, career daily plan, daily plan completion, discovery route unlock, resource/recipe slice가 모두 evidence를 남기는지 확인한다.
- 각 slice의 server/client log, screenshot, completion marker, reload marker, cleanup marker를 수집한다.
- 새 audit 문서를 작성한다.
  - `docs/superpowers/audits/2026-05-21-dedicated-core-gameplay-matrix-evidence-refresh.md`
- scenario catalog의 `NET-CORE-MATRIX-*` row가 최신 evidence를 설명하도록 업데이트가 필요한지 판단한다. 필요하면 TDD로 catalog test를 먼저 갱신한다.

필수 검증:
```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Network
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/qa/run-dedicated-core-gameplay-matrix.ps1 -RunName network-dedicated-core-gameplay-matrix-refresh -StartPort 8001 -WaitSeconds 120
Get-ChildItem Builds/Logs/network-dedicated-core-gameplay-matrix-refresh -Recurse -File | Select-Object FullName,Length,LastWriteTime
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-scenario-registry-sync.ps1
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-scenario-registry-sync.sh
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

완료 기준:
- core matrix가 현재 build artifact로 통과한다.
- 각 slice evidence가 screenshot과 completion/reload marker를 포함한다.
- process cleanup이 확인된다.
- audit 문서가 실제 evidence path와 marker를 기록한다.

커밋:
`[DOCS][NETWORK] Dedicated core gameplay matrix 증거 갱신`
```

---

## Prompt 4: Career/Job Daily Plan Route Expansion Hardening

Prompt 3 커밋 후 붙여 넣을 프롬프트:

```text
docs/superpowers/goals/2026-05-21-career-job-daily-plan-route-expansion-hardening-goal.md 진행.

목표:
Career/Job Daily Plan route가 player-facing flow, Objective Journal guidance, tracked HUD, save/reload, duplicate prevention까지 안정적으로 동작하도록 기존 slice를 강화한다.

필수 작업:
- 현재 `CareerJobRouteE2EScenarioTests`와 관련 EditMode tests를 조사한다.
- 이미 구현된 route expansion이 있다면 새 기능을 넓히기보다 failure path, duplicate prevention, reload persistence, Journal/HUD ownership을 강화한다.
- 부족한 데이터가 있으면 ScriptableObject asset과 registry wiring으로 추가한다.
- production C#에 career/job/route/activity ID별 분기를 추가하지 않는다.

필수 TDD:
RED:
- route outcome이 중복 progress/reward를 만들면 실패하는 EditMode test.
- reload 후 selected/completed/progress/Journal/HUD state가 어긋나면 실패하는 test.
- Objective Journal stable key가 중복되면 실패하는 test.

GREEN:
- SO 데이터와 generic strategy/path만으로 통과시킨다.
- player-facing route flow를 유지한다.

필수 PlayMode 시나리오:
- MainMenu SaveSlot flow에서 시작한다.
- Town에 진입한다.
- 실제 UI/입력으로 Daily Plan route를 선택한다.
- 플레이어 이동/상호작용/UI 선택으로 career/job 관련 route action을 수행한다.
- Objective Journal과 tracked HUD가 다음 행동을 표시하는지 확인한다.
- Game View 또는 Camera screenshot을 캡처한다.
- 같은 save slot reload 후 progress, selected/completed route, Journal/HUD state를 확인한다.
- 같은 action 반복 시 duplicate progress/reward/row가 없는지 확인한다.

필수 검증:
```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.StudentLife
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Careers
tests_run EditMode testNamespace Rootborn.Tests.EditMode.UI.Objectives
tests_run PlayMode testClass Rootborn.Tests.PlayMode.EndToEnd.CareerJobRouteE2EScenarioTests
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

완료 기준:
- Career/Job route가 실제 플레이 흐름으로 선택, 진행, reload 검증된다.
- Objective Journal과 tracked HUD 소유권이 유지된다.
- duplicate progress/reward/row가 없다.

커밋:
`[FEATURE][TEST] Career Job Daily Plan route 안정화`
```

---

## Prompt 5: Relationship Activity Reward and Commitment Route

Prompt 4 커밋 후 붙여 넣을 프롬프트:

```text
docs/superpowers/goals/2026-05-21-relationship-activity-reward-commitment-route-goal.md 진행.

목표:
Town에서 NPC와 실제 상호작용해 relationship progress, reward, commitment hint 중 하나 이상이 변화하고 Objective Journal이 다음 행동을 안내하는 작은 player-facing slice를 추가 또는 강화한다.

필수 작업:
- 기존 relationship milestone E2E와 데이터 정의를 조사한다.
- 새 slice가 필요하면 NPC/relationship/activity/outcome은 ScriptableObject와 strategy array로만 표현한다.
- reward가 있으면 지급 전 inventory capacity와 duplicate claim을 검증하고 실패 시 상태를 변경하지 않는다.
- Journal row stable key와 tracked HUD 중복을 방지한다.
- 같은 상호작용 반복과 save/reload를 멱등으로 만든다.

필수 TDD:
RED:
- relationship outcome 조건이 데이터와 다르게 적용되면 실패하는 test.
- reward capacity/duplicate failure에서 rollback이 되지 않으면 실패하는 test.
- reload 후 milestone/progress/Journal/HUD가 중복되면 실패하는 test.

GREEN:
- generic relationship outcome path로 통과시킨다.
- production C#에 NPC/activity/reward ID 분기를 넣지 않는다.

필수 PlayMode 시나리오:
- MainMenu SaveSlot flow에서 시작한다.
- Town에서 플레이어 이동으로 대상 NPC에게 접근한다.
- 실제 interaction/dialogue/UI 선택으로 relationship activity를 수행한다.
- relationship progress 또는 commitment guidance 변화를 확인한다.
- Tab으로 Objective Journal을 열어 guidance row를 확인한다.
- Game View 또는 Camera screenshot을 캡처한다.
- save slot reload 후 relationship/progress/Journal/HUD 상태를 확인한다.
- 같은 interaction 반복 후 duplicate reward/milestone/Journal row가 없는지 확인한다.

필수 검증:
```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Relationships
tests_run EditMode testNamespace Rootborn.Tests.EditMode.UI.Objectives
tests_run PlayMode testNamespace Rootborn.Tests.PlayMode.EndToEnd
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

완료 기준:
- relationship slice가 실제 player flow와 screenshot으로 검증된다.
- reward/commitment/progress가 reload 후 유지된다.
- duplicate와 partial reward failure가 방지된다.

커밋:
`[FEATURE][TEST] Relationship activity commitment route 추가`
```

---

## Prompt 6: Knowledge/Status Activity Chain

Prompt 5 커밋 후 붙여 넣을 프롬프트:

```text
docs/superpowers/goals/2026-05-21-knowledge-status-activity-chain-goal.md 진행.

목표:
Town 활동 하나가 Knowledge discovery와 Status/condition 변화를 함께 만들고, Objective Journal이 다음 대체 성장 경로를 안내하는 작은 vertical slice를 구현 또는 강화한다.

필수 작업:
- 기존 discovery route unlock, encyclopedia, status/day result E2E를 조사한다.
- 활동 결과는 LifeActivityDefinition, KnowledgeNode, Status/Trait 관련 ScriptableObject와 strategy array로 연결한다.
- 학교/수업 단일 경로가 아니라 마을 활동 기반 대체 성장 경로로 설계한다.
- Objective Journal guidance는 Journal source로만 노출하고 별도 persistent HUD를 만들지 않는다.
- performance risk를 확인한다. Update polling, 반복 전체 scan, runtime 문자열 할당 폭증을 피한다.

필수 TDD:
RED:
- activity outcome이 Knowledge discovery를 누락하면 실패하는 test.
- Status/condition delta가 저장/로드 후 중복 적용되면 실패하는 test.
- Journal guidance stable key가 중복되면 실패하는 test.

GREEN:
- 데이터 기반 outcome strategy로 통과시킨다.
- reload와 repeated action이 멱등이 되도록 한다.

필수 PlayMode 시나리오:
- MainMenu SaveSlot flow에서 시작한다.
- Town에서 실제 이동/상호작용/UI 선택으로 지식/상태 관련 활동을 수행한다.
- Encyclopedia 또는 관련 UI에서 discovery 상태를 확인한다.
- day result 또는 status feedback에서 condition 변화를 확인한다.
- Tab Objective Journal에서 다음 행동 guidance를 확인한다.
- Game View 또는 Camera screenshot을 캡처한다.
- save slot reload 후 Knowledge/Status/Journal/HUD 상태를 확인한다.
- 같은 활동 반복 후 duplicate discovery/status delta/row가 없는지 확인한다.

필수 검증:
```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.StudentLife
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Knowledge
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Status
tests_run EditMode testNamespace Rootborn.Tests.EditMode.UI.Objectives
tests_run PlayMode testNamespace Rootborn.Tests.PlayMode.EndToEnd
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

완료 기준:
- Knowledge와 Status 변화가 실제 player-facing flow로 확인된다.
- Journal/HUD ownership과 reload persistence가 유지된다.
- duplicate discovery/status/row가 없다.

커밋:
`[FEATURE][TEST] Knowledge Status activity chain 추가`
```

