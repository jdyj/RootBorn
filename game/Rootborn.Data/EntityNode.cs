using System.Text.Json;

namespace Rootborn.Data
{
    /// <summary>data/ 엔티티 파일의 원소 하나. 아직 Core 객체로 만들지 않은 JSON 그대로다.</summary>
    public sealed class EntityNode
    {
        internal EntityNode(string type, string id, string asset, JsonElement json, string file, int index)
        {
            Type = type;
            Id = id;
            Asset = asset;
            Json = json;
            File = file;
            Index = index;
        }

        public string Type { get; }
        public string Id { get; }

        /// <summary>원본 Unity 에셋 경로. 없으면 빈 문자열.</summary>
        public string Asset { get; }

        public JsonElement Json { get; }

        /// <summary>이 노드를 읽은 JSON 파일 경로.</summary>
        public string File { get; }

        /// <summary>파일 안 배열 순서.</summary>
        public int Index { get; }

        /// <summary>오류에 붙일 위치: asset, 없으면 파일 경로.</summary>
        public string Source => string.IsNullOrEmpty(Asset) ? File : Asset;
    }
}
