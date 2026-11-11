# 멀티플레이 조사 (프로젝트 5 준비)

2026-11-12. `2026-11-11-unity-removal-design.md` 6절 표의 프로젝트 5(멀티플레이와 Steam)를 설계하기 전에 사실을 모은 문서다. 코드는 쓰지 않았다.
Unity와 테스트는 실행하지 않았다. 아래 "Unity 판이 하는 일"은 기준본(`unity-playable-20261111`) 코드와 에셋을 읽어서 얻은 것이고, 실행해서 확인한 것은 아니다.
"작성자 판단"이라고 적은 곳은 추천이고, "추측"이라고 적은 곳은 확인하지 않은 것이다.

## 1. 지금 Unity 판이 하는 일

### 1.1 부품

`Assets/Scripts/Network`(asmdef `Rootborn.Network`, 21파일 1,903줄). 참조: NGO(`Unity.Netcode.Runtime`), Unity Transport, Steamworks.NET 2025.163.0(`Packages/manifest.json`의 git 주소), `Rootborn.Core`.

| 파일 | 하는 일 |
|---|---|
| `Session/INetworkSession.cs`, `NetworkSessionFactory.cs`, `NetworkBootstrap.cs` | 세션 4종(Single·Host·Client·Server) 중 하나를 만들어 시작한다. `NetworkBootstrap.ActiveSession`은 정적 필드다 |
| `Session/HostSession.cs` | `0.0.0.0:<port>`로 듣고 `StartHost`, Town 씬을 NGO 씬 관리로 연다 |
| `Session/ClientSession.cs` | `joinIp:port`로 `StartClient`. 끊김 이유(예: "server full")를 로그로 남긴다 |
| `Session/DedicatedServerSession.cs` | `StartServer`, 30fps, Town 씬을 연다. 화면 없는 실행(`-batchmode -nographics`) |
| `Session/SessionApproval.cs` | 접속 승인: 정원은 Core `SessionCapacity`, 도착 위치는 `SessionSpawn`(TownSpawnPoint + Core `SpawnSpread`) |
| `Player/NetworkPlayerIdentityBinder.cs` | 생긴 플레이어 오브젝트에 플레이어 id `client-<NGO client id>`를 붙인다 |
| `Player/NetworkLocalPlayerInputGate.cs` | 자기 것(owner)인 플레이어만 입력·상호작용 컴포넌트를 켠다 |
| `Player/NetworkLocalPlayerViewBinder.cs` | 자기 플레이어에 카메라를 붙인다 |
| `Characters/GrowingCharacterNetworkAppearanceSync.cs` | 겉모습 키 하나(`NetworkVariable<FixedString128Bytes>`, 쓰기 권한 owner) |
| `Time/NetworkWorldTimeState.cs` | 같이 쓰는 날짜·때(아침·낮·저녁·밤)·하루 단계, 하루 끝 준비 RPC |
| `Steam/SteamPlatform.cs` | Steam 초기화(앱 ID 480 기본, `-steamAppId`로 바꿈). 배치 실행·`-nosteam`·`-mode server`면 건너뜀 |
| `Steam/SteamNetworkingTransport.cs` | NGO용 직접 작성 전송. Steam Networking Sockets P2P(Steam 중계). 클라이언트 id는 Core `PeerTable` |
| `Steam/SteamLobby.cs`, `SteamSessions.cs`, `SteamCallbackRunner.cs` | 친구 전용 로비, rich presence `+connect_lobby`, 친구 방 목록, 콜백 펌프 |
| `StudentLife/StudentLifeNetworkStateBroadcaster.cs`, `Quests/QuestNetworkStateBroadcaster.cs` | 서버 권한일 때 활동 결과·퀘스트 상태를 등록된 클라이언트 수만큼 이벤트로 낸다(중복 억제) |

`NetworkBehaviour`는 5개: InputGate, ViewBinder, IdentityBinder, AppearanceSync, WorldTimeState. 모두 `Assets/Prefabs/Player.prefab`에 붙어 있다(프리팹의 스크립트 GUID로 확인). `NetworkObject`도 이 프리팹 하나뿐이고, `Boot.unity`의 NetworkManager가 이 프리팹을 PlayerPrefab으로 쓴다.

