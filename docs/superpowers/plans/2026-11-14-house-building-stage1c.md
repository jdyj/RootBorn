# 집 꾸미기 1단계 (다) — 같이 쓰는 집에서 꾸밈 공유(1-8) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 집을 같이 쓰는 방에서 누구든 가구 배치 화면(P)의 꾸미기로 벽지·바닥·띠 색을 바꾸면 방(서버)이 판정하고 모두의 화면에 같은 꾸밈이 보인다. 돈은 요청한 사람(돈을 같이 쓰면 방 지갑)이 한 번만 낸다. 곁가지: 같이 쓰는 집에서 보관한 가구 꺼내기(결정 16).

**Architecture:** 새 공유 조각 `house-build`(Core `RoomHouseBuild`, `house`를 따라 켜진다 — 상자 `chests`와 같은 등록)가 방 쪽 꾸밈 기록(혼자 하는 집과 같은 `HouseBuildSaveData`)과 잡기 목록을 든다. 요청은 (면, 꾸밈 id, 도구, 가리킨 칸, 기대한 바뀌는 칸 수)이고 방이 같은 Core `HouseFinishes`·`RoomFill`·출처표로 다시 판정해 칸을 **잡는다** → 요청한 쪽이 가구 사기와 같은 길(`Inventory.TryExchange`, 돈을 같이 쓰면 `Shared(…)`로 방 지갑)로 돈을 내며 같은 바꾸기 안에서 **쓰기**를 적는다 → 방이 적용하고 모두에게 알린다. 돈을 못 내면 **풀기**. 플레이어 쪽 `GameSession`은 방이 알린 기록으로 두 번째 `HouseFinishes`(비추기)를 채워 조회·미리 보기를 낸다 — Host 어댑터와 조합기는 바뀌지 않는다. 서버·내 PC의 방은 짓는 집 틀을 그 맵(LDtk)에서 만든다.

**Tech Stack:** C# .NET 8 + NUnit(Core/Data/Game/Screens/Server/Simulation/Host), MonoGame 3.8.6.1(Host만). 데이터·키트 그림은 고치지 않는다(도구 테스트는 돌리기만).

**Spec:** `docs/superpowers/specs/2026-11-14-house-building-design.md` 8절(같이 하기), 12.1절 1-8, 7절(세이브)(권위). 사용자 결정: 집을 같이 쓰는 방에서는 구조와 꾸밈도 같이 보인다(설계서 1절), 시간은 늘 공유·아이템은 절대 공유 안 함·돈 등은 방마다 고르는 설정(`docs/superpowers/specs/2026-11-12-multiplayer-design.md` 1·2절). 결정 대기: `docs/superpowers/specs/2026-11-14-house-building-open-decisions.md`(지금 값이 기준, 이 계획이 55~66번을 더함).

## 출발점 — 앞 계획(`…-stage1b.md`, 이 계획의 작업 9가 지운다)에서 받는 것

앞 계획 맨 끝 "다음 계획(1c)이 기대는 것"을 그대로 쓴다(이름을 바꾸지 않는다): `HouseFinishes`(`Load`·`Attach(frame, find)`·`Preview(finishId, tool, pointer, unlocks, canPay)`·`PreviewBand(finishId, unlocks, canPay)`·`Apply(preview)`·`ToSaveData()`·`Version`·`Band`·`BandColors`), 정적 `HouseFinishes.IsUnlocked(finish, unlocks)`, `RoomFill`, `HouseBuildFrame`(`FrameId`·`Tiles`·`DefaultWall`·`DefaultFloor`·`DefaultBand`), `HouseFinishTools`, `HouseFinishReasons`, `HouseFinishPreview`(`Cells`·`ChangedCells`·`HighlightCells`·`Cost`·`ReasonKey`·`StateVersion`·`CanApply`·`Refused`), `HouseBuildSaveData`·`HouseFinishCellSaveData`, `SaveFileNames.RoomPiece(name)`, World `IHouseFinishLookup`·`HouseBuildFrames.From(LoadedMap)`, Host `HostGame.SessionHouseFinishes`(`GameSession` 조회 셋만 부른다), 알림 `GameChangeTopics.HouseBuild`, `GameSession.PreviewHouseFinish`·`ApplyHouseFinish`·`PreviewHouseBand`·`ApplyHouseBand`·`HouseWallAt`·`HouseFloorAt`·`HouseBandColors`·`HouseBuildVersion`·`HouseBuildOpen`·`EnterHouseBuild`·`LeaveHouseBuild`, 가게 `PurchaseFinish`(개인 해금 — 이 계획과 무관).

## 이 계획을 쓰며 코드로 확인한 것

**이미 돼 있어서 뺀 것(근거)**
- **조각 상태를 나눠 보내기**: `game/Rootborn.Net/Room/RoomShareMessages.cs` `RoomPieceStates.Messages`가 메시지 하나에 안 들어가는 상태를 나눠 보낸다(`MaxStateIds` 8192, 줄 하나 `MaxIdBytes` 128바이트). 새 조각은 일반 `RoomPieceState`라 **Net에 새 메시지·코드가 필요 없다**(Net 테스트는 돌리기만).
- **같은 요청 id 한 번만**: `RoomPieceSet.Apply`의 요청 장부(`RoomRequestLedger`)가 모든 조각에 한다.
- **돈 공유 방의 미리 보기·기다림 문구**(앞 계획 최종 검토 I1): `d52908954`(`CanPayFinish`가 확인용 범위)·`dfd29e09b`(`FinishPalette`가 `WaitingForRoom`이면 `RoomMoneyText.WaitingKey`). 같이 쓰는 집에서도 같은 줄을 쓴다.
- **Host 다시 조합**: `HostGame`이 `ChangeTopics.HouseBuild`에서 `World.RefreshComposedTiles`를 부르고, 조합기는 `SessionHouseFinishes` → `GameSession` 조회만 본다. 조회가 방의 기록을 내면 **Host 그리기 코드는 바뀌지 않는다**.
- **처음 배치 가구는 같이 쓰는 집에 주지 않는다**(`GameSession.WantsHouseStarter`의 `_roomHouse == null`), **가게 구매·해금은 개인**(`PurchaseFinish` → `FurnitureUnlocks`).
- **옛 서버는 새 파일을 건드리지 않는다**: 따라 켜지는 조각은 방 설정에 이름이 없고(`RoomPieceCatalog.ActiveNames`가 등록 표의 `Follow`로 더한다) 옛 서버의 등록 표에는 `house-build`가 없어 만들지도 읽지도 쓰지도 않는다. 옛 판(프로그램·데이터 지문이 다르다)은 새 방에 들어오지 못한다(`RoomVersions`).
- **데이터 지문의 자리**: 셈은 `game/Rootborn.Data/DataFingerprint.cs` 한 곳이다(`game/Rootborn.Server/DataFingerprint.cs`는 그것을 부르는 통로). 지금 LDtk는 들지 않는다.

**가구 공유가 본보기다 — 지금 하는 일(코드로 확인)**
- 같이 쓰는 집의 가구는 방 조각 `house`(`RoomHouse`, `room-piece-house.json`)에 있고 플레이어 쪽은 `RoomHouseClient`가 방이 알린 목록을 비춘다. 놓기·옮기기·치우기는 "요청 → 방이 허락한 뒤 적용"이고 기다리는 요청은 하나(`_houseWaiting`, `WaitingForRoom`).
- **누가**: 옮기기·치우기는 방에 있는 누구나, 놓기는 그 가구를 해금한 사람 — **해금은 놓는 사람 쪽 게임 한 판이 확인하고 방은 믿는다**(`RoomHouse` 주석). 놓기는 값이 없다(가구 값은 가게에서 해금으로 낸다 — `PurchaseFurniture`가 `Shared(…)`로 개인 또는 방 지갑).
- **새 방은 빈 집이다**: `RoomServer`는 새 방이면 빈 조각을 저장하고, 방장 슬롯의 자기 배치 파일을 방으로 옮기는 코드가 없다. 같이 쓰는 동안 자기 배치 파일은 읽지도 쓰지도 않는다(`SaveHouseLayoutIfDirty`의 `_roomHouse != null` 갈래, Game `HouseShareOn_StartingAgain_RestoresTheRoomsHouse_NotTheOwnFile`). 그래서 같은 슬롯을 혼자 하면 자기 집이 그대로다. **꾸밈도 같게 한다**(결정 59).
- **잡기 → 쓰기/풀기**: 상자 `RoomChests`가 넣기·꺼내기를 잡고, 플레이어 쪽이 가방을 바꾸는 바꾸기 안에서 쓰기를, 못 바꾸면 풀기를 적는다. 잡힌 것은 그 사람만 쓰거나 풀고(`Invalid`), 잡기는 방 세이브에 남는다. 듣는 쪽이 붙기 전에 온 답은 들고 있다가 넘긴다(`RoomChestsClient._unheard`). 꾸밈의 지불은 이 틀을 쓴다.
- **방 지갑**: `RoomWalletClient`는 범위 밖의 돈 쓰기를 거절하고, `GameSession.Shared(run, applied)`가 범위를 열어 잡기를 보내고 허락이 오면 같은 명령을 다시 한다(`OnGranted`), 거절이면 `OnRefused`. 지금 `Shared`에는 "끝내 안 됐을 때 할 일"을 줄 자리가 없다 — 작업 2가 더한다.
- **보관 가구의 인스턴스 id**: 같이 쓰는 집의 배치는 방 기록과 방이 준 id로 다시 채운다(`SharedHouseLayout` → `RestoreRelocating(records, catalog, _houseIds)`). 보관(`StoredFurniture.Index`)은 기록 순서 번호(`SourceIndex`)라 `_houseIds[index]`가 그 방 인스턴스 id다. 방의 `Move`는 그 가구를 뺀 나머지 위에서 판정하고 자리를 바꾼다 — **보관 꺼내기 = 방 "옮기기" 요청**이라 새 요청 종류가 필요 없다(작업 6).
- 서버의 집 칸: `HouseGrids.Load`가 `interactions.json`의 `placementSurface`마다 `WorldMapLoader.LoadMap(mapId)`로 LDtk를 읽는다. 같은 `LoadedMap`에서 `HouseBuildFrames.From`으로 틀을 낼 수 있다. 내 PC의 방은 `HostGame.CreateHosting`이 `World?.Rooms.GridForSurface`를 넘긴다(`RoomAwareMaps`).

