using System;
using System.Collections.Generic;
using System.IO;
using Rootborn.Core.Room;
using Rootborn.Core.Save;
using Rootborn.Data;
using Rootborn.Data.Save;
using Rootborn.Game;
using Rootborn.Net.Room;

namespace Rootborn.Server
{
    /// <summary>
    /// 방의 손님 세이브(설계 3절·5절): &lt;세이브 폴더&gt;/&lt;방 슬롯&gt;/guests/&lt;플레이어 id&gt;/에 그 사람의 세이브 파일을 둔다.
    /// 사람마다 폴더를 따로 두는 것은 슬롯 공용 이름의 파일(집, 수첩 추적, 마을 상태 사본)이 사람끼리 겹치지 않게 하기 위해서다 — 집·마을 상태를
    /// 같이 쓰지 않는 방에서는 그것이 각자의 것이다. 방의 파일(시계·설정·조각)은 방 슬롯 폴더 맨 위에 있다.
    /// 받은 저장의 검사(<see cref="Accept"/>): 플레이어 id와 파일 이름이 경로 한 칸인지, 크기 한도(<see cref="RoomTransfer.MaxSaveBytes"/>),
    /// 그리고 옛 세이브에 받은 것을 겹친 세이브로 게임 한 판을 시작할 수 있는지(손상 슬롯 검사와 같은 규칙 — 깨진 파일, 더 새 형식 버전,
    /// 다른 플레이어의 진행, 스냅샷이 빠짐). 하나라도 걸리면 쓰지 않는다(옛 세이브를 지킨다).
    /// </summary>
    public sealed class GuestSaveFolder : IGuestSaves
    {
        public const string FolderName = "guests";

        private readonly FileSaveStore _store;
        private readonly GameDefinitions _definitions;
        private readonly GameStartSettings _settings;
        private readonly Action<string> _log;

        public GuestSaveFolder(string guestsRoot, GameDefinitions definitions, GameStartSettings settings, Action<string> log = null)
        {
            if (string.IsNullOrWhiteSpace(guestsRoot)) throw new ArgumentException("손님 세이브 폴더가 비어 있다", nameof(guestsRoot));
            _store = new FileSaveStore(guestsRoot);
            _definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _log = log;
        }

        /// <summary>방 슬롯의 손님 세이브 폴더.</summary>
        public static string FolderFor(string saveRoot, string roomSlot) => Path.Combine(saveRoot, roomSlot, FolderName);

        public string Root => _store.RootDirectory;

        public SaveFileSet Read(string playerId)
        {
            var set = new SaveFileSet();
            if (!SaveSlotMetadata.IsSafeSlotId(playerId)) return set;
            foreach (var pair in _store.ReadSlotFiles(playerId)) set.Writes.Add(pair);
            return set;
        }

        public string Accept(string playerId, SaveFileSet changes, out IReadOnlyList<RoomRequestSaveData> requests)
        {
            requests = Array.Empty<RoomRequestSaveData>();
            if (changes == null) return GuestSaveRefusal.Malformed;
            if (!SaveSlotMetadata.IsSafeSlotId(playerId)) return GuestSaveRefusal.BadFileName;
            foreach (var pair in changes.Writes) if (!IsSaveFileName(pair.Key)) return GuestSaveRefusal.BadFileName;
            foreach (var name in changes.Deletes) if (!IsSaveFileName(name)) return GuestSaveRefusal.BadFileName;

            var overlay = new MemorySaveSlot(RoomGuestLink.Slot, _store.ReadSlotFiles(playerId));
            overlay.Apply(new SaveFileChanges(changes.Writes, changes.Deletes));
            if (overlay.TotalBytes > RoomTransfer.MaxSaveBytes) return GuestSaveRefusal.TooLarge;

            RoomOutboxSaveData outbox;
            try
            {
                GameSession.Start(_definitions, _settings, overlay, RoomGuestLink.Slot, playerId);
                overlay.TryRead(RoomGuestLink.Slot, SaveFileNames.RoomOutbox(playerId), out outbox);
            }
            catch (Exception e) when (e is IOException || e is ArgumentException || e is InvalidOperationException || e is FormatException)
            {
                _log?.Invoke($"손님 저장 검사 실패: {playerId} — {e.Message}");
                return GuestSaveRefusal.Damaged;
            }

            _store.WriteSlotFiles(playerId, changes.Writes, changes.Deletes);
            if (outbox?.Requests != null) requests = outbox.Requests;
            return null;
        }

        public IReadOnlyList<RoomRequestSaveData> PendingRequests(string playerId)
        {
            if (!SaveSlotMetadata.IsSafeSlotId(playerId)) return Array.Empty<RoomRequestSaveData>();
            try
            {
                return _store.TryRead(playerId, SaveFileNames.RoomOutbox(playerId), out RoomOutboxSaveData outbox) && outbox.Requests != null
                    ? outbox.Requests
                    : (IReadOnlyList<RoomRequestSaveData>)Array.Empty<RoomRequestSaveData>();
            }
            catch (IOException)
            {
                return Array.Empty<RoomRequestSaveData>();
            }
        }

        public void KeepDayCopies(int day, int keep)
        {
            if (keep <= 0 || !Directory.Exists(_store.RootDirectory)) return;
            foreach (var dir in Directory.GetDirectories(_store.RootDirectory))
            {
                var playerId = Path.GetFileName(dir);
                if (SaveSlotMetadata.IsSafeSlotId(playerId)) _store.KeepDayCopy(playerId, day, keep);
            }
        }

        // 세이브 파일 이름: 경로 한 칸, '.'으로 시작하지 않음(임시·묶음), 사본 폴더 이름이 아님, .json.
        private static bool IsSaveFileName(string name)
        {
            if (string.IsNullOrEmpty(name) || name.StartsWith(".", StringComparison.Ordinal) || name == FileSaveStore.DayCopiesFolder) return false;
            if (!name.EndsWith(".json", StringComparison.Ordinal) || name.Contains("..") || name.IndexOfAny(new[] { '/', '\\', ':' }) >= 0) return false;
            return name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
        }
    }
}