### 1.2 부팅

- 실행 인자: `Assets/Scripts/Game/Bootstrap/ArgsParser.cs` — `-mode single|host|client|server`, `-port`(기본 7777), `-maxPlayers`(기본 4), `-saveSlot`(기본 "default"), `-joinIp`(기본 127.0.0.1). 값은 `AppConfig`.
- 메뉴: `UI/MainMenu/ModeSelectPanel.cs` — "호스트 모드"는 Steam이 있으면 Steam 전송 + 친구 로비, 없으면 Unity Transport로 명령줄 포트. "친구 게임 참가"는 `JoinGamePanel`(친구 방 목록, 주소 입력 없음). Steam이 없으면 "Steam이 실행 중이 아닙니다"를 보인다. 2026-11-09 사용자 결정 "처음부터 스팀출시만"에 따라 메뉴의 IP 입력은 지웠다(메모리 기록).
- 직접 접속(IP)은 명령줄 `-mode client -joinIp`로만 남아 있다. 데디케이티드 서버는 Steam을 켜지 않으므로 **Steam 친구 방으로는 데디케이티드 서버에 들어갈 수 없다.** 데디케이티드 서버에는 명령줄 IP 접속만 된다.

### 1.3 무엇이 동기화되는가

| 대상 | 방법 | 권한 |
|---|---|---|
| 플레이어 생김·사라짐 | NGO PlayerPrefab 자동 생성 | 서버 |
| 처음 위치 | 승인 응답의 `Position`, `NetworkObject.SynchronizeTransform: 1`(생길 때 한 번) | 서버 |
| **걷는 위치** | **없음.** 프로젝트 어디에도 `NetworkTransform`이 없다(`Assets` 전체 검색). 남의 캐릭터는 생긴 자리에 서 있다 | — |
| 겉모습(옷·몸) | `NetworkVariable` 문자열 키. 받는 쪽은 `WearableLookResolver`로 게임에 있는 그림만 푼다 | 각자(owner 쓰기) |
| 날짜·때·하루 단계 | `NetworkVariable<int>` 3개. 서버가 `world-time/<slot>.json`에 저장·복원 | 서버 |
| 하루 끝 | `RequestDayEndReadyServerRpc`: 접속자 전원이 준비하면 다음 날 아침. `AdvanceTimeOfDayServerRpc`(누구나 부를 수 있음)는 다음 때로 | 서버 |
| 학생 진행·퀘스트·인벤토리·가구·월드 상태 | **네트워크로 보내지 않는다.** Broadcaster 두 개는 EditMode 테스트에서만 만들어지고 게임 코드에서 쓰는 곳이 없다(`Assets` 검색) | 각자 기계 |

주의할 사실:

- `NetworkWorldTimeState`가 플레이어 프리팹에 붙어 있어서 플레이어마다 하나씩 생긴다. `Active`는 owner가 0인 것(호스트 플레이어) 또는 먼저 생긴 것이다. 데디케이티드 서버에는 owner 0 플레이어가 없으니 처음 들어온 사람의 것이 쓰인다(코드 읽기, 실행 확인 안 함).
- 같이 쓰는 시간이 있으면 각자의 `GameClock`을 그 때의 대표 시각(Core `WorldTimeHours`: 9, 14, 18.5, 22시)에 고정하고 멈춘다. 학생 진행의 날짜·시각(`StudentLifeProgress`)은 각자 따로 흐르고, 하루를 끝낼 때 공용 시간에 "준비"를 보낼 뿐이다(`UI/StudentLife/StudentDayEndInteractor.cs`).
- 서버 권한 규칙 판정은 없다. 입력이 owner 기계에서만 켜지고(`InputGate`), 활동·대화·퀘스트는 그 기계의 컴포넌트가 처리해 그 기계의 세이브 폴더에 쓴다.

### 1.4 네트워크 중에 막히는 것

