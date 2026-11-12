using System;
using System.Collections.Generic;
using Rootborn.Core.Save;

namespace Rootborn.Data.Save
{
    /// <summary>세이브 모양 ↔ JSON 원문. 파일 저장소(<see cref="FileSaveStore"/>)와 같은 형식이다(같은 옵션, 더 새 프로그램을 요구하는 파일은 읽지 않는다).</summary>
    public static class SaveJson
    {
        public static string Serialize<T>(T data) where T : class
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            return FileSaveStore.Serialize(data);
        }

        /// <param name="label">오류에 적을 이름(파일 이름 등).</param>
        /// <exception cref="SaveFileCorruptException">비었거나 JSON이 깨졌다.</exception>
        /// <exception cref="SaveFileTooNewException">파일이 더 새 프로그램을 요구한다(ReadableFrom).</exception>
        public static T Parse<T>(string text, string label) where T : class, new() => FileSaveStore.Parse<T>(text, label);
    }

    /// <summary>한 번의 저장으로 바뀐 파일들(원문). 쓰기는 이름 → JSON, 지우기는 이름.</summary>
    public sealed class SaveFileChanges
    {
        public SaveFileChanges(IReadOnlyList<KeyValuePair<string, string>> writes, IReadOnlyList<string> deletes)
        {
            Writes = writes ?? Array.Empty<KeyValuePair<string, string>>();
            Deletes = deletes ?? Array.Empty<string>();
        }

        public IReadOnlyList<KeyValuePair<string, string>> Writes { get; }
        public IReadOnlyList<string> Deletes { get; }
        public bool IsEmpty => Writes.Count == 0 && Deletes.Count == 0;
    }

    /// <summary>
    /// 슬롯 하나를 메모리에 든 원문 파일로 갖는 저장소(멀티플레이 설계 3.1절 "손님의 저장이 호스트로 가는 길").
    /// 같이 하는 방의 플레이어는 이 저장소로 게임 한 판을 돌린다: 처음 파일은 방이 내려 준 것이고, 저장할 때마다(묶음 Commit 또는 바로 쓰기)
    /// <see cref="Committed"/>로 바뀐 파일을 알린다 — 방 접속이 그것을 방으로 보낸다. 방도 받은 저장을 검사할 때 이것을 쓴다.
    /// 읽기는 파일 저장소와 같은 형식 검사를 한다(깨진 파일·더 새 프로그램을 요구하는 파일은 예외). 쓸 때 모르는 필드를 지키는 것도 같다. 다른 슬롯은 없는 것으로 읽고 쓰지 않는다.
    /// </summary>
    public sealed class MemorySaveSlot : IBatchSaveStore
    {
        private readonly Dictionary<string, string> _files = new Dictionary<string, string>(StringComparer.Ordinal);

        // 이 프로그램이 모르는 필드가 없다고 확인된 파일. 쓸 때 옛 내용과 합치지 않는다(파일 저장소와 같은 규칙).
        private readonly HashSet<string> _nothingUnknown = new HashSet<string>(StringComparer.Ordinal);

        public MemorySaveSlot(string saveSlot, IEnumerable<KeyValuePair<string, string>> files = null)
        {
            if (string.IsNullOrWhiteSpace(saveSlot)) throw new ArgumentException("슬롯이 비어 있다", nameof(saveSlot));
            Slot = saveSlot;
            if (files != null)
                foreach (var pair in files)
                    if (!string.IsNullOrEmpty(pair.Key)) _files[pair.Key] = pair.Value ?? string.Empty;
        }

        public string Slot { get; }

        public IReadOnlyDictionary<string, string> Files => _files;

        /// <summary>한 번의 저장이 끝났다(바뀐 파일). 듣는 쪽이 던지면 그 예외가 저장한 쪽으로 간다.</summary>
        public event Action<SaveFileChanges> Committed;

        public long TotalBytes
        {
            get
            {
                long sum = 0;
                foreach (var pair in _files) sum += System.Text.Encoding.UTF8.GetByteCount(pair.Value) + pair.Key.Length;
                return sum;
            }
        }

        /// <summary>바뀐 파일들을 그대로 적용한다(알리지 않는다). 방이 받은 저장을 겹쳐 볼 때 쓴다.</summary>
        public void Apply(SaveFileChanges changes)
        {
            if (changes == null) return;
            foreach (var pair in changes.Writes)
            {
                _files[pair.Key] = pair.Value ?? string.Empty;
                _nothingUnknown.Remove(pair.Key); // 밖에서 온 내용은 무엇이 들었는지 모른다
            }

            foreach (var name in changes.Deletes) _files.Remove(name);
        }

        // 지금 있는 내용의 모르는 필드를 새 내용에 옮겨 넣는다.
        private string WithUnknownKept(string fileName, string json, Type shapeType)
        {
            if (!_files.TryGetValue(fileName, out var existing))
            {
                _nothingUnknown.Add(fileName);
                return json;
            }

            if (_nothingUnknown.Contains(fileName)) return json;
            string merged = FileSaveStore.KeepUnknown(json, existing, shapeType);
            if (ReferenceEquals(merged, json)) _nothingUnknown.Add(fileName);
            return merged;
        }

        public bool TryRead<T>(string saveSlot, string fileName, out T data) where T : class, new()
        {
            data = null;
            if (saveSlot != Slot || fileName == null || !_files.TryGetValue(fileName, out var text)) return false;
            data = FileSaveStore.Parse<T>(text, saveSlot + "/" + fileName, out bool nothingUnknown);
            if (nothingUnknown) _nothingUnknown.Add(fileName);
            else _nothingUnknown.Remove(fileName);
            return true;
        }

        public void Write<T>(string saveSlot, string fileName, T data) where T : class
        {
            Check(saveSlot, fileName);
            string json = WithUnknownKept(fileName, SaveJson.Serialize(data), typeof(T));
            _files[fileName] = json;
            Committed?.Invoke(new SaveFileChanges(new[] { new KeyValuePair<string, string>(fileName, json) }, null));
        }

        public void Delete(string saveSlot, string fileName)
        {
            Check(saveSlot, fileName);
            if (!_files.Remove(fileName)) return;
            Committed?.Invoke(new SaveFileChanges(null, new[] { fileName }));
        }

        public ISaveBatch BeginBatch(string saveSlot)
        {
            Check(saveSlot, "batch");
            return new Batch(this);
        }

        private void Check(string saveSlot, string fileName)
        {
            if (saveSlot != Slot) throw new ArgumentException($"이 저장소는 슬롯 '{Slot}'만 쓴다: {saveSlot}/{fileName}", nameof(saveSlot));
            if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentException("파일 이름이 비어 있다", nameof(fileName));
        }

        private sealed class Batch : ISaveBatch
        {
            private readonly MemorySaveSlot _owner;
            private readonly List<KeyValuePair<string, string>> _writes = new List<KeyValuePair<string, string>>();
            private readonly List<string> _deletes = new List<string>();
            private bool _done;

            public Batch(MemorySaveSlot owner) => _owner = owner;

            public bool TryRead<T>(string saveSlot, string fileName, out T data) where T : class, new()
            {
                if (saveSlot == _owner.Slot)
                {
                    int index = IndexOf(fileName);
                    if (index >= 0)
                    {
                        data = SaveJson.Parse<T>(_writes[index].Value, saveSlot + "/" + fileName);
                        return true;
                    }

                    if (_deletes.Contains(fileName))
                    {
                        data = null;
                        return false;
                    }
                }

                return _owner.TryRead(saveSlot, fileName, out data);
            }

            public void Write<T>(string saveSlot, string fileName, T data) where T : class
            {
                if (_done) throw new InvalidOperationException("이미 끝난 묶음이다");
                _owner.Check(saveSlot, fileName);
                var json = _owner.WithUnknownKept(fileName, SaveJson.Serialize(data), typeof(T));
                _deletes.Remove(fileName);
                int index = IndexOf(fileName);
                if (index >= 0) _writes[index] = new KeyValuePair<string, string>(fileName, json);
                else _writes.Add(new KeyValuePair<string, string>(fileName, json));
            }

            public void Delete(string saveSlot, string fileName)
            {
                if (_done) throw new InvalidOperationException("이미 끝난 묶음이다");
                _owner.Check(saveSlot, fileName);
                int index = IndexOf(fileName);
                if (index >= 0) _writes.RemoveAt(index);
                if (!_deletes.Contains(fileName)) _deletes.Add(fileName);
            }

            public void Commit()
            {
                if (_done) throw new InvalidOperationException("이미 끝난 묶음이다");
                _done = true;
                if (_writes.Count == 0 && _deletes.Count == 0) return;
                var changes = new SaveFileChanges(_writes.ToArray(), _deletes.ToArray());
                foreach (var pair in changes.Writes) _owner._files[pair.Key] = pair.Value;
                foreach (var name in changes.Deletes) _owner._files.Remove(name);
                _owner.Committed?.Invoke(changes);
            }

            public void Dispose() => _done = true;

            private int IndexOf(string fileName)
            {
                for (int i = 0; i < _writes.Count; i++) if (_writes[i].Key == fileName) return i;
                return -1;
            }
        }
    }
}
