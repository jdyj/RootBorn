# Steam 조사 — MonoGame 판의 Steam 친구 방과 배포

- 날짜: 2026-11-12 (main `3943e078d` 기준). 조사와 문서만 했다. 코드·패키지는 더하지 않았고 Unity·Steam·테스트는 실행하지 않았다.
- 먼저 읽은 것: 멀티플레이 설계 2판(`2026-11-12-multiplayer-design.md`, 특히 6절과 7절 7단계), 멀티플레이 조사(`2026-11-12-multiplayer-research.md`).
- 표시: **[확인]** 저장소 파일이나 공식 문서·라이브러리 저장소에서 직접 본 것. **[추정]** 기억이나 추론이고 확인하지 않은 것. **[판단]** 작성자(Claude)의 추천.
- 외부 자료는 2026-11-12에 열람했다(주소는 각 절과 끝의 "출처"). 앱 id·계정·비밀값은 적지 않는다. 아래에 나오는 480은 Valve가 공개한 시험용 앱(Spacewar)이고 이 프로젝트의 앱 id가 아니다.

## 0. 요약

- **라이브러리는 Steamworks.NET을 그대로 쓴다.** NuGet 판(2024.8.0, SDK 1.60, netstandard2.1)은 .NET 8 프로젝트에서 플랫폼별 관리 DLL을 자동으로 고르지만 네이티브 파일이 없다. 네이티브 파일(`steam_api64.dll`, `libsteam_api.so`)은 GitHub 릴리스의 독립 실행판(최신 2025.165.0, SDK 1.65)에서 받아 버전을 고정해 둔다(2절).
- **Steam 코드는 새 프로젝트 하나(`game/Rootborn.Steam`, 이름은 추천)에 모은다.** `Rootborn.Net`과 그 테스트는 Steam을 모른 채 둔다. Host와 서버가 그 프로젝트를 선택으로 쓴다. Steam이 없으면 지금처럼 UDP와 주소 입력만 된다(7절).
- **전송은 지금 `ITransport` 뒤에 거의 그대로 들어간다.** 모자란 것은 셋이다: (1) 연결한 상대의 검증된 신원(Steam id)을 위로 넘길 길, (2) 끊김 이유가 바이트가 아니라 정수 코드와 짧은 문자열이라는 차이, (3) Steam 콜백을 돌리는 순서(3절).
- **친구 방**은 Unity 판 흐름(친구 전용 로비 → rich presence `connect` → `+connect_lobby`)을 옮기고, Unity 판에 없던 초대 오버레이(`ActivateGameOverlayInviteDialog`)와 실행 중 초대 콜백 하나를 더한다(4절).
- **데디케이티드 서버의 Steam 등록은 미룬다.** 별도 TOOL 앱 id, 익명 로그인, 서버용 재배포 파일이 필요하고 Steam에서 실제로 확인해야 할 것이 많다. 주소 입력만 두면 잃는 것은 "포트를 안 열어도 됨", "친구 따라 들어가기", "검증된 신원" 셋이다(5절).
- **사용자가 먼저 정할 것**: Steamworks 파트너 계정과 앱 id가 있는지, 앱 id를 어디에 둘지(9절). 이것이 없으면 480으로 개발용 확인까지만 된다.

## 1. Unity 판이 Steam을 어떻게 썼는가 (코드 읽기, 실행 안 함)

### 1.1 라이브러리와 앱 id

| 항목 | 내용 | 근거 |
|---|---|---|
| 라이브러리 | Steamworks.NET 2025.163.0, Unity 패키지를 git 주소로 받음 | [확인] `Packages/manifest.json:30` |
| 앱 id를 받는 곳 | 코드 상수 `SteamPlatform.DevelopmentAppId`(Valve 공개 시험용 앱) 기본값, 실행 인자 `-steamAppId <id>`로 바꿈 | [확인] `Assets/Scripts/Network/Steam/SteamPlatform.cs:15`, `:118-125` |
| `steam_appid.txt` | 쓰지 않았다. 대신 시작 전에 환경 변수 `SteamAppId`·`SteamGameId`를 넣었다("배포 폴더에 낱개 파일을 두지 않으려고"라는 주석) | [확인] `SteamPlatform.cs:61-64`. 저장소에 `steam_appid.txt`가 없다(`git ls-files` 검색) |
| 실제 상점 앱 id | 저장소 어디에도 없다 | [확인] 검색 |
| `SteamAPI_RestartAppIfNecessary` | 부르지 않았다 | [확인] `Assets/Scripts` 검색 |
| 초기화 | `SteamAPI.InitEx`. 실패하거나 DLL이 없으면(`DllNotFoundException` 등) 이유를 `FailureReason`에 두고 게임은 계속 돈다 | [확인] `SteamPlatform.cs:58-90` |
| 콜백 | 프레임마다 `SteamAPI.RunCallbacks()`, 끝낼 때 `SteamAPI.Shutdown()` | [확인] `SteamCallbackRunner.cs:12`, `:15`, `SteamPlatform.cs:100-107` |

### 1.2 Steam을 켜지 않는 경우

- 배치 실행(`-batchmode`), `-nosteam`, `-mode server`(데디케이티드 서버)면 Steam을 아예 켜지 않는다. [확인] `SteamPlatform.cs:44-56`
- Steam이 꺼져 있으면: 호스트는 Unity Transport와 명령줄 포트로 열린다(친구 로비 없음). "친구 게임 참가" 패널은 문구 키 `ui.join.no-steam`("Steam이 실행 중이 아닙니다 …")만 보이고 접속하지 않는다. [확인] `Assets/Scripts/UI/MainMenu/ModeSelectPanel.cs:167-172`, `JoinGamePanel.cs:72-74`, `:107-111`

### 1.3 친구 방 흐름

