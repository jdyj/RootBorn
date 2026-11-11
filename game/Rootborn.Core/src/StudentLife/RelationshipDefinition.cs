namespace Rootborn.Core.StudentLife
{
    /// <summary>
    /// 관계 하나(원본 Assets/Scripts/Game/StudentLife/RelationshipDefinition.cs). 관계 값은 StudentLifeProgress가 id로 갖고,
    /// 규칙은 이 정의를 id로만 가리킨다. 이 정의는 화면 이름과 관계의 상대(NPC id)를 준다.
    /// </summary>
    public sealed class RelationshipDefinition
    {
        public RelationshipDefinition(string id, string displayNameKey, string targetId = null)
        {
            Id = id ?? string.Empty;
            DisplayNameKey = string.IsNullOrEmpty(displayNameKey) ? Id : displayNameKey;
            TargetId = string.IsNullOrEmpty(targetId) ? Id : targetId;
        }

        public string Id { get; }

        /// <summary>비면 id(원본과 같다).</summary>
        public string DisplayNameKey { get; }

        /// <summary>관계의 상대. 비면 id(원본과 같다).</summary>
        public string TargetId { get; }
    }
}
