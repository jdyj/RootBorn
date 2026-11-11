using Rootborn.Core.StudentLife;

namespace Rootborn.Data
{
    /// <summary>
    /// Unity LocationStateConflictPolicyDefinition. Core는 정의 클래스 없이 LocationStateResolver가 모드 열거형만 받으므로,
    /// 데이터의 정책을 이 값으로 읽고 Host가 Mode를 넘긴다.
    /// </summary>
    public sealed class LocationStateConflictPolicy
    {
        public LocationStateConflictPolicy(string id, LocationStateConflictMode mode)
        {
            Id = id ?? string.Empty;
            Mode = mode;
        }

        public string Id { get; }
        public LocationStateConflictMode Mode { get; }

        public LocationStateResolver CreateResolver(LocationStateLookupCache cache) => new LocationStateResolver(cache, Mode);
    }
}