1. **방 만들기**: 호스트 세션을 Steam 전송으로 먼저 시작한 뒤 `SteamMatchmaking.CreateLobby(FriendsOnly, 정원)`. [확인] `SteamSessions.cs:20-32`, `SteamLobby.cs:31-56`
2. **로비 데이터**: 키 `rootborn_host` = 호스트의 Steam id **하나뿐**이다. 게임 버전·데이터 버전·정원·방 이름은 넣지 않았다. [확인] `SteamLobby.cs:15`, `:48`
3. **rich presence**: `connect` = `+connect_lobby <로비 id>`. 친구 목록의 "게임 참가"가 이 값을 쓴다. 문자열 만들기·읽기는 Core `SteamLaunchArgs`. [확인] `SteamLobby.cs:50`, `Packages/com.rootborn.core/Runtime/Network/SteamSessionRules.cs:47-66`
4. **초대**: 게임 안에서 초대 창을 여는 코드는 없다(`ActivateGameOverlayInviteDialog` 검색 결과 없음). 초대는 Steam 친구 목록에서 사용자가 직접 하는 것에 기댔다. [확인] 검색
5. **들어가기 요청이 오는 길**: 실행 중에는 `GameLobbyJoinRequested_t` 콜백, 꺼져 있을 때는 실행 인자 `+connect_lobby <id>`. 둘 다 `PendingLobbyId`에 두고 메뉴가 꺼내 간다. `GameRichPresenceJoinRequested_t`는 받지 않았다. [확인] `SteamPlatform.cs:84-87`, `:93-98`, `:109-116`, `ModeSelectPanel.cs:187-190`
6. **친구 방 목록**: 친구마다 `GetFriendGamePlayed`로 같은 앱이고 로비가 있는 사람만 고른다(Core `FriendRooms.Joinable`: 로비당 한 줄, 이름순). [확인] `SteamLobby.cs:95-109`, `SteamSessionRules.cs:99-`
7. **들어가기**: `JoinLobby` → 로비 데이터의 호스트 id(없으면 로비 주인) → Steam 전송 `ConnectP2P` → NGO 클라이언트 시작. [확인] `SteamLobby.cs:59-84`, `SteamSessions.cs:38-73`
8. **나가기**: 서버나 클라이언트가 멈추면 로비를 떠나고 rich presence를 지운다. [확인] `SteamLobby.cs:86-92`, `SteamSessions.cs:90-101`

### 1.4 Unity 판 전송 (`SteamNetworkingTransport.cs`)

- 듣기 `CreateListenSocketP2P(가상 포트 0)` + 폴 그룹, 연결 `ConnectP2P(호스트 Steam id, 0)`, 상태 콜백에서 들어오는 연결을 `AcceptConnection`, 받기 `ReceiveMessagesOnPollGroup`/`ReceiveMessagesOnConnection`, 보내기 플래그는 `ReliableNoNagle`/`UnreliableNoNagle`. 끊을 때 이유 코드는 늘 0이고 디버그 문자열("disconnected by host" 등)만 붙였다. [확인] `:60-61`, `:72`, `:76-117`, `:134-135`, `:171-178`, `:190`
- 연결 핸들(`uint`) ↔ NGO 클라이언트 id는 Core `PeerTable`. [확인] `:33`, `SteamSessionRules.cs:10-45`
- **확인된 적 없는 것**: 실제 Steam 두 계정 사이의 초대·참가·송수신. 스파이크(2026-11-11)는 별도 프로그램으로 480 앱의 친구 전용 로비 만들기까지만 통과했다. [확인] `2026-11-12-multiplayer-research.md` 1.7절, `2026-11-11-unity-removal-design.md:233`

## 2. .NET 8 + MonoGame(DesktopGL)에서 쓸 Steam 라이브러리

| | Steamworks.NET | Facepunch.Steamworks |
|---|---|---|
| 유지 | GitHub 릴리스 2025.165.0이 2026-10-29(SDK 1.65), 2025.164.1이 2026-08-02. [확인] GitHub API. NuGet은 2024.8.0(SDK 1.60, 2024-08-29)에서 멈춤. [확인] nuget.org | 저장소 커밋은 2026-10-13까지 있다. [확인] GitHub API. GitHub 릴리스는 비어 있고 NuGet 마지막 판은 2.3.3(2020년). [확인] |
| 대상 프레임워크 | NuGet 판: `netstandard2.1`, 관리 DLL이 `runtimes/<win-x64·linux-x64·osx-x64…>/` 폴더로 플랫폼마다 따로 들어 있다(Windows용과 Linux/macOS용 DLL이 서로 다르다). [확인] 패키지 내용. 독립 실행판: .NET Framework 4.8 프로젝트로 빌드한 DLL, 플랫폼별 폴더. [확인] `Standalone/Steamworks.NET.csproj` | `netstandard2.1;net6.0;net46`. Windows용(Win64·Win32)과 Posix용 프로젝트가 따로다. [확인] `Facepunch.Steamworks.Posix.csproj` |
| 네이티브 파일 | **NuGet 판에는 없다.** [확인] 독립 실행판 zip에는 있다: `Windows-x64/steam_api64.dll`, `OSX-Linux-x64/libsteam_api.so`, `steam_api.bundle`(macOS). [확인] 릴리스 zip 목록 | 저장소에 `steam_api.dll`·`steam_api64.dll`이 있다(Windows). Linux `.so`는 직접 넣어야 한다(추정) |
| API 모양 | SDK를 거의 그대로(콜백·`CallResult`·포인터). Unity 판 코드를 거의 그대로 옮길 수 있다 | C#다운 API(`SteamClient.Init`, 이벤트, async). 소켓 관리자(`SocketManager`/`ConnectionManager`)가 있다는 것은 [추정] — `Networking` 폴더만 확인했다 |
| 서버 | `GameServer`·`SteamGameServerNetworkingSockets` 감싸기 있음 [추정: SDK 전체를 감싸므로] | `SteamServer.Init` 예제가 README에 있다. [확인] |
| 라이선스 | MIT. [확인] 릴리스 zip `LICENSE.txt` | MIT. [확인] README |
| MonoGame과 함께 | 독립 실행판 README가 "렌더러를 만들기 전에 `SteamAPI.Init()`을 부르라"고 한다(오버레이가 그래픽 장치에 붙기 위해). [확인] `README-Standalone.md`. MonoGame 출시작 사례는 확인하지 않았다 [미확인] | 확인하지 않았다 |

Steam Deck: MonoGame DesktopGL의 linux-x64 빌드는 Deck에서 Linux 판으로 돈다 [추정]. Deck 권장 사항에서 이 게임에 걸리는 것은 [확인] (1) 글자 입력이 필요할 때 화면 키보드를 띄워야 한다(`ShowFloatingGamepadTextInput` 또는 `ShowGamepadTextInput`, Verified 조건) — 캐릭터 만들기의 이름 칸, 서버 주소 칸이 해당한다, (2) 기본 컨트롤러 설정으로 모든 기능에 닿아야 한다(Verified 조건) — 지금 게임은 키보드·마우스뿐이다. Steam Linux Runtime(sniper 등)으로 돌릴지는 Steamworks 설정에서 고르는 것으로 기억하지만 확인하지 않았다 [추정].

**추천 [판단]: Steamworks.NET, NuGet 2024.8.0 대신 독립 실행판 2025.165.0을 저장소에 고정.**

