namespace Rootborn.Simulation
{
    /// <summary>
    /// 걸음·프레임 비용의 한계(벽시계가 아니라 흔들리지 않는 값). 창 없는 몸에서 잰다(IFrameCost).
    /// </summary>
    public static class PlayLimits
    {
        /// <summary>
        /// 가만히 선 프레임 한 번에 새로 잡아도 되는 바이트. 화면 쌓기(ScreenStack.Tick·EnterMap)와 창 없는 몸의 월드 갱신은 0이다
        /// (SIM-001을 고친 뒤). 화면이 매 프레임 무언가를 만들기 시작하면 실패한다(coding-standards "성능": 매 프레임 새 객체 금지).
        /// </summary>
        public const double IdleFrameBytes = 0;
    }

    /// <summary>프레임 비용을 잴 수 있는 몸(창 없는 몸).</summary>
    public interface IFrameCost
    {
        double AllocatedBytesPerIdleFrame(int frames);
    }
}
