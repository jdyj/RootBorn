using System;
using System.Collections.Generic;
using Rootborn.Core.Time;

namespace Rootborn.Net.Session
{
    /// <summary>
    /// 클라이언트 쪽 방의 시계(흐르는 시간 4단계): 서버의 시계(<see cref="SessionClient.RoomTime"/>)를 비추고, 잠을 서버에 보낸다.
    /// <c>GameSession.Start(..., room: new RemoteRoomTime(client, playerId, rules))</c>로 끼우면 그 게임 한 판의 시계가 방의 시각을 따른다.
    ///
    /// 시각을 비추는 법: 서버는 매 프레임 보내지 않는다. 받은 시각에 그 뒤로 흐른 클라이언트 시간(<see cref="SessionClient.Now"/>)을 같은 속도로
    /// 더해 보인다(하루 길이는 시간 규칙에서). 새 시각을 받았을 때 보이던 시각이 조금(<see cref="ResyncMinutes"/> 이하) 앞서 있으면 뒤로 돌리지 않고
    /// 따라잡을 때까지 그대로 두고, 그보다 어긋나면 받은 시각으로 맞춘다. 같은 날 안에서 날 넘김 시각을 넘겨 보이지 않는다(다음 날은 서버가 알린다).
    ///
    /// 잠: 개인 진행(GameSession)이 먼저 하루를 끝낸 뒤 그 날의 잠을 보낸다. 서버는 다른 날의 잠은 받지 않는다. 보낸 잠은 서버의 시계가 그것을 셀 때까지
    /// 자는 중으로 보인다(되돌리지 않는다). 이 신원(playerId — 끼운 게임 한 판의 플레이어 id)만 다룬다.
    /// </summary>
    public sealed class RemoteRoomTime : IRoomTime, IDisposable
    {
        /// <summary>새로 받은 시각보다 보이던 시각이 이만큼(게임 분) 이하로 앞서 있으면 뒤로 돌리지 않는다.</summary>
        public const int ResyncMinutes = 5;

        private readonly SessionClient _client;
        private readonly string _playerId;
        private readonly TimeRules _rules;
        private readonly double _secondsPerMinute;
        private int _sleepSent;
        private int _shownDay = -1, _shownMinutes;
        private bool _disposed;

        /// <param name="rules">시간 규칙(하루 길이, 날 넘김 시각). 서버와 같은 데이터. 없으면 사이를 메우지 않는다(받은 시각만 보인다).</param>
        public RemoteRoomTime(SessionClient client, string playerId, TimeRules rules)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _playerId = playerId ?? string.Empty;
            _rules = rules;
            _secondsPerMinute = rules == null ? double.PositiveInfinity : rules.RealSecondsPerGameDay / TimeRules.MinutesPerDay;
            _client.RoomTimeChanged += OnRoomTime;
        }

        /// <summary>서버에서 방의 시계를 받을 때마다(SessionClient.Update 안에서). 보낸 잠이 셌는지 정리한 뒤에 부른다.</summary>
        public event Action Changed;

        /// <summary>들어와 있고 서버의 시계를 받았다.</summary>
        public bool Known => _client.Status == ClientStatus.Joined && View.Known;

        public int Day => Known ? View.Day : 0;

        public int Minutes
        {
            get
            {
                if (!Known) return 0;
                int estimate = Estimate();
                if (_shownDay == View.Day && estimate < _shownMinutes && _shownMinutes - estimate <= ResyncMinutes) return _shownMinutes;
                _shownDay = View.Day;
                _shownMinutes = estimate;
                return estimate;
            }
        }

        /// <summary>보냈지만 서버의 시계가 아직 세지 않은 잠이 있다.</summary>
        public bool HasPendingSleep => _sleepSent != 0;

        public bool IsAsleep(string participantId) => IsMe(participantId) && Known && (_sleepSent == View.Day || View.IsAsleep(_client.NetId));

        public bool SleepOn(string participantId, int day)
        {
            if (!IsMe(participantId) || !Known || day != View.Day || IsAsleep(participantId)) return false;
            _sleepSent = day;
            _client.RequestSleep(day);
            return true;
        }

        /// <summary>서버가 마지막으로 보낸 시계의 사람들. 신원을 모르는 번호는 뺀다. 자기가 보낸 잠은 자는 중으로 보인다.</summary>
        public void CopyPresent(List<RoomParticipantState> into)
        {
            if (into == null) return;
            into.Clear();
            if (!Known) return;
            foreach (var entry in View.Entries)
            {
                if (!_client.TryGetPlayerId(entry.NetId, out var id)) continue;
                bool me = entry.NetId == _client.NetId;
                // 자기 자신은 이 시계를 끼운 게임 한 판의 신원으로 보인다(방의 신원과 세이브의 플레이어 id가 다를 수 있다 — 손님 저장이 없는 방).
                into.Add(new RoomParticipantState(me ? _playerId : id, entry.Asleep || (me && _sleepSent == View.Day)));
            }
        }

        /// <summary>
        /// 방에 있는 그 참가자의 이름(화면 표시용). <see cref="CopyPresent"/>가 준 참가자 id로 묻는다 — 자기 자신은 이 시계를 끼운 게임 한 판의 신원이다.
        /// 모르거나 이름이 없으면 빈 문자열(화면이 기본 말을 쓴다).
        /// </summary>
        public string NameOf(string participantId)
        {
            if (string.IsNullOrEmpty(participantId)) return string.Empty;
            if (IsMe(participantId) && _client.TryGetPlayerId(_client.NetId, out var roomId)) participantId = roomId;
            return _client.TryGetPlayerName(participantId, out var name) ? name ?? string.Empty : string.Empty;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _client.RoomTimeChanged -= OnRoomTime;
            _disposed = true;
        }

        private RoomTimeView View => _client.RoomTime;

        private bool IsMe(string participantId) => string.Equals(participantId, _playerId, StringComparison.Ordinal);

        // 받은 시각 + 그 뒤로 흐른 클라이언트 시간. 같은 날 안에서 날 넘김 시각 앞까지만.
        private int Estimate()
        {
            double elapsed = Math.Max(0d, _client.Now - View.ReceivedAt);
            long minutes = View.Minutes + (long)Math.Floor(elapsed / _secondsPerMinute + 1e-9);
            if (_rules != null && minutes > _rules.DayEndMinutes - 1) minutes = Math.Max(View.Minutes, _rules.DayEndMinutes - 1);
            return minutes > int.MaxValue ? int.MaxValue : (int)minutes;
        }

        // 보낸 잠은 서버의 시계가 그것을 셌을 때, 날이 바뀌었을 때, 방에서 빠졌을 때 정리한다.
        private void OnRoomTime()
        {
            bool mine = View.TryGetEntry(_client.NetId, out var e);
            if (_sleepSent != 0 && (!mine || View.Day != _sleepSent || e.Asleep)) _sleepSent = 0;
            Changed?.Invoke();
        }
    }
}