- 이유: Unity 판과 스파이크가 이미 이것을 썼고 옮길 코드(전송, 로비, 실행 인자)가 있다. 릴리스가 SDK를 따라 꾸준히 나온다. Facepunch는 NuGet 판이 6년 묵었고 지금 코드와 모양이 다르다.
- 독립 실행판을 고르는 이유: NuGet 판은 SDK 1.60이고 네이티브 파일이 없어 어차피 SDK 재배포 파일을 따로 구해야 한다. 독립 실행판 zip에는 같은 판의 관리 DLL과 네이티브 파일이 함께 있어 짝이 맞는다.
- 다만 독립 실행판 관리 DLL은 플랫폼마다 다른 파일이라 `-r win-x64`·`-r linux-x64` 게시 때 맞는 것을 고르는 MSBuild 조건(RID별 참조)이 필요하다. NuGet 판은 이것을 `runtimes/` 폴더로 자동으로 한다. **대안**: NuGet 2024.8.0을 쓰고 네이티브 파일만 독립 실행판 2024.8.0 zip에서 가져오기. 어느 쪽이든 관리 DLL과 네이티브 파일의 SDK 판이 같아야 한다(다르면 진입점이 없어 시작에 실패할 수 있다 [추정]).
- 저장소에 둘 곳(예: `game/Rootborn.Steam/native/<rid>/`)과 크기: 네이티브 둘이 합쳐 약 0.7MB. [확인] zip 목록. Steamworks SDK 재배포 파일을 게임과 함께 배포하는 것은 Steamworks SDK 사용 조건 안의 일로 기억한다 [추정 — 파트너 계약서를 확인하지 않았다].

## 3. 전송 — Steam 네트워킹 소켓을 `ITransport` 뒤에

지금 인터페이스: `game/Rootborn.Net/Transport/ITransport.cs:55-67` — `Update(now)`, `TryReceive`, `Send(peer, bytes, Delivery)`, `Disconnect(peer, reason bytes)`, `Dispose`. 듣기·연결은 인터페이스가 아니라 구현의 정적 만들기 함수다(`UdpTransport.Listen`/`Connect`, `UdpTransport.cs:129-155`). Steam 전송도 같게 `SteamTransport.ListenP2P(...)`·`SteamTransport.ConnectP2P(hostSteamId, ...)`로 만들면 된다 [판단].

| 인터페이스의 동작 | Steam API ([확인] `ISteamNetworkingSockets` 문서, Unity 판 줄은 1.4절) | 메모 |
|---|---|---|
| 듣기(만들 때) | `CreateListenSocketP2P(가상 포트, …)` + `CreatePollGroup()`. 들어오는 연결은 상태 콜백 `Connecting`에서 `AcceptConnection` 후 폴 그룹에 넣는다 | 문서: 받아들이기는 "1~2초 안에" 하라. 콜백을 프레임마다 돌리면 충분 |
| 연결(만들 때) | `ConnectP2P(SteamNetworkingIdentity(호스트 Steam id), 가상 포트, …)`. 주소 대신 Steam id | 경로 찾기(`FindingRoute`)를 거칠 수 있다 |
| `Connected` 일 | 상태 콜백 `Connected` | |
| `Disconnected` 일 | 상태 콜백 `ClosedByPeer`(상대가 닫음·거절) / `ProblemDetectedLocally`(시간 초과·실패). 둘 다 이쪽에서도 `CloseConnection`을 불러 자원을 풀어야 한다 | 연결 실패도 이 길로 온다 → "붙인 바이트 없는 끊김"(인터페이스 약속과 같다) |
| `Data` 일, `TryReceive` | 서버 `ReceiveMessagesOnPollGroup`, 클라이언트 `ReceiveMessagesOnConnection`. 메시지 경계를 지킨다. 받은 메시지는 `Release` | 연결 하나 안에서는 순서가 지켜지고 연결끼리는 섞인다(문서) |
| `Send(ReliableOrdered)` | `SendMessageToConnection(…, k_nSteamNetworkingSend_Reliable \| NoNagle)` | |
| `Send(Unreliable)` | `… k_nSteamNetworkingSend_Unreliable \| NoNagle` | 잃을 수 있다(인터페이스 약속과 같다) |
| `Disconnect(peer, reason)` | `CloseConnection(hConn, nReason, pszDebug, bEnableLinger)` | 아래 "모자란 것" 2 |
| `PeerId` | `HSteamNetConnection`(32비트 핸들) → `PeerId(long)` | Core `PeerTable`(NGO 클라이언트 id용)은 필요 없다. 핸들을 그대로 `PeerId`로 쓴다 [판단] |
| `Update(now)` | 콜백을 돌린 뒤(아래 3) 받은 것을 모은다 | `now`는 쓰지 않는다(Steam이 자기 시계를 쓴다) |
| `Dispose` | 열린 연결 `CloseConnection(linger: true)`, `CloseListenSocket`, `DestroyPollGroup` | |

**인터페이스에 모자란 것** (고치지 않았다 — "필요한 변경"):

1. **상대의 검증된 신원이 위로 올라가지 않는다.** `TransportEvent.Connected`에는 `PeerId`뿐이다(`ITransport.cs:36-48`). 지금 신원은 클라이언트가 Hello로 스스로 밝힌 문자열이다(`SessionServer.cs:162-195`, `RootbornMessages.cs:8`). Steam 연결은 상대의 Steam id를 Steam이 확인해 준다(연결 정보의 원격 신원 [추정: 필드 이름 `m_identityRemote`은 문서 본문에서 확인하지 못했다]). 서버가 그 값을 쓰려면 "이 연결의 확인된 신원"을 얻는 길(예: Connected 일에 신원 문자열, 또는 `bool TryGetVerifiedIdentity(PeerId, out string)`)이 필요하다. 없으면 Steam으로 들어와도 남의 Steam id를 밝혀 그 사람 캐릭터로 들어갈 수 있다.
2. **끊김 이유의 모양이 다르다.** 인터페이스는 이유를 바이트로 싣는다. Steam의 `CloseConnection`은 정수 이유 코드(앱용은 정해진 범위, 0이면 `App_Generic`)와 짧은 디버그 문자열만 상대에게 보낸다. 지금 이유는 `RoomRefusal`의 짧은 영문 문자열이다(`RoomProtocol.cs:59-69`, 가장 긴 것이 "game version mismatch"). 디버그 문자열 칸에 그대로 실을 수 있다 [추정: 길이 상한 128자로 기억, 미확인]. 다른 길은 끊기 직전에 "작별" 메시지를 신뢰 순서로 보내고 linger로 닫는 것이다. 인터페이스를 바꿀 필요는 없고 Steam 전송 안에서 "이유 바이트 ↔ 디버그 문자열"을 바꾸면 된다 [판단]. 단 이유 바이트가 UTF-8 글자라는 것은 지금 인터페이스가 약속하지 않는다.
3. **콜백 순서.** Steamworks.NET에서 연결 상태 콜백은 `SteamAPI.RunCallbacks()`(서버는 `GameServer.RunCallbacks()`) 안에서 온다. 로비·초대 콜백도 같은 펌프다. 전송의 `Update`가 펌프를 직접 돌리면 다른 Steam 기능의 콜백까지 전송 안에서 돈다. Host가 프레임마다 펌프를 한 번 돌리고 그 다음 전송 `Update`를 부르는 순서로 두는 것이 낫다 [판단]. 인터페이스 변경은 필요 없다.
4. (작은 것) 메시지 크기. 지금 세션 최대 메시지는 4,096바이트다(`SessionSettings.cs:19`, `:44`). Steam 한 메시지 상한(`k_cbMaxSteamNetworkingSocketsMessageSizeSend`, 512KB로 기억 [추정])보다 훨씬 작아 문제가 없다. 손님 저장을 보내는 단계(설계 3.1절)에서 크기를 키울 때 이 상한을 본다.
5. (작은 것) 이쪽이 `CloseConnection`한 뒤에도 상태 콜백이 한 번 더 올 수 있는지 [미확인]. 인터페이스 약속("이쪽에는 끊김 일이 올라오지 않는다")을 지키려면 Steam 전송이 이미 닫은 핸들의 콜백을 버려야 한다. Unity 판은 `PeerTable`에서 지운 뒤 오는 것을 무시했다(`SteamNetworkingTransport.cs:98`).

