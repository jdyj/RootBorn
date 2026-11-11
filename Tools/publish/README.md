# Tools/publish — 저장소 없이 도는 실행 폴더 만들기

```
Tools/publish/publish.sh              # win-x64 와 linux-x64 둘 다
Tools/publish/publish.sh win-x64      # 하나만
Tools/publish/publish.sh --list       # 복사할 게임 파일 목록만(게시하지 않는다)
Tools/publish/publish.sh --server     # 데디케이티드 서버(game/Rootborn.Server) 배포 폴더, 둘 다(아래 "서버")
Tools/publish/verify.sh               # 만든 폴더가 저장소 없이 도는지 확인(아래 "확인하는 법")
Tools/publish/verify.sh --server      # 서버 배포 폴더 확인
Tools/publish/verify.sh --remote linux-x64           # linux-x64 를 원격 Linux 장치에서 실행해 확인(아래 "Linux 에서 확인")
Tools/publish/verify.sh --remote --server linux-x64  # linux-x64 서버를 원격에서
```

Git Bash(또는 Linux/macOS bash)에서 돈다. dotnet 은 `~/.dotnet` 또는 PATH 에 있으면 된다.
출력은 `output/publish/<플랫폼>/`(git 밖). 그 폴더가 곧 게임의 "저장소 루트"다 — 실행 파일을 거기서 바로 실행하면
실행 파일 위치에서 위로 올라가며 `Assets/modernuserinterface-win` 을 찾아 루트를 정하고, `--root <폴더>` 로 줄 수도 있다.

하는 일: `dotnet publish game/Rootborn.Host -c Release -r <플랫폼> --self-contained` → 출력 폴더에
`content.txt` 가 가리키는 게임 파일을 **같은 상대 경로로** 복사한다(`data/…`, `Assets/…`, `game/economy.json`, `game/world/…`, `game/localization/…`, `game/themes/…`).
테스트·문서(`*.md`, `*.Tests.dll`, NUnit·테스트 호스트)는 들어가지 않는다(`verify.sh`가 본다). 우리 어셈블리의 `.pdb`(클라이언트 8개 520KB)는 오류 위치를 남기려고 둔다.
Unity 전용 파일(`.meta .unity .asset .prefab .cs`)은 어떤 줄로도 복사되지 않는다.

2026-11-11 이 PC(Windows 11, dotnet 8.0.422, 빌드 캐시 있음)에서 잰 값:

| 플랫폼 | 걸린 시간 | 크기 | 파일 수 |
|---|---|---|---|
| win-x64 | 36초 | 99MB | 3,620개(실행 파일·런타임 204개 78MB + 게임 파일 3,416개 — `Assets` 17MB, `data` 5.4MB) |
| linux-x64 | 21초 | 100MB | 3,620개(같은 게임 파일 + ELF 실행 파일, SDL2·OpenAL `.so` 포함) |

목록 만들기는 그중 20초쯤이다. Windows 에서는 프로세스 하나 띄우는 데 수십 ms가 들므로 스크립트가 파일마다 grep·cp 를 띄우지 않게 했다.

**Linux 폴더를 Windows 에서 만들었으면** NTFS 가 실행 권한을 남기지 않는다. Linux 로 옮긴 뒤 `chmod +x Rootborn.Host` 한다(`verify.sh --remote`는 보낸 뒤 스스로 준다).

## 서버

`publish.sh --server [플랫폼]`은 `game/Rootborn.Server`를 자체 포함으로 게시하고 `content-server.txt`의 파일을 같은 상대 경로로 복사한다.
출력은 `output/publish/server-<플랫폼>/`. 클라이언트와 같은 방식(목록 파일)이다. 목록 문법은 `content.txt`와 같다.

서버가 실제로 읽는 것(코드에서 확인: `ServerOptions`, `GameDataLoader`, `EconomySettings`, `DataFingerprint`)만 넣는다.

| 파일 | 왜 |
|---|---|
| `data/**/*.json`(좌표표 `data/atlases`, 보고서 `export-report.json`, 문구 `data/localization` 제외) | 정의(등록부·엔티티 파일)를 읽는다 |
| `game/economy.json` | 통화 아이템 설정. 데이터 폴더 옆의 `game/economy.json`을 읽는다(없으면 통화 없음) |

그림·글꼴·테마·맵(LDtk)·문구·월드 지점(`game/world`)은 넣지 않는다 — 서버는 읽지 않는다(`verify.sh --server`가 PNG·글꼴·LDtk가 들어갔는지 본다).
서버가 새 파일을 읽게 되면 `content-server.txt`에 한 줄 더한다.