**설계서와 달리 정한 것(작업 9가 설계서 12.1.1절에 적는다)**
- 요청은 칸 목록이 아니라 (면, 꾸밈 id, 도구, 가리킨 칸) + **기대한 바뀌는 칸 수**(`Amount`). 방이 다시 낸 칸 수가 다르면 거절한다 — 손님 화면의 상태가 낡았다는 뜻이고, 돈은 그 칸 수로 셈하므로 어긋날 수 없다. 띠 색은 (면, 띠 꾸밈 id), 칸 수 1.
- **지불은 돈 공유 켬·끔이 한 길**이다: 방이 칸을 잡는다 → 요청한 쪽이 낸다(끔: 자기 지갑, 켬: 같은 명령을 `Shared(…)`로 — 방 지갑 잡기 → 쓰기) → 꾸밈 쓰기/풀기. 설계서 8.3절은 켬일 때 방 지갑을 먼저 잡는다고 적었다(결정 57).
- 방 조각 파일은 둘: 꾸밈 `room-piece-house-build.json`은 혼자 하는 집과 **같은 모양** `HouseBuildSaveData`(앞 계획의 약속), 잡기는 `room-piece-house-build-holds.json`(새 모양). 한 묶음으로 쓴다(`RoomPieceSet.Save`).
- 해금은 방이 확인하지 않는다(가구 놓기와 같다 — 결정 56). 설계서 8.3절 "해금은 각자"와 같은 뜻이다.
- 설계서 8.2절의 "`HouseGrids`가 `HouseStructure`로 칸을 만든다"는 2단계(벽) 일이다. 1c는 틀(출처표)만 서버에 둔다.

## 앞 계획 최종 검토의 Minor(`.superpowers/sdd/2026-11-14-house-building-stage1b/final-review.md`)

| # | 받는가 | 까닭 |
|---|---|---|
| 1 비용 아이템이 둘 이상이면 화면에 첫째만 | 받지 않는다 | 이 영역(공유)과 무관, 데이터는 하나뿐. 방 판정은 아이템별로 합친 `Cost`를 그대로 쓴다 |
| 2 벽지를 칸 붓으로 방 가운데에 대면 "이미 이 꾸밈" | **받는다(작업 1)** | 방도 같은 `Preview`를 쓰므로 이유가 방·화면에 함께 맞는다. 덮는 칸 중 정면 주인 칸이 0이면 `NoTarget` |
| 3 `build`가 있는데 출처표를 못 만든 맵 | 받지 않는다 | 드문 맵 문제, 콘솔에 이미 남는다. 서버 쪽은 작업 3이 같은 맵 문제를 로그로 남긴다 |
| 4 처음 배치 기록이 면과 상관없이 집 파일에 | 받지 않는다 | 혼자 하는 집의 일이고 지금 데이터는 면 하나 |
| 5 키트가 띠 꾸밈 시트를 안 봄 | 받지 않는다 | 도구 쪽, 이 계획은 키트를 고치지 않는다 |
| 6 제품의 시험용 고리 `HouseFinishCommitting` | 받지 않는다(늘리지 않는다) | 작업 2는 새 고리 없이 방을 손으로 돌리는 테스트(`FakeRoom`)로 실패를 만든다 |
| 7 목록 기록 안 필드는 미래 빌드가 더해도 사라짐 | **받는다(작업 1)** | 새 잡기 모양에도 같은 일이 있다. 두 모양 주석에 "칸마다 새 값은 맨 위 새 배열로" 한 줄 |
| 8 설계서 13절 성능 문장이 지금과 다름 | **받는다(작업 9)** | 문서 정리 |

## Global Constraints

- 앞 계획의 것을 그대로 잇는다: 규칙은 Core(`game/Rootborn.Core/src`)에만, Core는 `System.IO`·`System.Text.Json`·엔진을 참조하지 않는다(CI `check-core-references.sh`). 화면은 `GameSession` 명령·조회만, MonoGame 참조는 `Rootborn.Host`·`Rootborn.Host.Tests`에만.
- 엔티티 id로 분기하지 않는다 — 맵 id·꾸밈 id·가구 id·면 id·조각 이름 포함. 짓는 집은 맵 성질 `build`로, 조각은 등록 표로 고른다(`check-no-entity-id-branching.sh`). 요청 종류는 글 상수(`RoomHouseBuild.Finish` 등), enum으로 닫지 않는다.
- 사람이 읽는 문구는 현지화 키로 `game/localization/ko.json`·`en.json` 둘 다에. 글자 14 이상, 2배 길이 문구에도 패널 밖으로 안 나감.
- **아이템(가방)은 어떤 설정에서도 남에게 가지 않는다.** 이 계획의 지불은 돈(지갑)뿐이고, 꾸밈은 방의 것, 해금은 개인 것이다.
- **지급·지불은 전부 아니면 전무, 같은 요청은 한 번만.** 방이 칸을 잡은 뒤에만 돈이 나가고(돈을 내는 바꾸기 커밋 안에서 쓰기를 적는다 — 메모리만, I/O·예외 없음), 돈을 못 내면 풀기. 개인 쪽을 먼저 바꾸고 되돌리는 흐름은 없다. 같은 요청 id는 방의 장부가, 같은 꾸밈의 다시 요청은 "바뀌는 칸 0 → 거절"이 막는다.
- 잡힌 칸(그 잡기의 덮는 칸 전부, 같은 자리)은 잡은 사람이 쓰거나 풀 때까지 다른 꾸밈 요청이 바꾸지 못한다. 잡기는 방 세이브에 남고 서버를 다시 켜도 이어진다.
- **세이브 하위호환·상위호환**(스킬 `save-compat-change`): 있는 고정 폴더(`editor-20261105`, `exe-20261105`, `monogame-20261112`, `monogame-20261114`)를 고치지 않는다, 데이터에 없는 꾸밈 id의 기록을 버리지 않는다, 읽기만으로 파일을 쓰지 않는다, `ReadableFrom`을 올리지 않는다, `Require`에 넣지 않는다, 새 모양은 `SaveShapeCoverage.cs`에. **같이 쓰는 집에서는 자기 `house-build.json`을 읽은 대로 들고만 있고 쓰지 않는다**(방장·손님 모두).
- 상태 줄 하나는 128바이트(`RoomShareMessageBodies.MaxIdBytes`) 아래, 구분 글자 `RoomHouse.Separator`('|')가 든 id는 받지 않는다(`Invalid`).
- 매 프레임 다시 계산하지 않는다: 비추기 기록은 방의 상태가 올 때만 다시 읽고(`Load`), 화면 미리 보기는 포인터 칸·`HouseBuildVersion`이 바뀔 때만. 방은 요청을 받을 때만 판정한다.
- 테스트는 임시 폴더와 고정 파일만 쓴다. 실패 테스트 먼저. 그림의 특정 픽셀 색을 단언하지 않는다.
- 근거 종류를 나눠 쓴다: 자동 테스트 / 장치 이벤트 주입(OS 입력 계층 미확인, 두 프로세스·같은 기계 루프백) / 직접 본 화면(스크린샷 경로). Host 테스트에서 돈을 주거나 명령을 직접 부른 것은 "준비 단계, 실제 플레이 아님". 화면을 바꾼 작업은 스크린샷을 직접 연 뒤에 끝.
- **새 게임의 모습·여러 프로젝트가 쓰는 것(공유 조각 등록 표, 세이브 모양, 데이터 지문)을 바꾸는 작업은 Core·Data·Game·Screens·Server·Simulation·Net 전부를 돌린다.** 데이터(`data/`)·키트를 고치지 않으므로 도구 테스트(Python)는 작업 9에서 한 번 돌린다. 이 PC에서 Host는 `--filter`로만, 합치기 전 원격 전체 실행(`bash Scripts/remote-test/run.sh <커밋>`, 끊기면 `--resume`). Host 두 창 테스트(`TwoGamesHostTests`)는 무거우니 원격에서 돌린다.
- dotnet은 이 PC에서 한 번에 한 프로젝트씩: `export PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet"` 뒤 `dotnet test game/tests/<프로젝트>`. 지금 건수 Core 1328, Data 540, World 172, Game 1052, Screens 595, Ui 139, Net 87, Server 124, Simulation 107, Balance 100, Host 276(원격, 285 중 9 건너뜀), 도구 590 — 줄지 않는다(지운 테스트는 같은 뜻의 새 테스트로 바꾸고 보고에 적는다).
- 커밋: 한글 제목·본문, prefix(`[FEATURE]` `[NETWORK]` `[META]` `[UI]` `[TEST]` `[DOCS]` `[CHORE]`), 끝줄은 그 커밋을 만든 모델의 `Co-Authored-By: <모델 이름> <noreply@anthropic.com>`. 브랜치 `room-builder`에만, 푸시하지 않는다. 파일을 하나씩 골라 `git add <경로>`.
- **게임 수치·누가 꾸밀 수 있는가 같은 기획은 가구의 지금 규칙을 따르는 추천안**이고 결정 대기 55~66번에 있다. 수치는 앞 계획의 시험용 그대로(벽지 칸당 2, 바닥 1, 띠 색 무료).
- 사용자의 미커밋 파일 `art/world/rooms/test.two-rooms/{test.two-rooms.ldtk,tiles.png,tiles.slots.json}`을 건드리지 않는다(`room build test.two-rooms`·`repaint`를 돌리지 않는다, `git add` 하지 않는다).