**호스트 모드와 묶기**: 내 PC에서 방을 열 때 서버 쪽은 "같은 프로세스 안 전송(나) + UDP(주소로 오는 사람)"을 함께 받는 전송 묶음을 쓴다(세션 `mp-host-mode` 지시문). Steam이 켜져 있으면 그 묶음에 Steam 듣기를 하나 더 넣으면 된다 [판단]. 묶음은 구현마다 `PeerId`가 겹치지 않게 나눠야 한다(예: 상위 비트로 구현 번호).

## 4. 친구 방

### 4.1 흐름 (Unity 판 + 더할 것) [판단, Steam 부분은 [확인] matchmaking·ISteamFriends 문서]

1. **방 열기**: 시작 화면 "방 열기"(세션 `mp-host-mode`가 만드는 중) → 같은 프로세스 안 `RoomServer` 시작 → Steam이 켜져 있으면 Steam 듣기를 묶음에 더하고 `CreateLobby(FriendsOnly, 정원)`. 로비 데이터에 **호스트 Steam id, 프로토콜·게임·데이터 버전**(지금 `RoomVersions`), 정원을 넣는다. 버전을 넣으면 친구 방 목록에서 들어가기 전에 "버전이 다름"을 보일 수 있다(Unity 판에는 없었다). rich presence `connect` = `+connect_lobby <로비 id>`.
   - 문서: 로비 하나는 최대 250명. 로비 데이터는 주인만 쓴다. rich presence 키는 사용자당 30개. [확인]
2. **초대**: 방을 연 사람의 화면(방이 열려 있음을 보이는 자리)에 "친구 초대" 단추 → `SteamFriends.ActivateGameOverlayInviteDialog(로비 id)`. 문서: 이 창에서 보낸 초대는 그 로비로의 초대다. [확인] 오버레이가 뜨려면 Steam으로 실행되었거나 오버레이가 켜져 있어야 하고, MonoGame에서는 그래픽 장치를 만들기 전에 Steam을 초기화해야 한다 — Host `Program.cs:31`의 `new HostGame` 전이다 [확인: README 지시, Host 쪽은 추정].
3. **초대를 받고 들어가기 (게임 실행 중)**: 로비 초대를 받아들이거나 친구 목록에서 "게임 참가" → `GameLobbyJoinRequested_t`(로비 id). rich presence `connect`로 오는 경우는 `GameRichPresenceJoinRequested_t`(connect 문자열)로 온다(`InviteUserToGame` 설명). [확인] 둘 다 받아 같은 "로비에 들어가기 요청"으로 바꾼다. Unity 판은 앞의 것만 받았다(1.3절 5).
4. **게임이 꺼져 있을 때**: Steam이 게임을 `+connect_lobby <64비트 로비 id>`를 붙여 실행한다. 문서는 `ISteamApps::GetLaunchCommandLine`을 구현해 명령줄 실행 경고 창을 끄라고 한다. [확인] 읽기는 Core `SteamLaunchArgs.TryGetLobbyId` 그대로. Host 인자 표(`game/README.md` 인자 표)에 `+connect_lobby`를 "Steam이 붙이는 인자"로 더한다. 이 인자는 `--`로 시작하지 않으므로 Host 인자 읽기가 모르는 인자로 멈추지 않게 해야 한다 [미확인: `HostOptions.Resolve`가 모르는 인자를 어떻게 다루는지 보지 않았다].
5. **들어가기**: `JoinLobby` → 로비 데이터의 버전 확인(다르면 들어가지 않고 이유를 보인다) → 호스트 Steam id로 `SteamTransport.ConnectP2P` → 지금과 같은 접속 확인 관문(`HandshakeClientTransport`) + `SessionClient`. 슬롯(인물) 고르기는 지금 들어가기 창처럼 손님 화면에서.
6. **나가기·닫기**: 방을 닫거나 게임을 끄면 로비를 떠나고 rich presence를 지운다(Unity 판과 같다).

### 4.2 지금 Host 화면과 잇기

- 지금 들어가기는 `IRoomJoin.Begin(address, slotId)` 하나다(`game/Rootborn.Screens/Start/RoomJoin.cs:21-33`). Steam으로 들어가기는 "주소" 대신 "로비 id"를 받는다. **필요한 변경**: `IRoomJoin`에 로비로 들어가기를 더하거나, 들어갈 곳을 "주소 또는 로비"로 일반화한다. 단계(`RoomJoinPhase`)와 이유 코드 → 문구 키(`RoomJoinText.ReasonKey`)는 그대로 쓸 수 있다. Steam 쪽 이유 코드(예: "steam unavailable", "lobby not found", "lobby version mismatch")는 들어가는 쪽이 정하는 코드로 더한다.
- 시작 화면에 "친구 방" 목록(Unity 판 `JoinGamePanel`)을 둘지, "서버 주소로 들어가기"와 나란히 둘지는 화면 구성이라 사용자에게 묻는다(9절). Steam이 없을 때 그 단추의 동작(숨김 / 안내 문구)도 같다. Unity 판은 단추를 두고 "Steam이 실행 중이 아닙니다"를 보였다.
- 방 열기 모달: Steam이 켜져 있으면 주소·포트 칸 없이도 열 수 있어야 한다(친구는 Steam으로 들어오므로). 주소·포트는 "주소로도 받기"를 고를 때만 [판단].
- 로비 만들기·로비 데이터 쓰기는 방 쪽이 아니라 Host(또는 `Rootborn.Steam`)의 일이다. `RoomServer`는 Steam을 모른다.

