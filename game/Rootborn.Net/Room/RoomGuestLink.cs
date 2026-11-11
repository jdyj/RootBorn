using System;
using System.Collections.Generic;
using Rootborn.Core.Room;
using Rootborn.Net.Session;

namespace Rootborn.Net.Room
{
    /// <summary>
    /// 방에 보냈고 아직 방이 받았다고 답하지 않은 저장들(설계 3.1절). 접속보다 오래 산다: 끊겼다 다시 붙으면 새 접속(<see cref="RoomGuestLink"/>)이
    /// 이것을 받아 다시 보낸다. 같은 인물이 같은 방에 다시 들어갈 때 같은 것을 넘긴다(Host가 들고 있다).
    /// </summary>
    public sealed class GuestUploads
    {
        private readonly List<(uint Id, SaveFileSet Changes)> _pending = new List<(uint, SaveFileSet)>(4);
        private uint _nextId = 1;

        public int Count => _pending.Count;

        /// <summary>보낼 저장 하나를 적고 그 묶음 id를 준다.</summary>
        internal uint Add(SaveFileSet changes)
        {
            uint id = _nextId++;
            _pending.Add((id, changes));
            return id;
        }

        internal bool Acknowledge(uint id)
        {
            for (int i = 0; i < _pending.Count; i++)
            {
                if (_pending[i].Id != id) continue;
                _pending.RemoveAt(i);
                return true;
            }

            return false;
        }

        internal IReadOnlyList<(uint Id, SaveFileSet Changes)> Pending => _pending;

        /// <summary>아직 답이 없는 저장을 차례로 겹친 것(방이 다 받으면 방의 세이브가 이렇게 된다).</summary>
        public SaveFileSet Merged()
        {
            var merged = new SaveFileSet();
            foreach (var (_, changes) in _pending) merged.Overlay(changes);
            return merged;
        }
    }

    /// <summary>
    /// 방에 들어간 플레이어 쪽의 손님 저장과 공유 조각(설계 3.1절·3.2절). 세션 클라이언트에 메시지 처리기를 붙인다.
    /// - 들어오면 방 설정을 받아 조각 묶음(<see cref="Shares"/>)을 만들고, 그 사람의 세이브(<see cref="Files"/>)를 받는다. 둘 다 받으면 <see cref="Ready"/>:
    ///   게임 한 판을 그 파일로 시작한다(처음 온 사람이면 빈 묶음 — 새 게임). 받은 세이브 위에는 아직 답이 없는 저장(<see cref="GuestUploads"/>)을 겹친다.
    /// - 저장할 때마다 <see cref="Upload"/>로 바뀐 파일을 나눠 보낸다(<see cref="RoomTransfer"/>). 방이 받았다고 답할 때까지 들고 있다가, 다시 붙으면 다시 보낸다.
    /// - 조각 상태와 요청의 답은 조각 묶음에 넘긴다. 방이 저장을 거절하면 <see cref="UploadRefused"/>(이유 코드).
    /// 시간은 세션 클라이언트의 Update가 정한다(이 객체는 그 안에서 불린다).
    /// </summary>
    public sealed class RoomGuestLink
    {
        /// <summary>손님의 게임 한 판이 쓰는 세이브 슬롯 이름(방에 있는 그 사람의 세이브 폴더 하나). 방의 슬롯 id와 무관하다.</summary>
        public const string Slot = "room-guest";

        private readonly SessionClient _client;
        private readonly RoomPieceCatalog _catalog;
        private readonly Func<string> _newRequestId;
        private readonly ChunkAssembler _download = new ChunkAssembler();
        private readonly int _maxMessageBytes;
        private readonly HashSet<string> _sent = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<RoomRequestSaveData> _toSend = new List<RoomRequestSaveData>(4);

        /// <param name="uploads">끊겼다 다시 붙을 때 이어서 보낼 저장. 없으면 새로 만든다.</param>
        /// <param name="newRequestId">공유 조각 요청 id를 만든다. 없으면 GUID.</param>
        public RoomGuestLink(SessionClient client, int maxMessageBytes, GuestUploads uploads = null, RoomPieceCatalog catalog = null, Func<string> newRequestId = null)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _maxMessageBytes = maxMessageBytes;
            Uploads = uploads ?? new GuestUploads();
            _catalog = catalog ?? RoomPieceCatalog.CreateDefault();
            _newRequestId = newRequestId;
            _client.On<RoomSettingsMessage>(OnSettings)
                .On<SaveChunkMessage>(OnChunk)
                .On<SaveTransferAnswerMessage>(OnTransferAnswer)
                .On<RoomPieceStateMessage>(OnState)
                .On<RoomAnswerMessage>(OnAnswer);
        }

        public GuestUploads Uploads { get; }

        /// <summary>방 설정(받기 전에는 null).</summary>
        public RoomShareSettings Settings { get; private set; }

        /// <summary>플레이어 쪽 조각 묶음(방 설정을 받으면 만든다). 게임 한 판에 넘긴다.</summary>
        public RoomShares Shares { get; private set; }

        /// <summary>그 사람의 세이브(받은 것 + 아직 답이 없는 저장). 다 받기 전에는 null.</summary>
        public SaveFileSet Files { get; private set; }

