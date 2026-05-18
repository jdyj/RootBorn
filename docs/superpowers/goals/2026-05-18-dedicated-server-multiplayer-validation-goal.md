# Goal: Dedicated Server 빌드 및 실제 서버/클라이언트 멀티플레이 검증 완결

너는 ROOTBORN Unity 프로젝트의 멀티플레이 검증 담당 에이전트다.

이번 목표는 기존 Host + 2 Client 검증에서 남은 제한점인 "Dedicated Server executable 미검증"을 닫는 것이다. 반드시 실제 `rootborn-server.exe` 프로세스와 별도 `rootborn.exe` Client 2개 프로세스를 실행해서 검증하라.

## 배경

이전 검증 상태:

- Fresh client build는 성공했다.
- Host + Client1 + Client2 실제 프로세스 스모크는 통과했다.
- `Scripts/qa/run-direct-multiplayer-smoke.ps1`가 추가되어 있다.
- `Scripts/ci/run-multiplayer-validation-gate.ps1`가 추가되어 있다.
- Dedicated Server 빌드는 실패했고, 원인은 다음으로 기록되어 있다:
  - `Dedicated Server support for Win is not installed`
- 감사 문서:
  - `docs/superpowers/audits/2026-05-18-direct-multiplayer-executable-smoke-audit.md`

이번 목표는 이 제한점을 해소하거나, 해소가 불가능하면 정확히 어떤 환경 조치가 필요한지 증거 기반으로 남기는 것이다.

## 절대 규칙

- 이전 보고를 믿지 말고 직접 확인하라.
- `Assets/**/*.cs`는 디스크 직접 쓰기로 수정하지 마라. C# 수정이 꼭 필요하면 Unity MCP `script-update-or-create`를 사용하라.
- unrelated dirty/untracked 파일을 revert/add/commit 하지 마라.
- 엔티티 ID별 C# 분기 추가 금지.
- 완료 보고 전 반드시 fresh verification evidence를 확보하라.
- 실패를 성공처럼 포장하지 마라. Dedicated Server 모듈이 없으면 `blocked`로 명확히 보고하라.

## 작업 순서

### 1. 현재 상태 재확인

다음 명령으로 현재 브랜치/커밋/dirty 상태를 확인하라.

```powershell
git status --short --branch
git log -5 --oneline
Get-ChildItem "C:\Program Files\Unity\Hub\Editor\6000.3.13f1\Editor\Data\PlaybackEngines"
Get-ChildItem Builds\Server\Windows -ErrorAction SilentlyContinue
```

확인할 것:

- 현재 브랜치가 원격과 정렬되어 있는지
- `rootborn-server.exe`가 이미 존재하는지
- Unity Dedicated Server support module이 설치되어 있는지
- 기존 dirty/untracked 파일 중 이번 작업과 무관한 것이 무엇인지

### 2. Dedicated Server support 설치 여부 판정

Unity 설치 경로에서 Dedicated Server support가 있는지 확인하라.

기대:

- Windows dedicated server build가 가능해야 한다.
- 없으면 Unity Hub 모듈 설치가 필요하다.

설치가 안 되어 있다면:

- 임의로 성공 처리하지 마라.
- 사용자가 직접 설치해야 하는 항목을 정확히 적어라.
- 그래도 가능한 범위에서 서버 빌드 명령을 실행해 실패 로그를 확보하라.

### 3. Fresh Server Build 실행

다음 명령을 실행하라.

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.13f1\Editor\Unity.exe" `
  -batchmode -nographics `
  -projectPath "C:\Users\jdyj\farmer" `
  -executeMethod Rootborn.Editor.BuildScripts.BuildScript.BuildServerWindows64 `
  -logFile "Builds\Logs\server-build-windows-dedicated-verify-20260518.log" `
  -quit
```

성공 기준:

- 로그에 `Build Finished, Result: Success`
- 로그에 `[ROOTBORN] Server build -> Builds/Server/Windows/rootborn-server.exe result=Succeeded`
- `Builds\Server\Windows\rootborn-server.exe` 파일 존재

실패 기준:

- `Dedicated Server support for Win is not installed`
- Addressables/SBP 오류
- BuildPipeline 실패
- 산출물 미생성

실패하면 원인별로 분류해서 보고하라.

### 4. Fresh Client Build도 재확인

서버와 함께 실행할 최신 클라이언트를 다시 빌드하라.

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.13f1\Editor\Unity.exe" `
  -batchmode -nographics `
  -projectPath "C:\Users\jdyj\farmer" `
  -executeMethod Rootborn.Editor.BuildScripts.BuildScript.BuildClientWindows64 `
  -logFile "Builds\Logs\client-build-dedicated-verify-20260518.log" `
  -quit