2026-11-12 이 PC에서 잰 값(dotnet 8.0.422, 지문 범위를 고친 뒤): linux-x64 75MB / 400개(실행 파일·런타임 196개 + 게임 파일 204개, data 2.8MB).
클라이언트 linux-x64 는 101MB / 3,625개(`Assets` 17MB, `data` 5.4MB, `game` 77KB).

- **실행**: 실행 파일 위치에서 위로 올라가며 `data/registry.json`을 찾아 루트를 정하므로 배포 폴더에서 `--root` 없이 돈다.
  `--save-root`, `--slot`, `--bind`, `--port`는 기본값이 없어 반드시 준다(`Rootborn.Server --help`). 끝내기는 콘솔 `stop`, Ctrl+C, 종료 신호.
- **지문**: 방에 들어갈 때 맞춰 보는 데이터 지문(`Rootborn.Data/DataFingerprint`)은 **규칙에 쓰이는 데이터만** 셈한다.
  서버와 클라이언트가 같은 셈법을 쓰고, 두 배포 목록으로 셈한 지문이 같다(`DataFingerprintScopeTests`가 두 목록 파일을 읽어 확인한다).

  | 셈하는 것 | 셈하지 않는 것 |
  |---|---|
  | `data/registry.json`(등록부) | `data/atlases/**`(그림 좌표표 — 클라이언트는 쓰는 그림 몫만 갖는다) |
  | `data` 아래 맨 위가 배열인 `.json`(엔티티 파일, 정의 로더와 같은 기준) | `data/export-report.json`(변환기 기록, 게임이 읽지 않는다) |
  | data 옆 `game/economy.json`(통화 설정, 없으면 빠진다) | `data/localization/**`(문구 — 언어가 다른 두 사람도 같이 해야 한다) |
  | | `game/world`·`game/themes`·`game/localization`·그림·맵(서버가 읽지 않는다) |

  줄바꿈(CRLF/LF)과 맨 앞 UTF-8 BOM은 맞춰서 넣는다 — Windows 에서 만든 배포와 Linux 에서 만든 배포가 같은 지문이다.
  규칙 데이터를 한 글자만 바꿔도 지문이 달라져 "데이터 버전이 다르다"로 거절된다(같은 커밋에서 만든 두 폴더끼리만 들어간다).
- **버전**: 서버 로그의 `game 1.0.0+<커밋 해시>`는 .NET SDK가 git 저장소에서 빌드할 때 정보 버전에 붙이는 해시다. git이 없는 곳에서 빌드한 배포는 해시 없이 `1.0.0`이 된다.
  게임 버전을 `Directory.Build.props`로 정하는 세션(`mp-join-screen`)이 합쳐진 뒤에도 배포 빌드가 그 값을 깨지 않는지 확인할 일이다.

## 목록(`content.txt`) 고치기

한 줄에 하나. `#` 주석.

| 줄 | 뜻 |
|---|---|
| `경로/글롭` | 그 파일(폴더면 안의 파일 전부) |
| `!글롭` | 앞에서 모인 것 중 맞는 것을 뺀다 |
| `@refs 글롭` | 맞는 JSON 가운데 **배포에 들어가는 것**(앞 줄들이 모은 것) 안에 `"Assets/….png"` 로 적힌 그림을 전부 더한다. 앞에서 뺀 JSON(`!data/atlases/**` 등)은 보지 않는다 |
| `@ldtk 글롭` | `.ldtk` + 레벨 파일(`.ldtkl`) + 타일셋 PNG(`relPath`) |
| `@png 글롭` | 맞는 PNG |

더해진 PNG 마다 `data/atlases/<PNG 경로>.atlas.json`(그림 좌표표)이 있으면 자동으로 따라온다.

- **그림을 새로 바꿀 때**: 새 PNG 를 `Assets/…` 에 두고 데이터(JSON)가 그 경로를 가리키면 `@refs` 가 알아서 집는다.
  데이터가 가리키지 않는 PNG 는 `@png` 줄로 더한다. 쓰지 않게 된 폴더는 `!글롭` 으로 뺀다.
- **맵을 새로 그릴 때**: `.ldtk` 를 `Assets/LDtk/<이름>/<이름>.ldtk` 에 두면 `@ldtk Assets/LDtk/*/*.ldtk` 가 레벨 파일과 타일셋까지 집는다.
  다른 곳에 두면 `@ldtk` 줄을 더한다.
