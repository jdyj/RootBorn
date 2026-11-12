using System;

namespace Rootborn.Core.Workspaces
{
    /// <summary>공간이 가진 성질의 종류(아늑함, 정돈 등). 무엇이 있는지는 데이터가 정한다.</summary>
    public sealed class SpaceQuality
    {
        public SpaceQuality(string id, string displayNameKey, string iconKey)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("성질 id가 비어 있다", nameof(id));
            Id = id;
            DisplayNameKey = displayNameKey ?? string.Empty;
            IconKey = iconKey ?? string.Empty;
        }

        public string Id { get; }
        public string DisplayNameKey { get; }
        public string IconKey { get; }
    }

    /// <summary>성질 하나의 점수.</summary>
    public readonly struct QualityScore
    {
        public QualityScore(string qualityId, int score)
        {
            QualityId = qualityId ?? string.Empty;
            Score = score;
        }

        public string QualityId { get; }
        public int Score { get; }
    }
}