        /// <summary>이 방이 손님 저장을 받는다(받지 않는 방이면 세이브가 오지 않고 Ready는 방 설정만 기다린다 — 그때는 자기 PC에 저장한다).</summary>
        public bool TakesGuestSaves { get; private set; }

        public bool Ready => Settings != null && (!TakesGuestSaves || Files != null);

        /// <summary>방이 마지막으로 거절한 저장의 이유 코드(없으면 빈 글자).</summary>
        public string LastRefusal { get; private set; } = string.Empty;

        public event Action<string> UploadRefused;

        /// <summary>
        /// 저장 한 번에 바뀐 파일을 방으로 보낸다(들어가 있지 않으면 적어 두었다가 다시 붙을 때 보낸다). 크기 한도를 넘으면 보내지 않고 false.
        /// </summary>
        public bool Upload(SaveFileSet changes)
        {
            if (changes == null || changes.IsEmpty) return true;
            if (Settings != null && !TakesGuestSaves) return false;
            if (!RoomTransfer.TryEncode(changes, out _)) return false;
            uint id = Uploads.Add(changes);
            if (Ready) Send(id, changes);
            return true;
        }

        /// <summary>
        /// 손님 저장을 받지 않는 방: 보낼 목록(Shares.Outbox)에서 이 접속으로 아직 보내지 않은 요청을 방으로 보낸다. 게임 한 판이 그 요청을 자기 세이브에
        /// 쓴 뒤에 부른다(Host는 프레임마다 — 명령은 그 안에서 저장까지 끝낸다). 답을 받으면 목록에서 빠진다. 다시 붙으면(새 접속) 다시 보낸다 — 같은 id라
        /// 방은 한 번만 반영한다. 받는 방이거나 아직 들어가지 않았으면 아무것도 하지 않는다. 보낼 것이 없으면 할당하지 않는다.
        /// </summary>
        /// <returns>이번에 보낸 요청 수.</returns>
        public int FlushRequests()
        {
            if (!Ready || TakesGuestSaves || Shares == null) return 0;
            var requests = Shares.Outbox.Requests;
            if (requests.Count == 0)
            {
                if (_sent.Count > 0) _sent.Clear();
                return 0;
            }

            _toSend.Clear();
            for (int i = 0; i < requests.Count; i++)
                if (!_sent.Contains(requests[i].RequestId)) _toSend.Add(requests[i]);
            if (_toSend.Count == 0) return 0;
            if (_sent.Count > requests.Count * 2) Forget(requests);
            int sent = 0;
            for (int start = 0; start < _toSend.Count; start += RoomShareMessageBodies.MaxRequests)
            {
                int count = Math.Min(RoomShareMessageBodies.MaxRequests, _toSend.Count - start);
                var batch = new RoomRequestSaveData[count];
                for (int i = 0; i < count; i++)
                {
                    batch[i] = _toSend[start + i];
                    _sent.Add(batch[i].RequestId);
                }

                _client.Send(new RoomRequestsMessage { Requests = batch });
                sent += count;
            }

            _toSend.Clear();
            return sent;
        }

        // 답을 받아 목록에서 빠진 요청은 잊는다(같은 id가 다시 적히면 다시 보낸다 — 방은 같은 답을 준다).
        private void Forget(IReadOnlyList<RoomRequestSaveData> requests)
        {
            var keep = new HashSet<string>(StringComparer.Ordinal);
            foreach (var r in requests) if (_sent.Contains(r.RequestId)) keep.Add(r.RequestId);
            _sent.Clear();
            foreach (var id in keep) _sent.Add(id);
        }

        private void OnSettings(RoomSettingsMessage m)
        {
            if (Settings != null) return;
            TakesGuestSaves = m.GuestSaves;
            Settings = new RoomShareSettings(m.SharedPieces);
            Shares = new RoomShares(Settings, _catalog, _newRequestId);
        }

        private void OnChunk(SaveChunkMessage chunk)
        {
            if (Files != null || !_download.Add(chunk, out _, out var payload)) return;
            if (!RoomTransfer.TryDecode(payload, out var files))
            {
                LastRefusal = GuestSaveRefusal.Malformed;
                return;
            }

            files.Overlay(Uploads.Merged());
            Files = files;
            foreach (var (id, changes) in Uploads.Pending) Send(id, changes);
        }

        private void OnTransferAnswer(SaveTransferAnswerMessage m)
        {
            if (!Uploads.Acknowledge(m.TransferId) || m.Accepted) return;
            LastRefusal = m.Reason ?? string.Empty;
            UploadRefused?.Invoke(LastRefusal);
        }

        private void OnState(RoomPieceStateMessage m) => Shares?.Receive(new RoomPieceState(m.Piece, m.Amount, m.Ids));

        private void OnAnswer(RoomAnswerMessage m)
        {
            _sent.Remove(m.RequestId);
            Shares?.Receive(new RoomAnswer(m.Piece, m.RequestId, m.Outcome, m.Amount));
        }

        private void Send(uint id, SaveFileSet changes)
        {
            if (!RoomTransfer.TryEncode(changes, out var bytes)) return;
            foreach (var chunk in RoomTransfer.Split(id, bytes, _maxMessageBytes)) _client.Send(chunk);
        }
    }
}
