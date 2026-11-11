# Tools/publish — 저장소 없이 도는 실행 폴더 만들기

```
Tools/publish/publish.sh              # win-x64 와 linux-x64 둘 다
Tools/publish/publish.sh win-x64      # 하나만
Tools/publish/publish.sh --list       # 복사할 게임 파일 목록만(게시하지 않는다)
Tools/publish/verify.sh               # 만든 폴더가 저장소 없이 도는지 확인(아래 "확인하는 법")
```

Git Bash(또는 Linux/macOS bash)에서 돈다. dotnet 은 `~/.dotnet` 또는 PATH 에 있으면 된다.
출력은 `output/publish/<플랫폼>/`(git 밖). 그 폴더가 곧 게임의 "저장소 루트"다 — 실행 파일을 거기서 바로 실행하면
실행 파일 위치에서 위로 올라가며 `Assets/modernuserinterface-win` 을 찾아 루트를 정하고, `--root <폴더>` 로 줄 수도 있다.

하는 일: `dotnet publish game/Rootborn.Host -c Release -r <플랫폼> --self-contained` → 출력 폴더에
`content.txt` 가 가리키는 게임 파일을 **같은 상대 경로로** 복사한다(`data/…`, `Assets/…`, `game/world/…`, `game/localization/…`, `game/themes/…`).
Unity 전용 파일(`.meta .unity .asset .prefab .cs`)은 어떤 줄로도 복사되지 않는다.

2026-11-11 이 PC(Windows 11, dotnet 8.0.422, 빌드 캐시 있음)에서 잰 값:

| 플랫폼 | 걸린 시간 | 크기 | 파일 수 |
|---|---|---|---|
| win-x64 | 36초 | 99MB | 3,620개(실행 파일·런타임 204개 78MB + 게임 파일 3,416개 — `Assets` 17MB, `data` 5.4MB) |
| linux-x64 | 21초 | 100MB | 3,620개(같은 게임 파일 + ELF 실행 파일, SDL2·OpenAL `.so` 포함) |

목록 만들기는 그중 20초쯤이다. Windows 에서는 프로세스 하나 띄우는 데 수십 ms가 들므로 스크립트가 파일마다 grep·cp 를 띄우지 않게 했다.

**Linux 폴더를 Windows 에서 만들었으면** NTFS 가 실행 권한을 남기지 않는다. Linux 로 옮긴 뒤 `chmod +x Rootborn.Host` 한다.

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
글꼴이 정해지면 폴더에 넣고 `content.txt` 에 한 줄 더한 뒤 Host 가 그 경로를 기본으로 보게 한다(Host 변경 필요).

## 확인하는 법

```
Tools/publish/verify.sh                         # 이 PC 플랫폼(Windows: win-x64)의 배포 폴더
VERIFY_KEEP=1 Tools/publish/verify.sh           # 스크린샷을 남긴다(경로를 끝에 알린다)
Tools/publish/verify.sh --contents-only linux-x64   # 이 PC 에서 실행할 수 없는 플랫폼: 목록 대조만
```

1. `publish.sh --list` 의 파일이 배포 폴더에 다 있는지, Unity 전용 파일이 없는지 본다.
2. 배포 폴더를 **저장소 밖** 임시 폴더로 복사하고, 저장소를 가리키는 환경 변수(`ROOTBORN_REPO_ROOT` 등)를 지우고 `--root` 없이 실행한다.
3. 화면마다 한 번씩 `--frames <N> --screenshot <PNG>` 로 띄워 N 프레임 뒤 화면을 저장하고 끝내게 한다:
   슬롯 선택(`--save-root` 임시 폴더), 새 게임(`--slot`), `game/world/maps.json` 의 맵마다(`--world`).
   종료 코드 0, PNG 스크린샷이 비어 있지 않은지, 표준 오류가 비어 있는지 본다(Host 는 읽기 문제를 표준 오류에 쓰고, 멈추는 문제는 종료 코드 2).
4. Linux 에 `strace` 가 있으면 실행 중 연 파일 가운데 복사본 밖의 것을 보인다. Windows 에는 같은 수단이 없다
   — Host 가 연 파일 목록을 내는 인자가 생기면 그것으로 바꾼다.

세이브는 임시 폴더에만 쓴다(사용자 세이브 폴더를 건드리지 않는다). 스크린샷이 제대로 그려졌는지(그림, 한글 글자)는
스크립트가 판단하지 않는다 — `VERIFY_KEEP=1` 로 남겨 직접 연다. 2026-11-11 win-x64: 12번 실행 모두 통과, 134초.

호스트 테스트를 출력 폴더를 루트로 삼아 돌릴 수도 있다(테스트가 `HostOptions.Resolve` 로 루트를 얻으므로 환경 변수가 먹는다).

```
ROOTBORN_REPO_ROOT=<출력 폴더 복사본> dotnet test game/tests/Rootborn.Host.Tests
```