## Review Focus

1. **두 사람이 같은 방을 동시에 다른 꾸밈으로.** 먼저 도착한 요청만 칸을 잡고, 뒤 요청은 잡힌 칸이라 거절된다(둘 다 쓰기 전이면). 앞 것이 끝난 뒤 온 요청은 바뀐 상태로 다시 판정되어 기대한 칸 수가 달라 거절. 돈은 된 사람만 한 번 — 작업 1 `HeldCells_RefuseAnotherRequestOnTheSameSlot_OtherSlotStillWorks`, 작업 2 `TwoAtOnce_SameRoom_OnlyTheFirstIsHeld_TheSecondPaysNothing`, 작업 7 부딪힘 시험.
2. **요청 뒤 허락 전에 끊김, 허락 뒤 돈을 내기 전에 끊김.** 다시 들어오면 장부가 같은 답을 다시 주어 그때 돈을 내고 끝내거나, 다시 할 명령이 없는 내 잡기를 푼다. 돈은 많아야 한 번, 잡힌 칸은 풀린다 — 작업 2 `RestartAfterRequest_BeforeTheAnswer_AnswerReplays_PaysOnce`, `RestartAfterGrant_BeforePaying_MyStrayHoldIsReleased_NoMoney`, 작업 3 `AGuestDropsBetweenGrantAndPay_ComesBack_FinishesOnce`.
3. **손님의 데이터 지문이 다름(집 LDtk가 다른 판).** 짓는 집 LDtk가 지문에 들어 들어가기에서 거절되고 이유를 보인다. 짓는 집이 아닌 맵 LDtk·그림 PNG는 지문에 들지 않는다 — 작업 3 `BuildMapLdtk_IsCounted_OtherLdtkAndPictures_AreNot`, `AGuestWithADifferentHouseMap_IsRefusedAtTheDoor`.
4. **서버 다시 시작 뒤 첫 입장.** 꾸밈과 잡기가 파일에서 이어지고, 새로 들어온 사람의 첫 상태가 같은 그림이며, 잡힌 칸은 여전히 잡혀 있다 — 작업 3 `TheRoomRestarts_FinishesAndHoldsContinue_ANewArrivalSeesThem`, 작업 4 고정 폴더 `room-20261114`.
5. **호스트가 혼자 꾸민 집을 같이 쓰기 시작.** 방은 기본 꾸밈으로 시작하고(가구와 같다), 호스트의 `house-build.json`은 방에 있는 동안 바이트 그대로이며, 같은 슬롯을 혼자 하면 자기 꾸밈이 보인다 — 작업 2 `HostsSoloDecor_IsNotCarriedIntoANewRoom_AndIsBackWhenPlayingAlone`.

---

### Task 1: Core 방 조각 `house-build` — 잡기 → 쓰기/풀기

모델: 높음. 멈춤 없음.

**Files:**
- Create: `game/Rootborn.Core/src/Room/RoomHouseBuild.cs`(방 쪽 조각, 플레이어 쪽 조각, 상태 줄, 잡기 세이브 모양)
- Modify: `game/Rootborn.Core/src/Room/RoomHouse.cs`(`RoomHouseRules` 생성자 선택 인자 둘과 조회 둘), `game/Rootborn.Core/src/Room/RoomPieces.cs`(`CreateDefault`에 `Follow` 한 줄), `game/Rootborn.Core/src/Building/HouseFinishes.cs`(해금 판단을 받는 오버로드, 비용 셈 공개, Minor 2), `game/Rootborn.Core/src/Building/HouseBuildSave.cs`(Minor 7 주석), `game/Rootborn.Core/src/Save/SaveFileNames.cs`(`public const string RoomHouseBuildHolds = "room-piece-house-build-holds.json";`), `game/tests/Rootborn.Data.Tests/SaveShapeCoverage.cs`(두 줄)
- Test: `game/tests/Rootborn.Core.Tests/Room/RoomHouseBuildTests.cs`(새 — 같은 폴더가 없으면 `RoomChestsTests.cs` 옆), `game/tests/Rootborn.Core.Tests/Building/HouseFinishesTests.cs`(더함)

**Interfaces:**
- Consumes: `HouseFinishes`, `HouseBuildFrame`, `HouseFinishPreview`, `HouseFinishTools`, `RoomFinishSlots`, `RoomFinishDefinition`, `IRoomPiece`, `IRoomPieceClient`, `RoomOutbox`, `RoomRequestSaveData`, `RoomAnswer`, `RoomOutcomes`, `RoomPieceState`, `RoomHouse.Separator`, `SaveFileNames.RoomPiece`.
- Produces (모두 `sealed`):
  - `HouseFinishes`: `public HouseFinishPreview Preview(string finishId, string tool, CellPos pointer, Func<RoomFinishDefinition, bool> isUnlocked, Func<IReadOnlyList<ItemAmount<string>>, bool> canPay)`, `public HouseFinishPreview PreviewBand(string finishId, Func<RoomFinishDefinition, bool> isUnlocked, Func<IReadOnlyList<ItemAmount<string>>, bool> canPay)` — 있는 오버로드(`FurnitureUnlockProgress`)는 `f => IsUnlocked(f, unlocks)`로 이것을 부른다. `public static IReadOnlyList<ItemAmount<string>> CostFor(RoomFinishDefinition finish, int changedCells)`(지금 private 셈을 공개 — 띠는 `changedCells` 1이면 `Cost` 한 번, 0이면 빈 목록; 방·플레이어가 같은 하나를 쓴다).
  - `RoomHouseRules`: 생성자 끝에 선택 인자 `Func<string, HouseBuildFrame> buildFrameFor = null, IEnumerable<RoomFinishDefinition> finishes = null`(있는 호출은 그대로), `public HouseBuildFrame BuildFrameFor(string surfaceId)`(모르면 null), `public RoomFinishDefinition FindFinish(string id)`(없으면 null).
  - `public sealed class RoomHouseBuild : IRoomPiece` — `PieceName = "house-build"`, 요청 종류 `Finish = "finish"`, `Band = "band"`, `Use = "use"`, `Release = "release"`. `public RoomHouseBuild(RoomHouseRules rules = null)`, `Name`, `Revision`, `public int HeldCount { get; }`, `public HouseBuildSaveData ToSaveData()`(꾸밈 기록 — 시험·세이브용), `Apply`, `State`, `Load`, `Save`.
  - `public sealed class RoomHouseBuildSaveData`는 **만들지 않는다** — 꾸밈 파일은 `HouseBuildSaveData`. 잡기 파일: `[Serializable] public sealed class RoomHouseBuildHoldsSaveData : IVersionedSave { public const int KnownVersion = 1; public int Version; public RoomHouseBuildHoldSaveData[] Holds = Array.Empty<…>(); }`, `[Serializable] public sealed class RoomHouseBuildHoldSaveData { public string RequestId = "", PlayerId = "", SurfaceId = "", Kind = "", FinishId = "", Tool = ""; public int X, Y, Count; }`.
  - `public static class RoomHouseBuildLines` — 상태 줄: `"f|<frameId>"`(붙은 틀 또는 읽은 `FrameId`, 한 줄), `"b|<band>"`(한 줄, 빈 글 허용), `"c|<x>|<y>|<wall>|<floor>"`(기록마다, y 다음 x 순, 빈 자리는 빈 글), `"h|<requestId>|<playerId>"`(잡기마다, 요청 id 순). `public static bool TryDecode(IReadOnlyList<string> lines, out HouseBuildSaveData data, List<KeyValuePair<string, string>> holds)`. `State().Amount` = 기록 칸 수.
  - `public sealed class RoomHouseBuildClient : IRoomPieceClient` — `public RoomHouseBuildClient(RoomOutbox outbox)`, `Name`, `public bool Known`, `public HouseBuildSaveData Data`(마지막 상태로 만든 기록, 처음엔 빈 기록 — null 아님), `public void CopyHoldsOf(string playerId, List<string> holdIds)`, `public bool HasPending`(이 조각의 `finish`·`band` 요청이 답을 기다린다), `public string RequestFinish(string surfaceId, string finishId, string tool, CellPos pointer, int expectedCells)`, `public string RequestBand(string surfaceId, string finishId)`, `public void AddUse(string holdId)`, `public void AddRelease(string holdId)`, `public event Action Changed`, `public event Action<RoomRequestSaveData, bool> Answered`(`finish`·`band`의 답만 — `RoomChestsClient`처럼 듣는 쪽이 붙기 전 답은 들고 있다가 처음 붙을 때 넘긴다; `use`·`release`의 답은 `Changed`만).
  - `RoomPieceCatalog.CreateDefault`: `.Follow(RoomHouseBuild.PieceName, RoomHouse.PieceName, () => new RoomHouseBuild(houseRules), outbox => new RoomHouseBuildClient(outbox))`(상자 줄 다음).
