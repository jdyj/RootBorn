# Scripts/ci — Unity 없이 도는 검사

MonoGame 판(`game/`, `Packages/com.rootborn.core`, `Tools/`)을 검사한다. Unity 판은 얼렸고(태그 `unity-playable-20261111`) 여기서 Unity 테스트는 돌리지 않는다. GitHub Actions(`.github/workflows/ci.yml`)도 같은 스크립트를 부른다.

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

- `static-checks`: 자체 시험 + 검사 6개. 실패하면 막는다.
- `dotnet-tests`: Core, DataExport, `game/tests`의 Host 테스트를 뺀 나머지. 실패하면 막는다.
- `host-tests`: `game/Rootborn.sln` 전체를 `xvfb-run`으로. Host 테스트는 실제 창을 띄우므로 GitHub 러너에서 도는지 확인되기 전까지 `continue-on-error: true`다(이유는 워크플로 주석).
