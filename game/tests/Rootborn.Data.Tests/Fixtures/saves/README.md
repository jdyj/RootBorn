# 옛 세이브 고정 파일

Unity 판이 쓴 **QA 세이브**를 복사한 것이다. 사용자 세이브가 아니다.

| 폴더 | 출처 | 날짜 |
|---|---|---|
| `exe-20261105/slot-0/` | `output/dream-job-20261104/playthrough-20261105/saves-exe/slot-0/` (Unity 실행 파일로 플레이) | 2026-11-05 |
| `editor-20261105/slot-1/` | `output/dream-job-20261104/playthrough-20261105/saves/slot-1/` (Unity 편집기로 플레이. 같은 폴더의 `slot-0`은 비어 있다) | 2026-11-05 |

| `monogame-20261112/slot-robust/` | MonoGame 판이 쓴 슬롯: 메인 체인을 3일째까지 따라가고 집에 가구 하나를 놓은 뒤(Game 테스트 `SlotCheckpoints`의 `furniture-placed`) + 슬롯 정보와 자리. `SavedFilesAreRegisteredTests.WriteCurrentSlotFixture`로 내보냈다 | 2026-11-12 |
| `monogame-20261114/slot-robust/` | MonoGame 판이 쓴 슬롯: `furniture-placed`까지 같은 길 + 짓는 집에 들어서 왼쪽 위 방 바닥 꾸밈 하나를 바꾼 뒤(`SlotCheckpoints`의 `house-finished`, 짓는 집 꾸밈 `house-build.json` 형식 버전 1이 처음 든 슬롯) + 슬롯 정보와 자리. `WriteCurrentSlotFixture`로 내보냈다 | 2026-11-14 |
| `room-20261114/room-1/` | MonoGame 판이 쓴 **방 슬롯**(서버·내 PC의 방이 쓰는 것): 방 설정(돈·집을 같이 씀), 시계, 요청 장부, 조각(지갑·집·상자·꾸밈 `room-piece-house-build.json`·잡기 `room-piece-house-build-holds.json` — 꾸밈 조각 두 파일 형식 버전 1이 처음 든 방)과 손님 하나(기본 플레이어)의 세이브 `guests/local-player/`. 손님이 상자를 놓고 나무 5개를 넣고, 왼쪽 위 방 바닥을 방 지갑으로 꾸민 뒤(87칸), 옆 칸 하나를 붓으로 요청해 방이 잡았지만 답을 받기 전에 끊긴 때(잡기 하나, 손님의 보낼 목록에 그 요청). Game `RoomHouseBuildCompatTests.WriteCurrentRoomFixture`로 내보냈다 | 2026-11-14 |

**있는 폴더는 고치지 않는다.** 폴더 하나는 "그날의 프로그램이 쓴 세이브"다. 세이브 모양을 바꾸는 브랜치가 필드 이름·형을 바꾸면 이 파일들을 읽는 테스트(읽힌다, 모양이 버리는 필드가 없다, 읽고 다시 써도 같다, 그 슬롯으로 하루를 넘긴다)가 먼저 실패한다. 그때 고칠 것은 고정 파일이 아니라 코드다(옛 세이브를 계속 읽게). 형식을 바꾼 뒤(`Version`을 올린 뒤)에는 새 날짜 폴더를 **더한다**:

```bash
ROOTBORN_FIXTURE_OUT=<저장소>/game/tests/Rootborn.Data.Tests/Fixtures/saves/monogame-<날짜> \
  dotnet test game/tests/Rootborn.Game.Tests --filter "Name~WriteCurrentSlotFixture"
```

Unity 판 폴더는 2026-11-05 이전 파일이라 그 뒤에 생긴 필드(진로 후보, 예약된 미니게임 등)는 들어 있지 않다.
파일마다 무엇으로 읽는지, 아직 모양이 없는 파일, 일부러 버리는 필드는 `../../SaveShapeCoverage.cs`에 있다.
