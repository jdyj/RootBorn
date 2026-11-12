using System;
using System.Collections.Generic;
using Rootborn.Core.StudentLife;
using Rootborn.Core.Time;

namespace Rootborn.Game
{
    /// <summary>
    /// 방의 시계(흐르는 시간 설계 3.1·3.3절, 6절 4단계). GameSession은 시계를 "주는 쪽"(<see cref="IRoomTime"/>)으로 받는다.
    /// - 주지 않으면 혼자인 방이다: 읽은 개인 진행에서 만든다(<see cref="RoomClock.FromProgress"/>, 따로 저장하지 않는다). 개인 시계가 흐르고
    ///   활동은 시계를 건너뛴다. 방은 잠으로 날을 넘기는 데에만 쓴다.
    /// - 같이 하는 방: 시각은 방의 것 하나다. 개인 시계는 방의 시각을 앞으로만 따라간다(<see cref="FollowRoom"/>, 프레임마다 AdvanceTime이 부른다).
    ///   활동은 시계를 건너뛰지 않고 끝나는 시각까지 "하는 중"이다(Core ActivityClockMode.StayBusy).
    ///   - 방과 같은 날을 지내 오다 방이 다음 날이 되면(07:00) 깨어 있던 사람은 자지 않은 날로 넘긴다(회복 없음, 밤의 체력 감소).
    ///   - 방에 없던 사이 날이 지났으면(들어오기 전·나갔다 들어옴) 그 사이는 쉰 날이다(멀티플레이 설계 10절 7번, Core RestUntil).
    ///   - 낮 도중에 들어오면 방의 시각에서 시작한다. 방에 없던 동안의 밤 시간은 거슬러 깎지 않는다(Core JumpClockTo).
    ///   - 잠: 개인 하루를 끝내고 방에 "그 날 잠들었다"를 알린다. 모두 잠들거나 07:00이 되면 방이 다음 날이 되고, 그때 다음 날을 열 수 있다.
    /// 방이 바뀌면(다른 사람이 잠듦, 다음 날) 방을 잇는 쪽(Host)이 <see cref="SyncWithRoom"/>을 부른다.
    /// </summary>
    public sealed partial class GameSession
    {
        private static readonly string[] RoomTopics = { GameChangeTopics.Student, GameChangeTopics.Room };
        private readonly List<RoomParticipantState> _roomScratch = new List<RoomParticipantState>(8);

        // 개인 시계가 방의 시각을 따라 지내 온 날(-1이면 아직 따라가지 않았다: 들어온 직후). 이 날에 방이 다음 날이 되면 깨어 있는 채 넘긴 것이다.
        private int _followingDay = -1;

        // 자지 않고 날 넘김 시각이 되어 하루를 끝냈지만 방이 아직 다음 날을 알리지 않았다. 방이 다음 날이 되면 곧바로 연다.
        private bool _rollOverPending;

        /// <summary>이 판이 도는 방의 시계.</summary>
        public IRoomTime Room { get; private set; }

        /// <summary>
        /// HUD 시계·낮밤 빛이 보일 시각(0~1439분). 혼자면 흐르는 개인 시계, 같이 하는 방이면 방의 시각(자는 중에도 흐르는 것이 보인다).
        /// 개인 시계는 방의 시각을 따라가므로 장소 상태·NPC 일정과 같은 시각이다.
        /// </summary>
        public int RoomShownMinutes => !IsSoloRoom && Room.Known ? Core.Time.TimeRules.ClockMinutes(Room.Minutes) : ClockMinutes;

        /// <summary>
        /// 방에 있는 다른 사람 중 아직 깨어 있는 사람의 신원을 into에 채운다(비우고 채운다). 혼자면 비어 있다. 자는 중 화면이 보인다.
        /// </summary>
        public void RoomAwakeOthers(List<string> into)
        {
            if (into == null) return;
            into.Clear();
            Room.CopyPresent(_roomScratch);
            foreach (var p in _roomScratch)
                if (!p.Asleep && !string.Equals(p.ParticipantId, PlayerId, StringComparison.Ordinal)) into.Add(p.ParticipantId);
        }

        private IRoomNames _roomNames;

        /// <summary>
        /// 방에 있는 그 참가자(<see cref="RoomAwakeOthers"/>가 준 id)의 이름(화면 표시용). 혼자이거나 모르거나 이름이 없으면 빈 문자열 —
        /// 화면이 현지화된 기본 말을 쓴다. 신원 판단에는 쓰지 않는다.
        /// </summary>
        public string RoomNameOf(string participantId) => _roomNames?.NameOf(participantId) ?? string.Empty;

        /// <summary>자는 중: 잠들어 하루를 끝냈고 방이 아직 다음 날이 아니다(같이 하는 방에서 다른 사람이 깨어 있다). 자면 07:00까지 깨어날 수 없다.</summary>
        public bool IsSleepingInRoom => Student.DayState == StudentDayState.ResultReady && Student.LastNight.HasSlept && !CanStartNextDay;