`UI/World/PlayerWorldTravelAdapter.cs`와 `UI/Careers/AdultCareerRuntimeComponent.cs`의 `IsOfflineAuthority()`는 NGO가 듣고 있거나 세션이 Single이 아니면 false다. 그 결과(코드 읽기, 실행 확인 안 함):

- 성인기 진행 변경(`CanMutate`)이 막힌다.
- 문을 지나 다른 씬으로 가는 것(`TryBeginSceneTransfer`가 `CanMutate`를 요구)이 막힌다. 즉 멀티플레이 중에는 Town 한 씬에 머문다.
- 슬롯 고르기로 들어가는 길(`RequestEntry`)도 막힌다.

### 1.5 세이브는 누구 것인가

- 슬롯은 `-saveSlot`(또는 메뉴에서 호스트가 고른 슬롯). 클라이언트는 자기 `AppConfig.SaveSlot`(기본 "default")을 쓴다.
- 플레이어 id: 호스트 자신은 `client-0`(`TownSinglePlayerFallbackInstaller.ConfigureIdentityForSession`, 또는 IdentityBinder), 들어온 사람은 `client-<id>`. 혼자 하기는 `local-player`.
- 파일 이름은 Core `SaveFileNames`가 만든다. 기본 플레이어(`local-player`/`player`)는 옛 공용 이름, 다른 id는 이름 뒤에 id를 붙인다(`wearables-<id>.json`, `student-life-progress-<id>.json`, `inventory-<id>.json`, `adult-career-*`, `milestone-progress-<id>.json` 등).
- **플레이어별이 아닌(슬롯 공용) 파일**: `quest-log.json`, `world-state-progress-shared.json`(월드 상태 중 Shared·SaveSlot 범위), 집 단계 `house-state.json`과 단계별 가구 배치, `metadata.json`, `world-progress.json`.
- NGO 클라이언트 id는 접속 순서로 정해지므로 같은 사람이 다시 들어와도 같은 id라는 보장이 없다. Steam ID 같은 안정된 신원과 묶는 코드는 없다(검색으로 확인). 다시 들어온 사람이 전과 다른 `client-N` 진행을 읽을 수 있다(추측: 실행 확인 안 함).
- 진행 파일은 그 플레이어의 기계에 쓰인다(1.3). 호스트의 슬롯에 클라이언트 진행이 모이는 구조는 아니다(코드 읽기).

### 1.6 늦게 들어온 사람이 받는 것

NGO 기본 동작으로: 지금 있는 플레이어 오브젝트들, 각자의 겉모습 키, 공용 시간 값 3개. 겉모습은 등록부가 준비될 때까지 0.25초마다 다시 시도한다(`InstallWhenRegistryIsReady`). 그 밖의 상태(남의 진행, 가구, 퀘스트)는 받지 않는다. 남의 캐릭터는 마지막으로 생겼던 자리에 보인다.

### 1.7 Steam 친구 방 흐름

1. 호스트: `SteamSessions.HostAsync` → Steam 전송으로 바꾸고 `StartHost` → `SteamMatchmaking.CreateLobby(FriendsOnly, maxPlayers)` → 로비 데이터 `rootborn_host=<호스트 Steam ID>`, rich presence `connect = +connect_lobby <로비 id>`.
2. 친구: 메뉴 방 목록(`SteamLobby.FriendRoomsNow` → Core `FriendRooms.Joinable`: 같은 앱을 하는 친구 중 로비가 있는 사람, 로비당 한 줄, 이름순), Steam 친구 목록 "게임 참가", 초대, 또는 꺼진 게임이 `+connect_lobby <id>`로 시작됨(`SteamLaunchArgs`).
3. `SteamLobby.Join` → 로비 데이터의 호스트 ID(없으면 로비 주인) → `ConnectP2P` → NGO 클라이언트.
4. 서버나 클라이언트가 멈추면 로비를 떠나고 rich presence를 지운다.

