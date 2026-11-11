using System;
using System.Collections.Generic;
using Rootborn.Core.StudentLife;

namespace Rootborn.Core.Time
{
    /// <summary>방 설정 중 시계에 관한 것. 실시간이 아니라 부르는 쪽이 넘기는 "틱 수"로 센다.</summary>
    public sealed class RoomClockSettings
    {
        /// <param name="autoSkipAfterTicks">
        /// 누군가 지금 때를 끝내고 기다리기 시작한 뒤 이만큼 틱이 지나면 안 끝낸 사람을 넘긴 것으로 본다. 0이면 끔(기본).
        /// </param>
        public RoomClockSettings(int autoSkipAfterTicks = 0)
        {
            AutoSkipAfterTicks = Math.Max(0, autoSkipAfterTicks);
        }

        public static RoomClockSettings Default { get; } = new RoomClockSettings();

        public int AutoSkipAfterTicks { get; }
        public bool AutoSkipEnabled => AutoSkipAfterTicks > 0;
    }

    /// <summary>방에 있는 참가자 한 명의 지금 상태(화면·전송용 값).</summary>
    public readonly struct RoomParticipantState
    {
        public RoomParticipantState(string participantId, int completedParts, bool finishedPart, bool dayEnded)
        {
            ParticipantId = participantId;
            CompletedParts = completedParts;
            FinishedPart = finishedPart;
            DayEnded = dayEnded;
        }

        public string ParticipantId { get; }
        public int CompletedParts { get; }

        /// <summary>지금 때를 끝냈다(다른 사람을 기다린다).</summary>
        public bool FinishedPart { get; }

        public bool DayEnded { get; }
    }

    /// <summary>
    /// 방의 시계를 주는 쪽. 게임 한 판(GameSession)은 이것만 안다. 혼자 하기는 참가자 하나인 <see cref="RoomClock"/>이고,
    /// 같이 하기는 서버의 시계(또는 그것을 비추는 쪽)다. 참가자는 안정된 신원 문자열로 가리킨다.
    /// </summary>
    public interface IRoomTime
    {
        int Day { get; }
        int PartIndex { get; }
        int PartsToday { get; }
        bool IsDayOver { get; }

        /// <summary>지금 때의 대표 시각(분). 달력의 <see cref="PartOfDayTimes"/>에서.</summary>
        int RepresentativeMinutes { get; }

        bool HasFinishedPart(string participantId);

        /// <summary>그 참가자가 오늘 끝낸 때의 수(쓴 시간 포인트와 넘긴 때, 방이 대신 넘긴 때). 모르면 0.</summary>
        int CompletedParts(string participantId);

        /// <summary>지금 있는 참가자를 into에 채운다(비우고 채운다). 화면이 누가 아직 안 끝냈는지 보인다.</summary>
        void CopyPresent(List<RoomParticipantState> into);

        bool CanSpend(string participantId, int points);

        /// <returns>받아들였으면 true. 0 포인트는 아무것도 바꾸지 않고 true.</returns>
        bool Spend(string participantId, int points);

        /// <summary>그 참가자가 하루를 마감했다(남은 때도 모두 끝낸 것으로 센다).</summary>
        bool EndDay(string participantId);
    }

