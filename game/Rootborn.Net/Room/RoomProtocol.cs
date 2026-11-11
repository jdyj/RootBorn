using System;
using Rootborn.Core.Network;
using Rootborn.Net.Session;

namespace Rootborn.Net.Room
{
    /// <summary>
    /// 방 접속 약속. <see cref="Version"/>은 접속 확인(<see cref="RoomHandshakeMessages"/>)과 세션 메시지의 모양이 바뀌면 올린다.
    /// <see cref="ConnectionKey"/>는 UDP 전송의 연결 열쇠다. 환경에 따라 달라지는 값이 아니라 프로토콜 이름이라 코드에 둔다
    /// (열쇠가 다르면 이유 없이 끊기므로 바꾸지 않는다 — 버전 차이는 접속 확인이 이유와 함께 알린다).
    /// </summary>
    public static class RoomProtocol
    {
        public const int Version = 2; // 2: 방의 시계에 끝낸 때 수, 때 끝내기·하루 마감 요청에 날과 때
        public const string ConnectionKey = "rootborn-room";
    }

    /// <summary>들어올 때 서로 맞춰 보는 버전 셋. 하나라도 다르면 들어오지 못한다(설계 6절, 8절 "서로 다른 버전끼리 같이 하기" 없음).</summary>
    public readonly struct RoomVersions : IEquatable<RoomVersions>
    {
        public RoomVersions(int protocol, string game, string data)
        {
            Protocol = protocol;
            Game = game ?? string.Empty;
            Data = data ?? string.Empty;
        }

        /// <summary>
        /// 이 빌드의 버전: 프로토콜, 게임 프로그램(그 어셈블리의 정보 버전 — Directory.Build.props가 1.0.0+커밋으로 찍는다), 데이터 지문
        /// (Rootborn.Data.DataFingerprint). 서버와 들어가는 쪽(Host)이 같은 어셈블리(Rootborn.Game)를 넘긴다.
        /// </summary>
        public static RoomVersions ForProgram(System.Reflection.Assembly program, string dataFingerprint)
        {
            if (program == null) throw new ArgumentNullException(nameof(program));
            string game = (Attribute.GetCustomAttribute(program, typeof(System.Reflection.AssemblyInformationalVersionAttribute)) as System.Reflection.AssemblyInformationalVersionAttribute)?.InformationalVersion
                          ?? program.GetName().Version?.ToString() ?? string.Empty;
            return new RoomVersions(RoomProtocol.Version, game, dataFingerprint);
        }

        public int Protocol { get; }

        /// <summary>게임 프로그램 버전(빌드가 정한다).</summary>
        public string Game { get; }

        /// <summary>게임 데이터의 지문(data/ 내용이 같으면 같다).</summary>
        public string Data { get; }

        public bool Equals(RoomVersions other) => Protocol == other.Protocol && Game == other.Game && Data == other.Data;
        public override bool Equals(object obj) => obj is RoomVersions other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Protocol, Game, Data);
        public override string ToString() => "protocol " + Protocol + ", game " + Game + ", data " + Data;
    }

    /// <summary>
    /// 방이 접속을 끊거나 받지 않을 때 보내는 이유 코드. 플레이어 화면에 보일 것이라 문구가 아니라 코드다
    /// (화면이 현지화 키로 바꾼다). 클라이언트는 <see cref="SessionClient.FarewellReason"/>으로 받는다.
    /// 한 번 쓴 코드는 뜻을 바꾸지 않는다. 정원·교체·시간 초과·종료는 세션이 이미 쓰는 코드를 그대로 쓴다.
    /// </summary>
    public static class RoomRefusal
    {
        public const string Full = SessionCapacity.ServerFullReason;
        public const string ProtocolMismatch = "protocol mismatch";
        public const string GameVersionMismatch = "game version mismatch";
        public const string DataVersionMismatch = "data version mismatch";
        public const string HandshakeTimeout = "handshake timeout";
        public const string InvalidIdentity = SessionServer.InvalidIdentityReason;
        public const string Replaced = SessionServer.ReplacedReason;
        public const string Timeout = SessionServer.TimeoutReason;
        public const string Shutdown = SessionServer.ShutdownReason;

        /// <summary>두 버전을 맞춰 본다. 같으면 null, 다르면 먼저 걸린 이유(프로토콜 → 게임 → 데이터 순).</summary>
        public static string Check(RoomVersions server, RoomVersions client)
        {
            if (server.Protocol != client.Protocol) return ProtocolMismatch;
            if (!string.Equals(server.Game, client.Game, StringComparison.Ordinal)) return GameVersionMismatch;
            if (!string.Equals(server.Data, client.Data, StringComparison.Ordinal)) return DataVersionMismatch;
            return null;
        }
    }
}
