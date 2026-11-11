using System;
using System.Collections.Generic;
using Rootborn.Core.StudentLife;

namespace Rootborn.Core.Time
{
    /// <summary>방에 있는 참가자 한 명의 지금 상태(화면·전송용 값).</summary>
    public readonly struct RoomParticipantState
    {
        public RoomParticipantState(string participantId, bool asleep)
        {
            ParticipantId = participantId;
            Asleep = asleep;
        }

        public string ParticipantId { get; }

        /// <summary>오늘 잠들었다(다음 날 하루 시작까지 자는 중).</summary>
        public bool Asleep { get; }
    }

    /// <summary>
    /// 방의 시계를 주는 쪽. 게임 한 판(GameSession)은 이것만 안다. 혼자 하기는 참가자 하나인 <see cref="RoomClock"/>이고(개인 시계가 흐른다),
    /// 같이 하기는 서버의 시계를 비추는 쪽이다(Rootborn.Net RemoteRoomTime). 참가자는 안정된 신원 문자열로 가리킨다.
    /// </summary>
    public interface IRoomTime
    {
        /// <summary>시각을 알고 있다(서버를 비추는 쪽은 시계를 받은 뒤). 모르면 개인 시계는 방을 따라가지 않는다.</summary>
        bool Known { get; }

        int Day { get; }

        /// <summary>그날 0시부터 센 분(하루 시작 ~ 날 넘김 시각 앞). 서버를 비추는 쪽은 받은 시각 사이를 같은 속도로 메운 값이다.</summary>
        int Minutes { get; }

        bool IsAsleep(string participantId);

        /// <summary>지금 있는 참가자를 into에 채운다(비우고 채운다). 화면이 누가 아직 깨어 있는지 보인다.</summary>
        void CopyPresent(List<RoomParticipantState> into);

        /// <summary>그 참가자가 그 날(day) 잠들었다. 방의 날이 아니면 받지 않는다(지난 날의 잠이 새 날을 재우지 않게). 받아들였으면(또는 보냈으면) true.</summary>
        bool SleepOn(string participantId, int day);
    }

    /// <summary>
    /// 방의 시계(흐르는 시간 설계 3.1·3.3절). (날짜, 분)과 참가자별 "자는 중"을 갖는다.
    /// - 시각은 <see cref="Advance"/>로 받은 분만큼 흐른다(서버가 틱마다 실제 초를 분으로 바꿔 넘긴다). 방에 아무도 없으면 흐르지 않는다.
    /// - 날 넘김 시각(다음 날 하루 시작, <see cref="TimeRules.DayEndMinutes"/>)이 되면 모두에게 다음 날 하루 시작이 온다. 자던 사람은 일어난다.
    /// - 지금 있는 모두가 잠들면(<see cref="Sleep"/>) 곧바로 다음 날 하루 시작. 깨어 있는 사람이 있으면 시계는 그대로 흐른다.
    /// - 나간 사람은 기다리지 않는다(남은 모두가 자는 중이면 넘어간다). 같은 날 다시 들어오면 자는 중 그대로, 다음 날이면 잊는다.
    /// 바꾸는 명령은 바뀌었으면 true(또는 움직인 분)를 돌려준다. 엔진·실제 시계에 닿지 않는다.
    /// </summary>
    public sealed class RoomClock : IRoomTime
    {
        private readonly TimeRules _rules;
        private readonly Dictionary<string, Participant> _participants = new Dictionary<string, Participant>(8, StringComparer.Ordinal);

        /// <param name="minutes">그날 0시부터 센 분. 음수면 하루 시작. 하루 시작~날 넘김 시각 앞으로 자른다.</param>
        public RoomClock(TimeRules rules, int day = 1, int minutes = -1)
        {
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            Day = Math.Max(1, day);
            Minutes = minutes < 0 ? rules.DayStartMinutes : Clamp(minutes, rules.DayStartMinutes, rules.DayEndMinutes - 1);
        }

        public bool Known => true;
        public int Day { get; private set; }
        public int Minutes { get; private set; }
        public TimeRules Rules => _rules;

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
        public bool IsAsleep(string participantId) => Find(participantId, out var p) && p.Asleep;

        public void CopyPresent(List<RoomParticipantState> into)
        {
            if (into == null) return;
            into.Clear();
            foreach (var pair in _participants)
                if (pair.Value.Present) into.Add(new RoomParticipantState(pair.Key, pair.Value.Asleep));
        }

        public bool Join(string participantId)
        {
            if (string.IsNullOrEmpty(participantId)) return false;
            if (_participants.TryGetValue(participantId, out var p))
            {
                if (p.Present) return false;
                p.Present = true;
            }
            else _participants[participantId] = new Participant { Present = true };

            return true;
        }

