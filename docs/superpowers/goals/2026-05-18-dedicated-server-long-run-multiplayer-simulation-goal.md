# Dedicated Server Long-Run Multiplayer Simulation Goal Prompt

아래 프롬프트를 goal 세션에 그대로 붙여 넣고 진행한다.

```text
ROOTBORN Unity 프로젝트에서 기존 dedicated server + 2 client smoke 검증을 넘어서, "dedicated server 기반 장기 멀티플레이 시뮬레이션 게이트"를 설계/구현/검증해줘.

이번 goal은 단순 접속 확인이 아니다. rootborn-server.exe 1개와 rootborn.exe 클라이언트 최대 4개를 실제 별도 프로세스로 실행하고, 일정 시간 동안 Town 플레이 흐름, day 진행, 클라이언트 이탈/재접속, saveSlot 재시작 후 상태 복구를 검증하는 강한 멀티플레이 안정성 검증이다.

## 최종 목표

- dedicated server + 최대 4 clients 실제 실행 파일 검증을 자동화한다.
- 최소 10분 이상 유지 가능한 장기 시뮬레이션 모드를 만든다.
- 클라이언트 접속, 스폰, owner/non-owner 입력 게이트, day progression, player별 결과 기록, world time sync를 검증한다.
- 클라이언트 중도 이탈과 재접속을 검증한다.
- 서버 종료 후 같은 saveSlot으로 재시작했을 때 지속되어야 하는 상태가 복구되는지 검증한다.
- 가능한 범위에서 Town 실제 player-facing flow를 사용한다. 내부 메서드 직접 호출이나 상태 강제 세팅만으로 완료 처리하지 않는다.
- 자동화 결과는 PASS/FAIL과 로그 증거로 남기고, 실패 시 원인을 숨기지 않는다.

## 반드시 지킬 규칙

- AGENTS.md와 `.claude/rules/*`를 따른다.
- `Assets/**/*.cs`는 shell, echo, cat, apply_patch 등으로 직접 쓰지 않는다. C# 신규/수정이 필요하면 반드시 Unity MCP `script-update-or-create` 또는 Unity Editor 안전 경로를 사용한다.
- unrelated dirty/untracked 파일은 revert/add/commit 하지 않는다.
- entity ID별 C# `if/switch`, enum 기반 엔티티 분기, 특정 activity/quest/location/player id 전용 C# 분기 추가를 금지한다.
- 모든 신규 gameplay 조건과 차이는 ScriptableObject 데이터, 전략 배열, 설정값, 명령행 인자, 테스트/QA 스크립트 파라미터로 표현한다.
- visible/player-facing 검증이 필요한 경우 실제 PlayMode 또는 executable player flow, Game View/Camera/evidence screenshot, runtime state/log evidence를 함께 사용한다.
- "프로세스가 떠 있음"만으로 성공 처리하지 않는다. 접속, 스폰, 동기화, day progression, 재접속, save/load 증거가 있어야 한다.
- 테스트와 QA 스크립트는 실패를 먼저 드러내도록 작성한다. 실패 패턴을 무시하거나 로그 필터에서 누락하지 않는다.

## 현재 기준선

이미 완료된 기반:

- Windows Dedicated Server Build Support 설치 완료.
- `Builds/Server/Windows/rootborn-server.exe` 빌드 성공.
- `Scripts/qa/run-dedicated-multiplayer-smoke.ps1` 존재.
- `Scripts/ci/run-multiplayer-validation-gate.ps1 -RequireDedicatedServer`가 dedicated server smoke를 실행한다.
- `docs/qa/dedicated-server-multiplayer-runbook.md`에 수동/자동 실행 절차가 정리되어 있다.

이번 goal은 위 기반을 확장한다.

## 접근 순서

### Phase 1. 현 상태 재확인

다음을 먼저 확인한다.

```powershell
git status --short --branch
git log -5 --oneline
Test-Path Builds\Server\Windows\rootborn-server.exe
Test-Path Builds\Client\Windows\rootborn.exe
Get-Content docs\qa\dedicated-server-multiplayer-runbook.md -TotalCount 220
```

확인할 것:

- 현재 브랜치와 원격 추적 상태.
- 기존 dedicated smoke가 어떤 로그 패턴을 PASS로 보는지.
- 빌드 산출물이 최신인지, 필요하면 fresh build를 다시 수행해야 하는지.
- 기존 dirty/untracked 파일 중 이번 goal에서 건드리면 안 되는 항목.

### Phase 2. 장기 시뮬레이션 요구사항을 QA 스크립트로 명확화

기존 `Scripts/qa/run-dedicated-multiplayer-smoke.ps1`를 무리하게 복잡하게 만들지 말고, 필요하면 별도 스크립트를 추가한다.

권장 파일:

- `Scripts/qa/run-dedicated-multiplayer-longrun.ps1`

필수 파라미터:

- `-Port`
- `-RunName`
- `-ClientCount` 기본 4, 최소 2, 최대 4
- `-WaitSeconds` 기본 600
- `-SaveSlot`
- `-ReconnectClientIndex`
- `-DisconnectAfterSeconds`
- `-ReconnectAfterSeconds`
- `-RestartServerAfterFirstPass`
- `-RequireSaveReloadEvidence`

프로세스 구성:

- 1 server:
  - `Builds\Server\Windows\rootborn-server.exe -mode server -port <port> -maxPlayers 4 -saveSlot <slot> -directValidationTrace`
- N clients:
  - `Builds\Client\Windows\rootborn.exe -mode client -joinIp 127.0.0.1 -port <port> -saveSlot <slot> -directValidationTrace -directValidationAutoplay`

스크립트는 다음을 해야 한다.

- 로그 디렉터리를 run별로 분리한다.
- 포트 충돌을 명확히 실패로 보고한다.
- 서버와 클라이언트 프로세스 시작 실패를 즉시 실패로 보고한다.
- 정해진 시간 동안 프로세스가 죽었는지 감시한다.
- 지정된 클라이언트 하나를 중간에 종료하고, 일정 시간 뒤 같은 saveSlot/port로 재접속시킨다.
- 옵션이 켜져 있으면 첫 pass 후 서버를 종료하고 같은 saveSlot으로 재시작한 뒤 최소 1~2개 클라이언트가 다시 접속하는지 확인한다.
- 마지막에는 모든 child process를 정리한다.
- 실패하더라도 가능한 로그와 프로세스 종료 코드를 남긴다.

### Phase 3. PASS/FAIL 로그 패턴 강화

성공 조건은 최소 다음을 포함한다.

Server log:

- dedicated server started
- client connected id가 ClientCount만큼 관측됨
- reconnect 대상 클라이언트의 disconnect가 관측됨
- reconnect 후 client connected가 다시 관측됨
- network player spawned가 각 client/player identity에 대해 관측됨
- server authority world time/day progression 로그가 관측됨
- saveSlot 사용 로그 또는 save/load 관련 trace가 관측됨
- server restart 옵션 사용 시 재시작 후 listen/start와 client reconnect가 관측됨

Client logs:

- 각 클라이언트가 client mode로 시작됨
- 각 클라이언트가 자기 owner player를 가진다
- non-owner player에 대한 입력 차단 또는 non-owner ownership guard 로그가 관측됨
- 최소 1회 이상 autoplay 기반 Town flow 또는 day result가 관측됨
- world time/day progression sync가 관측됨
- reconnect 대상 클라이언트는 재접속 후 다시 owner spawn 또는 synced state를 관측한다

치명 실패 패턴:

- `StartClient failed`
- `StartHost failed`
- `StartServer failed`
- `Unhandled`
- `NullReferenceException`
- `InvalidOperationException`
- `Exception`
- `ERROR`
- `Failed to bind`
- `Address already in use`
- client count 부족
- owner mismatch
- server-only state가 client에 동기화되지 않음
- saveSlot restart 후 상태 증거 없음

단, Unity/플랫폼 로그 중 실제 실패가 아닌 known noisy pattern이 있다면 정확한 allowlist를 문서화하고, 넓은 문자열 무시는 금지한다.

### Phase 4. CI 게이트 확장

`Scripts/ci/run-multiplayer-validation-gate.ps1`에 장기 시뮬레이션을 선택적으로 연결한다.

권장 옵션:

- `-RequireDedicatedLongRun`
- `-LongRunClientCount 4`
- `-LongRunWaitSeconds 600`
- `-LongRunReconnect`
- `-LongRunRestartServer`

동작:

- 기본 빠른 CI는 기존 host smoke + dedicated smoke를 유지한다.
- release/nightly 또는 수동 강제 게이트에서는 `-RequireDedicatedLongRun`으로 장기 시뮬레이션까지 실행한다.
- dedicated server build support가 없거나 server/client artifact가 없으면 명확히 fail 또는 build 단계로 진입한다.
- 장기 검증이 skip되면 최종 출력에 skip 이유를 명확히 적는다.

### Phase 5. 필요 시 DirectValidationAutoplay 보강

현재 executable autoplay가 4클라이언트 장기 흐름, 재접속, day progression, save/load 증거를 충분히 남기지 못하면 TDD로 최소 보강한다.

가능한 보강 방향:

- command-line arg로 autoplay client identity 또는 delay를 안정적으로 분리한다.
- 각 client가 같은 행동만 반복해 race를 만들지 않도록 시작 지연과 행동 seed를 파라미터화한다.
- Town flow 결과 로그에 player id, day, activity count, world time, saveSlot을 포함한다.
- reconnect 후 상태 복구 로그를 남긴다.

금지:

- 특정 `client-1`, `client-2` 같은 entity id를 gameplay C# 분기로 하드코딩하지 않는다.
- 테스트 통과를 위해 gameplay state를 직접 mutate하는 shortcut을 만들지 않는다.
- user-facing flow 없이 내부 메서드 호출만으로 day result나 save/load를 조작하지 않는다.

### Phase 6. 문서화

다음을 추가 또는 갱신한다.

- `docs/qa/dedicated-server-multiplayer-runbook.md`
- 필요하면 `docs/superpowers/audits/2026-05-18-dedicated-server-long-run-multiplayer-simulation-audit.md`

문서에 반드시 포함:

- 실행 명령
- 요구 빌드 산출물
- 4 client long-run 예시
- reconnect 예시
- server restart + saveSlot reload 예시
- PASS 로그 패턴
- FAIL 로그 패턴
- 어떤 검증이 fast CI이고 어떤 검증이 release/nightly인지
- 이번 실행에서 실제로 확인한 로그 경로
- 아직 직접 검증하지 못한 항목

## 필수 테스트와 검증

문서 변경만 있으면:

```powershell
git diff --check -- docs\qa\dedicated-server-multiplayer-runbook.md
```

스크립트 변경이 있으면:

```powershell
git diff --check -- Scripts\qa\run-dedicated-multiplayer-longrun.ps1 Scripts\ci\run-multiplayer-validation-gate.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts\qa\run-dedicated-multiplayer-longrun.ps1 -Port 7861 -RunName dedicated-longrun-verify -ClientCount 4 -WaitSeconds 600 -ReconnectClientIndex 2 -DisconnectAfterSeconds 180 -ReconnectAfterSeconds 45 -RestartServerAfterFirstPass
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts\ci\run-multiplayer-validation-gate.ps1 -SmokePort 7864 -SmokeRunName multiplayer-ci-longrun -RequireDedicatedServer -RequireDedicatedLongRun -LongRunClientCount 4 -LongRunWaitSeconds 600 -LongRunReconnect -LongRunRestartServer
```

C# 변경이 있으면 반드시 추가:

```powershell
& "C:\Program Files\Git\bin\bash.exe" Scripts/ci/check-no-entity-id-branching.sh
```

Unity 코드/PlayMode 보강이 있으면 영향 범위의 EditMode/PlayMode 테스트도 실행한다.

## 완료 조건

완료 보고는 다음을 모두 만족할 때만 한다.

- dedicated server + 4 clients 장기 시뮬레이션 실행 결과가 PASS다.
- client disconnect/reconnect 증거가 있다.
- server restart + same saveSlot reload 증거가 있거나, 아직 자동화하지 못했다면 명확히 blocked/remaining으로 적었다.
- day progression 또는 Town autoplay result가 player별로 관측된다.
- owner/non-owner 입력 또는 ownership guard 증거가 있다.
- fatal log pattern이 없거나, known noisy pattern으로 문서화된 항목만 제외했다.
- 장기 검증 스크립트와 CI 옵션이 문서화되어 있다.
- `git diff --check`가 통과한다.
- C# 변경이 있었다면 `check-no-entity-id-branching.sh`가 통과한다.
- 최종 보고에 실행 명령, 로그 경로, PASS/FAIL 요약, 미검증 잔여 리스크를 포함한다.

## 산출물 후보

- `Scripts/qa/run-dedicated-multiplayer-longrun.ps1`
- `Scripts/ci/run-multiplayer-validation-gate.ps1`
- `docs/qa/dedicated-server-multiplayer-runbook.md`
- `docs/superpowers/audits/2026-05-18-dedicated-server-long-run-multiplayer-simulation-audit.md`
- 필요 시 DirectValidationAutoplay 관련 C# 파일과 대응 테스트

## 우선순위

1. 기존 dedicated smoke를 깨지 않는다.
2. 장기 시뮬레이션 스크립트를 독립적으로 추가한다.
3. 4 client 접속/유지/day progression을 먼저 통과시킨다.
4. disconnect/reconnect를 추가한다.
5. server restart + same saveSlot reload를 추가한다.
6. CI에는 빠른 smoke와 장기 게이트를 분리해서 연결한다.
7. 문서와 감사 결과를 남긴다.
```
