# Scripts/remote-test — 무거운 테스트를 원격 Linux 장치에서

이 PC에서 커밋 하나를 원격 Linux 장치(지금은 Strix Halo)로 보내 가상 화면(Xvfb)에서 dotnet 테스트를 돌리고, 프로젝트별 건수·실패 목록·스크린샷을 가져온다. 여러 세션이 이 PC에서 동시에 Host 테스트를 돌리면 메모리가 모자라서 만들었다.

```bash
bash Scripts/remote-test/run.sh                                  # HEAD로 전체(Core 테스트 + game/Rootborn.sln)
bash Scripts/remote-test/run.sh jdyj/my-branch                   # 브랜치·커밋 이름(푸시 안 된 것도 된다)
bash Scripts/remote-test/run.sh HEAD -- --filter "FullyQualifiedName~MainChain"          # 인자가 -로 시작하면 game/Rootborn.sln에
bash Scripts/remote-test/run.sh HEAD -- game/tests/Rootborn.Host.Tests --filter "..."    # 대상을 직접 주기
bash Scripts/remote-test/run.sh --setup                          # 원격 준비(처음 한 번)
```

- **커밋된 것만 간다.** 커밋하지 않은 변경은 원격 실행에 들어가지 않는다(HEAD가 더럽으면 경고한다).
- 결과: `output/remote-test/<실행 id>/` — `run.txt`(커밋, 화면 번호, 걸린 초, 최고 메모리), `summary.txt`(프로젝트별 전체·통과·실패·건너뜀과 실패 목록), `trx/`, `*.log`, `output/`(테스트가 찍은 PNG). 종료 코드는 원격 `dotnet test`의 것이다.
- 근거의 종류: 원격 Xvfb의 Host 테스트도 **장치 이벤트 주입(OS 입력 계층 미확인)**이다. 그리기는 소프트웨어 GL(`LIBGL_ALWAYS_SOFTWARE=1`)이라 GPU 렌더링과 픽셀 값이 1쯤 다를 수 있다.

## 환경 변수 (이 PC)

| 변수 | 뜻 |
|---|---|
| `ROOTBORN_REMOTE_HOST` | 원격 장치 주소. 없으면 오류 |
| `ROOTBORN_REMOTE_USER` | 원격 사용자. 없으면 오류 |
| `ROOTBORN_REMOTE_KEY` | 그 장치 전용 개인키 파일 경로. 없으면 오류 |
| `ROOTBORN_REMOTE_DIR` | 원격 작업 폴더(원격 홈 기준 상대 경로). 기본 `rootborn-test` |

실제 값은 저장소에 적지 않는다. Strix Halo의 값은 사용자 전역 지침(`~/.claude/CLAUDE.md` "FEVM Ubuntu SSH 접속")에 있다. 접속 옵션은 `-o IdentitiesOnly=yes -o BatchMode=yes -o StrictHostKeyChecking=yes`로 고정이다(호스트 키 검사를 끄지 않는다). 처음 쓰는 장치는 먼저 사람이 한 번 접속해 호스트 키를 `known_hosts`에 넣어 둔다.

## 하는 일

1. `git push`로 커밋을 원격 bare 저장소(`<작업 폴더>/repo.git`)의 실행 전용 ref `refs/runs/<실행 id>`에 넣는다. origin에는 밀지 않는다.
2. 원격 스크립트 `remote-run.sh`를 ssh 표준 입력으로 넘긴다(늘 이 PC의 스크립트가 돈다 — 옛 커밋을 돌려도 같다). 원격에서:
   - 동시 실행 자리(`locks/slot-N`, 기본 2개)를 잡는다. 다 차 있으면 10초마다 다시 본다(최대 30분).
   - 가용 메모리가 한계(기본 8GB)보다 적으면 돌리지 않고 `refused.txt`를 남긴다(종료 코드 3).
   - `runs/<실행 id>/src`에 해시로 체크아웃(worktree)하고, 화면 번호 범위(기본 140~199)에서 빈 번호로 Xvfb를 띄운다. 이미 떠 있는 화면은 건드리지 않는다.
   - `nice -n 10 dotnet test`를 `-maxcpucount:<동시 수>`(기본 2), `--disable-build-servers`로 돌린다. 1초마다 이 실행의 프로세스 RSS 합과 testhost 하나의 최고 값을 잰다.
   - 끝나면(실패·끊김 포함) 이 실행의 프로세스(환경 변수 `ROOTBORN_TEST_RUN_ID` 표시가 붙은 것)와 Xvfb를 끝내고, 체크아웃과 ref를 지운 뒤 `results/.done`을 남긴다.
   - **연결 감시**: pty 없는 ssh는 이 PC 쪽이 죽어도 원격에 HUP을 보내지 않는다. 그래서 원격 스크립트가 15초마다 자기 부모(ssh 세션)가 바뀌었는지 보고 1분마다 진행 줄을 써 본다. 끊겼으면 그 실행을 끝내고 위처럼 정리한다(시험: 이 PC 쪽을 테스트 도중 죽이고 5초 뒤 정리 확인).
3. 원격 `results/.done`이 생길 때까지(최대 2분) 기다린 뒤 결과를 가져오고 원격 `runs/<실행 id>`를 지운다. 끝나지 않았으면 지우지 않고 가져오는 명령을 보인다.
   - 이 PC 쪽이 통째로 죽었으면 원격에 `runs/<실행 id>/results`만 남는다(프로세스와 화면은 정리됨). 필요하면 가져오고 지운다.

## 원격 준비 (`--setup`)

root 없이 원격 홈의 작업 폴더 안에만 만든다. 여러 번 돌려도 된다.

- `repo.git` — bare 저장소
- `dotnet/` — .NET 8 SDK(`dotnet-install.sh --channel 8.0`)
- `x/` — `apt-get download xvfb x11-xkb-utils xkb-data xdotool libxdo3` 후 `dpkg -x`
- `nuget/` — NuGet 패키지 캐시(실행끼리 함께 쓴다)
- `env.sh` — 위 위치, 글꼴(`ROOTBORN_FONT`, `fc-list :lang=ko`에서 고른다), 화면 번호 범위, 동시 실행 수(`ROOTBORN_TEST_MAX_RUNS`), 한 실행 안의 동시 테스트 프로세스 수(`ROOTBORN_TEST_PARALLEL`), 필요한 가용 메모리(`ROOTBORN_TEST_MIN_AVAILABLE_GB`). 이미 있으면 덮어쓰지 않는다. 장치 사정에 맞게 손으로 고친다.

필요한 것(원격): bash, git, curl, python3(결과 요약), `apt-get download`·`dpkg -x`, 한글 글꼴, Mesa(소프트웨어 GL). SDL2는 MonoGame DesktopGL이 들고 온 것을 쓴다(시스템 패키지 불필요). Xvfb에 빠진 라이브러리가 있으면 `--setup`이 이름을 보이고 멈춘다 — 그때는 사용자가 원격에서 `sudo apt install <패키지>`를 한다.

## 다른 장치를 쓰려면

같은 변수에 그 장치의 값을 주고 `--setup`을 한 번 돌린다. 장치마다 `env.sh`의 한계 값(동시 실행 수, 가용 메모리)을 그 장치의 다른 일에 맞춘다.