메모리 기록(2026-11-09): **실제 Steam 두 계정으로 친구 참가·초대·`+connect_lobby`, Steam 전송의 실제 송수신은 확인하지 않았다.** 스파이크(2026-11-11)는 별도 프로그램으로 Steam 친구 전용 로비 만들기까지만 통과했다.

### 1.8 테스트가 고정하는 것

| 테스트 | 고정하는 것 |
|---|---|
| EditMode `NetworkSessionTests` (3) | 모드별 세션 종류, None·Single은 혼자 하기, 혼자 하기의 접속·끊김 이벤트 |
| EditMode `SessionApprovalTests` (2) | 서버·호스트는 승인 켬 + 정원 콜백, 클라이언트도 승인 켬(NGO 설정 해시 맞춤) |
| EditMode `SteamPlatformTests` (2) | Steam 건너뛰는 조건, 개발 앱 ID 480 |
| EditMode `NpcQuestNetworkSyncTests`, `QuestNetworkPlayerIsolationTests` (6) | Broadcaster: 서버만 보냄, 중복 억제, 받는 사람 수에 비례, 플레이어 id 포함 |
| EditMode `StudentLife*NetworkTests` (11) | Broadcaster 같은 성질 + **플레이어 1의 활동이 플레이어 2 진행을 바꾸지 않음**(사실상 Core 규칙 테스트) |
| PlayMode `NetworkSessionHostPlayModeTests` (1) | 호스트 시작 → 자기 플레이어 1명, owner, id `client-0` → 멈추면 남은 오브젝트 0 |
| PlayMode `JoinGamePanelPlayModeTests` (2) | 친구 참가 패널이 메뉴 위에 뜸, Steam 없으면 설명만 하고 접속 안 함 |
| PlayMode `DedicatedServerClientProcessHarnessTests` (9) | 서버·클라이언트 별도 프로세스 실행 준비(인자, 로그 경로, 준비 신호, 정리). 실제 접속 플레이는 FEVM 스크립트 몫 |

두 사람이 같이 걷거나 같은 걸 보는지를 고정하는 자동 테스트는 없다. FEVM에서 서버 정원 1 + 클라이언트 2로 "서버가 가득 찼습니다" → 자리가 나면 재접속해 Town 진입을 OS 입력으로 본 기록은 있다(메모리, `output/strix-verify-20261109-d93b0256d/join-os-input/report.md`).

## 2. Core와 MonoGame 판에 이미 있는 것

### 2.1 Core `Packages/com.rootborn.core/Runtime/Network`

| 타입 | 내용 | dotnet 테스트 |
|---|---|---|
| `SessionCapacity` | 접속 수 < 정원이면 승인, 아니면 "server full", 정원 0 이하는 "invalid player limit" | `SessionCapacityTests` 8, `SessionCapacityReasonTests` 1 |
| `SpawnSpread` | n번째 사람 도착 위치 차이(4열 2줄, 8명 뒤 반복). 단위는 Unity world unit(1칸) | `SpawnSpreadTests` 4 |
| `PeerTable` | 전송 연결 핸들 ↔ 클라이언트 id, 서버 0, id 재사용 안 함 | `SteamSessionRulesTests` 16(아래 둘 포함) |
| `SteamLaunchArgs`, `FriendRooms` | `+connect_lobby` 읽고 쓰기, 친구 방 목록 거르기 | 위 |
| `WorldTimeHours` | 때 → 대표 시각 | `WorldTimeHoursTests` 7 |

모두 엔진·Steam 참조가 없어서 그대로 쓸 수 있다. 다만 `SpawnSpread`는 단위가 world unit이고 MonoGame 판 월드는 픽셀 좌표(`WorldSession`, 칸 크기는 맵에서)라서 칸 크기를 곱해 써야 한다. `PeerTable`의 핸들은 `uint`(Steam 연결 핸들)라 다른 전송에서는 맞추거나 일반화가 필요하다.

### 2.2 세이브

`SaveFileNames`와 `ISaveStore`(슬롯 + 파일 이름 → 읽기/쓰기)가 플레이어 id별 파일을 이미 구분한다. `FileSaveStore`(Rootborn.Data)가 실제 파일 저장소다. 1.5의 슬롯 공용 파일 목록은 MonoGame 판도 같다.