## 5. 데디케이티드 서버와 Steam

### 5.1 Steam에 게임 서버로 등록하려면 [확인: Steamworks 문서 `distributing_gs`, `ISteamGameServer`, `ISteamNetworkingSockets`, 게임 서버 개요]

- **별도 앱 id(TOOL 종류)** 를 만들어 서버 전용으로 쓴다. 기술 설정의 "Installation / Redistributables"에서 **Dedicated Server Redistributables**를 켠다(Steam 클라이언트 없이 돌 Steam 바이너리가 따라온다). 서버 파일만 든 디포를 올린다.
- 서버 묶음에는 게임 본편 앱 id가 든 `steam_appid.txt`를 넣는다("서버가 본편 앱 id로 돌게"). — 게임 클라이언트에는 넣지 말라는 것과 반대다.
- 익명 SteamCMD 묶음에 넣으면 누구나 Steam 계정 없이 서버를 받아 돌릴 수 있다("Include this tool in Dedicated Server package"). 앱을 "released"로 표시해야 SteamCMD로 돈다.
- 코드: `steam_gameserver.h` 쪽만 초기화하면 된다(사용자 API는 필요 없다). 초기화에 바인드 IP, 게임 포트, 질의 포트, 서버 플래그, 앱 id, 버전 문자열을 준다. 버전 문자열로 마스터 서버가 서버가 최신인지 본다. 로그인은 `LogOnAnonymous()`(일회용 익명 계정) 또는 `LogOn(토큰)`(영구 서버 계정). `SetMaxPlayerCount`, `SetServerName`, `GetSteamID`. 클라이언트 신원 확인은 `BeginAuthSession`(→ `ValidateAuthTicketResponse_t`) / `EndAuthSession`.
- 전송: 게임 서버도 `SteamGameServerNetworkingSockets()`로 `CreateListenSocketP2P`를 쓸 수 있다(문서: "알려진 데이터 센터의 서버"에 대한 설명). Valve 중계 위에서 직접 호스팅하는 방식(`CreateHostedDedicatedServerListenSocket`, `SDR_LISTEN_PORT`)은 표 발급이 따로 필요해 이 게임에는 과하다 [판단]. 집에서 돌리는 커뮤니티 서버가 익명 로그인 + P2P 듣기로 "포트를 안 열고" 친구를 받을 수 있는지는 **[미확인]** — 문서가 데이터 센터 서버만 예로 든다. Steam에서 실제로 해 봐야 한다.
- 서버 목록: Steam 클라이언트·오버레이의 서버 브라우저, 게임 안에서는 `ISteamMatchmakingServers`로 같은 목록을 읽는다. [확인] (설계 8절이 "공개 방 목록"을 하지 않는다고 했으므로 서버 목록은 범위 밖이다.)
- 친구가 서버에 들어가 있을 때 따라 들어가기: 손님이 rich presence `connect`에 서버로 가는 문자열을 넣으면 친구 목록의 "게임 참가"가 된다 [추정: 로비 없이 connect 문자열만으로 되는 것은 ISteamFriends 설명에 기댄 추정].

### 5.2 화면 없는 Linux 서버에서

- Dedicated Server Redistributables가 Steam 클라이언트 없는 장치용이라 화면 없는 Linux에서 도는 것이 정상 경로다 [확인: 문서 취지]. Linux 서버에 필요한 파일(`libsteam_api.so` 외에 `steamclient.so`를 어디서 찾는지)은 문서에서 확인하지 못했다 [미확인 — 기억으로는 SteamCMD로 받은 `steamclient.so`를 `~/.steam/sdk64/`에 두는 방식].
- 지금 서버는 Steam을 모르고 UDP로만 듣는다(`game/Rootborn.Server`, `game/README.md` "서버"). Steam을 넣으면 `Rootborn.Server`가 `Rootborn.Steam`을 참조하고 `--steam` 같은 선택 인자로 켜는 모양이 된다 [판단]. 끄면 지금과 같아야 한다.

### 5.3 하지 않고 주소 입력만 둘 때 잃는 것

1. **포트를 안 열어도 됨**: 서버 주인이 공유기에서 UDP 포트를 열어야 한다(또는 같은 LAN·VPN).
2. **친구 따라 들어가기·초대**: 서버에 있는 친구를 Steam 친구 목록에서 눌러 따라 들어갈 수 없다. 주소를 따로 알려 줘야 한다.
3. **검증된 신원**: 주소로 들어온 사람의 신원은 스스로 밝힌 값이다. Steam 계정을 신원으로 쓰려면 손님이 Steam 인증 표를 보내고 서버가 `BeginAuthSession`으로 확인해야 하는데, 그것은 서버가 Steam 게임 서버로 로그인해 있어야 한다 [확인: `BeginAuthSession`이 `ISteamGameServer`에 있다]. 협동 게임이라 부정 방지는 목표가 아니지만(설계 3절) **남의 캐릭터로 들어가는 것**은 막고 싶을 수 있다(6절).

추천 [판단]: 설계 6절의 (가) 주소로 먼저(이미 있다) → Steam 전송·친구 방(7단계) → 그 다음 (나)를 별도 단계로. (나)는 사용자가 TOOL 앱 id를 만들 수 있게 된 뒤에만 시험할 수 있다.

## 6. 신원

지금: 방에서의 신원은 슬롯 정보(만든 시각, 시드 둘)로 만든 `slot-<16자리>`다. 임시라고 적혀 있고 "Steam 전송이 들어오면 여기를 바꾼다"고 되어 있다. [확인] `game/Rootborn.Host/Net/GuestProgress.cs:19-30`. 서버는 그 문자열을 그대로 믿고, 같은 신원이면 앞의 연결을 끊는다. [확인] `SessionServer.cs:162-195`. 방의 시계 세이브는 참가자별 줄(`Participants`)을 갖는다. [확인] `Packages/com.rootborn.core/Runtime/Time/RoomClockSaveData.cs:20` (참가자 id 필드 이름은 보지 않았다.)

Steam id로 바꿀 때 [판단]:

- **신원 문자열**: `steam-<64비트 Steam id>`. Steam 전송으로 들어오면 서버가 연결의 원격 신원에서 만든다(3절 모자란 것 1). Hello의 값은 그와 같아야 하고 다르면 거절한다.
- **세이브와 맞물림**: 설계는 "손님 진행은 방을 가진 쪽의 세이브에, 그 사람의 파일로"이다(3절). 파일 이름은 `SaveFileNames`가 플레이어 id를 붙여 만든다. 그래서 신원 = 파일 이름의 열쇠다. 신원을 바꾸면 그 방에서 그 사람의 진행은 새 이름의 파일로 가야 한다.
  - 지금(2026-11-12)은 손님 진행이 아직 손님 PC에 남는다(`GuestProgress.StoreFor`). 방에 남는 신원은 시계 참가자 줄 정도다. **Steam 신원을 손님 저장(설계 7절 4단계, 세션 `mp-shared-pieces`)보다 먼저, 또는 함께 정하면 옮길 것이 거의 없다.** 늦으면 `slot-…` 신원으로 쌓인 방 세이브를 Steam id로 옮기는 단계(누가 누구인지 묻는 화면)가 필요하다.
- **같은 사람이 캐릭터 여럿**: 설계는 "캐릭터는 그 방의 주민"이다. 그래서 **한 방에 한 Steam 계정당 캐릭터 하나**가 가장 단순하다(신원 = Steam id). 같은 사람이 다른 캐릭터를 원하면 다른 방(다른 슬롯·서버)에서 만든다. 한 방에 여럿을 허용하려면 신원을 `steam-<id>/<캐릭터 번호>`로 두고 들어갈 때 고르게 해야 하며, 동시에 둘로 들어가는 것은 막는다. 이것은 기획 결정이다(9절).
- **호스트 자신**: 내 PC에서 연 방에서 나도 `steam-<내 id>`다. 혼자 하기(방 없음)의 세이브는 지금처럼 기본 플레이어 id(`local-player`)를 쓴다. 혼자 하던 슬롯으로 방을 열 때 내 진행 파일 이름이 바뀌지 않게, 방을 연 사람은 기본 플레이어 id 그대로 두는 것이 낫다 [판단 — 세션 `mp-host-mode`가 정하는 중일 수 있다].
- **Steam 없이 들어오는 경우**(주소, 개발·테스트): 지금처럼 스스로 밝힌 신원(실행 인자나 슬롯에서 만든 값). 서버 설정으로 "Steam 신원만 받기"를 둘 수 있다(서버가 Steam 게임 서버일 때만 의미가 있다).

## 7. 개발 중에 Steam 없이

- **구조 [판단]**: Steam 참조는 새 프로젝트 `game/Rootborn.Steam` 하나에만 둔다(Steamworks.NET 참조, `Rootborn.Net` 참조, MonoGame 참조 없음). 안에 Steam 초기화(Unity `SteamPlatform`에 해당, 정적 상태 대신 객체), 콜백 펌프, `SteamTransport`, 로비·초대. Host는 이 프로젝트를 참조하되 **시작할 때 Steam을 켜 보고 안 되면 없이 돈다** — 지금처럼 UDP와 주소 입력만. `Rootborn.Net`·`Rootborn.Net.Tests`·Screens·Game 테스트는 Steam을 모른다.
  - Steam을 켜지 않는 조건: `--no-steam`(Unity 판 `-nosteam`에 해당), Host 테스트 모드, 데디케이티드 서버(Steam 서버 기능을 켜는 인자가 없을 때). 켜지 못한 이유는 로그에 한 줄 남기고 화면에는 키로 된 안내(Unity 판 `ui.join.no-steam`과 같은 뜻)를 보인다.
  - 네이티브 DLL이 없거나 Steam 클라이언트가 꺼져 있어도 예외로 끝나지 않는다(Unity 판처럼 `DllNotFoundException` 등을 잡는다).
  - **필요한 변경**: `rules/coding-standards.md` 프로젝트 표에 `Rootborn.Steam` 한 줄과 "Steamworks.NET 참조는 `Rootborn.Steam`(과 그 테스트)에만"을 더하고, CI에 MonoGame 참조 검사(`Scripts/ci/check-monogame-references.sh`)와 같은 모양의 Steam 참조 검사를 둔다.
- **테스트·CI·원격 테스트(Strix Halo)**: 모두 Steam 없이 돈다. Steam 전송의 단위 테스트는 Steam API를 얇은 인터페이스(연결 만들기, 보내기, 받기, 상태 바뀜) 뒤에 두고 가짜로 돌릴 수 있다 [판단]. 실제 Steam 전송 확인은 사람이 한다(10절 표의 오른쪽 칸).
- **`steam_appid.txt`와 480**:
  - 쓸 수 있는 곳: 개발 PC에서 Steam 밖에서 띄울 때(문서: 테스트용). 480으로 로비 만들기·친구 방·P2P 연결을 두 계정으로 시험하는 것. 앱 id는 Unity 판처럼 실행 인자나 환경 변수로 주고 저장소에 낱개 파일을 두지 않는 쪽이 낫다(`rules/environment-config.md`). [판단]
  - 쓰면 안 되는 곳: **배포 폴더에 `steam_appid.txt`를 넣지 않는다**("Do not ship this to customers!", "You should not ship this with your builds"). [확인] 배포 빌드의 기본 앱 id를 480으로 두지 않는다 — 480은 남의 앱이라 업적·상점·rich presence 문자열·로비가 그 앱 이름으로 보인다 [추정]. CI·테스트에서 480으로 Steam을 켜지 않는다(Steam 클라이언트가 없다).
  - 문서: `steam_appid.txt`가 있으면 `SteamAPI_RestartAppIfNecessary`가 늘 false를 돌려준다. [확인] 그래서 개발 중에는 재실행이 일어나지 않고, 배포판(파일 없음)에서는 Steam 밖에서 실행하면 Steam으로 다시 띄운다. 배포판에서 이 호출을 넣을지는 7단계에서 정한다(넣으면 "Steam으로만 실행"이 된다).

## 8. 배포

Steam에 올릴 때 필요한 것(목록만):

- **앱과 디포**: 본편 앱 id, Windows 디포(win-x64), Linux 디포(linux-x64), 필요하면 공통 내용 디포(데이터·그림이 두 플랫폼에 같으므로 나누면 받는 양이 준다 [판단]). 실행 설정(실행 파일 이름, 플랫폼별), SteamPipe로 올리기(`steamcmd` + 앱·디포 빌드 스크립트).
- **서버**: 서버를 Steam으로 배포하려면 위 5.1절의 별도 TOOL 앱 id와 디포. Steam으로 배포하지 않고 서버 폴더를 따로 나눠 줄 수도 있다(Steam 게임 서버 기능을 안 쓰면 이쪽이 단순하다).
- **Steam 네이티브 파일**: `steam_api64.dll`(Windows), `libsteam_api.so`(Linux)를 실행 파일 옆에. `steam_appid.txt`는 넣지 않는다(서버 묶음은 반대로 넣는다).
- **Steam Deck**: 화면 키보드, 컨트롤러 설정(2절). Deck 검사 제출은 선택.
- **상점 페이지·연령 등급·가격** 등은 이 문서 범위 밖.