- 뜻(테스트가 고정한다):
  - **`finish`**(Ids = 면, 꾸밈 id, 도구, x, y; Amount = 기대한 바뀌는 칸 수 > 0): 틀 = `rules.BuildFrameFor(면)`. 틀이 없거나(규칙 없음 포함) 이미 붙은 틀과 `FrameId`가 다르면 `Refused`. 처음이면 그 틀로 붙인다(`Attach`, 꾸밈 찾기 = `rules.FindFinish`). 미리 보기는 `Preview(id, tool, pointer, _ => true, _ => true)` — **해금은 방이 확인하지 않는다**(가구 놓기와 같다), 돈은 요청한 쪽이 낸다. 꾸밈 정의가 없거나 자리가 벽·바닥이 아니거나 도구가 `room`·`brush`가 아니거나 숫자가 아니면 `Invalid`. 이유가 있거나(`NoTarget`·`NothingToChange`) 바뀌는 칸 수 ≠ Amount거나 덮는 칸(`Cells`) 중 하나라도 다른 잡기가 같은 자리로 잡고 있으면 `Refused`. 아니면 잡기를 적고 `Revision++`, `Applied`(= 잡음). 꾸밈은 아직 바뀌지 않는다.
  - **`band`**(Ids = 면, 띠 꾸밈 id; Amount = 1): 같은 틀 판정, `PreviewBand(id, _ => true, _ => true)`, 잡힌 띠가 있으면 `Refused`, 아니면 잡기.
  - **`use`**(Ids[0] = 잡은 요청 id): 그 잡기가 없거나 다른 사람 것이면 `Invalid`. 아니면 같은 (꾸밈, 도구, 칸)으로 미리 보기를 다시 내 적용하고(칸을 잡아 두었으므로 칸 수가 같다 — 테스트가 지킨다; 혹시 다르면 그래도 적용한다, 돈은 이미 냈다) 잡기를 지우고 `Applied`. 두 번째 `use`는 `Invalid`.
  - **`release`**: 같은 판정, 꾸밈은 그대로 잡기만 지움, `Applied`.
  - 잡힌 칸은 저장하지 않는다 — 읽은 뒤 처음 필요할 때 (꾸밈 자리, 도구, 칸)과 틀로 다시 낸다(`Cells`는 기록과 무관하게 틀만으로 정해진다).
  - **기록 보존**: `Load`는 `HouseFinishes.Load`와 같은 규칙(데이터에 없는 id, 바닥이 아닌 칸, 다른 `FrameId` 포함 — 같은 칸이면 얹는다). `Save`는 붙었든 아니든 두 파일을 쓴다(`RoomPieceSet.Save`의 묶음 안).
- 함께 고친다: Minor 2 — 벽지 미리 보기에서 덮는 칸 중 정면 주인 칸이 0이면 이유 `NoTarget`(`NothingToChange`가 아니라). Minor 7 — `HouseBuildSaveData`·`RoomHouseBuildHoldSaveData` 주석에 "목록 기록 안에 필드를 더하지 않는다 — 칸·잡기마다 새 값은 맨 위 새 배열로(옛 빌드가 다시 쓰면 목록 안 필드는 사라진다)".
- [ ] **Step 1: 실패하는 테스트** — `RoomHouseBuildTests`(규칙은 생성자로: `RoomHouseRules(가구 없음, _ => null, buildFrameFor: s => s == "house" ? frame : null, finishes: 꾸밈 셋)`, 틀은 `HouseFinishesTests`의 작은 격자 틀을 같은 꼴로):
  - `Finish_HoldsCells_UseApplies_StateShowsIt`(잡은 뒤 `ToSaveData` 그대로 → `use` 뒤 덮는 칸 모두 새 바닥, `State()` 줄을 `RoomHouseBuildLines.TryDecode`하면 같은 기록과 잡기 0)
  - `Release_FreesTheCells_NothingChanges`
  - `HeldCells_RefuseAnotherRequestOnTheSameSlot_OtherSlotStillWorks`(같은 방 바닥 둘째 요청 `Refused`, 같은 방 벽지는 `Applied`)
  - `ExpectedCountDiffers_Refused`, `NothingToChange_Refused`, `WallpaperBrushOnACellWithoutAFace_Refused`
  - `UseOrReleaseByAnotherPlayer_Invalid`, `UseTwice_SecondInvalid`
  - `LockedFinish_IsTrustedToTheRequester`(해금 토큰이 있는 꾸밈도 잡힌다)
  - `UnknownFinish_BadTool_BadNumbers_Invalid`, `SurfaceWithoutAFrame_Refused`, `NoRules_Refused`
  - `Band_HoldThenUse_ChangesTheBand_SecondBandHoldWhileHeld_Refused`
  - `SaveAndLoad_HoldsSurvive_AndStillLockTheirCells`(메모리 저장소로 저장 → 새 조각에 읽기 → 같은 칸 요청 `Refused`, 그 잡기의 `use`가 적용)
  - `UnknownIdsAndOtherFrameRecords_KeptThroughLoadSaveAndState`
  - `StateLines_StayUnder128Bytes_ForTheLongestRealIds`(실제 데이터의 가장 긴 꾸밈 id 둘 + 칸 99,99 + 64자 플레이어 id)
  - `ThroughTheRoomPieceSet_SameRequestIdIsAppliedOnce`, `HouseBuildFollowsHouse_NotAChoosablePiece`(`ActiveNames(house)`에 들고 `Names`에는 없다)
  - `Client_DecodesData_AndHoldsOfAPlayer`, `Client_AnswersBeforeAListener_AreHandedOverOnce`, `Client_RequestsWithTheSeparator_Throw`
  - `HouseFinishesTests`(더함): `Preview_WithAnUnlockPredicate_SameAsWithProgress`, `CostFor_IsPerCell_AndBandOnce`, `Wallpaper_BrushOnACellWithoutAFace_NoTarget`.
- [ ] **Step 2: 실패를 본다** — `dotnet test game/tests/Rootborn.Core.Tests --filter "FullyQualifiedName~RoomHouseBuild|FullyQualifiedName~HouseFinishes"`. 기대: 컴파일 오류(타입 없음).
- [ ] **Step 3: 구현** — 위 시그니처와 뜻. 잡힌 칸 표는 `HashSet<(string Slot, CellPos Cell)>` 하나를 잡기가 바뀔 때만 다시 만든다.
- [ ] **Step 4: 통과를 본다** — Core 전부(1328 + 새 것; `SavedEnumValuesTests` 그대로), Data(`SaveShapeRegistrationTests`), `bash Scripts/ci/run-all.sh --checks-only`.
- [ ] **Step 5: 커밋** — `[NETWORK] 공유 조각 house-build — 방이 꾸밈 칸을 잡고 쓰기·풀기로 적용`.

### Task 2: `GameSession`이 같이 쓰는 집의 꾸밈을 방에서 받고 방에 요청한다 (HOUSE-004)

모델: 높음(두 지불 길, 끊김). 멈춤 없음. **가장 위험한 작업.**

**Files:**
- Create: `game/Rootborn.Game/GameSession.SharedHouseBuild.cs`
- Modify: `game/Rootborn.Game/GameSession.HouseBuild.cs`(같이 쓰는 집 갈래를 바꾸고 `ShowsOwnFinishes` 지움), `game/Rootborn.Game/GameSession.Shares.cs`(`Shared`에 셋째 인자, `WaitingForRoom`, `OnSharesChanged`, `AfterStartInRoom`), `game/Rootborn.Game/GameSession.SharedHouse.cs`(`UseSharedHouse`가 꾸밈 조각도 찾음), `game/Rootborn.Core/src/Building/HouseFinishes.cs`(`HouseFinishReasons.SharedHouse` 지우고 `RoomRefused = "ui.house-build.reason.room-refused"` 더함), `game/localization/ko.json`·`en.json`(`ui.house-build.reason.shared-house` 지우고 `ui.house-build.reason.room-refused` — ko "방이 받아 주지 않았어요. 그 사이 다른 사람이 먼저 바꿨을 수 있어요", en "The room did not accept it. Someone may have changed it first.")
- Test: `game/tests/Rootborn.Game.Tests/SharedHouseBuildTests.cs`(새 — `SharedHouseSessionTests`의 `FakeRoom` 꼴, 규칙에 `buildFrameFor: s => s == HouseRoom.SurfaceId ? HouseRoom.Frame : null, finishes: TestWorld.Definitions.All<RoomFinishDefinition>()`), `game/tests/Rootborn.Game.Tests/HouseFinishTests.cs`(아래 세 테스트를 바꿈), `game/tests/Rootborn.Core.Tests/Building/HouseFinishesTests.cs`(312줄의 `SharedHouse` → `RoomRefused`)
- Modify: `game/tests/Rootborn.Game.Tests/SCENARIOS.md`, `.claude/rules/testing-discipline.md` 시나리오 표 — `HOUSE-004 | 같이 쓰는 집에서 한 사람이 꾸미면 모두에게 보이고 돈은 한 번만 | Core RoomHouseBuildTests, Game SharedHouseBuildTests`(작업 3·5·7·8이 더한다)

