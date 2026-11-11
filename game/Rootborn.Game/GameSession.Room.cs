using Rootborn.Core.StudentLife;
using Rootborn.Core.Time;

namespace Rootborn.Game
{
    /// <summary>
    /// 방의 시계(멀티플레이 설계 2.1절). GameSession은 시계를 "주는 쪽"(<see cref="IRoomTime"/>)으로 받는다.
    /// 주지 않으면 혼자인 방이다: 읽은 개인 진행에서 만든다(<see cref="RoomClock.FromProgress"/>, 따로 저장하지 않는다).
    /// 개인 시간 포인트는 이 방에 묻고 알린다(<see cref="RoomTimePointGate"/>). 혼자인 방은 개인 예산과 같은 답을 주므로 동작이 지금과 같다.
    /// </summary>
    public sealed partial class GameSession
    {
        /// <summary>이 판이 도는 방의 시계.</summary>
        public IRoomTime Room { get; private set; }

        /// <summary>지금 때의 대표 시각(분). 화면은 아직 분 단위 개인 시각(<see cref="StudentLifeProgress.TimeMinutes"/>)을 쓴다.</summary>
        public int RoomTimeMinutes => Room.RepresentativeMinutes;

        // 읽은 뒤 한 번 부른다. 같이 하는 방의 참가자 들이기는 방을 가진 쪽이 한다.
        private void UseRoom(IRoomTime room)
        {
            Room = room ?? RoomClock.FromProgress(Student, PlayerId);
            Student.ConfigureTimeGate(new RoomTimePointGate(Room, PlayerId));
        }
    }
}
