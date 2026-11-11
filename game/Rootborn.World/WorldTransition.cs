using System;
using Rootborn.Core.World;

namespace Rootborn.World
{
    /// <summary>지금 보이는 연출: 없음, 자동문이 열림, 이동 구역 페이드.</summary>
    public enum WorldTransitionKind { None, Door, Area }

    /// <summary>
    /// 문을 지날 때의 연출 상태(그리기 전의 값만). Core WorldSession.OpeningDoor가 열리는 문을 알려 주고, 여기서는 그 문이 열린 뒤로 흐른 시간을 센다
    /// (Core DoorWatch처럼 들어선 프레임은 0부터). 자동문(Unity TownAutoDoorPortal)은 문 위 사각형의 폭·알파를, 이동 구역(TownLevelTransitionTrigger)은
    /// 화면 덮개의 알파를 낸다. 걷기를 막는지도 데이터가 정한다(Unity 판은 막지 않았다). 그림과 무관하다.
    /// </summary>
    public sealed class WorldTransition
    {
        private readonly WorldPresentation _p;
        private float _elapsed;

        public WorldTransition(WorldPresentation presentation) { _p = presentation ?? throw new ArgumentNullException(nameof(presentation)); }

        public WorldTransitionKind Kind { get; private set; }
        /// <summary>열리는 문(없으면 null).</summary>
        public DoorArea Door { get; private set; }
        /// <summary>0(들어선 순간) ~ 1(지나기 직전).</summary>
        public float Progress { get; private set; }
        /// <summary>끝난(문이 열리는 동안이 끝나 사라진) 연출 수(시험용).</summary>
        public int Completed { get; private set; }

        public bool BlocksInput => Kind != WorldTransitionKind.None && _p.TransitionsBlockInput;
        public float DoorWidthRatio => Kind == WorldTransitionKind.Door ? Lerp(_p.DoorWidthFrom, _p.DoorWidthTo, Progress) : 0f;
        public float DoorAlpha => Kind == WorldTransitionKind.Door ? Lerp(_p.DoorAlphaFrom, _p.DoorAlphaTo, Progress) : 0f;
        public float FadeAlpha => Kind == WorldTransitionKind.Area ? _p.AreaFadeAlpha : 0f;

        /// <summary>
        /// 걷기를 한 프레임 돌린 뒤에 부른다. opening은 지금 열리는 문(WorldSession.OpeningDoor), isArea는 그것이 맵 목록의 이동 구역인가(맵 성질).
        /// </summary>
        public void Update(DoorArea opening, bool isArea, float dt)
        {
            if (opening == null)
            {
                if (Kind != WorldTransitionKind.None) Completed++;
                Kind = WorldTransitionKind.None;
                Door = null;
                Progress = 0f;
                _elapsed = 0f;
                return;
            }
            if (!ReferenceEquals(opening, Door))
            {
                Door = opening;
                Kind = isArea ? WorldTransitionKind.Area : WorldTransitionKind.Door;
                _elapsed = 0f;
            }
            else _elapsed += Math.Max(0f, dt);
            float seconds = opening.OpenSeconds ?? DoorWatch.UnityOpeningSeconds;
            Progress = seconds <= 0f ? 1f : Math.Clamp(_elapsed / seconds, 0f, 1f);
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }
}