**Interfaces:**
- Consumes: 작업 1 전부, `Shared`, `InScope`, `Saved`, `SaveAndNotify`, `TryGrants`, `GrowByPlacing`, `FindFinish`, `House`, `PlayerId`, `Shares.Outbox`.
- Produces(`GameSession`):
  - `public bool SharesHouseBuild => _roomBuild != null;`
  - `public string LastHouseFinishRefusal { get; }` — 방이 마지막 꾸밈 잡기를 거절했으면 `HouseFinishReasons.RoomRefused`, 다음 꾸밈 요청에서 지움. 화면이 보인다.
  - `WaitingForRoom`: 지금 조건 + `_finishWaiting != null`(꾸밈 잡기의 답을 기다림). 방 지갑을 기다리는 동안은 지금처럼 `_waiting`이 맡는다.
  - `private T Shared<T>(Func<T> run, Func<T, bool> applied, Action notApplied = null)` — `notApplied`는 그 범위가 끝내 안 됐을 때(`OnRefused`, `OnGranted`에서 다시 한 명령이 안 됨) 한 번 부른다. 있는 호출은 그대로.
  - 조회 셋(`HouseWallAt`·`HouseFloorAt`·`HouseBandColors`)·`HouseBuildVersion`·`HouseBuildOpen`은 같이 쓰는 집이면 **비추기**(`_sharedFinishes`: 방이 알린 `RoomHouseBuildClient.Data`를 `Load`한 `HouseFinishes`, `EnterHouseBuild`가 붙인다)를, 아니면 지금처럼 자기 것을 쓴다. 자기 `_houseFinishes`는 같이 쓰는 집에서 붙이지도 더럽히지도 않는다.
  - `PreviewHouseFinish`·`PreviewHouseBand`: 같이 쓰는 집이면 비추기로 지금과 같은 판정(자기 해금, `CanPayFinish`). `SharedHouse` 이유는 없다.
  - `ApplyHouseFinish`·`ApplyHouseBand`: 같이 쓰는 집이면 미리 보기가 `CanApply`이고 기다리는 것이 없을 때만 `RequestFinish(House.SurfaceId, id, tool, pointer, preview.ChangedCells.Count)`(띠는 `RequestBand`), `LastRoomCommandApplied = null`, `LastHouseFinishRefusal = null`, `Save()`, 알림 `HouseBuild`·`Room`, **false**(지금 바뀐 것이 없다 — 가구 요청과 같다). 집을 같이 쓰지 않으면 지금 길 그대로.
- 흐름(테스트가 고정한다):
  - **잡기 답**(`RoomHouseBuildClient.Answered`): 거절이면 `_finishWaiting = null`, `LastHouseFinishRefusal = RoomRefused`, `LastRoomCommandApplied = false`, 저장·알림. 허락이면 비용 = `HouseFinishes.CostFor(정의, request.Amount)`(띠는 1). 돈 내기 명령 = `Inventory.TryExchange(비용, null, () => _roomBuild.AddUse(잡기 id))` 다음 적용됐으면(칸 꾸밈만) `GrowByPlacing(House.SurfaceId)`, `Saved(…)`. 이 명령을 `Shared(명령, done => done, notApplied: () => 풀기 + 저장·알림)`로 감싼다 — 돈을 같이 쓰지 않으면 바로 되고(자기 지갑), 같이 쓰면 방 지갑의 잡기 → 허락 → 다시 하기. 바로 안 됐고 방 지갑에 묻지도 않았으면(자기 돈이 그 사이 모자람) 그 자리에서 풀기. 잡기를 기다리는 동안의 잡기 id는 `_finishPaying`(범위 → 잡기 id, 메모리만)에 든다.
  - **방의 상태가 오면**(`OnSharesChanged`의 `house-build`): 비추기를 `Load`하고 알림 `HouseBuild`. 그리고 **떠도는 내 잡기 풀기**: 상태의 내 잡기(`CopyHoldsOf(PlayerId)`) 중 보낼 목록에 그 잡기의 `use`·`release`가 없고 `_finishWaiting`도 `_finishPaying`에도 없는 것은 `AddRelease`(한 번 — 다음엔 보낼 목록에 있다)하고 저장. `AfterStartInRoom`에서도 한 번 본다(다시 켠 판은 `_finishPaying`이 비어 있다 — 허락받은 방 지갑 잡기는 지금처럼 `ReleaseGrant`).
  - **기술**: 돈 내기가 된 바꾸기에서만 요청한 사람의 `GrowByPlacing`(그 사람의 하루 한도). 띠는 자라지 않는다(지금과 같다).
- [ ] **Step 1: 실패하는 테스트** — `SharedHouseBuildTests`(실제 `data/`, 임시 폴더, `FakeRoom.Deliver`로 방을 손으로 돌림; 돈 준비는 `Inventory.Add`):
  - `HOUSE_004_OneDecorates_EveryoneSeesIt_TheDecoratorPaysOnce`(집만 공유: a가 왼쪽 위 방 바닥 → `ApplyHouseFinish` false·`WaitingForRoom` → `Deliver(a)`(잡기) → a의 돈이 87 × 칸당 값만큼 줄고 → `Deliver(a)`(쓰기) → a·b 모두 그 방 87칸 `HouseFloorAt` = 새 바닥, b의 돈 그대로, b에 `HouseBuild` 알림, 방 `ToSaveData()` 기록 87)
  - `MoneyShared_TheRoomWalletPaysOnce_NotTheBag`(돈+집 공유: 방 지갑 잔액이 비용만큼 한 번 줄고 a·b 개인 쪽은 그대로)
  - `TwoAtOnce_SameRoom_OnlyTheFirstIsHeld_TheSecondPaysNothing`(a·b가 같은 방에 다른 바닥을 요청하고 한 번에 전달 → 먼저 온 것만 잡히고 뒤 것은 `LastHouseFinishRefusal == RoomRefused`, 돈 그대로)
  - `AfterOthersChange_MyStaleRequest_IsRefused_AndMyPreviewIsFresh`(b의 쓰기가 반영된 상태를 받은 뒤 a의 미리 보기는 새 칸 수; 상태를 받기 전에 보낸 a의 요청은 칸 수가 달라 거절, 돈 그대로)
  - `MoneyRunsOutBeforeTheGrant_HoldReleased_NothingChanges`(자기 지갑: 잡기 답 전에 돈을 다른 데 씀 → 답이 오면 풀기, 꾸밈·돈 그대로, 방 잡기 0)
  - `RoomWalletRefuses_HoldReleased`(돈 공유: 잡힌 뒤 다른 사람이 방 지갑을 비움 → 방 지갑 잡기 거절 → 꾸밈 풀기, 방 잡기 0)
  - `RestartAfterRequest_BeforeTheAnswer_AnswerReplays_PaysOnce`(요청 저장 → 새 `GameSession`(같은 저장소, 보낼 목록을 읽는다) → `Deliver` 두 번 → 돈 한 번, 꾸밈 됨)
  - `RestartAfterGrant_BeforePaying_MyStrayHoldIsReleased_NoMoney`(돈 공유: 잡힘 → 방 지갑 잡기 요청까지 저장 → 새 판 → 방의 상태가 오면 떠도는 내 잡기를 풀고, 방 지갑 허락이 오면 지금처럼 `ReleaseGrant`; 돈·꾸밈 그대로)
  - `MyLockedFinish_IsNotAskedFor_SomeoneElsesLockedFinish_StaysVisibleToMe`(a는 잠긴 꾸밈 요청이 안 됨(`Locked`); b가 해금해 칠한 칸은 a에게 그 꾸밈으로 보인다)
  - `Band_GoesThroughTheRoom_AndIsFree`
  - `Growth_GoesToTheDecorator_WithTheirOwnDailyLimit`(a가 같은 날 칸 꾸밈 넷을 끝내면 a의 인테리어 +3 — 하루 3번 한도, 띠 색은 세지 않음; b의 기술은 그대로)
  - `OwnHouseBuildFile_NeitherShownNorWritten_InASharedHouse`(a 슬롯에 미리 쓴 `house-build.json` 바이트가 꾸민 뒤·저장 뒤 그대로)
  - `HostsSoloDecor_IsNotCarriedIntoANewRoom_AndIsBackWhenPlayingAlone`(혼자 꾸민 슬롯 → 같은 슬롯으로 새 방(빈 조각) → 기본 꾸밈 → 방에서 꾸밈 → 같은 슬롯을 공유 없이 새 판 → 혼자 꾸민 그대로)
  - `ReloadInTheRoom_SameFinishesAsBefore`(같이 쓰는 판을 새로 시작해 같은 방의 상태를 받으면 같은 조회)
  - `HouseFinishTests`에서 바꿈: `SharedHouse_RefusedWithReason` → `SharedHouse_PreviewIsFromTheRoom`, `SharedHouse_ShowsFrameDefaults_OwnFinishesKeptForTheSoloHouse` → `SharedHouse_ShowsTheRoomsFinishes_OwnFinishesKeptForTheSoloHouse`, `HouseBand_RefusedOutsideTheHouseAndInASharedHouse` → `HouseBand_RefusedOutsideTheHouse`(집 밖 부분만 남김).
- [ ] **Step 2: 실패를 본다** — `dotnet test game/tests/Rootborn.Game.Tests --filter "FullyQualifiedName~SharedHouseBuild|FullyQualifiedName~HouseFinishTests"`. 기대: 컴파일 오류 또는 `SharedHouse` 이유로 실패.
- [ ] **Step 3: 구현** — 위 흐름. 잡기 답을 기다리는 동안 다른 꾸밈 요청은 받지 않는다(가구 요청과 같은 한 번에 하나).
- [ ] **Step 4: 통과를 본다** — Game 전부(`SharedHouseSessionTests`, `SavedFilesAreRegisteredTests`, `HouseBuildCompatTests` 포함), Core 전부, Screens 전부(이유 상수가 없어져 `FinishPaletteScreenTests`의 `SharedHouse_ShowsReason`·`BandTab_SharedHouse_ShowsReason_NothingChanges`는 컴파일되지 않는다 — **이 작업이 둘을 지우고**, 같은 자리의 새 테스트는 작업 5가 쓴다. Screens 건수가 595 → 593으로 잠깐 줄어드는 것을 보고에 적는다), Server·Simulation·Net 전부(공유 조각 등록 표가 바뀌었다), `run-all.sh --checks-only`(현지화 키 두 언어).
- [ ] **Step 5: 커밋 둘** — `[FEATURE] 같이 쓰는 집의 꾸밈을 방에서 받고 방에 요청 — 칸을 잡은 뒤 요청한 사람이 낸다(HOUSE-004)`, `[REFACTOR] Shared에 끝내 안 된 범위의 할 일 — 꾸밈 잡기를 푼다`(둘째가 먼저여도 된다).

