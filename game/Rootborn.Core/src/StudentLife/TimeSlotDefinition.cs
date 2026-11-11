namespace Rootborn.Core.StudentLife
{
    /// <summary>
    /// 하루의 시간대 하나(원본 Assets/Scripts/Game/StudentLife/TimeSlotDefinition.cs). 규칙(장소 상태 조건, NPC 일정)은
    /// 시간대를 id로만 가리키고, 지금 시간대는 <see cref="LocationStateTimeSlots"/>가 시각으로 정한다. 이 정의는 화면 이름과 순서를 준다.
    /// </summary>
    public sealed class TimeSlotDefinition
    {
        public TimeSlotDefinition(string id, string displayNameKey, int order)
        {
            Id = id ?? string.Empty;
            DisplayNameKey = string.IsNullOrEmpty(displayNameKey) ? Id : displayNameKey;
            Order = order;
        }

        public string Id { get; }

        /// <summary>비면 id(원본과 같다).</summary>
        public string DisplayNameKey { get; }

        /// <summary>하루 안의 순서(아침 0부터).</summary>
        public int Order { get; }
    }
}