### 2.3 `GameSession`(game/Rootborn.Game)

- `GameSession.Start(정의, 설정, 저장소, 슬롯, 플레이어 id)`. 주석 그대로 "게임 한 판(한 세이브 슬롯의 한 플레이어)"이다.
- 전역 상태(쓰기 가능한 정적 필드)는 `Rootborn.Game`·`Rootborn.Screens`·`Rootborn.World`·`Rootborn.Data`·`Rootborn.Host`에서 찾지 못했다(검색). 정의(`GameDefinitions`)는 읽기 전용이라 여러 판이 같이 써도 된다(추측: 정의 객체가 내부 캐시를 바꾸지 않는지는 하나하나 보지 않았다).
- 화면은 `SessionCommands.Run(Func<GameSession, T>)`로 명령을 **같은 프로세스에서 바로** 부르고 결과를 바로 받는다(`Rootborn.Screens/GameScreen.cs`). 공개 명령·조회는 약 58개.
- 시간: 학생의 날·시각이 그 플레이어 진행 안에 있다(`StudentDay`, `StudentHour`). 공용 월드 시간은 없다.
- 미니게임은 화면이 Core `MinigameSession`을 dt로 돌리고 끝에 `SettleMinigame(requestId, 성공)` 한 번을 부른다. 씨앗은 화면 쪽에서 `Guid.NewGuid().GetHashCode()`로 만든다(`MinigameTriggers.cs`).
- 월드: `Rootborn.Core.World.WorldSession`은 맵 하나 + 걷는 몸(`Walker`) 하나다. `Update(dx, dy, dt)`, dt는 `float`, 한 걸음을 0.1초 이하로 쪼갠다.

"서버가 한 판 + 플레이어 여럿"으로 쓸 때 걸리는 곳:

1. **같은 슬롯에서 `GameSession`을 플레이어마다 하나씩 만들면 슬롯 공용 파일이 서로 덮인다.** `Save()`가 명령마다 `quest-log.json`, `world-state-progress-shared.json`을 쓰고, 집 배치도 슬롯 공용이다. 각 판이 자기 메모리의 사본을 들고 있다가 통째로 쓰므로 나중에 쓴 쪽이 이긴다. 공용 상태를 한 객체로 빼서 판들이 나눠 가지거나, 공용 파일을 한 곳에서만 쓰게 바꿔야 한다.
2. 퀘스트 로그가 슬롯 공용인데 진행 체인(`QuestChains`)은 플레이어별이다. Unity 판도 같았다. 둘이 같이 할 때 퀘스트 로그를 누구 것으로 볼지 정해야 한다(4절).
3. `WorldSession`은 걷는 몸 하나만 안다. 여러 명이면 맵마다 몸 여럿을 들고, 다른 사람 몸은 막지 않는(또는 막는) 규칙을 정해야 한다. Unity 판에서는 서로 위치를 몰랐으므로 몸끼리 충돌도 없었다.
4. 화면이 명령을 바로 부르는 구조라 클라이언트 쪽에 원격 `GameSession`을 두려면 `Func<GameSession,T>` 대신 보낼 수 있는 명령(이름 + 인자)과, 결과가 늦게 오는 흐름이 필요하다.
5. 미니게임 씨앗과 퀘스트 이벤트 키(`newEventKey`, 기본 새 GUID)가 각자 기계에서 만들어진다. 서버 권한이면 서버가 정하거나, 클라이언트가 만든 요청 id를 그대로 멱등 키로 쓰면 된다(Core 명령들이 이미 requestId로 중복을 막는다).

## 3. 고를 수 있는 길

### 3.1 전송