### Task 3: 방이 짓는 집 틀을 맵에서 만들고 데이터 지문에 집 LDtk

모델: 중간. 멈춤 없음.

**Files:**
- Modify: `game/Rootborn.Server/HouseGrids.cs`(`Rules`가 틀·꾸밈 정의도 넘김, `public static Dictionary<string, HouseBuildFrame> LoadFrames(string repoRoot, Action<string> log)` — `Load`와 같은 면 목록, 같은 예외 갈래·로그 "집 맵을 읽지 못했다 — 같이 쓰는 집을 꾸밀 수 없다: …"), `game/Rootborn.Host/World/FurnitureView.cs`(`RoomAwareMaps.FrameForSurface`), `game/Rootborn.Host/HostGame.cs`(`CreateHosting`의 `RoomHouseRules`에 `buildFrameFor: surface => World?.Rooms.FrameForSurface(surface)`, `finishes`), `game/Rootborn.Data/DataFingerprint.cs`(짓는 집 LDtk)
- Test: `game/tests/Rootborn.Server.Tests/SharedHouseBuildRoomTests.cs`(새 — `SharedHouseRoomTests`의 `Guest` 꼴), `game/tests/Rootborn.Data.Tests/DataFingerprintScopeTests.cs`(더함)

**Interfaces:**
- Consumes: 작업 1 `RoomHouseRules`(새 인자), 작업 2 `GameSession`, `HouseBuildFrames.From(LoadedMap)`, `WorldMapLoader.LoadMap`.
- Produces: `public HouseBuildFrame RoomAwareMaps.FrameForSurface(string surfaceId)`(그 면의 맵이 짓는 집이면 틀, 아니면 null; 맵마다 한 번 만들어 기억 — 같은 맵이면 같은 인스턴스), `HouseGrids.LoadFrames`. 지문 셈: `maps.json`(주석 허용 JSON)에서 `build`가 있는 항목의 `ldtk` 경로(저장소 뿌리 기준, 데이터 폴더의 부모)를 읽어 그 파일 바이트(BOM·CRLF 맞춤)를 이름 `"../" + ldtk 경로`로 셈에 넣는다. `build`가 없는 맵의 LDtk, 타일·조각 시트 PNG, 좌표표는 넣지 않는다. 파일이 없으면 넣지 않는다(지금 다른 파일과 같은 규칙). 클래스 주석의 "셈하는 것" 목록에 한 줄.
- [ ] **Step 1: 실패하는 테스트**
  - Data `DataFingerprintScopeTests.BuildMapLdtk_IsCounted_OtherLdtkAndPictures_AreNot`(임시 사본에서 집 LDtk 한 바이트 → 지문 바뀜, 짓는 집이 아닌 맵 LDtk·`tiles.png` → 그대로), `ServerAndGameStillAgree`는 있는 것 그대로 통과.
  - Server `SharedHouseBuildRoomTests`(실제 전송·`RoomServer`·손님 세이브 폴더·`HouseGrids.Rules`): `WhatOneDecorates_TheOtherSees_ThroughTheTransport`, `MoneyNotShared_TheDecoratorPays_FromTheirOwnWallet`, `MoneyShared_TheRoomWalletPaysOnce`, `TheRoomRestarts_FinishesAndHoldsContinue_ANewArrivalSeesThem`(꾸밈 하나 끝냄 + 잡기 하나 걸린 채 서버 끄고 켬 → 새 손님 첫 상태가 같은 꾸밈, 잡힌 칸 요청은 거절, 잡은 사람이 돌아와 끝내면 한 번), `AGuestDropsBetweenGrantAndPay_ComesBack_FinishesOnce`, `TheServersHouseFrame_IsTheSameAsThePlayersFrame`(서버 `LoadFrames`의 틀과 World 로더 틀의 `FrameId`·출처표 칸 수·부분이 같다 — `RoomFill` 방 수도), `WithoutTheMaps_TheRoomRefusesToDecorate_AndSaysWhy`, `AGuestWithADifferentHouseMap_IsRefusedAtTheDoor`(손님 버전의 데이터 지문을 집 LDtk를 바꾼 사본으로 셈 → 들어가기 거절, 이유 로그).
- [ ] **Step 2: 실패를 본다** — `dotnet test game/tests/Rootborn.Server.Tests --filter "FullyQualifiedName~SharedHouseBuildRoom"`, Data `--filter "FullyQualifiedName~DataFingerprint"`.
- [ ] **Step 3: 구현.** Host 쪽 변경은 작업 8의 두 게임 시험이 실제로 지난다(이 작업은 컴파일과 Host 필터만).
- [ ] **Step 4: 통과를 본다** — Data, Server 전부, Net 전부, Simulation 전부(`RoomPlay`가 `HouseGrids.Rules`를 쓴다), Game, Host `--filter "FullyQualifiedName~MultiplayerRoomShares|FullyQualifiedName~HouseFinishHost"`, `run-all.sh --checks-only`.
- [ ] **Step 5: 커밋 둘** — `[NETWORK] 방이 짓는 집 틀을 맵에서 만든다 — 서버 HouseGrids, 내 PC의 방 RoomAwareMaps`, `[NETWORK] 데이터 지문에 짓는 집 LDtk — 집 맵이 다른 판은 같이 할 수 없다`.

### Task 4: 방 조각 세이브의 하위호환·상위호환과 새 고정 폴더

모델: 높음(세이브). 멈춤 없음.

**Files:**
- Test: `game/tests/Rootborn.Data.Tests/RoomHouseBuildSaveTests.cs`(새), `game/tests/Rootborn.Data.Tests/NewerSaveInAnOlderProgramTests.cs`(사례 하나), `game/tests/Rootborn.Game.Tests/RoomHouseBuildCompatTests.cs`(새), `game/tests/Rootborn.Data.Tests/OldRoomSaveTests.cs`(새)
- Create(명령의 결과, 커밋): `game/tests/Rootborn.Data.Tests/Fixtures/saves/room-20261114/room-1/…`(방 설정 `house` + `wallet`, 요청 장부, 지갑·집·상자·꾸밈·잡기 조각, 손님 하나의 `guests/` 폴더); Modify: `Fixtures/saves/README.md` 표에 한 줄

스킬 `save-compat-change`의 순서를 단계로 푼다. 방 슬롯 고정 폴더는 지금까지 없었다 — 새 폴더로 **더한다**(있는 폴더는 고치지 않는다).

**Interfaces:** Consumes 작업 1·2. Produces 없음(테스트·고정 파일).
- [ ] **Step 1: 하위호환 실패 테스트** — `RoomHouseBuildCompatTests`: `OldRoomSlotWithoutTheFiles_OpensWithDefaults_ReadingWritesNothing`(꾸밈 조각이 생기기 전의 방 슬롯 — 지금 코드로 `house`만 든 방을 만들고 두 파일을 지운 사본 → `RoomPieceSet.Load` → 상태가 빈 기록, 슬롯 파일 목록·바이트 그대로), `UnknownFinishIdsInTheRoom_KeptAndDrawnAsDefault`, `GuestsOwnHouseBuildFile_InTheGuestsFolder_Untouched`.
- [ ] **Step 2: 상위호환 실패 테스트** — Data `NewerSaveInAnOlderProgramTests`에 `RoomHouseBuildHolds_FromANewerProgram_KeepsTopLevelFields`(맨 위 모르는 필드가 읽고 다시 쓴 뒤 남는다), Data `RoomHouseBuildSaveTests.FileStoreRoundTrip_BothFiles_VersionWritten`, Game `RoomHouseBuildCompatTests.AnOlderServer_LeavesTheNewFilesUntouched`(꾸밈 조각을 등록하지 않은 등록 표 — `CreateDefault`와 같은데 `Follow(house-build)` 줄만 없는 표를 테스트 안에서 만든다 — 로 같은 슬롯을 읽고 요청을 받고 저장해도 두 파일 바이트 그대로, 가구·상자는 그대로 플레이).
- [ ] **Step 3: 실패를 본다** — `dotnet test game/tests/Rootborn.Game.Tests --filter "FullyQualifiedName~RoomHouseBuildCompat"`, Data `--filter "FullyQualifiedName~RoomHouseBuild|FullyQualifiedName~NewerSaveInAnOlder"`. 작업 1·2가 맞게 했으면 일부는 처음부터 통과한다 — 그럼 `RoomHouseBuild`의 보존 코드를 일부러 망가뜨려(기록 버리기) 실패하는 것까지 보고 되돌린다(보고에 적는다).
- [ ] **Step 4: 고친다**(필요하면). 고정 파일·기대값을 고쳐 통과시키지 않는다.
- [ ] **Step 5: 새 날짜 고정 폴더** — Game `RoomHouseBuildCompatTests.WriteCurrentRoomFixture`(`[Explicit]`, `ROOTBORN_FIXTURE_OUT`가 있을 때만 그 폴더에 쓴다 — `WriteCurrentSlotFixture`와 같은 꼴, `FakeRoom`으로 꾸밈 하나 끝내고 잡기 하나 걸어 둔 방): `ROOTBORN_FIXTURE_OUT=<저장소>/game/tests/Rootborn.Data.Tests/Fixtures/saves/room-20261114 dotnet test game/tests/Rootborn.Game.Tests --filter "Name~WriteCurrentRoomFixture"`. README 표에 한 줄. Data `OldRoomSaveTests.Room20261114_Reads_AndRewritesTheSame`(조각마다 읽고 다시 쓴 바이트가 같다, 꾸밈 기록·잡기 수).
- [ ] **Step 6: 통과를 본다** — Core, Data(`OldSaveTests`, `SaveShapeRegistrationTests`, `NewerSaveInAnOlderProgramTests`, 새 것), Game(`SavedFilesAreRegisteredTests`, `DeletedSaveFileTests`, `NewerSlotIsPlayableTests`, 새 것), Server.
- [ ] **Step 7: 커밋** — `[TEST] 방 꾸밈 조각 세이브 하위·상위호환과 새 고정 폴더 room-20261114`. 본문에 "하위호환 확인"·"상위호환 확인" 테스트 이름을 나눠 적고, 실제 옛 빌드 서버로 연 것은 아니라고 쓴다.

