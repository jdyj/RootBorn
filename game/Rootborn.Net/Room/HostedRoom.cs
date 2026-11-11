using System;
using System.Globalization;
using System.Net;
using Rootborn.Core.Save;
using Rootborn.Core.StudentLife;
using Rootborn.Net.Transport;

namespace Rootborn.Net.Room
{
    /// <summary>
    /// 내 PC에서 여는 방(설계 6절 "내 PC에서 열기 — 같은 프로세스 안에 서버가 돌고 나는 거기에 접속한 플레이어"). 데디케이티드 서버와 같은
    /// <see cref="RoomServer"/>를 같은 프로세스에서 돌린다. 듣는 전송은 둘을 묶은 것이다: 방을 연 사람이 들어올 같은 프로세스 안 전송
    /// (<see cref="ConnectLocal"/>, 지연 없음)과 다른 사람이 들어올 UDP. 틱은 넘긴 시계로 센 고정 간격이고(<see cref="RoomLoop"/>),
    /// 부르는 쪽(Host의 게임 루프)이 프레임마다 <see cref="RunDue"/>를 부른다 — 스레드도 실시간 기다림도 없다.
    /// 방의 상태(시계)는 넘긴 저장소의 그 슬롯에 저장된다(방을 연 사람의 슬롯). 같은 슬롯으로 다시 열면 이어진다.
    /// </summary>
    public sealed class HostedRoom : IDisposable
    {
        private readonly LoopbackNetwork _local;
        private readonly RoomLoop _loop;

        private HostedRoom(IPAddress bind, int port, LoopbackNetwork local, RoomServer server, RoomServerSettings settings, IRoomClock clock)
        {
            BindAddress = bind;
            Port = port;
            _local = local;
            Server = server;
            _loop = new RoomLoop(server, clock, settings.TickSeconds);
        }

        /// <summary>
        /// 방을 연다: UDP로 듣고(실패하면 예외 — 아무것도 열어 두지 않는다), 그 슬롯의 방 상태를 읽어(없으면 새 방) 방을 만든다.
        /// </summary>
        /// <param name="bind">듣는 주소(이 기계의 LAN 주소 등). 기본값이 없다 — 부르는 쪽이 인자·화면에서 받는다.</param>
        /// <param name="port">듣는 포트. 0이면 운영체제가 빈 포트를 준다(<see cref="Port"/>).</param>
        /// <param name="clock">방의 틱을 세는 시계(Host가 넘긴다).</param>
        /// <exception cref="InvalidOperationException">듣지 못했다(포트를 쓰는 중 등).</exception>
        /// <exception cref="System.Net.Sockets.SocketException">듣지 못했다.</exception>
        /// <remarks>그 슬롯의 방 상태를 읽을 수 없으면 저장소가 던진 예외가 그대로 나온다(파일 저장소: 깨진 파일·새 형식). 덮어쓰지 않는다.</remarks>
        public static HostedRoom Open(IPAddress bind, int port, RoomServerSettings settings, RoomVersions versions, ISaveStore store, string saveSlot,
            IRoomClock clock, Action<string> log, LifeCalendar calendar = null)
        {
            if (bind == null) throw new ArgumentNullException(nameof(bind));
            if (clock == null) throw new ArgumentNullException(nameof(clock));
            settings ??= new RoomServerSettings();
            settings.Validate();
            UdpTransport udp = UdpTransport.Listen(bind, port, RoomProtocol.ConnectionKey);
            var local = new LoopbackNetwork();
            var merged = new MergedServerTransport(local.Listen(), udp);
            RoomServer server;
            try
            {
                server = new RoomServer(merged, settings, versions, store, saveSlot, log, calendar);
            }
            catch
            {
                merged.Dispose();
                throw;
            }

            log?.Invoke("내 PC에서 방을 엶: 듣는 중 " + bind + ":" + udp.LocalPort.ToString(CultureInfo.InvariantCulture));
            return new HostedRoom(bind, udp.LocalPort, local, server, settings, clock);
        }

        /// <summary>방(서버 쪽 논리). 데디케이티드 서버와 같은 코드다.</summary>
        public RoomServer Server { get; }

        public IPAddress BindAddress { get; }

        /// <summary>실제로 듣는 UDP 포트(0으로 열었으면 운영체제가 준 것).</summary>
        public int Port { get; }

        /// <summary>다른 사람이 들어올 주소 "주소:포트".</summary>
        public string ListenAddress => BindAddress + ":" + Port.ToString(CultureInfo.InvariantCulture);

        /// <summary>정원(방 설정 — 다시 연 방이면 만들 때의 값).</summary>
        public int MaxPlayers => Server.MaxPlayers;

        /// <summary>그 슬롯에 이미 방이 있으면 true와 그 설정(<see cref="RoomServer.TryReadSaved"/>).</summary>
        public static bool TryReadSaved(ISaveStore store, string saveSlot, out SavedRoomSettings saved) => RoomServer.TryReadSaved(store, saveSlot, out saved);

        /// <summary>지금 들어와 있는 사람 수(방을 연 사람 포함).</summary>
        public int PlayerCount => Server.IsShutDown ? 0 : Server.Players.Count;

        public bool IsOpen => !Server.IsShutDown;

        /// <summary>
        /// 같은 프로세스 안에서 이 방에 붙는 클라이언트 쪽 전송(방을 연 사람의 게임이 쓴다). 위에 <see cref="RoomClient.Create"/>로 세션을 얹는다.
        /// 시간은 그 클라이언트가 Update에 넘기는 값을 쓴다(지연 0이라 방의 틱과 같은 시계면 바로 닿는다).
        /// </summary>
        public ITransport ConnectLocal()
        {
            if (!IsOpen) throw new InvalidOperationException("닫은 방에는 붙을 수 없다");
            return _local.Connect();
        }

        /// <summary>시계가 지난 만큼 방의 틱을 돌린다(프레임마다 부른다). 닫은 방은 돌지 않는다.</summary>
        /// <returns>이번에 돌린 틱 수.</returns>
        public int RunDue() => IsOpen ? _loop.RunDue() : 0;

        /// <summary>방 상태를 저장하고 들어와 있는 모두에게 닫혔다고 알린 뒤(<see cref="RoomRefusal.Shutdown"/>) 듣기를 멈춘다. 두 번 불러도 된다.</summary>
        public void Shutdown()
        {
            if (!IsOpen) return;
            // 같은 프로세스 안 끝으로 보낸 작별은 그 클라이언트가 다음에 Update할 때 닿는다(지연 0). UDP는 닫을 때 끊는다고 보낸다.
            Server.Shutdown();
        }

        public void Dispose() => Shutdown();
    }
}