```

성공 기준:

- `Build Finished, Result: Success`
- `[ROOTBORN] Client build -> Builds/Client/Windows/rootborn.exe result=Succeeded`

### 5. Dedicated Server + 2 Clients 실제 실행 스모크 작성/실행

`rootborn-server.exe`가 생성되면 다음 구조의 QA 스크립트를 추가하라.

파일:

- `Scripts/qa/run-dedicated-multiplayer-smoke.ps1`

동작:

- `rootborn-server.exe`를 별도 프로세스로 실행
- `rootborn.exe` Client 1 실행
- `rootborn.exe` Client 2 실행
- 로그를 `Builds\Logs\dedicated-multiplayer-<runName>\`에 저장
- 일정 시간 대기 후 프로세스 정리
- 로그 패턴을 검사해 pass/fail 판정

서버 실행 예:

```powershell
Builds\Server\Windows\rootborn-server.exe `
  -batchmode -nographics `
  -logFile Builds\Logs\<run>\server.log `
  -mode server `
  -port 7851 `
  -maxPlayers 4 `
  -saveSlot <run> `
  -directValidationTrace
```

클라이언트 실행 예:

```powershell
Builds\Client\Windows\rootborn.exe `
  -batchmode -nographics `
  -logFile Builds\Logs\<run>\client1.log `
  -mode client `
  -joinIp 127.0.0.1 `
  -port 7851 `
  -saveSlot <run> `
  -directValidationTrace `
  -directValidationAutoplay `
  -directValidationAutoplayDelaySeconds 55
```

검증할 로그 패턴:

- Server started 또는 서버 listen 성공 로그
- Client 1 started
- Client 2 started
- `playerId=client-1`
- `playerId=client-2`
- Client 1 owns `client-1`
- Client 2 owns `client-2`
- non-owner input gate disabled
- `Student day result player=client-1`
- `Student day result player=client-2`
- 서버 권한 world time transition
- Client 1/2 Day 2 sync
- `StartClient failed`, `StartHost failed`, `Exception`, `ERROR` 없음

만약 dedicated server 모드에서 기존 `DirectValidationAutoplay`나 trace가 부족하면:

- TDD 원칙에 따라 실패를 먼저 확인하고 최소한의 테스트/스크립트 보강만 하라.
- gameplay state를 직접 mutate하는 우회 검증은 금지한다.

### 6. CI 게이트 확장

Dedicated server smoke가 통과하면 다음 파일을 확장하라.

- `Scripts/ci/run-multiplayer-validation-gate.ps1`

목표:

- `-RequireDedicatedServer` 옵션이 있으면 dedicated server build + dedicated smoke까지 필수로 실행
- Dedicated Server support가 없으면 명확히 fail
- support가 있으면 `rootborn-server.exe + 2 clients` 검증까지 통과해야 pass

### 7. 감사 문서 작성/갱신

다음 문서를 작성하거나 갱신하라.

- `docs/superpowers/audits/2026-05-18-dedicated-server-multiplayer-validation-audit.md`

반드시 포함:

- Unity Dedicated Server support 설치 여부
- server build command
- server build result
- client build result
- dedicated server smoke command
- 로그 경로
- 핵심 로그 라인
- 실패/차단 시 정확한 원인
- Host + Client smoke와 Dedicated Server + Client smoke의 차이
- 남은 제한점

### 8. 필수 검증

완료 전 반드시 실행하라.

```powershell
git diff --check
& "C:\Program Files\Git\bin\bash.exe" Scripts/ci/check-no-entity-id-branching.sh
```

Dedicated server support가 설치되어 있고 빌드가 성공했다면:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File Scripts\qa\run-dedicated-multiplayer-smoke.ps1 `
  -Port 7851 `
  -RunName dedicated-multiplayer-verify-20260518 `
  -WaitSeconds 125
```

CI 게이트:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File Scripts\ci\run-multiplayer-validation-gate.ps1 `
  -SmokePort 7852 `
  -SmokeRunName multiplayer-ci-dedicated-verify-20260518 `
  -SmokeWaitSeconds 125 `
  -RequireDedicatedServer
```

### 9. 커밋/푸시

성공 또는 명확한 blocked 문서화 후, 이번 작업 파일만 스테이징하라.

예상 파일:

- `Scripts/qa/run-dedicated-multiplayer-smoke.ps1`
- `Scripts/ci/run-multiplayer-validation-gate.ps1`
- `docs/superpowers/audits/2026-05-18-dedicated-server-multiplayer-validation-audit.md`

커밋 메시지 예:

```text
[TEST][DOCS] dedicated server 멀티플레이 검증 게이트 추가
```

푸시:

```powershell
git push origin town-playable-baseline-recovery
```

## 완료 판정

성공으로 보고하려면 다음 중 하나여야 한다.

### 완료 성공

- Dedicated Server support 설치 확인
- server build 성공
- `rootborn-server.exe` 생성
- `rootborn-server.exe + rootborn.exe client 2개` 직접 실행 검증 통과
- CI 게이트 `-RequireDedicatedServer` 통과
- 감사 문서/커밋/푸시 완료

### 환경 차단

- server build를 직접 시도함
- 실패 로그가 `Dedicated Server support for Win is not installed`로 확인됨
- 설치 필요 모듈과 후속 명령을 문서화함
- Host + Client smoke는 유지 검증함
- CI 게이트가 dedicated server 미설치 상태를 명확히 fail/block 처리하도록 보강됨
- 감사 문서/커밋/푸시 완료

절대 "dedicated server 검증 완료"라고 말하지 마라. 실제 `rootborn-server.exe` 프로세스를 실행하지 못했다면 "환경 차단"으로 보고하라.
