using System;
using System.Collections.Generic;
using System.Text.Json;
using Rootborn.Core.Save;

namespace Rootborn.Core.Tests
{
    /// <summary>
    /// 테스트용 메모리 저장소. 읽을 때마다 새 사본을 준다(파일에서 매번 새 객체를 만들던 것과 같다).
    /// 사본은 테스트 프로젝트에서만 JSON 왕복으로 만든다(Core는 JSON을 모른다).
    /// </summary>
    public sealed class InMemorySaveStore : ISaveStore
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions { IncludeFields = true };
        private readonly Dictionary<string, string> _files = new Dictionary<string, string>(StringComparer.Ordinal);

        public int Reads { get; private set; }
        public int Writes { get; private set; }
        public int Deletes { get; private set; }

        /// <summary>테스트 준비용으로 파일 하나를 넣는다(쓰기 횟수에 세지 않는다).</summary>
        public void Put<T>(string saveSlot, string fileName, T data) where T : class
        {
            _files[Key(saveSlot, fileName)] = JsonSerializer.Serialize(data, Options);
        }

        public bool Contains(string saveSlot, string fileName) => _files.ContainsKey(Key(saveSlot, fileName));

        public bool TryRead<T>(string saveSlot, string fileName, out T data) where T : class, new()
        {
            Reads++;
            if (!_files.TryGetValue(Key(saveSlot, fileName), out var json))
            {
                data = null;
                return false;
            }

            data = JsonSerializer.Deserialize<T>(json, Options) ?? new T();
            return true;
        }

        public void Write<T>(string saveSlot, string fileName, T data) where T : class
        {
            Writes++;
            _files[Key(saveSlot, fileName)] = JsonSerializer.Serialize(data, Options);
        }

        public void Delete(string saveSlot, string fileName)
        {
            Deletes++;
            _files.Remove(Key(saveSlot, fileName));
        }

        private static string Key(string saveSlot, string fileName) => saveSlot + "/" + fileName;
    }
}