| 후보 | 라이선스 | .NET 8·Linux | 장점 | 단점 |
|---|---|---|---|---|
| **Steamworks.NET**(지금 Unity 판과 스파이크가 씀) | MIT | Standalone 판이 Unity 밖 프로젝트용. 최신 2025.165.0(2025-10-29, SDK 1.65). 스파이크가 Windows에서 로비를 만들었다 | SDK를 거의 그대로 감싼다. Unity 판 `SteamNetworkingTransport`·`SteamLobby` 코드를 거의 그대로 옮길 수 있다. 같은 사실(PeerTable 등)이 Core에 있다 | NuGet에 네이티브 라이브러리(`steam_api64.dll`, `libsteam_api.so`)가 없어 저장소에 버전 고정해 둬야 한다(스파이크 결론, 아직 저장소에 없음). 콜백·포인터 다루기가 저수준 |
| Facepunch.Steamworks | MIT | README상 Windows·macOS·Linux 지원. 릴리스 태그가 페이지에 없음 | C#스러운 API(`SocketManager`/`ConnectionManager`가 있다는 것은 작성자 기억, 이번에 문서로 확인 못 함) | 지금 코드와 다시 맞춰야 한다. 릴리스 관리 상태가 불분명 |
| LiteNetLib | MIT | .NET Standard 2.1, .NET 8 최적화 언급, Linux 지원 | 순수 C# UDP, 신뢰·순서 채널, NAT 구멍 뚫기, IPv6. 네이티브 파일 없음. 같은 기계 두 프로세스로 테스트하기 쉽다 | 공개 IP·포트 열기가 필요(친구끼리는 불편). Steam 중계가 없다 |
| 같은 프로세스 안 전송(테스트용) | — | — | 서버·클라이언트 둘을 한 테스트에서 돌린다. 네트워크 없이 메시지 계층을 검증 | 실제 전송 검증은 아님 |

작성자 판단: **전송 인터페이스 하나 + 구현 셋**(같은 프로세스, LiteNetLib UDP, Steamworks.NET 소켓). 설계 문서 4절의 `Rootborn.Net`(전송은 인터페이스 뒤)과 같다. Steamworks.NET을 고르는 이유는 이미 쓰고, 옮길 코드가 있고, 스파이크가 통과했기 때문이다. UDP는 데디케이티드 서버와 Steam 없는 개발·FEVM 검증용이다.

### 3.2 메시지 형식

- 후보: (가) 직접 쓴 이진 형식(`BinaryWriter` 같은 것), (나) JSON(Host·Net 쪽에서 `System.Text.Json`), (다) MessagePack 같은 라이브러리.
- 오가는 양이 작다(명령 수십 종, 걷기는 초당 몇십 번 좌표 몇 개). 작성자 판단: **메시지마다 종류 번호 + 형식 버전을 앞에 두고, 몸통은 직접 쓴 이진**. 걷기처럼 잦은 것은 이진이 맞고, 명령은 개수가 많아 손으로 쓰기 귀찮으면 명령만 JSON으로 해도 된다. 종류 번호를 enum으로 닫지 말고 등록 표에 한 줄로 더하게 한다(엔진 이전 규칙 "확장하기 쉽게").
- Core는 `System.Text.Json`을 참조할 수 없으므로 직렬화는 `Rootborn.Net`(Core 밖)에 둔다.

### 3.3 권한 모델

| 길 | 내용 | 지금 구조와 맞는 정도 | 바꿔야 할 양 |
|---|---|---|---|
| A. 각자 자기 판(Unity 판과 같음) | 각 기계가 자기 `GameSession`을 돌리고 자기 세이브에 쓴다. 네트워크로는 위치·겉모습·공용 시간만 보낸다 | 가장 잘 맞는다. `GameSession`을 안 바꾼다 | 작다. 걷기·겉모습·시간 메시지와 남의 몸 그리기 |
| B. 서버가 모든 판을 가짐 | 서버가 플레이어마다 `GameSession`을 같은 슬롯에서 돌린다. 클라이언트는 명령을 보내고 바뀐 상태를 받는다 | 판 단위로는 맞는다(`GameSession`이 플레이어 하나). 공용 파일 문제(2.3의 1)를 먼저 풀어야 한다 | 크다. 명령 약 58개를 메시지로, 화면이 결과를 늦게 받게, 상태 보내기(처음 전체 + 바뀐 것) |
| C. 섞음 | 걷기는 각자(클라이언트가 위치를 보내고 서버가 충돌만 확인), 슬롯 공용 상태(시간, 집, 공용 월드 상태, 퀘스트 로그)만 서버가 가짐 | 중간 | 중간. 공용 상태를 `GameSession` 밖 객체로 빼는 일이 필요 |

