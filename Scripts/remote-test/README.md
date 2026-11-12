# Scripts/remote-test — 무거운 테스트를 원격 Linux 장치에서

이 PC에서 커밋 하나를 원격 Linux 장치(지금은 Strix Halo)로 보내 가상 화면(Xvfb)에서 dotnet 테스트를 돌리고, 프로젝트별 건수·실패 목록·스크린샷을 가져온다. 여러 세션이 이 PC에서 동시에 Host 테스트를 돌리면 메모리가 모자라서 만들었다.

```bash
bash Scripts/remote-test/run.sh                                  # HEAD로 전체(game/Rootborn.sln. 옛 커밋은 Tools/Rootborn.Core.Tests도)
bash Scripts/remote-test/run.sh jdyj/my-branch                   # 브랜치·커밋 이름(푸시 안 된 것도 된다)
bash Scripts/remote-test/run.sh HEAD -- --filter "FullyQualifiedName~MainChain"          # 인자가 -로 시작하면 game/Rootborn.sln에
bash Scripts/remote-test/run.sh HEAD -- game/tests/Rootborn.Host.Tests --filter "..."    # 대상을 직접 주기
bash Scripts/remote-test/run.sh --no-wait HEAD                  # 시작만 하고 실행 id를 찍는다
bash Scripts/remote-test/run.sh --resume [<실행 id>]             # 이어서 기다리고 받기(id가 없으면 이 작업 폴더의 가장 최근 안 받은 실행)
bash Scripts/remote-test/run.sh --status                         # 원격에서 도는 실행과 아직 안 받아 간 실행, 자리
bash Scripts/remote-test/run.sh --cancel <실행 id>               # 도는 실행을 멈추고 거기까지의 결과를 받는다
bash Scripts/remote-test/run.sh --force HEAD                     # 같은 커밋·인자의 실행이 있어도 새로 시작
bash Scripts/remote-test/run.sh --setup                          # 원격 준비(처음 한 번)
```

- **원격 실행은 연결에서 떼어 돈다.** 이 PC 쪽 `run.sh`가 끊기거나 꺼져도(메모리 부족 등) 원격은 끝까지 가서 결과를 남긴다. 실행 id는 시작하자마자 화면과 `output/remote-test/<실행 id>/run-id`에 남으므로, 다시 `run.sh --resume`만 하면 이어서 기다리거나 받는다.
- **같은 커밋을 두 번 돌리지 않는다.** 같은 커밋·같은 인자의 실행이 돌고 있거나 받을 결과가 남아 있으면 그 실행에 붙는다(메모리 부족으로 거절된 실행과 취소된 실행에는 붙지 않는다). 여러 작업 폴더가 한 실행에 붙으면 모두 받아 간 뒤에 원격 폴더가 지워진다.

- **커밋된 것만 간다.** 커밋하지 않은 변경은 원격 실행에 들어가지 않는다(HEAD가 더럽으면 경고한다).
- 결과: `output/remote-test/<실행 id>/` — `run-id`, 받은 뒤 `fetched`, `remote.log`(원격 진행 줄), `exit-code`, `run.txt`(커밋, 화면 번호, 걸린 초, 최고 메모리), `summary.txt`(프로젝트별 전체·통과·실패·건너뜀과 실패 목록), `trx/`, `*.log`, `output/`(테스트가 찍은 PNG, 균형 보고 `output/balance/`의 파일). 종료 코드는 원격 `dotnet test`의 것이다(취소는 130, 정리 없이 죽은 실행은 137).
- 근거의 종류: 원격 Xvfb의 Host 테스트도 **장치 이벤트 주입(OS 입력 계층 미확인)**이다. 그리기는 소프트웨어 GL(`LIBGL_ALWAYS_SOFTWARE=1`)이라 GPU 렌더링과 픽셀 값이 1쯤 다를 수 있다.