        public bool Leave(string participantId)
        {
            if (!Find(participantId, out var p) || !p.Present) return false;
            p.Present = false;
            if (EveryonePresentAsleep()) NextDay();
            return true;
        }

        public bool Sleep(string participantId)
        {
            if (!Find(participantId, out var p) || !p.Present || p.Asleep) return false;
            p.Asleep = true;
            if (EveryonePresentAsleep()) NextDay();
            return true;
        }

        /// <summary>그 날(day)에 잠들었다는 요청. 다른 날의 잠은 받지 않는다(지난 날의 잠이 새 날을 재우지 않게).</summary>
        public bool SleepOn(string participantId, int day) => day == Day && Sleep(participantId);

        /// <summary>
        /// 시계를 minutes만큼 보낸다. 방에 아무도 없으면 가지 않는다. 날 넘김 시각에서 다음 날 하루 시작으로 넘어가고 남은 분은 그 날에 이어 간다.
        /// 움직인 분을 돌려준다.
        /// </summary>
        public int Advance(int minutes)
        {
            if (minutes <= 0 || PresentCount == 0) return 0;
            int moved = 0;
            while (minutes > 0)
            {
                int step = Math.Min(minutes, _rules.DayEndMinutes - Minutes);
                Minutes += step;
                moved += step;
                minutes -= step;
                if (Minutes >= _rules.DayEndMinutes) NextDay();
            }

            return moved;
        }

        public RoomClockSaveData ToSaveData()
        {
            var list = new RoomClockParticipantSaveData[_participants.Count];
            int i = 0;
            foreach (var pair in _participants)
                list[i++] = new RoomClockParticipantSaveData { ParticipantId = pair.Key, Asleep = pair.Value.Asleep };
            return new RoomClockSaveData { Version = RoomClockSaveData.KnownVersion, Day = Day, Minutes = Minutes, Participants = list };
        }

        /// <summary>
        /// 날은 1 이상, 분은 하루 시작~날 넘김 시각 앞으로 자른다. 읽은 뒤에는 아무도 없다(들어오면 다시 센다). null은 첫날.
        /// 형식 1(때 단위 — 몇 번째 때, 끝낸 때, 하루 마감)은 그 날의 하루 시작으로 읽고 마감은 잠으로 보지 않는다
        /// (마감했던 사람은 들어온 뒤 자기 게임 한 판이 잠을 다시 알린다).
        /// </summary>
        public static RoomClock FromSaveData(RoomClockSaveData data, TimeRules rules)
        {
            if (data == null) return new RoomClock(rules);
            bool parts = data.Version < 2;
            var clock = new RoomClock(rules, data.Day, parts ? -1 : Math.Max(rules.DayStartMinutes, data.Minutes));
            if (data.Participants == null) return clock;
            foreach (var entry in data.Participants)
            {
                if (entry == null || string.IsNullOrEmpty(entry.ParticipantId)) continue;
                clock._participants[entry.ParticipantId] = new Participant { Asleep = !parts && entry.Asleep };
            }

            return clock;
        }

        /// <summary>
        /// 혼자인 방(개인 진행에서 만든다, 따로 저장하지 않는다): 날짜는 개인의 날, 시각은 개인 시각. 이미 마감한 날(결과를 기다리는 중)이면
        /// 혼자인 방은 그때 다음 날로 넘어갔으므로 다음 날 하루 시작이다. 그 참가자가 들어와 있다.
        /// </summary>
        public static RoomClock FromProgress(StudentLifeProgress progress, string participantId, TimeRules rules)
        {
            if (progress == null) throw new ArgumentNullException(nameof(progress));
            bool ended = progress.DayState != StudentDayState.InProgress;
            var clock = ended ? new RoomClock(rules, progress.CurrentDay + 1) : new RoomClock(rules, progress.CurrentDay, progress.TimeMinutes);
            clock.Join(participantId);
            return clock;
        }

        private void NextDay()
        {
            Day = Day >= int.MaxValue ? Day : Day + 1;
            Minutes = _rules.DayStartMinutes;
            var gone = new List<string>(2);
            foreach (var pair in _participants)
            {
                if (!pair.Value.Present) gone.Add(pair.Key);
                pair.Value.Asleep = false;
            }

            // 방에 없던 사람의 지난 날 기록은 버린다.
            foreach (var id in gone) _participants.Remove(id);
        }

        private bool EveryonePresentAsleep()
        {
            bool any = false;
            foreach (var p in _participants.Values)
            {
                if (!p.Present) continue;
                any = true;
                if (!p.Asleep) return false;
            }

            return any;
        }

        private bool Find(string id, out Participant participant)
        {
            participant = null;
            return !string.IsNullOrEmpty(id) && _participants.TryGetValue(id, out participant);
        }

        private static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;

        private sealed class Participant
        {
            public bool Present;
            public bool Asleep;
        }
    }
}