- 목록의 글롭이 아무것도 못 찾으면 "맞는 파일이 없는 줄" 경고, 데이터가 가리키는데 디스크에 없는 PNG 는 "없는 파일" 경고가 뜬다(경고는 복사를 막지 않는다).

## 글꼴

배포 폴더에는 글꼴 파일이 들어가지 않는다(저장소에도 TTF/OTF/TTC 가 없다). 배포용 한글 글꼴은 사용자 결정 대기다.
지금은 Host 가 운영체제 글꼴을 찾는다(`HostOptions.FindSystemKoreanFont`, 순서: `malgun.ttf` → `NanumGothic.ttf` → Noto CJK …).
Windows 에서는 맑은 고딕(`C:\Windows\Fonts\malgun.ttf`, Windows 와 함께 깔리는 Microsoft 글꼴 — 게임과 함께 배포할 수 없다)을 쓴다.
못 찾으면 `--font <TTF>` 또는 `ROOTBORN_FONT` 를 요구하며 종료 코드 2로 멈춘다(예: 한글 글꼴이 없는 Linux, GitHub ubuntu 러너).
Linux 원격 확인(`--remote`)은 원격 `env.sh`의 `ROOTBORN_FONT`(Strix Halo: 시스템의 Noto Sans CJK `.ttc`)로 돌았다 — 배포 폴더의 글꼴이 아니다.
글꼴이 정해지면 폴더에 넣고 `content.txt` 에 한 줄 더한 뒤 Host 가 그 경로를 기본으로 보게 한다(Host 변경 필요).

## 확인하는 법

```
Tools/publish/verify.sh                         # 이 PC 플랫폼(Windows: win-x64)의 배포 폴더
VERIFY_KEEP=1 Tools/publish/verify.sh           # 스크린샷·로그를 남긴다(경로를 끝에 알린다)
Tools/publish/verify.sh --contents-only linux-x64   # 이 PC 에서 실행할 수 없는 플랫폼: 목록 대조만
Tools/publish/verify.sh --server                # 서버 배포 폴더(server-win-x64)
Tools/publish/verify.sh --server --bind <IP>    # 서버가 들을 주소를 바꾼다(기본 127.0.0.1, 시험용이라 밖에 열리지 않는다)
```

1. `publish.sh --list` 의 파일이 배포 폴더에 다 있는지, Unity 전용 파일이 없는지 본다.
2. 배포 폴더를 **저장소 밖** 임시 폴더로 복사하고, 저장소를 가리키는 환경 변수(`ROOTBORN_REPO_ROOT` 등)를 지우고 `--root` 없이 실행한다.
3. 화면마다 한 번씩 `--frames <N> --screenshot <PNG>` 로 띄워 N 프레임 뒤 화면을 저장하고 끝내게 한다:
   슬롯 선택(한국어·영어), 새 게임(`--slot slot-1`), 같은 슬롯 이어 하기, 슬롯이 있는 슬롯 선택, 견본 화면(`--sample`),
   `game/world/maps.json` 의 맵마다(`--world`, 맵 이름은 스크립트에 적지 않는다), 첫 맵을 영어로 한 번 더.
   종료 코드 0, PNG 스크린샷이 비어 있지 않은지, 표준 오류가 비어 있는지 본다(Host 는 읽기 문제를 표준 오류에 쓰고, 멈추는 문제는 종료 코드 2).
4. Linux 에 `strace` 가 있으면 실행 중 연 파일 가운데 복사본 밖의 것을 보인다. Windows 에는 같은 수단이 없다
   — Host 가 연 파일 목록을 내는 인자가 생기면 그것으로 바꾼다(Host 변경 필요). 그동안은 3번의 화면 범위를 넓혀
   읽지 못한 파일이 표준 오류에 나오는지 본다.

세이브는 임시 폴더에만 쓴다(사용자 세이브 폴더를 건드리지 않는다). 스크린샷이 제대로 그려졌는지(그림, 한글 글자)는
스크립트가 판단하지 않는다 — `VERIFY_KEEP=1` 로 남겨 직접 연다. 2026-11-12 win-x64: 17번 실행 모두 통과, 약 4분(맵 9개).