### Task 5: 꾸미기 화면이 같이 쓰는 집에서 열린다

모델: 중간. 멈춤 없음(스크린샷은 작업 8).

**Files:**
- Modify: `game/Rootborn.Screens/Furniture/FinishPalette.cs`(같이 쓰는 집: 누르면 `waiting`, 기다림 문구는 지금 `RoomMoneyText.WaitingKey`, 답이 오면 `LastHouseFinishRefusal`이 있으면 이유 줄에 그 문구, 없으면 지금처럼 미리 보기 이유; 남이 바꿔 `HouseBuildVersion`이 오르면 지금 포인터 칸으로 미리 보기를 다시 냄 — 지금 있는 길)
- Test: `game/tests/Rootborn.Screens.Tests/Furniture/FinishPaletteScreenTests.cs`(더함 — 595 건수로 돌아오고 늘어난다)

**Interfaces:**
- Consumes: 작업 2 `SharesHouseBuild`, `LastHouseFinishRefusal`, `WaitingForRoom`, `LastRoomCommandApplied`, `HouseFinishReasons.RoomRefused`.
- Produces: 없음(요소 id·문구 키는 앞 계획 그대로, 새 키는 작업 2의 `ui.house-build.reason.room-refused` 하나).
- [ ] **Step 1: 실패하는 테스트** — 작업 2가 지운 두 테스트 자리에: `SharedHouse_PickAndClick_WaitsForTheRoom_ThenShowsTheNewFinish`(방은 테스트가 `RoomPieceSet`으로 손으로 돌림 — 지금 `RoomWallet` 테스트와 같은 꼴, 기다림 문구 → 잡기·쓰기 전달 → 문구 사라짐, `HouseFloorAt` 바뀜, 다시 누르면 `NothingToChange`), `BandTab_SharedHouse_WaitsThenApplies`, 그리고: `SharedHouse_RoomRefuses_ShowsTheRoomRefusedReason`, `SharedHouse_SomeoneElsesChange_RefreshesThePreview`(포인터를 둔 채 남의 쓰기가 반영되면 미리 보기 칸 수가 바뀐다 — 시험 뷰가 받은 `ShowFinishPreview` 횟수 +1), `SharedHouse_WhileWaiting_ClicksAskNothingMore`(보낼 목록의 꾸밈 요청 1).
- [ ] **Step 2: 실패를 본다** — `dotnet test game/tests/Rootborn.Screens.Tests --filter "FullyQualifiedName~FinishPalette"`.
- [ ] **Step 3: 구현.**
- [ ] **Step 4: 통과를 본다** — Screens 전부(`TestThemeScreensTests`, 문구 키 모든 언어, 긴 문구), `run-all.sh --checks-only`.
- [ ] **Step 5: 커밋** — `[UI] 같이 쓰는 집에서 꾸미기 — 방의 허락을 기다리고 거절 이유를 보인다`.

### Task 6: 같이 쓰는 집에서 보관한 가구 꺼내기 (결정 16)

모델: 중간. 멈춤 없음.

**Files:**
- Modify: `game/Rootborn.Game/GameSession.Furniture.cs`(`CanPlaceStoredFurniture`·`PlaceStoredFurniture`·`MayPlaceStored`), `game/Rootborn.Game/GameSession.SharedHouse.cs`(보관 꺼내기 요청), `game/Rootborn.Screens/Furniture/StoredFurnitureModel.cs`·`FurniturePlacementScreen.cs`(같이 쓰는 집 갈래: 줄을 고를 수 있고, 놓으면 `waiting`), `game/localization/ko.json`·`en.json`(`ui.furniture.stored.reason.shared` 지움 — 다른 곳이 쓰지 않으면)
- Test: `game/tests/Rootborn.Game.Tests/SharedHouseSessionTests.cs`(더함), `game/tests/Rootborn.Screens.Tests/Furniture/StoredFurnitureScreenTests.cs`(두 테스트 바꿈)

**Interfaces:**
- Consumes: `RequestSharedMove`, `_houseIds`, `HouseFurnitureLayout.CanPlaceStored`, `StoredFurniture.Index`.
- Produces: `CanPlaceStoredFurniture(index, anchor, direction)`는 같이 쓰는 집에서도 `House.CanPlaceStored`로 본다. `PlaceStoredFurniture`는 같이 쓰는 집이면 `RequestHouse(House.CanPlaceStored(…), () => _roomHouse.RequestMove(_houseIds[index], anchor, direction))`(요청만, false — 허락되면 방 기록의 그 가구 자리가 바뀌어 모두에게 놓인다; 일터는 그대로 false). `StoredFurnitureModel.Build`의 `shared` 인자는 더 쓰이지 않으면 지운다.
- 뜻: 보관 꺼내기는 방의 "옮기기" — 같은 판정(그 가구를 뺀 나머지 위에 놓기), 먼저 온 요청만. 꺼낸 가구는 방 기록의 자리가 바뀌므로 다시 읽어도 놓인다(이 가구 하나는 굳혀진다 — 나머지 옮긴 가구 굳히기는 결정 64).
- [ ] **Step 1: 실패하는 테스트** — Game `SharedHouse_StoredFurniture_PlacedThroughTheRoom_EveryoneSeesIt`(방 기록에 벽 칸 자리 가구 → 둘 다 보관에 보임 → a가 꺼냄 → 기다림 → 전달 → 둘 다 놓임, 보관 0, 같은 index 다시 false), `SharedHouse_TwoTakeOutTheSameStoredOneAtOnce_OnlyTheFirst`; Screens `Model_FromASharedHouse_CannotPick_AndGivesTheReason` → `Model_FromASharedHouse_CanPick`, `SharedHouse_StoredTab_ShowsTheReason_AndRowsCannotBePicked` → `SharedHouse_StoredTab_PlacingWaitsThenPlaces`.
- [ ] **Step 2: 실패를 본다.** **Step 3: 구현.**
- [ ] **Step 4: 통과를 본다** — Game 전부, Screens 전부, Server(`SharedHouseRoomTests`), Simulation 전부, `run-all.sh --checks-only`.
- [ ] **Step 5: 커밋** — `[FEATURE] 같이 쓰는 집에서 보관한 가구 꺼내기 — 방의 옮기기 요청으로`.

### Task 7: 같이 하는 방 시뮬레이션에 꾸미기와 꾸밈 불변식

모델: 중간. 멈춤 없음.

**Files:**
- Modify: `game/tests/Rootborn.Simulation.Tests/Rooms/RoomPolicies.cs`(나누는 사람 정책에 꾸미기 한 걸음: P → 꾸미기 분류 → 아무 탭·카드 → 아무 바닥 칸 누름, 씨앗으로), `game/tests/Rootborn.Simulation.Tests/Rooms/RoomChecks.cs`(꾸밈 불변식 둘), `game/tests/Rootborn.Simulation.Tests/RoomPlayTests.cs`(부딪힘 시험에 꾸미기), `game/tests/Rootborn.Simulation.Tests/Play/PlayRunner.cs`(다시 읽기 검사가 같이 쓰는 집의 꾸밈 줄을 가구 줄처럼 뺀다 — `StateSnapshot`에 꾸밈 줄이 있으면)
- Test: 위 파일 자체(시뮬레이션은 테스트 프로젝트다)

**Interfaces:** Consumes 작업 2·3·5(요소 id `furniture.category.finish`, `furniture.finish.card.<n>`, 작업 2의 조회). Produces 없음.
- 불변식(`RoomChecks`): (가) 방이 조용할 때(그 자리의 보낼 목록에 꾸밈 요청이 없고 기다림이 없을 때) 집을 같이 쓰는 모든 자리의 `HouseFloorAt`·`HouseWallAt`(집 바닥 칸 전부)·`HouseBandColors`가 방 조각 `RoomHouseBuild.ToSaveData()`로 만든 비추기와 같다. (나) 자리마다 꾸밈으로 나간 돈(자기 지갑 또는 그 자리가 쓴 방 지갑 몫) = 그 자리의 쓰인 잡기 비용 합 — 풀린 잡기는 0. (다) 날마다 서버를 끄고 켠 뒤 꾸밈 기록·잡기 수가 같다(있는 "조각이 같음" 검사가 새 조각을 저절로 본다 — 확인만).
- [ ] **Step 1: 실패하는 테스트** — 불변식 (가)를 먼저 넣고, 비추기를 일부러 안 고친 판(`OnSharesChanged`의 `house-build` 갈래를 빼고)에서 `SeveralPlayers_PlaySeveralDaysInOneRoom_KeepingEveryInvariant`(집 조합)이 실패하는 것을 본 뒤 되돌린다(보고에 적는다). `FourSharers_ContendForTheSameCellsAndChest_OnlyTheFirstRequestWins`에 같은 방 꾸미기 부딪힘을 더함.
- [ ] **Step 2: 실패를 본다** — `dotnet test game/tests/Rootborn.Simulation.Tests --filter "FullyQualifiedName~RoomPlayTests"`.
- [ ] **Step 3: 구현.** 정책이 꾸미기를 못 했으면(카드가 없음, 돈 없음) 다음 걸음으로 넘어간다 — 막힌 것으로 세지 않는다. 기록 `summary.md`에 꾸미기 요청 수·잡힘·거절·쓰기 수를 한 줄.
- [ ] **Step 4: 통과를 본다** — Simulation 전부(107 + 새 것), 씨앗 셋 더 손으로(`ROOTBORN_SIM_ROOM_SEEDS`) 한 번.
- [ ] **Step 5: 커밋** — `[TEST] 같이 하는 방 시뮬레이션에 꾸미기 — 모두 같은 꾸밈, 돈은 쓴 잡기만큼`.

