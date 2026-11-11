using System;
using System.Collections.Generic;
using Rootborn.Core.StudentLife;
using Rootborn.Core.Time;

namespace Rootborn.Game
{
    /// <summary>
    /// 방의 시계(멀티플레이 설계 2.1절). GameSession은 시계를 "주는 쪽"(<see cref="IRoomTime"/>)으로 받는다.
    /// 주지 않으면 혼자인 방이다: 읽은 개인 진행에서 만든다(<see cref="RoomClock.FromProgress"/>, 따로 저장하지 않는다).
    /// 개인 시간 포인트는 이 방에 묻고 알린다(<see cref="RoomTimePointGate"/>). 혼자인 방은 개인 예산과 같은 답을 주므로 동작이 지금과 같다.
    /// 같이 하는 방에서 방이 바뀌면(다른 사람이 끝냄, 자동 넘기기) 방을 잇는 쪽(Host)이 <see cref="SyncWithRoom"/>을 부른다.
    /// </summary>
    public sealed partial class GameSession
    {
        private static readonly string[] RoomTopics = { GameChangeTopics.Student, GameChangeTopics.Room };
        private RoomTimePointGate _roomGate;

        /// <summary>이 판이 도는 방의 시계.</summary>
        public IRoomTime Room { get; private set; }

        /// <summary>지금 때의 대표 시각(분). HUD의 시각 숫자. 장소 상태·NPC 일정의 시간대는 아직 분 단위 개인 시각(<see cref="StudentLifeProgress.TimeMinutes"/>)을 쓴다.</summary>
        public int RoomTimeMinutes => Room.RepresentativeMinutes;

        /// <summary>
        /// HUD가 보일 시각(분): 하루가 진행 중이면 방의 지금 때의 대표 시각, 오늘을 마감했으면 밤 시각(방은 이미 다음 날일 수 있다).
        /// 분 단위 개인 시각(<see cref="StudentLifeProgress.TimeMinutes"/>)과 다르다 — 장소 상태·NPC 일정은 아직 개인 시각을 따른다.
        /// </summary>
        /// 혼자인 방은 흐르는 개인 시계(<see cref="ClockMinutes"/>)를 보인다(흐르는 시간 2단계 — 때는 4단계에서 없앤다).
        public int RoomShownMinutes => IsSoloRoom && TimeRules != null ? ClockMinutes
            : Student.DayState == StudentDayState.InProgress ? Room.RepresentativeMinutes : Student.Calendar?.PartTimes.DayOverMinutes ?? Room.RepresentativeMinutes;

        /// <summary>방의 시계 기준 오늘 남은 시간 포인트(때). 오늘을 마감했으면 0.</summary>
        public int RoomTimePointsRemaining => Student.DayState == StudentDayState.InProgress ? Math.Max(0, Room.PartsToday - Room.CompletedParts(PlayerId)) : 0;

        /// <summary>
        /// 방에 있는 다른 사람 중 지금 때를 아직 안 끝낸 사람의 신원을 into에 채운다(비우고 채운다). 혼자면 비어 있다.
        /// 오늘을 마감한 뒤에는 아직 마감 안 한 사람이다.
        /// </summary>
        public void RoomWaitingFor(List<string> into)
        {
            if (into == null) return;
            into.Clear();
            _roomScratch.Clear();
            Room.CopyPresent(_roomScratch);
            bool dayEnded = Student.DayState != StudentDayState.InProgress;
            foreach (var p in _roomScratch)
            {
                if (string.Equals(p.ParticipantId, PlayerId, StringComparison.Ordinal)) continue;
                if (dayEnded ? !p.DayEnded : !p.FinishedPart) into.Add(p.ParticipantId);
            }
        }

        private readonly List<RoomParticipantState> _roomScratch = new List<RoomParticipantState>(8);

        /// <summary>지금 때를 넘길 수 있다: 하루가 진행 중이고, 이 때를 아직 안 끝냈고, 남은 시간 포인트가 있다.</summary>
        public bool CanSkipPart => Student.CanSpendTimePoints(1) && Room.CanSpend(PlayerId, 1);

