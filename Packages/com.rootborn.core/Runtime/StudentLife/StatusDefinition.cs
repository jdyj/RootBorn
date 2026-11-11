namespace Rootborn.Core.StudentLife
{
    /// <summary>
    /// 학생 컨디션 상태(피로 등) 하나(원본 Assets/Scripts/Game/StudentLife/StatusDefinition.cs).
    /// 상태 값은 StudentLifeProgress가 id로 갖고, 규칙은 이 정의를 id로만 가리킨다. 이 정의는 화면 이름과
    /// "하루가 지나도 이어지는지"를 준다(하루 요약 문장이 쓴다).
    /// </summary>
    public sealed class StatusDefinition
    {
        public StatusDefinition(string id, string displayNameKey, bool persistsToNextDay = true)
        {
            Id = id ?? string.Empty;
            DisplayNameKey = string.IsNullOrEmpty(displayNameKey) ? Id : displayNameKey;
            PersistsToNextDay = persistsToNextDay;
        }

        public string Id { get; }

        /// <summary>비면 id(원본과 같다).</summary>
        public string DisplayNameKey { get; }

        public bool PersistsToNextDay { get; }
    }
}
