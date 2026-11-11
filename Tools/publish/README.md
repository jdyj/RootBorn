# Tools/publish — 저장소 없이 도는 실행 폴더 만들기

```
Tools/publish/publish.sh              # win-x64 와 linux-x64 둘 다
Tools/publish/publish.sh win-x64      # 하나만
```

Git Bash(또는 Linux/macOS bash)에서 돈다. dotnet 은 `~/.dotnet` 또는 PATH 에 있으면 된다.
출력은 `output/publish/<플랫폼>/`(git 밖). 그 폴더가 곧 게임의 "저장소 루트"다 — 실행 파일을 거기서 바로 실행하면
실행 파일 위치에서 위로 올라가며 `Assets/modernuserinterface-win` 을 찾아 루트를 정하고, `--root <폴더>` 로 줄 수도 있다.

하는 일: `dotnet publish game/Rootborn.Host -c Release -r <플랫폼> --self-contained` → 출력 폴더에
`content.txt` 가 가리키는 게임 파일을 **같은 상대 경로로** 복사한다(`data/…`, `Assets/…`, `game/world/…`, `game/localization/…`).
Unity 전용 파일(`.meta .unity .asset .prefab .cs`)은 어떤 줄로도 복사되지 않는다.

## 목록(`content.txt`) 고치기

한 줄에 하나. `#` 주석.

| 줄 | 뜻 |
|---|---|
| `경로/글롭` | 그 파일(폴더면 안의 파일 전부) |
| `!글롭` | 앞에서 모인 것 중 맞는 것을 뺀다 |
| `@refs 글롭` | 맞는 JSON 안에 `"Assets/….png"` 로 적힌 그림을 전부 더한다 |
| `@ldtk 글롭` | `.ldtk` + 레벨 파일(`.ldtkl`) + 타일셋 PNG(`relPath`) |
| `@png 글롭` | 맞는 PNG |

더해진 PNG 마다 `data/atlases/<PNG 경로>.atlas.json`(그림 좌표표)이 있으면 자동으로 따라온다.

- **그림을 새로 바꿀 때**: 새 PNG 를 `Assets/…` 에 두고 데이터(JSON)가 그 경로를 가리키면 `@refs` 가 알아서 집는다.
  데이터가 가리키지 않는 PNG 는 `@png` 줄로 더한다. 쓰지 않게 된 폴더는 `!글롭` 으로 뺀다.
- **맵을 새로 그릴 때**: `.ldtk` 를 `Assets/LDtk/<이름>/<이름>.ldtk` 에 두면 `@ldtk Assets/LDtk/*/*.ldtk` 가 레벨 파일과 타일셋까지 집는다.
  다른 곳에 두면 `@ldtk` 줄을 더한다.
- 목록의 글롭이 아무것도 못 찾으면 "맞는 파일이 없는 줄" 경고, 데이터가 가리키는데 디스크에 없는 PNG 는 "없는 파일" 경고가 뜬다(경고는 복사를 막지 않는다).

## 글꼴

글꼴 파일은 넣지 않는다. 배포용 글꼴은 사용자 결정 대기다. 지금은 Host 가 운영체제 글꼴(Windows `malgun.ttf` 등)을 찾고,
못 찾으면 `--font <TTF>` 또는 `ROOTBORN_FONT` 를 요구하며 멈춘다. 글꼴이 정해지면 폴더에 넣고 `content.txt` 에 한 줄 더한 뒤 Host 가 그 경로를 기본으로 보게 한다(Host 변경 필요).

## 확인하는 법

출력 폴더를 저장소 밖으로 복사해 실행한다:

```
cp -r output/publish/win-x64 /tmp/rootborn-check
/tmp/rootborn-check/Rootborn.Host.exe --world Shop        # 인자 없이도 위치에서 루트를 찾는다
```

더 단단한 확인: 호스트 테스트를 출력 폴더를 루트로 삼아 돌린다(테스트가 `HostOptions.Resolve` 로 루트를 얻으므로 환경 변수가 먹는다).

```
ROOTBORN_REPO_ROOT=<출력 폴더 복사본> dotnet test game/tests/Rootborn.Host.Tests
```