- Unity 판은 A였다. 그래서 진행이 실제로는 공유되지 않았고, 같은 집·같은 퀘스트를 같이 하는 경험은 없었다.
- 걷기 결정성: `Walker`는 `float`와 가변 dt를 쓴다. .NET 8 x64에서 같은 입력·같은 dt면 같은 결과가 나올 가능성이 높지만(추측), 프레임 dt가 기계마다 다르므로 **록스텝(입력만 보내고 각자 계산)은 맞지 않는다.** 위치 자체를 보내는 방식(주인이 보내고 받는 쪽은 사이를 메움)이 맞다. 서버 권한 걷기를 하려면 고정 dt 걸음(예: 1/60초)으로 서버와 클라이언트가 같이 계산하고 서버 값으로 바로잡는 방식이 필요하다(작성자 판단: 이 게임에서는 과하다).
- 작성자 판단: **A로 시작해 C로 넓힌다.** 먼저 걷기 위치·겉모습·공용 시간·맵 이동(누가 어느 맵에 있는지)을 맞추면 "2인 접속과 동기화"(6절 끝 기준)가 된다. 그 다음 사용자가 "같이 공유할 것"(4절)을 정하면 그 상태만 서버로 옮긴다. B는 진행을 서버에 모아야 할 이유(부정 방지, 데디케이티드 서버에 진행 보관)가 생길 때만 한다. 협동 게임이라 부정 방지는 약한 이유다.

### 3.4 데디케이티드 서버

- 가능하다: `Rootborn.Game`·`Rootborn.Data`·`Rootborn.World`는 MonoGame 참조가 없다. 콘솔 프로그램(`Rootborn.Server` 같은 것)이 데이터·맵을 읽고, 맵마다 충돌 격자를 들고, 전송을 돌리면 된다. 그림·글꼴·SDL이 필요 없다.
- 단, A 모델에서는 서버가 할 일이 적다(접속 승인, 위치 전달, 공용 시간·공용 상태 저장). 서버만 따로 둘 이유는 "호스트가 꺼져도 세계가 남는다" 정도다.
- Steam과의 관계: Unity 판 데디케이티드 서버는 Steam을 안 켰다(1.2). Steam 친구 방에서 데디케이티드 서버로 가려면 Steam GameServer API(익명 로그인, 서버 목록)를 써야 한다. 이번에 확인하지 않았다.
- 작성자 판단: 서버 쪽 코드(세션 진행, 접속 관리)를 Host와 분리해 두면 호스트 모드는 "같은 프로세스 안 서버 + 자기 클라이언트"가 되고, 데디케이티드 서버는 같은 코드를 콘솔에서 돌리는 것이 된다. 별도 실행 파일을 지금 배포할지는 4절에서 사용자가 정한다.

### 3.5 길별로 지금 코드에서 바꿀 곳(A→C 기준, 작성자 판단)

1. `Rootborn.Net`(새 프로젝트, MonoGame 참조 없음): 전송 인터페이스, 메시지 종류 등록 표, 세션(접속 승인 — Core `SessionCapacity`, 도착 위치 — `SpawnSpread`, 플레이어 신원).
2. Core: `PeerTable` 핸들 일반화, 여러 몸이 있는 맵(남의 몸 목록, 위치 받기·사이 메우기 규칙), 공용 시간 규칙(Unity `NetworkWorldTimeState`의 "모두 준비하면 다음 날"을 순수 규칙으로).
3. `Rootborn.Game`: 슬롯 공용 상태를 판 밖으로 빼기(C 단계에서). 미니게임 씨앗을 넘겨받게.
4. `Rootborn.Host`: 남의 캐릭터 그리기(겉모습 키 → 기존 옷 겹치기), 메뉴의 호스트·참가 화면, Steam 콜백 펌프, 네이티브 Steam 라이브러리 복사.
5. 테스트: 같은 프로세스 전송으로 서버 1 + 클라이언트 2를 한 테스트에서 돌리는 dotnet 테스트, UDP로 두 프로세스, FEVM에서 실제 두 창. Steam은 실제 계정 두 개가 있어야 한다.

