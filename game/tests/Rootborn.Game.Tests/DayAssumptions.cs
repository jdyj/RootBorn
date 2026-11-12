using System;
using System.IO;
using System.Text.Json;
using Rootborn.Core.Minigames;
using Rootborn.Core.Time;

namespace Rootborn.Game.Tests
{
    /// <summary>
    /// 하루 균형을 볼 때 쓰는 가정(day-balance.json 한 곳): 활동 사이 걷는 시간, 미니게임을 하는 실제 시간의 몫.
    /// DayBalanceTests와 균형 시뮬레이터(Rootborn.Balance.Tests)가 같은 사본을 쓴다.
    /// </summary>
    internal sealed class DayAssumptions
    {
        public int Min, Max, BodyMayStopFrom, TravelMinutes;
        public float MinigameShare;

        private static readonly Lazy<DayAssumptions> _current = new Lazy<DayAssumptions>(Read);

        public static DayAssumptions Current => _current.Value;

        /// <summary>활동 사이 걷는 시간을 보낸다(시계만 흐른다).</summary>
        public static void Walk(GameSession game, int minutes)
        {
            if (minutes <= 0) return;
            game.AdvanceTime(RealSeconds(game.TimeRules, minutes) + 0.001f, false);
        }

        /// <summary>게임 분 → 실제 초(하루 길이 game.TimeRules.RealSecondsPerGameDay). 걷기·날 넘김·걷기 실제 초 기록이 같은 식을 쓴다.</summary>
        public static float RealSeconds(TimeRules rules, int gameMinutes) => gameMinutes * rules.RealSecondsPerGameDay / (24 * 60);

        /// <summary>
        /// 실제 초 → 게임 분, 올림. 시계(Core FlowingClock)는 1분이 안 되는 조각을 다음 부름에 넘기므로 그 초가 실제로 보내는 분은 이 값 이하다.
        /// </summary>
        public static int GameMinutesUp(TimeRules rules, float realSeconds)
            => (int)Math.Ceiling(realSeconds / (rules.RealSecondsPerGameDay / (double)TimeRules.MinutesPerDay) - 1e-9);

        /// <summary>미니게임을 하는 실제 초: 제한 시간 합의 일정 몫(day-balance.json의 몫).</summary>
        public static float MinigameSeconds(Minigame minigame) => MinigameSeconds(minigame, Current.MinigameShare);

        /// <summary>미니게임을 하는 실제 초: 제한 시간 합의 share만큼(균형 시뮬레이터가 몫을 바꿔 돌릴 때).</summary>
        public static float MinigameSeconds(Minigame minigame, float share)
        {
            float limit = 0f;
            foreach (var challenge in minigame.Challenges)
                if (challenge is ChallengeBase c) limit += c.TimeLimitSeconds;
            return limit * share;
        }

        // 미니게임을 하는 동안 시계가 흐른다(혼자 하기, 미니게임 화면은 시계를 멈추지 않는다).
        public static void PlayMinigame(GameSession game, Minigame minigame)
            => PlayMinigame(game, minigame, Current.MinigameShare);

        public static void PlayMinigame(GameSession game, Minigame minigame, float share)
            => game.AdvanceTime(MinigameSeconds(minigame, share), false);

        private static DayAssumptions Read()
        {
            var path = Path.Combine(TestWorld.RepoRoot, "game", "tests", "Rootborn.Game.Tests", "day-balance.json");
            using var document = JsonDocument.Parse(File.ReadAllBytes(path));
            var b = document.RootElement;
            var range = b.GetProperty("bigActivitiesPerDay");
            var stop = b.GetProperty("bodyMayStopFrom").GetString().Split(':');
            return new DayAssumptions
            {
                Min = range.GetProperty("min").GetInt32(),
                Max = range.GetProperty("max").GetInt32(),
                BodyMayStopFrom = int.Parse(stop[0]) * 60 + int.Parse(stop[1]),
                TravelMinutes = b.GetProperty("travelMinutesPerActivity").GetInt32(),
                MinigameShare = b.GetProperty("minigamePlayShareOfTimeLimit").GetSingle(),
            };
        }
    }
}
