using System;
using System.Collections.Generic;

namespace Rootborn.Core.Workspaces
{
    /// <summary>공간의 성질별 점수. 성질 id 순으로 정렬돼 있다.</summary>
    public sealed class SpaceScores
    {
        public static readonly SpaceScores Empty = new SpaceScores(Array.Empty<QualityScore>());

        private readonly QualityScore[] _all;
        private readonly Dictionary<string, int> _byId;

        internal SpaceScores(QualityScore[] sortedScores)
        {
            _all = sortedScores;
            _byId = new Dictionary<string, int>(sortedScores.Length, StringComparer.Ordinal);
            for (int i = 0; i < sortedScores.Length; i++) _byId[sortedScores[i].QualityId] = sortedScores[i].Score;
        }

        public IReadOnlyList<QualityScore> All => _all;

        /// <summary>모르는 성질은 0.</summary>
        public int Get(string qualityId) => qualityId != null && _byId.TryGetValue(qualityId, out var score) ? score : 0;
    }

    /// <summary>공간 점수에 더해 주는 쪽. 가구 말고 다른 근원이 생기면 클래스 하나로 더한다.</summary>
    public interface ISpaceQualitySource
    {
        void AddTo(Dictionary<string, long> totals);
    }

    /// <summary>놓인 가구가 주는 점수. 같은 가구의 n번째는 앞 값에 repeatPercent%를 차례로 곱해 내린다.</summary>
    public sealed class FurnitureQualitySource : ISpaceQualitySource
    {
        private readonly IReadOnlyList<string> _placed;
        private readonly FurnitureQualityTable _table;
        private readonly int _repeatPercent;

        public FurnitureQualitySource(IReadOnlyList<string> placedFurnitureIds, FurnitureQualityTable table, int repeatPercent)
        {
            _placed = placedFurnitureIds ?? Array.Empty<string>();
            _table = table ?? throw new ArgumentNullException(nameof(table));
            _repeatPercent = Math.Max(0, repeatPercent);
        }

        public void AddTo(Dictionary<string, long> totals)
        {
            var count = new Dictionary<string, int>(StringComparer.Ordinal);
            var current = new Dictionary<(string, string), long>();
            for (int i = 0; i < _placed.Count; i++)
            {
                var id = _placed[i];
                if (id == null) continue;
                var scores = _table.ScoresOf(id);
                if (scores.Count == 0) continue;
                count.TryGetValue(id, out int before);
                count[id] = before + 1;
                for (int s = 0; s < scores.Count; s++)
                {
                    var key = (id, scores[s].QualityId);
                    long value = before == 0 ? scores[s].Score : current[key] * _repeatPercent / 100;
                    current[key] = value;
                    totals.TryGetValue(scores[s].QualityId, out long sum);
                    totals[scores[s].QualityId] = sum + value;
                }
            }
        }
    }

    public static class SpaceScoreCalculator
    {
        /// <summary>근원들의 합에 scorePercent를 한 번 곱한다(합 × scorePercent / 100, 내림). 결과는 int.MaxValue로 자른다.</summary>
        public static SpaceScores Calculate(IReadOnlyList<ISpaceQualitySource> sources, int scorePercent)
        {
            if (sources == null || sources.Count == 0) return SpaceScores.Empty;
            var totals = new Dictionary<string, long>(StringComparer.Ordinal);
            for (int i = 0; i < sources.Count; i++) sources[i]?.AddTo(totals);
            if (totals.Count == 0) return SpaceScores.Empty;

            var ids = new List<string>(totals.Keys);
            ids.Sort(StringComparer.Ordinal);
            long percent = Math.Max(0, scorePercent);
            var scores = new QualityScore[ids.Count];
            for (int i = 0; i < scores.Length; i++)
            {
                long value = totals[ids[i]] * percent / 100;
                scores[i] = new QualityScore(ids[i], (int)Math.Min(value, int.MaxValue));
            }
            return new SpaceScores(scores);
        }
    }
}
