using System;
using System.Collections.Generic;
using System.Text.Json;
using Rootborn.Core.Save;

namespace Rootborn.Net.Tests
{
    /// <summary>테스트용 메모리 저장소(읽을 때마다 JSON 왕복 사본).</summary>
    internal sealed class MemorySaveStore : ISaveStore
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions { IncludeFields = true };
        private readonly Dictionary<string, string> _files = new Dictionary<string, string>(StringComparer.Ordinal);

        public bool Contains(string saveSlot, string fileName) => _files.ContainsKey(saveSlot + "/" + fileName);

        public bool TryRead<T>(string saveSlot, string fileName, out T data) where T : class, new()
        {
            data = null;
            if (!_files.TryGetValue(saveSlot + "/" + fileName, out var json)) return false;
            data = JsonSerializer.Deserialize<T>(json, Options) ?? new T();
            return true;
        }

        public void Write<T>(string saveSlot, string fileName, T data) where T : class
            => _files[saveSlot + "/" + fileName] = JsonSerializer.Serialize(data, Options);

        public void Delete(string saveSlot, string fileName) => _files.Remove(saveSlot + "/" + fileName);
    }
}
