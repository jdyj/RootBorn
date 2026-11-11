# Scripts/ci — 정적 검사

MonoGame 판(`game/`, `Packages/com.rootborn.core`, `Tools/`)을 검사한다. Unity 판은 저장소에서 지웠다(태그 `unity-playable-20261111`에 있다). GitHub Actions(`.github/workflows/ci.yml`)도 같은 스크립트를 부른다.

## 한 번에 돌리기

```bash
export PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet"
bash Scripts/ci/run-all.sh                 # 자체 시험 + 검사 6개 + dotnet 테스트 전부 (Host 테스트는 실제 창을 띄운다)
bash Scripts/ci/run-all.sh --checks-only   # 검사 스크립트만 (몇 초)
bash Scripts/ci/run-all.sh --skip-host     # dotnet 테스트에서 창을 띄우는 Host 테스트를 뺀다
```

끝에 통과/실패 요약을 내고, 실패가 하나라도 있으면 종료 코드 1이다. 필요한 것: bash, git, `python3`(검사가 C# 주석·문자열을 가를 때), dotnet 8.

## 검사 목록

각 스크립트는 위반을 `파일:줄:내용  <- 이유`로 출력하고 종료 코드(0 통과, 1 위반)로 알린다.

| 스크립트 | 보는 것 |
|---|---|
| `check-core-references.sh` | `Packages/com.rootborn.core/Runtime`에 `UnityEngine`, `System.Text.Json`, `System.IO` 참조가 없고 asmdef의 `noEngineReferences`가 true |
| `check-monogame-references.sh` | MonoGame(`Microsoft.Xna`)·`MonoGame` 참조(csproj와 .cs의 using·완전한 이름)가 `game/Rootborn.Host`, `game/tests/Rootborn.Host.Tests` 밖에 없다 |
| `check-no-entity-id-branching.sh` | Core 런타임과 `game/`(tests 제외)에서 `…Id == "…"`, `switch (…Id)`, `Equals`, `is "…"`, `enum …Id` 같은 엔티티 id 분기가 없다. 허용 목록: `allowlists/entity-id-branching.txt` |
| `check-no-hardcoded-text.sh` | `game/Rootborn.Screens` 코드에 한글 문구 리터럴이 없다(주석·예외 메시지·tests 제외). 허용 목록: `allowlists/hardcoded-text.txt` |
| `check-localization-keys.sh` | `data/localization`과 `game/localization`을 합친 뒤 모든 언어가 같은 키를 가진다(언어는 파일 이름에서 읽는다). `jq`가 있으면 jq, 없으면 파이썬 |
| `check-meta-files.sh` | Core 런타임의 `.cs`와 폴더마다 `.meta`가 있다(Unity 판이 같은 폴더를 패키지로 읽는다) |

주석과 문자열은 `lib/cs_scan.py`가 갈라서 본다(주석 안의 이름이나 문자열 안의 `//`에 속지 않는다).

## 허용 목록

형식은 `<경로>|<줄에 들어 있는 코드 조각>|<이유>`. 이유 없는 항목은 받지 않는다. 걸리는 코드는 먼저 고치고, 정말 둘 수밖에 없을 때만 적는다. 지금은 두 목록 모두 비어 있다(걸리는 코드가 없다).

## 자체 시험

`tests/run-tests.sh`가 `tests/test-*.sh`를 모두 돌린다. 시험마다 작은 임시 폴더(`CI_ROOT`)에 통과·위반 예시를 만들어 스크립트의 종료 코드와 출력을 본다. 검사를 고치면 먼저 시험을 더하거나 고친다.

환경 변수: `CI_ROOT`(검사할 저장소 루트, 기본은 git 루트), `ENTITY_ID_ALLOWLIST`, `TEXT_ALLOWLIST`(허용 목록 파일 바꾸기, 시험용).

## 워크플로(`.github/workflows/ci.yml`)

GitHub 기록(`gh run view`)을 읽어 확인한 값이다(main 43022156721, 2026-11-11).

| job | 하는 일 | 마지막 결과 | 시간 | 막는가 |
|---|---|---|---|---|
| `static-checks` | 자체 시험 + 검사 6개 | 통과 | 약 20초 | 막는다 |
| `dotnet-tests` | Core, `game/tests`의 Host 테스트를 뺀 나머지(Server, Simulation 포함) | 통과(Core 970, Data 256, Game 520, Screens 393, Server 40, Simulation 36 …) | 약 1.5분 | 막는다 |
| `server-publish` | `Tools/publish/publish.sh --server linux-x64` 뒤 `verify.sh --server linux-x64` | 연속 두 번 통과(43022156721, 43027519152) | 35~50초 | 막는다 |
| `host-tests` | `game/tests/Rootborn.Host.Tests`를 `xvfb-run` + 소프트웨어 GL로 | 171건 중 169 통과, 성능 시험 2건 실패 | 27분 | **막지 않는다**(`continue-on-error`), 시간 제한 45분 |

- `dotnet-tests`는 한 프로젝트가 실패해도 나머지를 끝까지 돌리고 실패를 모아 알린다. 새 테스트 프로젝트는 `game/tests/<이름>/<이름>.csproj`에 두면 저절로 돈다(글롭). `Tools/` 아래 테스트 프로젝트는 워크플로와 `run-all.sh`에 한 줄씩 더한다.
- 서버 테스트는 UDP를 루프백(`127.0.0.1`, 포트 0)에서만 열고 별도 프로세스를 띄운다. 러너에서 문제 없었다.
- `host-tests`는 러너에 `fonts-nanum`을 깔고 `ROOTBORN_FONT`로 준다(깔기 전 실행은 한글 글꼴을 못 찾아 125건이 실패했다). 통과한 테스트도 시간과 함께 로그에 남기고(`verbosity=normal`) trx를 아티팩트 `host-test-results`로 올린다(14일).
  실패 2건(`OBJECTIVE_JOURNAL_PERF_200Rows_OpenAndScroll_FrameTime`, `SCREENS_003_Performance_1500CellGrid_OpenAndScroll`)은 소프트웨어 렌더링 러너가 프레임 시간 중앙값 16.7ms 기준을 못 맞춘 것이다. 기준은 다른 세션(play-soak)이 바꾸는 중이라 이 쪽에서 고치지 않는다.
  `continue-on-error`를 떼는 조건: (1) 위 두 시험이 러너에서 통과한다(기준이 바뀌었거나 러너에서 건너뛴다), (2) 그 뒤 main 실행에서 `host-tests`가 연속 두 번 통과한다, (3) 실행 시간이 시간 제한(45분)에 한참 못 미친다.
- `concurrency`: 같은 PR의 낡은 실행만 취소하고, main 푸시는 취소하지 않는다(연달아 푸시해도 커밋마다 결과가 남는다).
- Host는 원격 Linux 장치(Xvfb, 소프트웨어 GL)에서는 전체가 약 4분에 통과한다. 러너(2 코어쯤)에서는 27분이다. 느린 이유는 소프트웨어 그리기와 코어 수로 보인다. 어느 테스트가 느린지는 `verbosity=normal` 로그를 읽은 뒤에 정한다.
- 클라이언트 배포 폴더(`Tools/publish/publish.sh`)는 CI가 만들지 않는다(Windows 와 글꼴 때문). 로컬에서 `Tools/publish/publish.sh` 뒤 `Tools/publish/verify.sh`(`Tools/publish/README.md`).