이 저장소의 미결 사항과의 관계:

- **한글 글꼴**: 배포 폴더에 글꼴이 없고 운영체제 글꼴(맑은 고딕 등)을 찾는다. 맑은 고딕은 게임과 함께 배포할 수 없다. [확인] `Tools/publish/README.md` "글꼴". Steam 판에는 배포할 수 있는 글꼴(예: OFL 글꼴)을 넣어야 하고, Linux·Deck에는 한글 글꼴이 없을 수 있어 **출시 전 필수**다. 사용자 결정 대기.
- **그림·맵 교체 예정**: 디포 구성에는 영향이 없다(폴더 안 파일이 바뀔 뿐).
- **데이터 지문**: 서버 폴더와 클라이언트 폴더의 데이터 지문이 다르게 셈해질 수 있다는 미결 사항이 있다(`Tools/publish/README.md` "지문"). 친구 방에서 호스트 PC의 방도 같은 지문을 쓰므로 배포판끼리는 같지만, **배포판 클라이언트 ↔ 배포판 서버**에서 버전 확인이 거절할 수 있다. 서버 Steam 배포 전에 정할 일이다.
- **게임 버전 문자열**: git 없이 빌드하면 해시 없이 `1.0.0`이 된다(`Tools/publish/README.md` "버전"). Steam 빌드 서버(또는 CI)에서 빌드할 때 버전이 겹치지 않게 정해야 버전 확인이 의미가 있다.

**지금 `Tools/publish`가 만드는 폴더를 그대로 올릴 수 있는가**: 모양은 거의 맞다 [판단]. 저장소 없이 도는 자체 포함 폴더(`output/publish/<rid>/`, 실행 파일이 위로 올라가며 루트를 찾음)이고 Unity 전용 파일이 빠져 있다. [확인] `Tools/publish/README.md`. 모자란 것:

1. Steam 네이티브 파일이 없다(아직 Steam 코드가 없으므로). 목록(`content.txt`)이나 csproj 복사 규칙에 한 줄 필요.
2. 글꼴이 없다(위).
3. Linux 실행 권한: Windows에서 만든 Linux 폴더는 실행 권한이 빠진다(README에 적혀 있다). SteamPipe가 Linux 디포의 실행 권한을 어떻게 다루는지는 [미확인] — Linux에서 빌드하거나 CI(ubuntu)에서 만든 폴더를 올리는 쪽이 안전하다.
4. 루트 표지: 루트를 `Assets/modernuserinterface-win`으로 찾는다. 그림 교체 때 이 표지가 사라지면 배포판이 루트를 못 찾는다(그림 교체 쪽 일).
5. 크기 약 100MB, 파일 3,620개(README 수치). Steam 디포로 문제없는 규모다 [추정].

## 9. 사용자가 정하거나 준비해야 할 것 (순서대로)

1. **Steamworks 파트너 계정과 본편 앱 id가 있는가.** 없으면 만든다(가입비 등 조건은 Steamworks 쪽 확인). 그 전까지는 480으로 개발용 확인만 된다. 앱 id 값은 저장소에 적지 않고 실행 인자·환경 변수·빌드 설정 중 어디에 둘지 정한다 — 추천은 "배포 빌드에만 들어가는 빌드 설정 값, 개발은 실행 인자" [판단].
2. **시험할 Steam 계정 둘과 PC 둘**(또는 PC 하나 + Strix Halo/VM — 같은 PC에서 Steam 두 계정은 동시에 안 된다 [추정]). 두 계정이 서로 친구여야 한다. 480으로 먼저 하고, 본편 앱 id가 생기면 그 앱의 베타 브랜치나 개발자 키로 다시.
3. **메뉴 구성**: 시작 화면에 "친구 방" 목록을 둘지(Unity 판처럼), 친구 방은 Steam 초대·친구 목록으로만 들어갈지. Steam이 없을 때 그 단추를 숨길지 안내만 보일지. "서버 주소로 들어가기"를 Steam 판 메뉴에 계속 둘지(2026-11-09 결정은 "Steam만", 설계 2판은 서버용으로 주소 입력을 다시 둠).
4. **한 방에 한 계정당 캐릭터 하나**로 할지(6절 추천).
5. **데디케이티드 서버를 Steam으로 배포할지**(TOOL 앱 id, 익명 SteamCMD) 아니면 서버 폴더를 따로 나눠 줄지. 그리고 Steam 게임 서버 등록(친구 따라 들어가기, 검증된 신원)을 할지. 추천은 친구 방이 끝난 뒤 정하기.
6. **배포할 한글 글꼴**(출시 전 필수).
7. Steam으로만 실행되게 할지(`RestartAppIfNecessary`), Steam Deck Verified를 목표로 할지(컨트롤러 지원은 큰 일이다).

## 10. 만드는 순서 제안

설계 7절의 7단계(Steam 전송)를 나눈 것이다. 각 단계에서 "Steam 없이 되는 확인"과 "Steam이 있어야 되는 확인(사람이 함)"을 나눴다.

