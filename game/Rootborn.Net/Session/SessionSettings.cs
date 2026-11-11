using System;
using Rootborn.Core.Time;

namespace Rootborn.Net.Session
{
    /// <summary>서버 설정. 값은 실행 인자·설정에서 넘긴다. 기본값은 Unity 판(-maxPlayers 4)과 이 판의 판단이다.</summary>
    public sealed class SessionServerSettings
    {
        /// <summary>서버 자신이 플레이어면(호스트) 그 클라이언트도 센다.</summary>
        public int MaxPlayers { get; set; } = 4;

        /// <summary>아무것도 보내지 않은 동안 살아 있다는 표시를 보내는 간격.</summary>
        public double HeartbeatSeconds { get; set; } = 1.0;

        /// <summary>이만큼 아무것도 받지 못하면 끊는다(들어오기 전 연결 포함).</summary>
        public double TimeoutSeconds { get; set; } = 10.0;

        /// <summary>메시지 하나의 최대 바이트(머리 포함). 넘는 것은 보내지도 받지도 않는다.</summary>
        public int MaxMessageBytes { get; set; } = 4096;

        /// <summary>방의 시계 설정(자동 넘기기, 기본 끔). 틱은 서버 Update 한 번이다.</summary>
        public RoomClockSettings Clock { get; set; } = RoomClockSettings.Default;

        internal void Validate()
        {
            if (Clock == null) throw new ArgumentException("방의 시계 설정이 없다");
            if (HeartbeatSeconds <= 0 || TimeoutSeconds <= 0) throw new ArgumentException("간격과 시간 초과는 양수여야 한다");
            if (MaxMessageBytes < 256) throw new ArgumentException("최대 메시지 크기가 너무 작다");
        }
    }

    /// <summary>클라이언트 설정.</summary>
    public sealed class SessionClientSettings
    {
        /// <summary>자기 위치를 보내는 초당 횟수(맵이 바뀌면 그때 바로 한 번 더 신뢰로 보낸다).</summary>
        public double PoseSendsPerSecond { get; set; } = 10.0;

        public double HeartbeatSeconds { get; set; } = 1.0;
        public double TimeoutSeconds { get; set; } = 10.0;

        /// <summary>남의 몸을 이만큼 늦게 그려 두 점 사이를 메운다. 보내는 간격보다 커야 끊기지 않는다.</summary>
        public double InterpolationDelaySeconds { get; set; } = 0.15;

        public int MaxMessageBytes { get; set; } = 4096;

        internal void Validate()
        {
            if (PoseSendsPerSecond <= 0 || HeartbeatSeconds <= 0 || TimeoutSeconds <= 0) throw new ArgumentException("횟수·간격·시간 초과는 양수여야 한다");
            if (InterpolationDelaySeconds < 0) throw new ArgumentException("사이 메우기 지연은 음수일 수 없다");
            if (MaxMessageBytes < 256) throw new ArgumentException("최대 메시지 크기가 너무 작다");
        }
    }
}