### Task 8: 두 게임 창에서 한 사람이 꾸미면 다른 사람 화면에 보인다

모델: 높음(두 프로세스 시험). **멈춤: 두 게임 창에서 손님이 꾸민 것이 방장 화면에 보이는 스크린샷(손님·방장 각각)을 사용자에게 보인다.**

**Files:**
- Modify: `game/tests/Rootborn.Host.Tests/TwoGames/PlayActions.cs`(`PlayerDriver` 확장 `Decorate(string tabId, string finishId, string tool, CellPos mapCell)` — P → `furniture.category.finish` → 탭 → 그 꾸밈의 카드 → 그 칸에 포인터(`HostPlacementView.CellRect` + `ClickAt`) → 누름, 바뀔 칸 수를 돌려줌), `PeerVerbs.cs`(`decorate`), `PeerGame.cs` 또는 질의 표(`house-floor-at`, `house-wall-at`, `house-band`, `wallet`, `finish-refusal`), `game/tests/Rootborn.Host.Tests/TwoGames/TwoGamesSharesHostTests.cs`(새 시험)
- Modify: `SCENARIOS.md`·`testing-discipline.md`의 HOUSE-004 줄에 `Server SharedHouseBuildRoomTests, Screens FinishPaletteScreenTests, Simulation RoomPlayTests, Host TwoGamesHostTests.TWO_GAMES_005`

**Interfaces:** Consumes 작업 2·3·5. Produces 동사 `decorate <tab> <finishId> <tool> <x> <y>`(답: 바뀔 칸 수), 질의 다섯.
- [ ] **Step 1: 실패하는 테스트** — `TWO_GAMES_005_GuestDecoratesTheSharedHouse_HostSeesIt_GuestPaysOnce`: `WriteStory` 두 슬롯 → 방장이 집만 공유로 방을 엶(`HostOpensFromStartMenu(null, RoomHouse.PieceName)`) → 손님 들어옴 → 준비 단계: 손님에게 돈(질의로 전후 기록) → 손님 `decorate floor finish.floor.carpet room` 왼쪽 위 방 칸(실제 입력, 손님 프로세스) → `UntilQuery("False", …, "waiting-for-room")` → 방장 `Until(() => 방장 HouseFloorAt(그 칸) == 카펫 && 방장 World.ComposeCount 증가)` → 손님 돈이 87 × 칸당 값만큼 줄고 방장 돈 그대로 → 방장이 그 방 옆으로 `WalkNear` → 스크린샷 `two-05-host-sees-guest-floor` → 손님 스크린샷 `two-05-guest-decorated` → 방장이 자기 `PlayerDriver`로 같은 방 벽지(`Decorate(wall, finish.wall.wallpaper-sprig, room, …)`) → 손님 질의 `house-wall-at`가 바뀜 → 두 스크린샷 `two-05-*-wallpaper`. 출력 `output/house-building/stage1c/`.
- [ ] **Step 2: 실패를 본다** — 이 PC: `dotnet test game/tests/Rootborn.Host.Tests --filter "FullyQualifiedName~TWO_GAMES_005"`(무거우면 원격 `bash Scripts/remote-test/run.sh <커밋>` — 둘 중 하나로, 동시에 돌리지 않는다).
- [ ] **Step 3: 구현**(테스트 도우미만 — 제품 코드는 작업 2·3·5에서 끝났어야 한다. 제품 결함이 나오면 그 작업으로 돌아가 고치고 보고에 적는다).
- [ ] **Step 4: 통과를 본다** — Host `--filter "FullyQualifiedName~TwoGamesHostTests|FullyQualifiedName~MultiplayerRoomShares|FullyQualifiedName~HouseFinishHost"`, 그 뒤 원격 전체 실행(`run.sh <커밋>`) — 모든 프로젝트 건수가 줄지 않았다.
- [ ] **Step 5: 직접 본다** — 스크린샷을 열어 방장 화면의 카펫 방, 문간, 띠 양옆 바닥, 손님 화면의 꾸미기 패널(겹친 글자·잘린 패널), 벽지. **사용자에게 경로를 보이고 멈춘다.** 근거: 두 프로세스·같은 기계 루프백·장치 이벤트 주입(OS 입력 계층 미확인), 돈은 준비 단계.
- [ ] **Step 6: 커밋** — `[TEST] 두 게임 창 — 손님이 꾸민 바닥이 방장 화면에, 돈은 손님이 한 번(TWO_GAMES_005)`.

### Task 9: 문서·스킬·결정 대기 정리, 끝난 계획 지우기

모델: 낮음. 멈춤 없음.

**Files:** Modify `game/README.md`(같이 하는 방 절: 공유 조각 표에 `house-build`(`house`를 따름), 두 방 조각 파일, 지문에 짓는 집 LDtk), `game/world/README.md` 6절(짓는 집 LDtk는 데이터 지문에 든다 — 고치면 옛 판과 같이 못 함), `game/Rootborn.Server/README.md`(방 조각 목록), `.claude/skills/pixel-kit-room/SKILL.md`와 `.agents/skills/pixel-kit-room/SKILL.md`(바이트 같게: "집 맵을 바꿀 때" 3번 뒤에 "집 LDtk를 다시 만들면(repaint 포함) 데이터 지문이 바뀐다 — 옛 판과 같이 할 수 없고 서버도 같은 판이어야 한다" 한 줄), `.claude/skills/host-play-test/SKILL.md`와 `.agents/skills/host-play-test/SKILL.md`(바이트 같게: "같이 하기를 게임 둘로"의 동사 예에 `Call("decorate", …)`, "같이 하는 방(창 없는 층만)"의 불변식 목록에 꾸밈 둘 — 필요한 것만), `.claude/rules/changelog.md`(항목 하나 — 일반화 "칸마다 값을 내는 공유 변경은 방이 칸을 잡은 뒤 요청한 사람이 내고 쓰기/풀기 — 돈 공유 켬·끔이 한 길, 떠도는 잡기는 그 사람의 판이 상태를 보고 푼다"), `.claude/rules/testing-discipline.md`(HOUSE-004 줄 최종 — 작업 2·8이 넣은 것 확인), 설계서 12.1절 표(1-8 끝, 진행 문단)·12.1.1절(위 "설계서와 달리 정한 것")·13절 성능 문장(Minor 8 — 꾸밈이 바뀌면 집 전체 다시 조합)·15절(풀린 것·남은 것: 개인 지갑 + 같이 쓰는 집의 칸 잡기는 만들었다, 옮긴 자리 굳히기 남음), 결정 대기 문서(16·32·44번을 끝난 사실로, 55~66번은 그대로 두고 답 대기 목록에 더함, 머리말의 계획 목록), **끝난 앞 계획 `docs/superpowers/plans/2026-11-14-house-building-stage1b.md`를 지운다**(이 계획 머리말 "출발점"이 그 약속을 옮겨 받았다) — 그 경로를 가리키던 곳(`git grep -n "house-building-stage1b.md" -- . ':!.superpowers'`)을 이 계획이나 설계서로 바꾼다(`.superpowers/sdd/…`의 작업 기록은 고치지 않는다).
- [ ] **Step 1:** 쓴다. 새 스킬은 만들지 않는다.
- [ ] **Step 2:** `cmp .claude/skills/pixel-kit-room/SKILL.md .agents/skills/pixel-kit-room/SKILL.md`, `cmp .claude/skills/host-play-test/SKILL.md .agents/skills/host-play-test/SKILL.md`, 위 `git grep`이 0건(작업 기록 제외), `git grep -n "shared-house\|HouseFinishReasons.SharedHouse" -- game` 0건.
- [ ] **Step 3:** 도구 테스트 전부(`python -m unittest discover -s Tools/pixel-kit/tests -t Tools/pixel-kit`, 590), `run-all.sh --checks-only`. 커밋 `[DOCS] 집 꾸미기 1단계(다) — 같이 쓰는 집의 꾸밈 공유, 끝난 계획 정리`.

---

## 다음(2단계 안쪽 벽)이 기대는 것

- 방 조각 `house-build`(`RoomHouseBuild`)는 요청 종류를 글 상수로 늘린다 — 2단계는 `wall-add`·`wall-remove`를 같은 조각에 더하고, 잡기 → 쓰기/풀기 틀을 그대로 쓴다. 꾸밈 파일은 `HouseBuildSaveData`이므로 2단계 벽은 설계서 7.1절대로 맨 위 새 필드(`WallsAdded`·`WallsRemoved`)로 더하면 방·혼자 하는 집이 같이 읽는다.
- 서버의 틀은 `HouseGrids.LoadFrames`, 내 PC의 방은 `RoomAwareMaps.FrameForSurface`, 규칙의 입구는 `RoomHouseRules.BuildFrameFor`·`FindFinish`. 2단계에서 `HouseStructure`(테두리 + 차이)를 만들면 이 셋이 출처표 대신 구조를 넘긴다.
- 플레이어 쪽: `GameSession`의 비추기(`_sharedFinishes`)와 `RoomHouseBuildClient.Data`, 떠도는 잡기 풀기, `Shared(run, applied, notApplied)`.
- 결정 대기 64번(옮긴 가구 자리 굳히기)은 2단계 벽이 가구를 밀어낼 때 다시 본다.