        /// <summary>
        /// 방이 바뀐 뒤 부른다(다른 사람이 잠듦·나감, 다음 날, 시각 맞춤). 개인 시계를 방에 맞추고, 방이 모르는 내 잠을 다시 알리고, 방이 바뀌었다고 알린다.
        /// 맞춘 것이 있으면 true.
        /// </summary>
        public bool SyncWithRoom()
        {
            bool changed = FollowRoom(out _);
            ResendToRoom();
            if (!changed) Notify(null, GameChangeTopics.Room);
            return changed;
        }

        /// <summary>
        /// 같이 하는 방: 개인 시계를 방의 시각에 맞춘다(앞으로만). 혼자인 방·방의 시각을 모를 때는 아무것도 하지 않는다. 저장·알림까지 한다. 바뀌었으면 true.
        /// </summary>
        private bool FollowRoom(out int moved)
        {
            moved = 0;
            if (IsSoloRoom || !Room.Known) return false;
            int dayBefore = Student.CurrentDay, hourBefore = Student.TimeMinutes / 60, energyBefore = Student.Energy;
            bool exhaustedBefore = Student.IsExhausted;

            // 자지 않고 하루를 끝낸 뒤 방이 다음 날이 되기를 기다리던 중이면 연다.
            if (_rollOverPending && CanStartNextDay)
            {
                _rollOverPending = false;
                StartNextDay();
            }

            if (Student.DayState == StudentDayState.InProgress && Room.Day > Student.CurrentDay)
            {
                if (_followingDay == Student.CurrentDay && Student.TimeRules != null)
                {
                    // 이 날을 방과 지내 왔다: 07:00이 되어 방이 다음 날이 됐다. 깨어 있던 채로 넘긴다(회복 없음, 밤의 체력 감소).
                    moved += Student.AdvanceClock(Student.MinutesUntilDayEnd);
                    if (Student.HasDayRunOut) RollOverAwake();
                }

                if (Room.Day > Student.CurrentDay) CatchUpRoomDay(); // 방에 없던 사이의 날은 쉰 날
            }

            if (Student.DayState == StudentDayState.InProgress && Room.Day == Student.CurrentDay)
            {
                int target = Room.Minutes;
                if (target > Student.TimeMinutes)
                    moved += _followingDay == Student.CurrentDay ? Student.AdvanceClock(target - Student.TimeMinutes) : Student.JumpClockTo(target);
                _followingDay = Student.CurrentDay;
            }

            if (Student.CurrentDay != dayBefore) return true; // 날 넘김·다음 날 시작이 저장하고 알렸다
            bool vitals = Student.Energy != energyBefore || Student.IsExhausted != exhaustedBefore;
            if (vitals || Student.TimeMinutes / 60 != hourBefore) Save();
            if (vitals) Notify(null, GameChangeTopics.Student, GameChangeTopics.Time);
            else if (moved > 0) Notify(null, GameChangeTopics.Time);
            return moved > 0 || vitals;
        }

        /// <summary>
        /// 방이 이 사람의 날보다 앞서 있고 이 사람의 하루가 진행 중이면(방에 없던 사이 날이 지났다) 그 사이를 쉰 날로 넘겨 방의 날에서 시작한다
        /// (멀티플레이 설계 10절 7번, Core StudentLifeProgress.RestUntil). 오늘을 마감한 뒤라면 다음 날 시작(<see cref="StartNextDay"/>)이 한다.
        /// </summary>
        private bool CatchUpRoomDay()
        {
            if (IsSoloRoom || !Room.Known || Room.Day <= Student.CurrentDay || Student.DayState != StudentDayState.InProgress) return false;
            if (Student.RestUntil(Room.Day, _dayEndRules) == 0) return false;
            BeginNewDay();
            SaveNewDay();
            return true;
        }

        /// <summary>오늘 잠들어 하루를 끝냈는데 방이 모르면(보낸 뒤 끊겨 방이 세지 못했다) 그 날의 잠을 다시 알린다. 방은 날로 받으므로 두 번 세지 않는다.</summary>
        private bool ResendToRoom()
        {
            if (IsSoloRoom || !Room.Known || Room.Day != Student.CurrentDay) return false;
            if (Student.DayState == StudentDayState.InProgress || !Student.LastNight.HasSlept || Room.IsAsleep(PlayerId)) return false;
            return Room.SleepOn(PlayerId, Student.CurrentDay);
        }

        // 읽은 뒤 한 번 부른다. 같이 하는 방의 참가자 들이기는 방을 가진 쪽이 한다.
        private void UseRoom(IRoomTime room)
        {
            IsSoloRoom = room == null;
            Room = room ?? RoomClock.FromProgress(Student, PlayerId, TimeRules ?? Core.Time.TimeRules.Legacy);
            Student.ConfigureActivityClock(IsSoloRoom ? ActivityClockMode.SkipAhead : ActivityClockMode.StayBusy);
        }
    }
}
