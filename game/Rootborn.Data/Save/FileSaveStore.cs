using System;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Rootborn.Core.Save;

namespace Rootborn.Data.Save
{
    /// <summary>
    /// 세이브 파일을 &lt;root&gt;/&lt;슬롯&gt;/&lt;파일&gt;에 JSON으로 읽고 쓴다. Unity 판과 같은 배치다. 저장 폴더는 Host가 넘긴다.
    /// 옛 파일 읽기: 파일에 없는 필드는 클래스 기본값, 모양에 없는 필드는 버린다(무엇이 버려지는지는 고정 파일 테스트가 잡는다).
    /// 쓰기는 같은 폴더의 임시 파일에 쓴 뒤 바꿔치기해서, 쓰다 꺼져도 원래 파일이 깨지지 않는다.
    /// </summary>
    public sealed class FileSaveStore : ISaveStore
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            IncludeFields = true,
            WriteIndented = true,
            PropertyNamingPolicy = null,
            PropertyNameCaseInsensitive = false,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // 한글을 \u 없이 그대로 쓴다
        };

        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);
        private readonly string _root;

        public FileSaveStore(string rootDirectory)
        {
            if (string.IsNullOrWhiteSpace(rootDirectory)) throw new ArgumentException("저장 폴더가 비어 있다", nameof(rootDirectory));
            _root = Path.GetFullPath(rootDirectory);
        }

        public string RootDirectory => _root;

        public bool TryRead<T>(string saveSlot, string fileName, out T data) where T : class, new()
        {
            var path = PathFor(saveSlot, fileName);
            data = null;
            if (!File.Exists(path)) return false;

            var text = File.ReadAllText(path, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(text)) throw new SaveFileCorruptException(path, "파일이 비어 있다");

            T read;
            try
            {
                read = JsonSerializer.Deserialize<T>(text, Options);
            }
            catch (JsonException e)
            {
                throw new SaveFileCorruptException(path, e.Message, e);
            }
            catch (NotSupportedException e)
            {
                throw new SaveFileCorruptException(path, e.Message, e);
            }

            if (read == null) throw new SaveFileCorruptException(path, "파일 내용이 null이다");

            if (read is IVersionedSave versioned && versioned.Version > versioned.KnownVersion)
                throw new SaveFileTooNewException(path, versioned.Version, versioned.KnownVersion);

            data = read;
            return true;
        }

        public void Write<T>(string saveSlot, string fileName, T data) where T : class
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            var path = PathFor(saveSlot, fileName);

            // 버전 없이 만든 모양(옛 파일에서 읽은 것 포함)은 이 프로그램이 아는 버전으로 쓴다.
            if (data is IVersionedSave versioned && versioned.Version == SaveVersion.Unity) versioned.Version = versioned.KnownVersion;

            var json = JsonSerializer.Serialize(data, Options);
            var folder = Path.GetDirectoryName(path);
            Directory.CreateDirectory(folder);
            var temp = Path.Combine(folder, "." + Path.GetFileName(path) + "." + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                File.WriteAllText(temp, json, Utf8NoBom);
                File.Move(temp, path, overwrite: true);
            }
            finally
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
        }

        public void Delete(string saveSlot, string fileName)
        {
            var path = PathFor(saveSlot, fileName);
            if (File.Exists(path)) File.Delete(path);
        }

        private string PathFor(string saveSlot, string fileName)
        {
            CheckName(saveSlot, nameof(saveSlot));
            CheckName(fileName, nameof(fileName));
            var path = Path.GetFullPath(Path.Combine(_root, saveSlot, fileName));
            var rootWithSeparator = _root.EndsWith(Path.DirectorySeparatorChar) ? _root : _root + Path.DirectorySeparatorChar;
            if (!path.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException($"저장 폴더 밖을 가리킨다: {saveSlot}/{fileName}");
            return path;
        }

        /// <summary>슬롯과 파일 이름은 경로 한 칸이어야 한다. 비었거나 구분자·'..'·':'·쓸 수 없는 글자가 있으면 거절한다.</summary>
        private static void CheckName(string name, string parameter)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("이름이 비어 있다", parameter);
            if (name.Contains("..") || name == "." || name.IndexOf('/') >= 0 || name.IndexOf('\\') >= 0 || name.IndexOf(':') >= 0
                || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new ArgumentException($"경로 한 칸이 아닌 이름: '{name}'", parameter);
        }
    }
}