멈춤 감시: 원격 `dotnet test`는 `--blame-hang --blame-hang-timeout <ROOTBORN_TEST_HANG_MINUTES>m`(기본 10, 원격 `env.sh`의 값 — `--setup`이 없을 때만 더한다)로 돈다. 한 테스트가 그 시간 동안 끝나지 않으면 testhost를 죽이고 돌던 테스트 이름을 `trx/` 아래 `Sequence_*.xml`에 남기며, 결과 폴더째 받아진다(덤프는 만들지 않는다).

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
2. 이 PC의 `remote-run.sh`·`remote-ctl.sh`를 `runs/<실행 id>/bin/`에 풀고 `remote-ctl.sh start`가 실행을 **연결에서 떼어**(`setsid nohup`, 표준 입출력은 `results/remote.log`) 띄운다. 늘 이 PC의 스크립트가 돈다(옛 커밋을 돌려도 같다). 띄우기 전에:
   - 끝난 지 `ROOTBORN_TEST_RESULT_TTL_HOURS`(기본 24시간)가 지난 결과, 정리 없이 죽은 채 그만큼 지난 실행을 지운다(남은 프로세스가 있는 실행, 도는 실행, 옛 스크립트로 시작한 끝나지 않은 실행은 건드리지 않는다).
   - 같은 커밋·인자의 실행(`meta`의 `key`)이 있으면 새로 띄우지 않고 거기에 붙는다(`--force`면 건너뜀).
3. 원격 실행(`remote-run.sh`)은:
   - 동시 실행 자리(`locks/slot-N`, 기본 2개)를 잡는다. 다 차 있으면 10초마다 다시 본다(최대 30분). 자리 주인을 `locks/slot-N.owner`(실행 id, pid, 시작 시각)에 적는다. 배경으로 띄우는 것은 잠금을 물려받지 않으므로 주 프로세스가 죽으면 자리가 풀린다. 주인이 끝났는데 자리가 잡혀 있으면(옛 스크립트로 띄운 프로세스가 남은 경우) 로그와 `--status`에 알린다 — 그 실행은 `--cancel <id>`로 끝낸다.
   - 가용 메모리가 한계(기본 8GB)보다 적으면 돌리지 않고 `refused.txt`를 남긴다(종료 코드 3).
   - `runs/<실행 id>/src`에 해시로 체크아웃(worktree)하고, 화면 번호 범위(기본 140~199)에서 빈 번호로 Xvfb를 띄운다. 이미 떠 있는 화면은 건드리지 않는다.
   - `nice -n 10 dotnet test`를 `-maxcpucount:<동시 수>`(기본 2), `--disable-build-servers`로 돌린다. 1초마다 이 실행의 프로세스 RSS 합과 testhost 하나의 최고 값을 잰다.
   - 끝나면(실패·취소 포함) 이 실행의 프로세스(환경 변수 `ROOTBORN_TEST_RUN_ID` 표시가 붙은 것)와 Xvfb를 끝내고, 체크아웃과 ref를 지운 뒤 `results/exit-code`와 `results/.done`을 남긴다.
4. 이 PC 쪽은 20초마다 `remote-ctl.sh state`로 상태를 보고, 바뀌었을 때만 진행(끝난 프로젝트의 통과/전체, 원격 마지막 줄)을 찍는다. 연결이 안 되면 30초마다 다시 보고 10번 실패하면 멈춘다(원격은 계속 돈다). `.done`이 생기면 결과를 받고 `fetched`를 남긴 뒤 자기 받을 쪽 표시(`runs/<id>/claims/`)를 지운다. 남은 받을 쪽이 없으면 원격 `runs/<실행 id>`를 지운다.
   - 주 프로세스가 정리 없이 죽었으면(원격 재부팅, 강제 종료) 받는 쪽이 그 실행의 남은 프로세스를 끝내고 뒷정리한 뒤 거기까지를 받는다.

원격 폴더에서 새 스크립트가 더하는 것: `runs/<id>/{pid,meta,bin/,claims/}`, `results/{remote.log,exit-code,cancelled}`, `locks/slot-N.owner`. 옛 스크립트로 시작한 실행과 섞여도 된다 — 옛 실행은 `--status`에 "옛 형식"으로만 보이고 새 스크립트는 그것을 끝내거나 지우지 않는다(끝나서 `.done`이 생긴 뒤 보관 시간이 지나면 지운다).

## 원격 준비 (`--setup`)

root 없이 원격 홈의 작업 폴더 안에만 만든다. 여러 번 돌려도 된다.

