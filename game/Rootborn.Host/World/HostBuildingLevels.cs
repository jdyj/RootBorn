using System;
using Rootborn.Game;
using Rootborn.World;

namespace Rootborn.Host.World
{
    /// <summary>
    /// 게임 한 판을 읽는 부지 단계 종류(maps.json lots[].level.kind). World는 GameSession을 모르므로 Host가 더한다.
    /// 게임 한 판이 아직 없으면(월드 화면을 먼저 만든다) 단계 1 — 한 판이 생겨 단계가 달라지면 그 맵을 다시 만든다(WorldMapLoader.IsStale).
    /// </summary>
    public static class HostBuildingLevels
    {
        public const string HouseStage = "house-stage", FacilityLevel = "facility-level";

        /// <summary>fixed와 게임 한 판의 종류를 담은 표.</summary>
        public static BuildingLevelKinds Create(Func<GameSession> game)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            return BuildingLevelKinds.WithFixed()
                .Add(HouseStage, new HouseStageLevel(game))
                .Add(FacilityLevel, new FacilityLevelKind(game));
        }
    }

    /// <summary>house-stage: 집 단계(GameSession.HouseStage, 0부터) + 1.</summary>
    public sealed class HouseStageLevel : IBuildingLevelKind
    {
        private readonly Func<GameSession> _game;

        public HouseStageLevel(Func<GameSession> game) { _game = game ?? throw new ArgumentNullException(nameof(game)); }

        public int LevelOf(LotLevel level)
        {
            GameSession session = _game();
            return session == null ? 1 : session.HouseStage + 1;
        }
    }

    /// <summary>facility-level: 성인 시설 레벨. key가 시설 id이고, 지금 맡은 시설이 그 id가 아니면(또는 아직 성인이 아니면) 1.</summary>
    public sealed class FacilityLevelKind : IBuildingLevelKind
    {
        private readonly Func<GameSession> _game;

        public FacilityLevelKind(Func<GameSession> game) { _game = game ?? throw new ArgumentNullException(nameof(game)); }

        public int LevelOf(LotLevel level)
        {
            var adult = _game()?.Adult;
            if (adult == null || !string.Equals(adult.FacilityId, level.Key, StringComparison.Ordinal)) return 1;
            return Math.Max(1, adult.FacilityLevel);
        }
    }
}
