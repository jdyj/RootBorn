namespace Rootborn.Screens.Hud
{
    /// <summary>
    /// 돈을 같이 쓰는 방에서 돈 쓰는 화면(가게의 가구 구매, 성인 시설)이 보이는 문구 키(멀티플레이 설계 3.2절). 명령이 방의 허락을 기다리면
    /// (<see cref="Rootborn.Game.GameSession.WaitingForRoom"/>) 기다림 문구를 보이고 버튼을 다시 누르지 않는다. 끝나면
    /// (<see cref="Rootborn.Game.GameSession.LastRoomCommandApplied"/>) 그 명령의 결과 문구, 방이 거절했으면 거절 문구다.
    /// 혼자 하기와 돈을 같이 쓰지 않는 방에서는 기다림이 없다.
    /// </summary>
    public static class RoomMoneyText
    {
        public const string WaitingKey = "ui.room.wallet-waiting";
        public const string RefusedKey = "ui.room.wallet-refused";
    }
}