- `repo.git` — bare 저장소
- `dotnet/` — .NET 8 SDK(`dotnet-install.sh --channel 8.0`)
- `x/` — `apt-get download xvfb x11-xkb-utils xkb-data xdotool libxdo3` 후 `dpkg -x`
- `nuget/` — NuGet 패키지 캐시(실행끼리 함께 쓴다)
- `env.sh` — 위 위치, 글꼴(`ROOTBORN_FONT`, `fc-list :lang=ko`에서 고른다), 화면 번호 범위, 동시 실행 수(`ROOTBORN_TEST_MAX_RUNS`), 한 실행 안의 동시 테스트 프로세스 수(`ROOTBORN_TEST_PARALLEL`), 필요한 가용 메모리(`ROOTBORN_TEST_MIN_AVAILABLE_GB`), 안 받아 간 결과의 보관 시간(`ROOTBORN_TEST_RESULT_TTL_HOURS`, 기본 24). 이미 있으면 덮어쓰지 않고, 나중에 생긴 값 중 없는 것만 끝에 더한다. 장치 사정에 맞게 손으로 고친다.

필요한 것(원격): bash, git, curl, python3(결과 요약), `setsid`·`nohup`·`flock`(util-linux·coreutils), `apt-get download`·`dpkg -x`, 한글 글꼴, Mesa(소프트웨어 GL). SDL2는 MonoGame DesktopGL이 들고 온 것을 쓴다(시스템 패키지 불필요). Xvfb에 빠진 라이브러리가 있으면 `--setup`이 이름을 보이고 멈춘다 — 그때는 사용자가 원격에서 `sudo apt install <패키지>`를 한다.

## 다른 장치를 쓰려면

같은 변수에 그 장치의 값을 주고 `--setup`을 한 번 돌린다. 장치마다 `env.sh`의 한계 값(동시 실행 수, 가용 메모리)을 그 장치의 다른 일에 맞춘다.

## 두 기계로 같이 하기 (`two-machines.sh`)

같이 하기를 실제 네트워크로 본다: 이 PC(Windows)의 게임과 원격 Linux 장치(Strix Halo)의 데디케이티드 서버·게임이 집 안 네트워크(LAN)의 UDP로 만난다.
같은 기계 루프백 확인(`game/tests/Rootborn.Host.Tests/TwoGames`의 두 프로세스 테스트)이 못 보는 것 — 다른 운영체제, 실제 지연과 패킷, LAN 주소에 묶기, 배포 폴더의 서버 — 을 본다.
두 기계가 필요해 CI에는 넣지 않는다.

```bash
bash Scripts/remote-test/two-machines.sh                                   # 전부: 준비 → 테스트 → 증거 받기 → 원격 뒷정리와 확인
bash Scripts/remote-test/two-machines.sh --filter "FullyQualifiedName~TWO_MACHINES_001"   # 일부
bash Scripts/remote-test/two-machines.sh --no-publish                      # output/publish 를 그대로(이 스크립트가 이 커밋에서 만든 것일 때만, 아니면 다시 게시)
bash Scripts/remote-test/two-machines.sh --check                           # 원격에 남은 것(프로세스·듣는 포트·폴더)만 본다
```

접속 값은 위와 같은 환경 변수(`ROOTBORN_REMOTE_HOST`·`USER`·`KEY`, 작업 폴더 `ROOTBORN_REMOTE_DIR`)로만 받는다. **없으면 아무것도 하지 않고 "건너뛰었다"고 알린다**(종료 코드 0).
원격은 `run.sh --setup`으로 준비된 것(`env.sh`의 dotnet·Xvfb·글꼴)을 쓴다. 다른 커밋의 서버는 `TWO_MACHINES_OTHER_COMMIT`(기본 `HEAD~1`).

하는 일:

1. 이 PC에서 `Tools/publish/publish.sh linux-x64`·`--server linux-x64`, 다른 커밋의 소스를 `git archive`로 **저장소 밖**에 풀어 `ROOTBORN_COMMIT=<그 커밋>`으로 서버 게시
   (저장소 안에서 빌드하면 소스 링크가 이 저장소의 HEAD를 찍는다), `Rootborn.Host.Tests`를 Release로 빌드.
