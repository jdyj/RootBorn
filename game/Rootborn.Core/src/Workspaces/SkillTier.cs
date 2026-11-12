using System;
using Rootborn.Core.StudentLife;

namespace Rootborn.Core.Workspaces
{
    /// <summary>기술 하나의 단계(예: 요리 20 이상). 어떤 단계가 있는지는 데이터가 정한다.</summary>
    public sealed class SkillTier
    {
        public SkillTier(string id, string skillId, int minimumValue, string displayNameKey)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("단계 id가 비어 있다", nameof(id));
            Id = id;
            SkillId = skillId ?? string.Empty;
            MinimumValue = Math.Max(0, minimumValue);
            DisplayNameKey = displayNameKey ?? string.Empty;
        }

        public string Id { get; }
        public string SkillId { get; }
        public int MinimumValue { get; }
        public string DisplayNameKey { get; }

        public bool IsReached(StudentLifeProgress student)
            => student != null && student.GetSkillValue(SkillId) >= MinimumValue;
    }
}
