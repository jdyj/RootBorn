namespace Rootborn.Screens
{
    /// <summary>
    /// 지금 상호작용 키를 누르면 반응할 월드 대상. Host가 넘긴다. X, Y는 대상 가운데의 화면 논리 좌표(1920x1080 기준),
    /// Cell은 맵 한 칸이 화면에서 차지하는 논리 크기(안내 글자를 대상 위 몇 칸에 띄울지 셀 때 쓴다).
    /// </summary>
    public readonly record struct InteractionTargetView(string Kind, string Id, float X, float Y, float Cell);
}