2. 원격 `<작업 폴더>/two-machines/<실행 id>/`에 올린다: `bin/two-machines-remote.sh`(원격 도우미), `peer/`(테스트 어셈블리), `client/`(linux-x64 배포 — 원격 게임의 루트), `server/`, `server-other/`.
3. 이 PC에서 `dotnet test --filter Category=TwoMachines`(`TwoGames/TwoMachinesHostTests.cs`, `[Explicit]`)를 `ROOTBORN_TWO_MACHINES_RUN=<원격 실행 폴더>`로 돌린다.
   테스트가 ssh로 원격 도우미를 부른다:
   - `server` — 서버를 **원격 장치의 LAN 주소 하나**(ssh가 닿은 주소, `SSH_CONNECTION`의 셋째 칸 — 스크립트에 적지 않는다)에 포트 0(운영체제가 준 빈 포트)으로 띄운다. 0.0.0.0으로 듣지 않는다.
     ssh 표준 입력의 줄이 서버 콘솔로 가고, 입력이 닫히면(테스트·ssh가 끝났다) `stop`을 넣어 저장하고 끝내게 한다. 시간 한도 `TWO_MACHINES_LIMIT`(기본 2h).
   - `peer` — 원격 가상 화면(범위 220~239, 원격 테스트·배포 확인과 겹치지 않는다)에서 `Rootborn.Host.Tests.dll --two-games-peer`. 동사 줄은 ssh 표준 입출력으로 간다(같은 `PeerGame`).
     두 게임은 동사 줄로 번갈아 한 프레임씩 돌지만 네트워크는 실제 시간이라 확인은 모두 "조건이 될 때까지 기다린다(넉넉한 프레임 한도)"로 쓴다.
   - `client-address` — 이 PC의 LAN 주소(ssh 출발 주소). 이 PC에서 방을 열 때 그 주소 하나에 묶는다.
   - `guest-files` — 서버 세이브(`guests/` 포함) 목록.
   띄운 프로세스에는 `ROOTBORN_TWO_MACHINES_RUN=<실행 id>` 표시를 붙인다.
4. 증거를 `output/two-machines/<실행 id>/`로: `two-machines.trx`, `test.log`, 이 PC 스크린샷(`two-machines-*.png`), 원격 스크린샷과 서버 로그(`remote-evidence/`), 재기 표(`two-machines-measurements.md`).
5. 끝나면(실패·Ctrl+C 포함) 원격 도우미 `cleanup`이 그 표시가 붙은 프로세스만 끝내고 실행 폴더를 지운다. 이어서 `check`가 남은 것(표시가 붙은 프로세스, 이 사용자의 Rootborn 프로세스,
   이 사용자 프로세스가 듣는 LAN 포트, `two-machines/` 아래 폴더)을 찍어 `check.txt`에 남긴다.

| 테스트 | 무엇 |
|---|---|
| `TWO_MACHINES_001` | 원격 서버(돈·집 공유) + 이 PC 게임: 서버 주소로 들어가기 → 걷기, 알바로 돈(방 지갑이 HUD에), 상자 놓고 넣기, 가구 놓기, 자기(혼자라 바로 다음 날), 서버의 `guests/`에 진행, 게임을 끄고 다시 들어오면 이어짐, 서버를 껐다 켜도 이어짐 |
| `TWO_MACHINES_002` | 원격 서버 + 이 PC 게임 + 원격 게임: 서로 보이고 걷는 것이 양쪽으로 따라옴, 원격이 번 돈이 이 PC HUD에, 이 PC가 상자에 넣은 나무를 원격이 꺼냄, 1분 재기, 둘 다 자면 다음 날 |
| `TWO_MACHINES_003` | 이 PC가 방 열기(이 PC의 LAN 주소), 원격 게임이 들어옴 — Windows 방화벽이 받는 UDP를 막으면 실패하고 "사용자 확인 필요"로 적는다(방화벽 창은 조작하지 않는다) |
| `TWO_MACHINES_004` | 다른 커밋의 원격 서버에 이 PC 게임이 들어가려 하면 "게임 버전이 다르다"로 거절되고 들어가기 창에 이유가 보인다 |

근거의 종류: 두 기계(Windows ↔ Linux), 실제 LAN, 장치 이벤트 주입(OS 입력 계층 미확인). 원격 게임의 그리기는 소프트웨어 GL이다. 사람이 직접 한 플레이와 인터넷을 거치는 접속은 미확인이다.
재기의 왕복 지연·바이트는 이 PC 게임의 연결(LiteNetLib `NetManager`)을 테스트가 리플렉션으로 찾아 통계를 켜고 읽은 값이다(`TwoGames/NetProbe.cs` — 게임 코드에 재는 입구는 없다).
