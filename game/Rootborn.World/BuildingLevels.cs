using System;
using System.Collections.Generic;

namespace Rootborn.World
{
    /// <summary>부지의 단계를 어디서 읽는지(maps.json lots[].level): 종류 문자열, 열쇠(종류가 쓰면 — 예: 시설 id), 값(fixed).</summary>
    public sealed class LotLevel
    {
        public string Kind = FixedLevel.Kind;
        public string Key = string.Empty;
        public int Value = 1;
    }

    /// <summary>단계 종류 하나. 새 종류는 클래스 하나와 BuildingLevelKinds.Add 한 줄이다(종류를 enum·switch로 닫지 않는다).</summary>
    public interface IBuildingLevelKind
    {
        /// <summary>지금 단계(1부터). 건물의 단계 수 밖이면 놓을 때 맞춘다(BuildingPlacement.ClampLevel).</summary>
        int LevelOf(LotLevel level);
    }

    /// <summary>fixed: 맵 목록에 적은 값을 그대로.</summary>
    public sealed class FixedLevel : IBuildingLevelKind
    {
        public const string Kind = "fixed";
        public int LevelOf(LotLevel level) => level.Value;
    }

    /// <summary>
    /// 단계 종류 표. World는 fixed만 안다 — 게임 한 판을 읽는 종류(house-stage, facility-level)는 GameSession을 아는 층(Host)이 더한다.
    /// 모르는 종류는 단계 1로 놓고 맵 문제로 알린다.
    /// </summary>
    public sealed class BuildingLevelKinds
    {
        private readonly Dictionary<string, IBuildingLevelKind> _kinds = new Dictionary<string, IBuildingLevelKind>(StringComparer.Ordinal);

        public static BuildingLevelKinds WithFixed() => new BuildingLevelKinds().Add(FixedLevel.Kind, new FixedLevel());

        public BuildingLevelKinds Add(string kind, IBuildingLevelKind level)
        {
            if (string.IsNullOrWhiteSpace(kind)) throw new ArgumentException("단계 종류 이름이 비었다");
            _kinds[kind] = level ?? throw new ArgumentNullException(nameof(level));
            return this;
        }

        public IEnumerable<string> Names => _kinds.Keys;

        /// <summary>그 종류를 알면 지금 단계. 모르면 false(단계는 1).</summary>
        public bool TryLevelOf(LotLevel level, out int value)
        {
            if (level != null && _kinds.TryGetValue(level.Kind ?? string.Empty, out IBuildingLevelKind kind)) { value = kind.LevelOf(level); return true; }
            value = 1;
            return false;
        }
    }
}
