namespace Rootborn.Data
{
    /// <summary>
    /// 데이터 읽기·만들기 중에 모은 오류 하나. 처음 하나에서 멈추지 않고 모은다.
    /// Kind: parse, duplicate, missing-field, wrong-type, missing-ref, wrong-ref-type, cycle, unknown-type, enum-range, constructor.
    /// </summary>
    public sealed class LoadError
    {
        public LoadError(string kind, string type, string id, string source, string message)
        {
            Kind = kind;
            Type = type ?? "";
            Id = id ?? "";
            Source = source ?? "";
            Message = message ?? "";
        }

        public string Kind { get; }
        public string Type { get; }
        public string Id { get; }

        /// <summary>엔티티의 asset 값. 없으면 JSON 파일 경로.</summary>
        public string Source { get; }

        public string Message { get; }

        public override string ToString() => $"[{Kind}] {Type}/{Id} ({Source}): {Message}";
    }
}
