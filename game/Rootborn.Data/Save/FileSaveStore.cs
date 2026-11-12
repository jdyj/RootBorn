using System;
using System.Collections.Generic;
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
    /// 여러 파일을 함께 쓰는 묶음(<see cref="BeginBatch"/>)은 슬롯 폴더의 준비 폴더(<see cref="BatchFolder"/>)에 모두 쓰고, 끝났다는 표시
    /// (<see cref="BatchMark"/>: 바꿔 넣을 파일과 지울 파일 목록)를 남긴 뒤 바꿔 넣는다. 그 슬롯을 다음에 읽거나 쓸 때 표시가 있는 묶음은 마저 적용하고,
    /// 표시가 없는 묶음(준비 중에 꺼짐)은 버린다. 그래서 묶음 도중 꺼져도 슬롯은 전부 옛것이거나 전부 새것이다.
    /// 준비 폴더가 없는 슬롯(묶음 전에 만든 세이브 포함)은 지금처럼 읽는다.
    /// 한 프로세스 안의 묶음 적용·되살리기는 하나씩 한다. 같은 저장 폴더를 두 프로세스가 동시에 쓰는 것은 다루지 않는다.
    /// 파일을 디스크에 강제로 내리지(fsync) 않는다: 프로그램이 꺼지는 것은 막지만 전원이 나가는 경우는 운영체제 캐시에 따른다.
    /// </summary>
    public sealed partial class FileSaveStore : IBatchSaveStore
    {
        /// <summary>슬롯 폴더 안의 묶음 준비 폴더 이름. 세이브 파일 이름과 겹치지 않게 '.'으로 시작한다.</summary>
        public const string BatchFolder = ".save-batch";

        /// <summary>준비 폴더 안의 "준비가 끝났다" 표시 파일 이름.</summary>
        public const string BatchMark = ".batch-ready.json";

        private static readonly object BatchLock = new object();

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

        /// <summary>테스트용: 묶음의 단계(준비 쓰기, 표시, 바꿔 넣기, 지우기, 정리)가 끝날 때마다 단계 이름으로 부른다. 여기서 던지면 그 자리에서 꺼진 것과 같다.</summary>
        internal Action<string> BatchStepForTests { get; set; }

        public bool TryRead<T>(string saveSlot, string fileName, out T data) where T : class, new()
        {
            var path = PathFor(saveSlot, fileName);
            data = null;
            RecoverBatch(saveSlot);
            if (!File.Exists(path)) return false;

            data = Parse<T>(File.ReadAllText(path, Encoding.UTF8), path);
            return true;
        }

        internal static T Parse<T>(string text, string path) where T : class, new()
        {
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

            if (read is IVersionedSave versioned) RefuseNewer(versioned, path);

            return read;
        }

        // 형마다 "형식 버전이 있는 모양을 담는 필드" 목록. 읽을 때마다 리플렉션으로 다시 찾지 않는다.
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, System.Reflection.FieldInfo[]> VersionedFields =
            new System.Collections.Concurrent.ConcurrentDictionary<Type, System.Reflection.FieldInfo[]>();

        /// <summary>
        /// 맨 위 모양과 그 안에 필드로 든 모양(스냅샷의 월드 상태·체인·가구 해금·지갑 등)의 형식 버전을 본다. 하나라도 이 프로그램이 아는 것보다
        /// 높으면 읽지 않는다. 안쪽 모양의 버전만 올린 세이브를 옛 프로그램이 읽고 모르는 필드를 버린 채 다시 쓰지 않게 한다.
        /// 배열 안의 기록은 보지 않는다(형식 버전을 가진 기록이 없다).
        /// </summary>
        private static void RefuseNewer(IVersionedSave shape, string path)
        {
            if (shape.Version > shape.KnownVersion) throw new SaveFileTooNewException(path, shape.Version, shape.KnownVersion);
            var fields = VersionedFields.GetOrAdd(shape.GetType(), type =>
            {
                var found = new List<System.Reflection.FieldInfo>();
                foreach (var field in type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                    if (typeof(IVersionedSave).IsAssignableFrom(field.FieldType)) found.Add(field);
                return found.ToArray();
            });
            for (int i = 0; i < fields.Length; i++)
                if (fields[i].GetValue(shape) is IVersionedSave inner) RefuseNewer(inner, path);
        }

        public void Write<T>(string saveSlot, string fileName, T data) where T : class
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            var path = PathFor(saveSlot, fileName);
            var json = Serialize(data);
            RecoverBatch(saveSlot);
            WriteReplacing(path, json);
        }

        internal static string Serialize<T>(T data) where T : class
        {
            // 버전 없이 만든 모양(옛 파일에서 읽은 것 포함)은 이 프로그램이 아는 버전으로 쓴다.
            if (data is IVersionedSave versioned && versioned.Version == SaveVersion.Unity) versioned.Version = versioned.KnownVersion;
            return JsonSerializer.Serialize(data, Options);
        }

        // 같은 폴더의 임시 파일에 쓰고 바꿔치기한다.
        private static void WriteReplacing(string path, string json)
        {
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
            RecoverBatch(saveSlot);
            if (File.Exists(path)) File.Delete(path);
        }

        // ---------- 원문 파일(방이 손님의 세이브를 해석하지 않고 옮길 때) ----------

        /// <summary>
        /// 슬롯 폴더 맨 위 칸의 세이브 파일 이름과 내용(JSON 원문). '.'으로 시작하는 임시·묶음 파일과 폴더(사본 등)는 뺀다. 슬롯이 없으면 빈 목록.
        /// 이름 순이다.
        /// </summary>
        public IReadOnlyList<KeyValuePair<string, string>> ReadSlotFiles(string saveSlot)
        {
            var folder = SlotFolder(saveSlot);
            RecoverBatch(saveSlot);
            var files = new List<KeyValuePair<string, string>>();
            if (!Directory.Exists(folder)) return files;
            var names = new List<string>();
            foreach (var file in Directory.GetFiles(folder))
            {
                var name = Path.GetFileName(file);
                if (!name.StartsWith(".", StringComparison.Ordinal)) names.Add(name);
            }

            names.Sort(StringComparer.Ordinal);
            foreach (var name in names) files.Add(new KeyValuePair<string, string>(name, File.ReadAllText(Path.Combine(folder, name), Encoding.UTF8)));
            return files;
        }

        /// <summary>원문 쓰기·지우기를 한 묶음으로 적용한다(도중에 꺼져도 전부 옛것 또는 전부 새것). 내용은 해석하지 않는다.</summary>
        public void WriteSlotFiles(string saveSlot, IReadOnlyList<KeyValuePair<string, string>> writes, IReadOnlyList<string> deletes)
        {
            CheckName(saveSlot, nameof(saveSlot));
            var w = new List<KeyValuePair<string, string>>(writes ?? Array.Empty<KeyValuePair<string, string>>());
            var d = new List<string>(deletes ?? Array.Empty<string>());
            foreach (var pair in w) CheckWritable(pair.Key);
            foreach (var name in d) CheckWritable(name);
            if (w.Count == 0 && d.Count == 0) return;
            CommitBatch(saveSlot, w, d);
        }

        private static void CheckWritable(string fileName)
        {
            CheckName(fileName, nameof(fileName));
            if (fileName.StartsWith(".", StringComparison.Ordinal) || fileName == DayCopiesFolder)
                throw new ArgumentException("세이브 파일 이름이 아니다: " + fileName, nameof(fileName));
        }

        // ---------- 묶음 ----------

        public ISaveBatch BeginBatch(string saveSlot)
        {
            CheckName(saveSlot, nameof(saveSlot));
            RecoverBatch(saveSlot);
            return new Batch(this, saveSlot);
        }

        /// <summary>표시 파일 내용. 바꿔 넣을 파일(준비 폴더에 같은 이름으로 있다)과 지울 파일.</summary>
        private sealed class BatchMarkData
        {
            public string[] Writes = Array.Empty<string>();
            public string[] Deletes = Array.Empty<string>();
        }

        private string SlotFolder(string saveSlot) => Path.GetDirectoryName(PathFor(saveSlot, BatchMark));

        private void Step(string label) => BatchStepForTests?.Invoke(label);

        private void CommitBatch(string saveSlot, List<KeyValuePair<string, string>> writes, List<string> deletes)
        {
            lock (BatchLock)
            {
                RecoverBatchLocked(saveSlot);
                var folder = SlotFolder(saveSlot);
                var staging = Path.Combine(folder, BatchFolder);
                Directory.CreateDirectory(staging);
                var mark = new BatchMarkData { Writes = new string[writes.Count], Deletes = deletes.ToArray() };
                for (int i = 0; i < writes.Count; i++)
                {
                    File.WriteAllText(Path.Combine(staging, writes[i].Key), writes[i].Value, Utf8NoBom);
                    mark.Writes[i] = writes[i].Key;
                    Step("stage:" + writes[i].Key);
                }

                // 표시는 한 번에 생겨야 한다(임시 이름에 쓰고 바꿔치기). 표시가 생긴 순간부터 이 묶음은 "새것"이다.
                WriteReplacing(Path.Combine(staging, BatchMark), JsonSerializer.Serialize(mark, Options));
                Step("mark");
                ApplyLocked(folder, staging, mark);
            }
        }

        // 표시대로 바꿔 넣고 지운 뒤 표시와 준비 폴더를 치운다. 중간에 끊겨도 다시 부르면 남은 것만 한다.
        private void ApplyLocked(string folder, string staging, BatchMarkData mark)
        {
            foreach (var name in mark.Writes ?? Array.Empty<string>())
            {
                CheckName(name, nameof(mark));
                var staged = Path.Combine(staging, name);
                if (!File.Exists(staged)) continue; // 앞서 바꿔 넣었다
                File.Move(staged, Path.Combine(folder, name), overwrite: true);
                Step("replace:" + name);
            }

            foreach (var name in mark.Deletes ?? Array.Empty<string>())
            {
                CheckName(name, nameof(mark));
                var target = Path.Combine(folder, name);
                if (File.Exists(target)) File.Delete(target);
                Step("delete:" + name);
            }

            File.Delete(Path.Combine(staging, BatchMark));
            Step("unmark");
            Directory.Delete(staging, true);
            Step("cleanup");
        }

        private void RecoverBatch(string saveSlot)
        {
            if (!Directory.Exists(Path.Combine(SlotFolder(saveSlot), BatchFolder))) return;
            lock (BatchLock) RecoverBatchLocked(saveSlot);
        }

        /// <summary>남은 묶음: 표시가 있으면 마저 적용하고, 없으면(준비 중에 꺼짐) 버린다. 표시가 깨졌으면 읽기 오류로 알린다(슬롯은 그대로 둔다).</summary>
        private void RecoverBatchLocked(string saveSlot)
        {
            var folder = SlotFolder(saveSlot);
            var staging = Path.Combine(folder, BatchFolder);
            if (!Directory.Exists(staging)) return;
            var markPath = Path.Combine(staging, BatchMark);
            if (!File.Exists(markPath))
            {
                Directory.Delete(staging, true);
                return;
            }

            ApplyLocked(folder, staging, Parse<BatchMarkData>(File.ReadAllText(markPath, Encoding.UTF8), markPath));
        }

        /// <summary>
        /// 한 슬롯의 쓰기·지우기를 모았다가 Commit 때 함께 적용한다. 모으는 동안 슬롯 폴더는 그대로다.
        /// 모양은 Write 때 JSON으로 바꿔 두므로 그 뒤에 객체가 바뀌어도 묶음에 들어간 내용은 그대로다.
        /// </summary>
        private sealed class Batch : ISaveBatch
        {
            private readonly FileSaveStore _store;
            private readonly string _slot;
            private readonly List<KeyValuePair<string, string>> _writes = new List<KeyValuePair<string, string>>();
            private readonly List<string> _deletes = new List<string>();
            private bool _done;

            public Batch(FileSaveStore store, string slot)
            {
                _store = store;
                _slot = slot;
            }

            public bool TryRead<T>(string saveSlot, string fileName, out T data) where T : class, new()
            {
                if (saveSlot == _slot)
                {
                    int index = IndexOf(fileName);
                    if (index >= 0)
                    {
                        data = Parse<T>(_writes[index].Value, _store.PathFor(saveSlot, fileName));
                        return true;
                    }

                    if (_deletes.Contains(fileName))
                    {
                        data = null;
                        return false;
                    }
                }

                return _store.TryRead(saveSlot, fileName, out data);
            }

            public void Write<T>(string saveSlot, string fileName, T data) where T : class
            {
                if (data == null) throw new ArgumentNullException(nameof(data));
                Check(saveSlot, fileName);
                var json = Serialize(data);
                _deletes.Remove(fileName);
                int index = IndexOf(fileName);
                if (index >= 0) _writes[index] = new KeyValuePair<string, string>(fileName, json);
                else _writes.Add(new KeyValuePair<string, string>(fileName, json));
            }

            public void Delete(string saveSlot, string fileName)
            {
                Check(saveSlot, fileName);
                int index = IndexOf(fileName);
                if (index >= 0) _writes.RemoveAt(index);
                if (!_deletes.Contains(fileName)) _deletes.Add(fileName);
            }

            public void Commit()
            {
                if (_done) throw new InvalidOperationException("이미 끝난 묶음이다");
                _done = true;
                if (_writes.Count == 0 && _deletes.Count == 0) return;
                _store.CommitBatch(_slot, _writes, _deletes);
            }

            public void Dispose() => _done = true;

            private void Check(string saveSlot, string fileName)
            {
                if (_done) throw new InvalidOperationException("이미 끝난 묶음이다");
                if (saveSlot != _slot) throw new ArgumentException($"묶음은 슬롯 '{_slot}'에만 쓴다: {saveSlot}/{fileName}", nameof(saveSlot));
                _store.PathFor(saveSlot, fileName);
                if (fileName == BatchFolder || fileName == BatchMark) throw new ArgumentException("묶음이 쓰는 이름이다: " + fileName, nameof(fileName));
            }

            private int IndexOf(string fileName)
            {
                for (int i = 0; i < _writes.Count; i++) if (_writes[i].Key == fileName) return i;
                return -1;
            }
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