    /// <summary>
    /// 방의 시계(멀티플레이 설계 2.1절). (날짜, 몇 번째 때)와 참가자별 "이 때를 끝냈는지"를 갖는다.
    /// - 한 때는 시간 포인트 한 칸이다. 하루의 때 수는 달력(<see cref="LifeCalendar.GetBudget"/>)에서 얻는다.
    /// - 참가자가 그 때의 시간 포인트를 쓰거나(<see cref="Spend"/>) 넘기면(<see cref="Skip"/>) 끝낸 것이다. 끝낸 사람은 다음 때가
    ///   올 때까지 포인트를 더 쓰지 못한다. 포인트를 안 쓰는 일은 언제든 된다.
    /// - 지금 있는 모두가 끝내면 다음 때. 마지막 때가 끝나면 하루가 끝난 상태(<see cref="IsDayOver"/>)이고, 모두 하루를 마감하면
    ///   (<see cref="EndDay"/>) 다음 날 첫 때. 먼저 마감한 사람은 남은 때를 모두 끝낸 것으로 센다.
    /// - 나간 사람은 기다리지 않는다. 도중에 들어오면 지금 때부터 하고, 같은 날 다시 들어오면 그날 끝낸 것은 그대로다.
    /// - 아무도 없으면 멈춘다. 자동 넘기기는 방 설정(<see cref="RoomClockSettings"/>, 기본 끔)이고 때에만 쓴다(하루 마감은 각자 한다).
    /// 바꾸는 명령은 무엇이든 바뀌었으면 true를 돌려준다. 엔진·실제 시계에 닿지 않는다.
    /// </summary>
    public sealed class RoomClock : IRoomTime
    {
        private readonly LifeCalendar _calendar;
        private readonly RoomClockSettings _settings;
        private readonly Dictionary<string, Participant> _participants = new Dictionary<string, Participant>(8, StringComparer.Ordinal);
        private int _waitingTicks;

        public RoomClock(LifeCalendar calendar, RoomClockSettings settings = null, int day = 1, int partIndex = 0)
        {
            _calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
            _settings = settings ?? RoomClockSettings.Default;
            Day = Math.Max(1, day);
            PartIndex = Clamp(partIndex, 0, PartsToday);
        }

        public int Day { get; private set; }
        public int PartIndex { get; private set; }
        public int PartsToday => _calendar.GetBudget(Day);
        public bool IsDayOver => PartIndex >= PartsToday;
        public int RepresentativeMinutes => _calendar.PartTimes.MinutesFor(PartIndex, PartsToday);
        public RoomClockSettings Settings => _settings;

        public int PresentCount
        {
            get
            {
                int count = 0;
                foreach (var p in _participants.Values) if (p.Present) count++;
                return count;
            }
        }

        public bool IsPresent(string participantId) => Find(participantId, out var p) && p.Present;
        public bool IsDayEnded(string participantId) => Find(participantId, out var p) && p.DayEnded;
        public int CompletedParts(string participantId) => Find(participantId, out var p) ? p.Completed : 0;
        public bool HasFinishedPart(string participantId) => Find(participantId, out var p) && Finished(p);

        /// <summary>지금 있는 참가자를 into에 채운다(비우고 채운다).</summary>
        public void CopyPresent(List<RoomParticipantState> into)
        {
            if (into == null) return;
            into.Clear();
            foreach (var pair in _participants)
                if (pair.Value.Present) into.Add(new RoomParticipantState(pair.Key, pair.Value.Completed, Finished(pair.Value), pair.Value.DayEnded));
        }

        public bool Join(string participantId)
        {
            if (string.IsNullOrEmpty(participantId)) return false;
            if (_participants.TryGetValue(participantId, out var p))
            {
                if (p.Present) return false;
                p.Present = true;
                p.Completed = Math.Max(p.Completed, PartIndex);
            }
            else _participants[participantId] = new Participant { Present = true, Completed = PartIndex };

            Advance();
            return true;
        }

        public bool Leave(string participantId)
        {
            if (!Find(participantId, out var p) || !p.Present) return false;
            p.Present = false;
            // 아무도 없으면 멈춘다. 남은 사람이 있으면 나간 사람을 빼고 다시 본다.
            if (PresentCount > 0) Advance();
            return true;
        }

        public bool CanSpend(string participantId, int points)
        {
            if (points < 0 || !Find(participantId, out var p) || !p.Present) return false;
            if (points == 0) return true;
            return !p.DayEnded && p.Completed == PartIndex && PartIndex + points <= PartsToday;
        }

        public bool Spend(string participantId, int points)
        {
            if (!CanSpend(participantId, points)) return false;
            if (points == 0) return true;
            var p = _participants[participantId];
            p.Completed = PartIndex + points;
            Advance();
            return true;
        }

        /// <summary>"넘기기": 시간 포인트를 쓰지 않고 지금 때를 끝낸다.</summary>
        public bool Skip(string participantId) => Spend(participantId, 1);