서버 확인(`--server`)은 같은 1·2번 뒤 이렇게 한다:
임시 세이브 폴더·슬롯과 `--port 0`(빈 포트)으로 실행 → 로그에 `듣는 중:` → 로그의 데이터 폴더가 복사본 안인지 → 콘솔에 `stop`을 넣어 종료 코드 0과 `저장하고 닫음` →
같은 세이브로 다시 켜서 `방을 이어서 엶` → 다시 `stop` → 인자 없이 켜면 종료 코드 2와 도움말. 표준 오류는 비어 있어야 한다.
`--root`·`ROOTBORN_*` 환경 변수 없이 돈다. 2026-11-12 win-x64: 통과 34초. 잘못된 `--bind`로는 실패 13건으로 잡힌다.
Linux(SIGTERM 포함)는 아래 "Linux 에서 확인". GitHub Actions의 `server-publish` job도 ubuntu 에서 실행까지 한다(아직 GitHub 미확인).

클라이언트 확인(인자 없는 `verify.sh <플랫폼>`)은 같은 플랫폼의 서버 배포 폴더(`server-<플랫폼>`, 또는 `VERIFY_SERVER_SRC`)가 있으면
**들어가기**도 본다: 새 게임 뒤 그 서버를 빈 포트로 띄우고 클라이언트를 `--slot slot-1 --join 127.0.0.1:<포트>`로 띄워
서버 로그에 `들어옴:`이 있고 `접속 거절`이 없는지 본다 — 배포한 두 폴더의 버전(프로그램·데이터 지문)이 맞는다는 실제 확인이다.
서버 폴더가 없으면 건너뛰고 끝에 알린다. 서버 확인은 Linux 에서 콘솔 없이 띄워 SIGTERM 으로 끝내도 `저장하고 닫음`·종료 코드 0 인지 본다.

## Linux 에서 확인 (`--remote`)

이 PC(Windows)에서 만든 linux-x64 폴더를 원격 Linux 장치(지금은 Strix Halo)에서 실행한다.
접속 값은 `Scripts/remote-test`와 같은 환경 변수(`ROOTBORN_REMOTE_HOST`·`ROOTBORN_REMOTE_USER`·`ROOTBORN_REMOTE_KEY`, 작업 폴더 `ROOTBORN_REMOTE_DIR` 기본 `rootborn-test`)로만 받는다.
**값이 없으면 1번(목록 대조)만 하고 "원격 실행을 건너뛰었다"고 알린다.** 원격은 `Scripts/remote-test/run.sh --setup`으로 준비된 것(`env.sh`의 Xvfb·글꼴)을 쓴다.

1. 목록 대조는 이 PC 에서 한다.
2. 배포 폴더(클라이언트면 `server-linux-x64`도)와 `verify.sh`를 원격 `~/<작업 폴더>/deploy/<실행 id>/`로 보내 실행 권한을 준다(Windows 가 실행 권한을 남기지 않으므로).
3. 원격에서 `verify.sh --copied`로 위 2~4번을 돈다. 클라이언트는 가상 화면(Xvfb, 소프트웨어 GL)에서 돈다.
   화면 번호는 원격 테스트의 범위(140~199)와 겹치지 않는 `VERIFY_DISPLAY_FIRST`~`LAST`(기본 200~219)에서 빈 것을 고른다.
   임시 폴더·홈(글꼴·셰이더 캐시)도 그 실행 폴더 안에 둔다 — 원격 작업 폴더 밖에 쓰지 않고 sudo 를 쓰지 않는다.
4. 스크린샷·로그를 `output/publish-verify/<실행 id>/`로 가져오고, 원격 실행 폴더·띄운 프로세스·화면을 지운다.

2026-11-12 Strix Halo(Ubuntu, 원격 `env.sh`의 글꼴 Noto Sans CJK): 서버 통과 45초(새 방 → stop → 이어서 엶 → SIGTERM 으로 저장하고 닫음 → 인자 없음 종료 코드 2),
클라이언트 통과 2분(화면 19개 + 들어가기, 서버 로그 `들어옴:`, 지문 `65bc37d92f8fbaeb` 일치). strace 로 본 복사본 밖 열기는 실행 폴더 안 셰이더 캐시, `/var/lib/dbus/machine-id`뿐이었다.

호스트 테스트를 출력 폴더를 루트로 삼아 돌릴 수도 있다(테스트가 `HostOptions.Resolve` 로 루트를 얻으므로 환경 변수가 먹는다).

```
ROOTBORN_REPO_ROOT=<출력 폴더 복사본> dotnet test game/tests/Rootborn.Host.Tests
```
