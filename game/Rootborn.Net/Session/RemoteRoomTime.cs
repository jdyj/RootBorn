using System;
using System.Collections.Generic;
using Rootborn.Core.StudentLife;
using Rootborn.Core.Time;

namespace Rootborn.Net.Session
{
    /// <summary>
    /// 클라이언트 쪽 방의 시계: 서버의 시계(<see cref="SessionClient.RoomClock"/>)를 비추고, 쓴 시간 포인트와 하루 마감을 서버에 보낸다.
    /// <c>GameSession.Start(..., room: new RemoteRoomTime(client, playerId, calendar))</c>로 끼우면 그 게임 한 판이 방의 시계를 따른다.
    ///
    /// 개인 진행을 되돌리지 않는 방법(멀티플레이 설계 3.2절, data-driven "주고 나서 실패하면 되돌리는 흐름을 쓰지 않는다"):
    /// - 쓸 수 있는지는 서버가 마지막으로 보낸 시계로만 정한다. "이 때를 아직 안 끝냈다"는 서버의 알림이 곧 허락이다. 시계를 받기 전·접속이 끊긴 뒤에는 허락이 없다.
    /// - 쓰면 (날, 때, 포인트)를 적어 보내고, 서버의 시계가 그것을 셀 때까지 더 쓰지 못한다(보낸 요청 하나만 둔다).
    /// - 서버는 그 허락으로 보낸 요청을 거절하지 않는다: 그 사이 방이 대신 넘긴 때(자동 넘기기)는 이미 센 것으로 보고(Core RoomClock.SpendFrom),
    ///   다른 날의 요청은 받지 않는다. 그래서 개인 진행이 먼저 바뀌어도 방과 어긋나지 않고 되돌릴 일이 없다.
    /// - 방이 이 사람의 때를 대신 끝내면 개인 포인트가 뒤처진다. <see cref="Changed"/>에서 GameSession.SyncWithRoom을 불러 맞춘다(Host가 잇는다).
    /// 이 신원(playerId)만 다룬다. 다른 신원을 물으면 아무것도 허락하지 않는다.
    /// </summary>
    public sealed class RemoteRoomTime : IRoomTime, IDisposable
    {
        private readonly SessionClient _client;
        private readonly string _playerId;
        private readonly PartOfDayTimes _times;
        private int _spendDay, _spendTarget;
        private int _dayEndSent;
        private bool _disposed;

        public RemoteRoomTime(SessionClient client, string playerId, LifeCalendar calendar)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _playerId = playerId ?? string.Empty;
            _times = calendar?.PartTimes ?? PartOfDayTimes.Default;
            _client.RoomClockChanged += OnRoomClock;
        }

        /// <summary>서버에서 방의 시계를 받을 때마다(SessionClient.Update 안에서). 보낸 요청이 셌는지 정리한 뒤에 부른다.</summary>
        public event Action Changed;

        /// <summary>들어와 있고 서버의 시계를 받았다.</summary>
        public bool Known => _client.Status == ClientStatus.Joined && View.Known;

        /// <summary>보냈지만 서버의 시계가 아직 세지 않은 시간 포인트가 있다.</summary>
        public bool HasPendingSpend => _spendTarget > 0;

        public int Day => Known ? View.Day : 0;
        public int PartIndex => Known ? View.PartIndex : 0;
        public int PartsToday => Known ? View.PartsToday : 0;
        public bool IsDayOver => Known && View.IsDayOver;
        public int RepresentativeMinutes => View.RepresentativeMinutes(_times);

        public bool HasFinishedPart(string participantId) => IsMe(participantId) && (HasPendingSpend || (Mine(out var e) && e.FinishedPart));

        public int CompletedParts(string participantId) => IsMe(participantId) && Mine(out var e) ? e.CompletedParts : 0;

        public bool CanSpend(string participantId, int points)
        {
            if (points < 0 || !IsMe(participantId) || !Mine(out var e)) return false;
            if (points == 0) return true;
            return !HasPendingSpend && _dayEndSent == 0 && !e.DayEnded && e.CompletedParts == View.PartIndex && View.PartIndex + points <= View.PartsToday;
        }

        public bool Spend(string participantId, int points)
        {
            if (!CanSpend(participantId, points)) return false;
            if (points == 0) return true;
            _spendDay = View.Day;
            _spendTarget = View.PartIndex + points;
            _client.RequestFinishPart(View.Day, View.PartIndex, points);
            return true;
        }

        public bool EndDay(string participantId)
        {
            if (!IsMe(participantId) || !Mine(out var e) || e.DayEnded || _dayEndSent != 0) return false;
            _dayEndSent = View.Day;
            _client.RequestDayEndReady(View.Day);
            return true;
        }

        /// <summary>서버가 마지막으로 보낸 시계의 사람들. 신원을 모르는 번호는 뺀다. 자기가 보낸 요청은 끝낸 것으로 보인다.</summary>
        public void CopyPresent(List<RoomParticipantState> into)
        {
            if (into == null) return;
            into.Clear();
            if (!Known) return;
            foreach (var entry in View.Entries)
            {
                if (!_client.TryGetPlayerId(entry.NetId, out var id)) continue;
                bool me = entry.NetId == _client.NetId;
                into.Add(new RoomParticipantState(id, entry.CompletedParts, entry.FinishedPart || (me && HasPendingSpend), entry.DayEnded || (me && _dayEndSent != 0)));
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _client.RoomClockChanged -= OnRoomClock;
            _disposed = true;
        }

        private RoomClockView View => _client.RoomClock;

        private bool IsMe(string participantId) => string.Equals(participantId, _playerId, StringComparison.Ordinal);

        private bool Mine(out Messages.RoomClockEntry entry)
        {
            entry = default;
            return Known && View.TryGetEntry(_client.NetId, out entry);
        }

        // 보낸 요청은 서버의 시계가 그것을 셌을 때(끝낸 때 수가 닿음), 날이 바뀌었을 때, 방에서 빠졌을 때 정리한다.
        // "아무 시계나 받으면" 정리하지 않는다: 서버가 요청을 처리하기 전에 보낸 시계일 수 있다.
        private void OnRoomClock()
        {
            bool mine = Mine(out var e);
            if (HasPendingSpend && (!mine || View.Day != _spendDay || e.CompletedParts >= _spendTarget || e.DayEnded)) _spendTarget = 0;
            if (_dayEndSent != 0 && (!mine || View.Day != _dayEndSent || e.DayEnded)) _dayEndSent = 0;
            Changed?.Invoke();
        }
    }
}
