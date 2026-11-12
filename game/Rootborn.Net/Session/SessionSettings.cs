using System;

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

        /// <summary>
        /// 방의 시계를 보내는 간격(실제 초). 바뀔 때(잠, 들어옴·나감, 다음 날)는 곧바로 보내고, 그 사이에는 이 간격으로만 맞춘다
        /// (받는 쪽이 같은 속도로 메운다). 하루 10분이면 5초는 게임 12분이다.
        /// </summary>
        public double ClockSyncSeconds { get; set; } = 5.0;

        /// <summary>
        /// 서버 틱 한 번에 넘기는 실제 초의 상한. 서버가 멈췄다 돌아와도(밀린 틱을 버림) 시계가 한꺼번에 뛰지 않게.
        /// 시간 초과도 같다: Update 사이가 이보다 길면 서버가 멈춘 것이라 넘친 만큼은 상대가 조용했던 시간으로 세지 않는다.
        /// </summary>
        public double MaxClockStepSeconds { get; set; } = 1.0;

        internal void Validate()
        {
            if (ClockSyncSeconds <= 0 || MaxClockStepSeconds <= 0) throw new ArgumentException("시계 맞춤 간격과 한 번에 넘기는 초는 양수여야 한다");
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

        /// <summary>
        /// Update 사이가 이보다 길면 이 프로그램의 루프가 멈춘 것으로 보고(창 끌기, 디버거, 느린 디스크) 넘친 만큼은 서버가 조용했던 시간으로
        /// 세지 않는다 — 멈춘 뒤 첫 Update에서 받은 것을 처리하기도 전에 스스로 시간 초과로 끊지 않게. 서버의 MaxClockStepSeconds와 같은 생각.
        /// </summary>
        public double MaxUpdateGapSeconds { get; set; } = 1.0;

        internal void Validate()
        {
            if (MaxUpdateGapSeconds <= 0) throw new ArgumentException("멈춤으로 보는 갱신 간격은 양수여야 한다");
            if (PoseSendsPerSecond <= 0 || HeartbeatSeconds <= 0 || TimeoutSeconds <= 0) throw new ArgumentException("횟수·간격·시간 초과는 양수여야 한다");
            if (InterpolationDelaySeconds < 0) throw new ArgumentException("사이 메우기 지연은 음수일 수 없다");
            if (MaxMessageBytes < 256) throw new ArgumentException("최대 메시지 크기가 너무 작다");
        }
    }
}