## 4. 사용자가 정할 것

1. **데디케이티드 서버를 유지할지.** Unity 판에는 있었다(`-mode server`, Windows·Linux 빌드). Steam 친구 방으로는 들어갈 수 없었고 명령줄 IP로만 들어갔다. 작성자 판단: 서버 코드는 Host와 분리해 두되, 데디케이티드 실행 파일 배포는 미룬다.
2. **몇 명까지.** Unity 판 기본 4(`-maxPlayers`), Steam 로비 정원도 같은 값. `SpawnSpread`는 8명까지 겹치지 않는다.
3. **같이 하는 사람들이 무엇을 공유하는지.** Unity 판 사실:
   - 진행(학생 진행, 인벤토리, 체인, 성인기, 마일스톤 등)은 플레이어별 파일이고 네트워크로 보내지 않았다 — 각자 진행.
   - 시간(날짜·때)은 공용, 하루는 모두가 준비해야 넘어갔다. 학생 진행의 날짜는 각자.
   - 퀘스트 로그·공용 월드 상태·집 단계·가구 배치는 **세이브 파일로는 슬롯 공용**이지만 각자 기계에 썼으므로 실제로는 공유되지 않았다.
   - 멀티플레이 중에는 문 이동·성인기 진행이 막혀 Town에만 있었다.
   정할 것: 각자 진행인지, 한 가족(집·가구·돈을 공유)인지, 그 중간인지. 그리고 각자 진행이면 손님의 진행은 손님 기계에 남는지 호스트 슬롯에 남는지.
4. **Steam만인지 직접 접속도인지.** 2026-11-09 결정은 "처음부터 스팀출시만"이고 메뉴의 IP 입력을 지웠다. 작성자 판단: 메뉴는 Steam만, 직접 접속(UDP)은 명령줄과 테스트·데디케이티드 서버용으로만 둔다.
5. 다시 들어온 사람을 같은 사람으로 알아볼 신원(Steam ID 등). Unity 판에는 없었다(1.5).
6. 걷기 동기화 품질: 남이 걷는 것이 보이기만 하면 되는지(위치 보내기 + 사이 메우기), 서로 몸이 막히는지.

## 5. 확인하지 못한 것

- Unity 판을 실행하지 않았다. 1절은 모두 코드·에셋 읽기다. 특히 1.4(네트워크 중 문 이동·성인기 막힘), 1.3의 데디케이티드 서버 시간 소유자, 1.5의 재접속 신원은 실행으로 보지 않았다.
- 실제 Steam 두 계정 사이의 접속·송수신은 Unity 판에서도 확인된 적이 없다(메모리 기록).
- Facepunch.Steamworks의 소켓 API와 릴리스 상태, Steamworks.NET Standalone 판의 Linux 네이티브 파일 구성은 문서로 다 확인하지 못했다. Steam GameServer API로 데디케이티드 서버를 친구 방에 넣는 방법도 조사하지 않았다.
- `GameDefinitions`·Core 정의 객체를 여러 `GameSession`이 같이 써도 안전한지(내부 캐시 변경 여부) 하나하나 보지 않았다.
- `Walker`가 기계 사이에서 비트 단위로 같은지 재 보지 않았다(3.3에서 록스텝을 권하지 않으므로 결정에는 영향이 작다).
- 테스트는 돌리지 않았다(지시문상 해당 없음).

참고한 외부 페이지: [Steamworks.NET 릴리스](https://github.com/rlabrecque/Steamworks.NET/releases), [Facepunch.Steamworks](https://github.com/Facepunch/Facepunch.Steamworks), [LiteNetLib](https://github.com/RevenantX/LiteNetLib) (2026-11-12 열람).