        /// <summary>
        /// 멀리 있는 참가자가 (day, fromPart)에서 시간 포인트를 points만큼 썼다는 요청. 요청이 늦게 와서 그 사이 방이 대신 넘긴 때(자동 넘기기)는
        /// 이미 센 것으로 보고 나머지만 끝낸다. 같은 요청을 다시 받아도, 다른 날·앞으로 올 때의 요청이어도 바꾸지 않는다. 바뀌었으면 true.
        /// 그래서 참가자는 방이 "이 때를 아직 안 끝냈다"고 알린 동안 쓴 포인트를 되돌릴 일이 없다.
        /// </summary>
        public bool SpendFrom(string participantId, int day, int fromPart, int points)
        {
            if (points < 1 || fromPart < 0 || day != Day || !Find(participantId, out var p) || !p.Present || p.DayEnded) return false;
            int target = fromPart + points;
            if (p.Completed >= target || fromPart > p.Completed || p.Completed != PartIndex) return false;
            return Spend(participantId, target - p.Completed);
        }

        public bool EndDay(string participantId)
        {
            if (!Find(participantId, out var p) || !p.Present || p.DayEnded) return false;
            p.DayEnded = true;
            p.Completed = PartsToday;
            Advance();
            return true;
        }

        /// <summary>자동 넘기기용. 틱 수만큼 기다린 것으로 센다. 넘겼으면 true.</summary>
        public bool Tick(int ticks)
        {
            if (!_settings.AutoSkipEnabled || ticks <= 0 || IsDayOver) return false;
            bool anyWaiting = false, anyLeft = false;
            foreach (var p in _participants.Values)
            {
                if (!p.Present) continue;
                if (Finished(p)) anyWaiting = true;
                else anyLeft = true;
            }

            if (!anyWaiting || !anyLeft)
            {
                _waitingTicks = 0;
                return false;
            }

            _waitingTicks = _waitingTicks > int.MaxValue - ticks ? int.MaxValue : _waitingTicks + ticks;
            if (_waitingTicks < _settings.AutoSkipAfterTicks) return false;
            foreach (var p in _participants.Values)
                if (p.Present && !Finished(p)) p.Completed = PartIndex + 1;
            Advance();
            return true;
        }

        public RoomClockSaveData ToSaveData()
        {
            var list = new RoomClockParticipantSaveData[_participants.Count];
            int i = 0;
            foreach (var pair in _participants)
                list[i++] = new RoomClockParticipantSaveData { ParticipantId = pair.Key, CompletedParts = pair.Value.Completed, DayEnded = pair.Value.DayEnded };
            return new RoomClockSaveData { Version = RoomClockSaveData.KnownVersion, Day = Day, PartIndex = PartIndex, Participants = list };
        }

        /// <summary>날은 1 이상, 때와 끝낸 때는 그날의 범위로 자른다. 읽은 뒤에는 아무도 없다(들어오면 다시 센다). null은 첫날.</summary>
        public static RoomClock FromSaveData(RoomClockSaveData data, LifeCalendar calendar, RoomClockSettings settings = null)
        {
            if (data == null) return new RoomClock(calendar, settings);
            var clock = new RoomClock(calendar, settings, data.Day, data.PartIndex);
            if (data.Participants == null) return clock;
            foreach (var entry in data.Participants)
            {
                if (entry == null || string.IsNullOrEmpty(entry.ParticipantId)) continue;
                clock._participants[entry.ParticipantId] = new Participant
                {
                    Completed = Clamp(entry.CompletedParts, 0, clock.PartsToday),
                    DayEnded = entry.DayEnded,
                };
            }

            return clock;
        }

