namespace Rootborn.Game
{
    /// <summary>
    /// 같이 하는 방에 있는 사람의 이름(캐릭터 만들기에서 정한 이름, 화면 표시용). 신원(참가자 id — 세이브 폴더, 방장 판정)과 따로다.
    /// 방을 잇는 쪽(Host)이 준다. 혼자 하기에서는 없다.
    /// </summary>
    public interface IRoomNames
    {
        /// <summary>
        /// 방의 시계가 준 참가자 id(<see cref="Rootborn.Core.Time.IRoomTime.CopyPresent"/>)의 이름. 모르거나 이름이 없으면 빈 문자열
        /// (화면이 현지화된 기본 말을 쓴다). 줄바꿈·제어 문자는 이미 빠져 있다.
        /// </summary>
        string NameOf(string participantId);
    }
}
