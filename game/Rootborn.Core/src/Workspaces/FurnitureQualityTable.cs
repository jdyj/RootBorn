using System;
using System.Collections.Generic;
using Rootborn.Core.Placement;

namespace Rootborn.Core.Workspaces
{
    /// <summary>
    /// 가구 분류 또는 가구 하나가 성질에 주는 점수 규칙. 분류와 가구 id 중 정확히 하나만 채운다. 점수는 0 이상.
    /// </summary>
    public sealed class FurnitureQualityRule
    {
        public FurnitureQualityRule(string id, string category, string furnitureId, string qualityId, int score)
        {
            Id = id ?? string.Empty;
            Category = category ?? string.Empty;
            FurnitureId = furnitureId ?? string.Empty;
            QualityId = qualityId ?? string.Empty;
            Score = score;
        }

        public string Id { get; }
        public string Category { get; }
        public string FurnitureId { get; }
        public string QualityId { get; }
        public int Score { get; }

        public bool IsValid =>
            QualityId.Length > 0 && Score >= 0 && (Category.Length > 0) != (FurnitureId.Length > 0);
    }

    /// <summary>가구 id별 성질 점수 표. 생성할 때 한 번 펼치고, 조회는 표 찾기만 한다.</summary>
    public sealed class FurnitureQualityTable
    {
        private static readonly IReadOnlyList<QualityScore> NoScores = Array.Empty<QualityScore>();

        private readonly Dictionary<string, QualityScore[]> _byFurniture = new Dictionary<string, QualityScore[]>(StringComparer.Ordinal);
        private readonly List<string> _problems = new List<string>();

        public FurnitureQualityTable(IEnumerable<FurnitureQualityRule> rules, IEnumerable<FurnitureDefinition> furniture)
        {
            var furnitureById = new Dictionary<string, FurnitureDefinition>(StringComparer.Ordinal);
            if (furniture != null)
                foreach (var f in furniture) if (f != null) furnitureById[f.StableId] = f;

            var categoryRules = new List<FurnitureQualityRule>();
            var furnitureRules = new List<FurnitureQualityRule>();
            if (rules != null)
            {
                foreach (var rule in rules)
                {
                    if (rule == null) continue;
                    if (!rule.IsValid)
                    {
                        _problems.Add("잘못된 가구 성질 규칙: " + rule.Id);
                        continue;
                    }
                    if (rule.FurnitureId.Length > 0)
                    {
                        if (!furnitureById.ContainsKey(rule.FurnitureId))
                        {
                            _problems.Add("가구 성질 규칙 " + rule.Id + "이 모르는 가구를 가리킨다: " + rule.FurnitureId);
                            continue;
                        }
                        furnitureRules.Add(rule);
                    }
                    else categoryRules.Add(rule);
                }
            }

            // 가구 id → 성질 id → 점수. 분류 규칙을 먼저 넣고 가구 규칙이 덮어쓴다(더하지 않는다).
            var merged = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);
            // 맞는 가구가 하나도 없는 분류 규칙은 문제다(분류 이름 오타가 조용히 효과를 없애지 않게). 비교는 대소문자를 가린다.
            foreach (var rule in categoryRules)
            {
                bool matched = false;
                foreach (var f in furnitureById.Values)
                {
                    if (!string.Equals(f.Category, rule.Category, StringComparison.Ordinal)) continue;
                    Slot(merged, f.StableId)[rule.QualityId] = rule.Score;
                    matched = true;
                }
                if (!matched) _problems.Add("가구 성질 규칙 " + rule.Id + "이 가구가 없는 분류를 가리킨다: " + rule.Category);
            }
            foreach (var rule in furnitureRules) Slot(merged, rule.FurnitureId)[rule.QualityId] = rule.Score;

            foreach (var pair in merged)
            {
                var ids = new List<string>(pair.Value.Keys);
                ids.Sort(StringComparer.Ordinal);
                var scores = new QualityScore[ids.Count];
                for (int i = 0; i < scores.Length; i++) scores[i] = new QualityScore(ids[i], pair.Value[ids[i]]);
                _byFurniture[pair.Key] = scores;
            }
        }

        /// <summary>모르거나 규칙이 없는 가구는 빈 목록.</summary>
        public IReadOnlyList<QualityScore> ScoresOf(string furnitureId)
            => furnitureId != null && _byFurniture.TryGetValue(furnitureId, out var scores) ? scores : NoScores;

        public IReadOnlyList<string> Problems => _problems;

        private static Dictionary<string, int> Slot(Dictionary<string, Dictionary<string, int>> merged, string furnitureId)
        {
            if (!merged.TryGetValue(furnitureId, out var slot))
            {
                slot = new Dictionary<string, int>(StringComparer.Ordinal);
                merged[furnitureId] = slot;
            }
            return slot;
        }
    }
}