        /// <summary>
        /// 시계가 없던 세이브(혼자 하던 판)에서 혼자인 방을 만든다: 날짜는 개인의 날, 때는 오늘 쓴 시간 포인트.
        /// 이미 마감한 날(결과를 기다리는 중)이면 혼자인 방은 그때 다음 날로 넘어갔으므로 다음 날 첫 때다. 그 참가자가 들어와 있다.
        /// </summary>
        public static RoomClock FromProgress(StudentLifeProgress progress, string participantId, RoomClockSettings settings = null)
        {
            if (progress == null) throw new ArgumentNullException(nameof(progress));
            var calendar = progress.Calendar ?? new LifeCalendar(0, 0);
            bool ended = progress.DayState != StudentDayState.InProgress;
            var clock = ended
                ? new RoomClock(calendar, settings, progress.CurrentDay + 1)
                : new RoomClock(calendar, settings, progress.CurrentDay, progress.TimePointsSpentToday);
            clock.Join(participantId);
            return clock;
        }

        private void Advance()
        {
            if (PresentCount == 0) return;
            while (PartIndex < PartsToday && AllPresent(Finished))
            {
                PartIndex++;
                _waitingTicks = 0;
            }

            if (AllPresent(p => p.DayEnded)) NextDay();
        }

        private void NextDay()
        {
            Day = Day >= int.MaxValue ? Day : Day + 1;
            PartIndex = 0;
            _waitingTicks = 0;
            var gone = new List<string>(2);
            foreach (var pair in _participants)
            {
                if (!pair.Value.Present) gone.Add(pair.Key);
                pair.Value.Completed = 0;
                pair.Value.DayEnded = false;
            }

            // 방에 없던 사람의 지난 날 기록은 버린다(다음에 들어오면 그날 첫 때부터).
            foreach (var id in gone) _participants.Remove(id);
        }

        private bool AllPresent(Func<Participant, bool> test)
        {
            bool any = false;
            foreach (var p in _participants.Values)
            {
                if (!p.Present) continue;
                any = true;
                if (!test(p)) return false;
            }

            return any;
        }

        private bool Finished(Participant p) => p.DayEnded || p.Completed > PartIndex;

        private bool Find(string id, out Participant participant)
        {
            participant = null;
            return !string.IsNullOrEmpty(id) && _participants.TryGetValue(id, out participant);
        }

        private static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;

        private sealed class Participant
        {
            public bool Present;
            public int Completed;
            public bool DayEnded;
        }
    }

    /// <summary>
    /// 개인 시간 포인트를 방의 시계에 잇는다: 쓸 수 있는지는 방에 묻고, 쓴 포인트는 방에 알린다.
    /// 방이 그 참가자의 때를 대신 끝냈으면(자동 넘기기) <see cref="CatchUp"/>으로 개인 포인트를 방에 맞춘다.
    /// </summary>
    public sealed class RoomTimePointGate : ITimePointGate
    {
        private readonly IRoomTime _room;
        private readonly string _participantId;
        private bool _catchingUp;

        public RoomTimePointGate(IRoomTime room, string participantId)
        {
            _room = room ?? throw new ArgumentNullException(nameof(room));
            _participantId = participantId ?? string.Empty;
        }

        public bool CanSpend(int points) => _catchingUp || _room.CanSpend(_participantId, points);

        public void Spent(int points)
        {
            if (!_catchingUp) _room.Spend(_participantId, points);
        }

        /// <summary>
        /// 이 문을 쓰는 진행(progress)의 오늘 쓴 시간 포인트가 방이 센 끝낸 때보다 적으면 그만큼 쓴 것으로 맞춘다. 방에는 다시 알리지 않는다.
        /// 방과 같은 날이고 하루가 진행 중일 때만 한다. 맞춘 포인트 수(없으면 0). 앞선 쪽(개인이 방보다 많이 씀)은 건드리지 않는다.
        /// </summary>
        public int CatchUp(StudentLifeProgress progress)
        {
            if (progress == null || progress.DayState != StudentDayState.InProgress || progress.CurrentDay != _room.Day) return 0;
            int behind = Math.Min(_room.CompletedParts(_participantId), progress.DailyTimePointBudget) - progress.TimePointsSpentToday;
            if (behind <= 0) return 0;
            _catchingUp = true;
            try
            {
                return progress.TrySpendTimePoints(behind) ? behind : 0;
            }
            finally
            {
                _catchingUp = false;
            }
        }
    }
}
