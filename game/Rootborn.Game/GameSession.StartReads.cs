using System;
using System.Collections.Generic;
using System.IO;
using Rootborn.Core.Save;

namespace Rootborn.Game
{
    public sealed partial class GameSession
    {
        /// <summary>
        /// 시작할 때만 쓰는 읽기 전용 저장소. 읽은 파일을 기억하고, 읽을 수 없는 파일(저장소가 IOException을 낸 것)은 문제로 모은 뒤
        /// "파일 없음"으로 돌려 나머지 영역을 계속 읽는다. 끝에 <see cref="ThrowIfDamaged"/>가 모은 문제를 한 번에 던진다.
        /// 쓰기·지우기는 받지 않는다(시작은 슬롯을 바꾸지 않는다).
        /// </summary>
        private sealed class StartReads : ISaveStore
        {
            private readonly ISaveStore _inner;
            private readonly string _slot;
            private readonly List<SaveSlotProblem> _problems = new List<SaveSlotProblem>();
            private readonly List<string> _found = new List<string>();
            private readonly HashSet<string> _unreadable = new HashSet<string>(StringComparer.Ordinal);

            public StartReads(ISaveStore inner, string slot)
            {
                _inner = inner;
                _slot = slot;
            }

            public bool TryRead<T>(string saveSlot, string fileName, out T data) where T : class, new()
            {
                data = null;
                bool ours = saveSlot == _slot;
                try
                {
                    if (!_inner.TryRead(saveSlot, fileName, out data)) return false;
                }
                catch (IOException error)
                {
                    // 같은 파일을 두 영역이 읽으면(월드 상태 사본 확인) 문제는 한 번만 적는다.
                    if (_unreadable.Add(saveSlot + "/" + fileName)) _problems.Add(new SaveSlotProblem(ours ? fileName : saveSlot + "/" + fileName, SaveSlotProblemKind.Unreadable, error));
                    data = null;
                    return false;
                }

                if (ours && !_found.Contains(fileName)) _found.Add(fileName);
                return true;
            }

            /// <summary>읽혔지만 이어 갈 수 없는 파일을 문제로 적는다(reason이 비면 아무것도 하지 않는다).</summary>
            public void Refuse(string fileName, string reason)
            {
                if (string.IsNullOrEmpty(reason) || !_unreadable.Add(_slot + "/" + fileName)) return;
                _problems.Add(new SaveSlotProblem(fileName, SaveSlotProblemKind.Unreadable, new InvalidDataException(fileName + ": " + reason)));
            }

            public void Write<T>(string saveSlot, string fileName, T data) where T : class
                => throw new InvalidOperationException("시작할 때는 세이브를 쓰지 않는다: " + saveSlot + "/" + fileName);

            public void Delete(string saveSlot, string fileName)
                => throw new InvalidOperationException("시작할 때는 세이브를 지우지 않는다: " + saveSlot + "/" + fileName);

            /// <summary>
            /// 모은 문제가 있으면 던진다. requiredSnapshot이 있으면(스냅샷을 읽지 못한 경우) 이 플레이어의 영역 파일이 하나라도 읽혔는데
            /// 옛 Unity 영역 파일이 없을 때 그 스냅샷이 빠진 것으로 본다. slotShared에 든 파일은 이 플레이어 것이 아니라 세지 않는다.
            /// </summary>
            public void ThrowIfDamaged(string requiredSnapshot, string[] slotShared, string[] preSnapshot)
            {
                if (!string.IsNullOrEmpty(requiredSnapshot) && !_unreadable.Contains(_slot + "/" + requiredSnapshot)
                    && !AnyFound(preSnapshot) && HasOwnFile(slotShared))
                    _problems.Insert(0, new SaveSlotProblem(requiredSnapshot, SaveSlotProblemKind.Missing));
                if (_problems.Count > 0) throw new SaveSlotDamagedException(_slot, _problems.ToArray(), _found.ToArray());
            }

            private bool AnyFound(string[] names)
            {
                for (int i = 0; i < names.Length; i++) if (_found.Contains(names[i])) return true;
                return false;
            }

            private bool HasOwnFile(string[] slotShared)
            {
                for (int i = 0; i < _found.Count; i++) if (Array.IndexOf(slotShared, _found[i]) < 0) return true;
                return false;
            }
        }
    }
}
