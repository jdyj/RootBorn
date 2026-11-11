namespace Rootborn.Simulation
{
    /// <summary>
    /// 걸음·프레임 비용의 한계(벽시계가 아니라 흔들리지 않는 값). 창 없는 몸에서 잰다(IFrameCost).
    /// </summary>
    public static class PlayLimits
    {
        /// <summary>
        /// 가만히 선 프레임 한 번에 새로 잡아도 되는 바이트. 지금 게임 코드가 매 프레임 약 100바이트를 잡는다(알려진 결함 SIM-001:
        /// ScreenStack.Tick 64, 가구 방에서 ScreenStack.EnterMap 32). 그보다 크게 늘면(목록을 매 프레임 다시 만드는 식) 실패한다.
        /// SIM-001이 고쳐지면 이 값을 줄인다.
        /// </summary>
        public const double IdleFrameBytes = 256;
    }

    /// <summary>프레임 비용을 잴 수 있는 몸(창 없는 몸).</summary>
    public interface IFrameCost
    {
        double AllocatedBytesPerIdleFrame(int frames);
    }
}