        /// <summary>
        /// "넘기기": 지금 때를 아무것도 하지 않고 끝낸다. 개인 시간 포인트도 하나 써서 개인 진행과 방이 어긋나지 않는다.
        /// partIndex는 화면이 보인 때(<see cref="IRoomTime.PartIndex"/>)다. 그 사이 때가 바뀌었으면(두 번 누름) 넘기지 않는다.
        /// 활동 기록·생활 수치는 바꾸지 않는다. 하루를 끝내는 것은 따로(<see cref="EndDay"/>, 시간 포인트를 안 써도 된다).
        /// </summary>
        public bool SkipPart(int partIndex)
        {
            if (Room.PartIndex != partIndex || Room.Day != Student.CurrentDay || !CanSkipPart || !Student.TrySpendTimePoints(1)) return false;
            SaveAndNotify(RoomTopics);
            return true;
        }

        /// <summary>
        /// 방이 바뀐 뒤 부른다. 방이 이 사람의 때를 대신 끝냈으면(자동 넘기기) 개인 시간 포인트를 방에 맞춰 저장한다(true).
        /// 맞출 것이 없어도 방이 바뀌었다고 알린다(HUD가 누가 기다리는지 다시 읽는다).
        /// </summary>
        public bool SyncWithRoom()
        {
            bool newDay = CatchUpRoomDay();
            bool caughtUp = _roomGate.CatchUp(Student) > 0;
            ResendToRoom();
            if (newDay || caughtUp)
            {
                if (newDay) SaveNewDay();
                else SaveAndNotify(RoomTopics);
                return true;
            }

            Notify(null, GameChangeTopics.Room);
            return false;
        }

        /// <summary>
        /// 방이 이 사람의 날보다 앞서 있고 이 사람의 하루가 진행 중이면(방에 없던 사이 날이 지났다) 그 사이를 쉰 날로 넘겨 방의 날에서 시작한다
        /// (멀티플레이 설계 10절 7번, Core StudentLifeProgress.RestUntil). 오늘을 마감한 뒤라면 다음 날 시작(<see cref="StartNextDay"/>)이 한다.
        /// 방의 날을 모르면(접속 직후) 하지 않는다. 저장은 부르는 쪽이 한다.
        /// </summary>
        private bool CatchUpRoomDay()
        {
            if (Room.Day <= Student.CurrentDay || Student.DayState != StudentDayState.InProgress) return false;
            if (Student.RestUntil(Room.Day, _dayEndRules) == 0) return false;
            BeginNewDay();
            return true;
        }

        /// <summary>
        /// 개인 진행이 방보다 앞서 있으면(요청을 보낸 직후 끊겨 방이 세지 못했다) 같은 날·같은 때의 요청을 다시 보낸다. 방은 (날, 때)로 받으므로
        /// 이미 센 것은 두 번 세지 않는다(Core RoomClock.SpendFrom). 오늘을 마감했는데 방이 모르면 마감을 다시 알린다.
        /// </summary>
        private bool ResendToRoom()
        {
            if (Room.Day != Student.CurrentDay) return false;
            if (Student.DayState != StudentDayState.InProgress) return !RoomKnowsMyDayEnded() && Room.EndDay(PlayerId);
            int ahead = Math.Min(Student.TimePointsSpentToday, Room.PartsToday) - Room.CompletedParts(PlayerId);
            return ahead > 0 && Room.CanSpend(PlayerId, ahead) && Room.Spend(PlayerId, ahead);
        }

        private bool RoomKnowsMyDayEnded()
        {
            Room.CopyPresent(_roomScratch);
            foreach (var p in _roomScratch)
                if (string.Equals(p.ParticipantId, PlayerId, StringComparison.Ordinal)) return p.DayEnded;
            return true; // 방에 없으면(모름) 보내지 않는다
        }

        // 읽은 뒤 한 번 부른다. 같이 하는 방의 참가자 들이기는 방을 가진 쪽이 한다.
        // 혼자인 방은 활동을 막지 않는다(흐르는 시간 — GameSession.Time의 SoloRoomGate).
        private void UseRoom(IRoomTime room)
        {
            IsSoloRoom = room == null;
            Room = room ?? RoomClock.FromProgress(Student, PlayerId);
            _roomGate = new RoomTimePointGate(Room, PlayerId);
            Student.ConfigureTimeGate(IsSoloRoom ? new SoloRoomGate(Room, PlayerId) : (ITimePointGate)_roomGate);
        }
    }
}
