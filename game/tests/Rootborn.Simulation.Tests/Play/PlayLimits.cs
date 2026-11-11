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

        /// <summary>
        /// 시계의 분이 바뀐 프레임 한 번에 새로 잡아도 되는 바이트(흐르는 시간: 게임 1분 = 약 25프레임에 한 번). 알림을 받은 HUD가 시각 글자를
        /// 새로 만들고, GameSession이 알림 묶음을 만든다. 게임 1시간마다 저장하는 프레임은 세이브 파일을 쓰느라 크다 — 그 프레임은 이 한계에 넣지 않고
        /// 가만히 선 프레임 측정(60프레임 안팎)이 시각이 정시를 지나지 않도록 잰다. 매 프레임이 아니라 분마다의 비용이다.
        /// </summary>
        public const long MinuteFrameBytes = 16 * 1024;
    }

    /// <summary>프레임 비용을 잴 수 있는 몸(창 없는 몸).</summary>
    public interface IFrameCost
    {
        double AllocatedBytesPerIdleFrame(int frames);
    }

    /// <summary>
    /// 프레임마다 학생 시계를 보는 몸(창 없는 몸): 혼자인 방에서 시계를 멈추는 화면이 떠 있는데 시계가 움직인 프레임 수.
    /// 실제 창 몸은 한 프레임 안의 명령과 시간 흐름을 나눠 볼 수 없어 이 검사를 하지 않는다(Host 테스트가 따로 본다).
    /// </summary>
    public interface IClockWatch
    {
        int ClockViolations { get; }
    }
}