| # | 단계 | Steam 없이 확인 (자동 테스트, 근거 종류) | Steam이 있어야 하는 확인 (사람) |
|---|---|---|---|
| 1 | `game/Rootborn.Steam` 뼈대: Steamworks.NET 고정(관리 DLL + 네이티브), 초기화·종료·콜백 펌프, "Steam 없이 돈다". Host가 그래픽 장치 전에 초기화하고 실패하면 없이 계속. CI Steam 참조 검사, 규칙 문서 한 줄 | 네이티브 DLL이 없을 때·`--no-steam`일 때 Host·Server가 지금과 같이 돈다(Host 테스트 — 장치 이벤트 주입). 배포 폴더에 네이티브 파일이 들어가고 `steam_appid.txt`가 없다(`verify.sh`) | 한 계정, 480: Steam이 켜진 PC에서 띄우면 "Steam 준비" 로그와 오버레이(Shift+Tab)가 뜬다 — Windows·Linux 각각 |
| 2 | `SteamTransport`(P2P 듣기·연결, 신뢰·비신뢰, 끊김 이유 문자열). 신원을 위로 넘기는 길(3절 모자란 것 1) — `Rootborn.Net`의 인터페이스 변경 | Steam API를 가짜로 둔 단위 테스트: 상태 바뀜 → 일, 이유 문자열 왕복, 닫은 핸들의 콜백 버리기. 신원 길은 루프백·UDP 전송에서 "확인 안 된 신원"으로 같은 테스트 | 두 계정, 480: 한 쪽이 듣고 다른 쪽이 Steam id로 연결해 방에 들어가 서로 걷는 것이 보인다. 끊기·다시 들어오기 |
| 3 | 친구 방: 로비 만들기(버전 데이터), rich presence, 초대 오버레이 단추, 두 가지 들어가기 콜백, `+connect_lobby` 실행 인자, `IRoomJoin`의 로비 들어가기, 시작 화면 구성(사용자 결정 3) | `+connect_lobby` 인자 읽기(Core 테스트 있음), Host가 그 인자로 띄워졌을 때 들어가기 창이 열림(가짜 Steam), 버전이 다른 로비는 들어가지 않음, 문구 키가 모든 언어에 | 두 계정: 초대 보내기 → 받는 쪽이 실행 중일 때/꺼져 있을 때 들어가기, 친구 목록 "게임 참가", 방 닫기 → 손님에게 알림 |
| 4 | 신원을 Steam id로(손님 저장 단계와 함께) | 신원 = 연결의 확인된 값, 다른 Hello는 거절, 같은 계정 두 번째 접속은 앞을 끊음(가짜 Steam). 방 세이브를 닫았다 열면 같은 사람으로 이어짐 | 두 계정: 손님이 나갔다 들어오면 같은 진행 |
| 5 | 배포: SteamPipe 스크립트, Windows·Linux 디포, 글꼴(결정 6) | 배포 폴더 확인(`verify.sh`) | 본편 앱 id: 베타 브랜치로 올리고 두 계정이 상점 판으로 받아 친구 방 |
| 6 | (선택) 데디케이티드 서버의 Steam 게임 서버 등록, 서버 Steam 배포(결정 5) | 서버가 `--steam` 없이는 지금과 같음, 있으면 익명 로그인 실패를 로그로 알리고 UDP로 계속(가짜) | TOOL 앱 id: 화면 없는 Linux에서 익명 로그인, 친구가 P2P로 들어감, 따라 들어가기 |

- 1·2단계는 지금 바로 시작할 수 있다(480과 계정 둘만 있으면 사람 확인까지). 3단계의 시작 화면 부분은 세션 `mp-host-mode`(방 열기 모달)가 main에 들어온 뒤가 겹침이 적다.
- 자동 테스트는 모두 "자동 테스트" 또는 "장치 이벤트 주입(OS 입력 계층 미확인)"이다. **"Steam에서 확인"은 사람이 두 계정으로 해 본 뒤에만 쓴다**(설계 9절).

## 11. 확인하지 못한 것

- Steam을 실행하지 않았다. Steamworks.NET이 .NET 8 + MonoGame DesktopGL 프로세스에서 초기화·오버레이·소켓까지 도는지(스파이크는 별도 프로그램으로 로비까지).
- 독립 실행판 관리 DLL(.NET Framework 4.8로 빌드)이 .NET 8에서 그대로 로드되는지 [추정: 대체로 되지만 확인 안 함]. NuGet 판(netstandard2.1)은 문제가 없을 것이다.
- `SteamNetConnectionInfo_t`의 원격 신원 필드 이름과, 이쪽이 닫은 뒤 콜백이 오는지(3절 1·5), 디버그 문자열 길이 상한, 메시지 크기 상한 수치.
- 커뮤니티 데디케이티드 서버가 익명 로그인 + P2P 듣기로 포트 없이 친구를 받을 수 있는지, Linux 서버의 `steamclient.so` 배치(5절).
- 로비 없이 rich presence `connect`만으로 서버 따라 들어가기가 되는지, `GameLobbyJoinRequested_t`와 `GameRichPresenceJoinRequested_t` 중 어느 것이 어느 상황에 오는지 실제로(4.1절 3).
- `HostOptions.Resolve`가 `+connect_lobby`처럼 모르는 인자를 어떻게 다루는지.
- Steamworks SDK 재배포 조건, Steam Linux Runtime 선택, SteamPipe의 Linux 실행 권한 처리.
- MonoGame으로 Steam에 출시한 사례와 그 구성.

## 12. 고친 것과 "필요한 변경"

- 이 세션이 고친 것: 이 문서 하나.
- 필요한 변경(다른 세션·조정 담당 몫, 모두 7단계를 시작할 때):
  1. `ITransport`에 연결의 확인된 신원을 얻는 길(3절 모자란 것 1).
  2. `IRoomJoin`에 로비로 들어가기(4.2절).
  3. `rules/coding-standards.md` 프로젝트 표에 `Rootborn.Steam`, CI에 Steam 참조 검사(7절).
  4. `game/README.md` 인자 표에 `--no-steam`, Steam 앱 id 인자, `+connect_lobby`(Steam이 붙임).
  5. `Tools/publish`: Steam 네이티브 파일 복사, `steam_appid.txt`가 배포에 없는지 검사.
- 이전 조사 문서 정정: `2026-11-12-multiplayer-research.md` 3.1절의 "최신 2025.165.0(2025-10-29)"은 GitHub API로 보면 2026-10-29에 나온 것이다.

## 출처 (2026-11-12 열람)

- Steamworks.NET 릴리스: https://github.com/rlabrecque/Steamworks.NET/releases (GitHub API `releases`, `Steamworks.NET-Standalone_2025.165.0.zip` 내용과 `README-Standalone.md`)
- Steamworks.NET 설치: https://steamworks.github.io/installation/
- Steamworks.NET 독립 실행판 프로젝트: https://github.com/rlabrecque/Steamworks.NET/blob/master/Standalone/Steamworks.NET.csproj
- NuGet Steamworks.NET 2024.8.0: https://www.nuget.org/packages/Steamworks.NET (패키지 내용 목록)
- Facepunch.Steamworks: https://github.com/Facepunch/Facepunch.Steamworks (README, `Facepunch.Steamworks.Posix.csproj`, GitHub API `commits`), NuGet: https://www.nuget.org/packages/Facepunch.Steamworks
- Steamworks API 개요(`steam_appid.txt`, `RestartAppIfNecessary`, 게임 서버 초기화): https://partner.steamgames.com/doc/sdk/api
- ISteamNetworkingSockets: https://partner.steamgames.com/doc/api/ISteamNetworkingSockets
- ISteamGameServer: https://partner.steamgames.com/doc/api/ISteamGameServer
- 데디케이티드 서버 배포: https://partner.steamgames.com/doc/sdk/uploading/distributing_gs
- 게임 서버 개요: https://partner.steamgames.com/doc/features/multiplayer/game_servers
- 매치메이킹·로비: https://partner.steamgames.com/doc/features/multiplayer/matchmaking
- ISteamFriends: https://partner.steamgames.com/doc/api/ISteamFriends
- Steam Deck 권장 사항: https://partner.steamgames.com/doc/steamdeck/recommendations
