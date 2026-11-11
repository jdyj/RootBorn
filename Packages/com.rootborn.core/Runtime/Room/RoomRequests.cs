using System;
using System.Collections.Generic;
using Rootborn.Core.Save;

namespace Rootborn.Core.Room
{
    /// <summary>
    /// 공유 조각에 보내는 요청 하나(모든 조각이 같은 모양을 쓴다 — 조각마다 Kind·Amount·Ids의 뜻을 정한다).
    /// RequestId는 보내는 쪽이 만든 겹치지 않는 id다. 방은 같은 id를 한 번만 반영하고 같은 답을 다시 준다(<see cref="RoomRequestLedger"/>).
    /// </summary>
    [Serializable]
    public sealed class RoomRequestSaveData
    {
        public string Piece;
        public string RequestId;
        public string Kind;
        public int Amount;
        public string[] Ids = Array.Empty<string>();

        public RoomRequestSaveData Copy() => new RoomRequestSaveData
        {
            Piece = Piece, RequestId = RequestId, Kind = Kind, Amount = Amount, Ids = Ids == null ? Array.Empty<string>() : (string[])Ids.Clone(),
        };
    }

    /// <summary>방이 요청 하나에 준 답. Outcome은 <see cref="RoomOutcomes"/>의 코드다. Amount는 조각이 정한 값(지갑은 그 뒤 잔액).</summary>
    public readonly struct RoomAnswer
    {
        public RoomAnswer(string piece, string requestId, string outcome, int amount)
        {
            Piece = piece ?? string.Empty;
            RequestId = requestId ?? string.Empty;
            Outcome = outcome ?? RoomOutcomes.Refused;
            Amount = amount;
        }

        public string Piece { get; }
        public string RequestId { get; }
        public string Outcome { get; }
        public int Amount { get; }
        public bool Accepted => Outcome != RoomOutcomes.Refused && Outcome != RoomOutcomes.Invalid;
    }

    /// <summary>답 코드. 한 번 쓴 코드는 뜻을 바꾸지 않는다.</summary>
    public static class RoomOutcomes
    {
        /// <summary>반영했다.</summary>
        public const string Applied = "applied";
        /// <summary>반영하지 않았다(돈이 모자람 등). 방 상태는 그대로다.</summary>
        public const string Refused = "refused";
        /// <summary>읽을 수 없는 요청(모르는 조각·종류, 음수 등). 방 상태는 그대로다.</summary>
        public const string Invalid = "invalid";
    }

    /// <summary>공유 조각 하나의 지금 상태(모두에게 보낸다). 조각마다 Amount·Ids의 뜻을 정한다(지갑: 잔액, 마을 상태: 켜진 깃발).</summary>
    public readonly struct RoomPieceState
    {
        public RoomPieceState(string piece, int amount, string[] ids)
        {
            Piece = piece ?? string.Empty;
            Amount = amount;
            Ids = ids ?? Array.Empty<string>();
        }

        public string Piece { get; }
        public int Amount { get; }
        public string[] Ids { get; }
    }

    /// <summary>
    /// 방이 답한 요청 장부. 같은 요청 id가 다시 오면(답이 사라져 다시 보냄, 저장을 다시 받음) 반영하지 않고 처음 답을 준다.
    /// 다른 플레이어가 남의 요청 id를 쓰면 반영하지 않는다(<see cref="RoomOutcomes.Invalid"/>).
    /// 오래된 것부터 <see cref="Capacity"/>개만 둔다: 보내는 쪽은 답을 받으면 그 요청을 지우므로, 답을 받기 전의 요청이 그만큼 밀려나는 일은 없다고 본다.
    /// </summary>
    public sealed class RoomRequestLedger
    {
        public const int Capacity = 2048;

        private readonly Dictionary<string, Entry> _byId = new Dictionary<string, Entry>(64, StringComparer.Ordinal);
        private readonly Queue<string> _order = new Queue<string>(64);

        public int Count => _byId.Count;

        /// <returns>이 id를 이미 답했으면 true와 그 답(owner가 다르면 Invalid).</returns>
        public bool TryGet(string playerId, string requestId, out RoomAnswer answer)
        {
            answer = default;
            if (string.IsNullOrEmpty(requestId) || !_byId.TryGetValue(requestId, out var entry)) return false;
            answer = string.Equals(entry.PlayerId, playerId, StringComparison.Ordinal)
                ? entry.Answer
                : new RoomAnswer(entry.Answer.Piece, requestId, RoomOutcomes.Invalid, 0);
            return true;
        }

        public void Record(string playerId, RoomAnswer answer)
        {
            if (string.IsNullOrEmpty(answer.RequestId) || _byId.ContainsKey(answer.RequestId)) return;
            _byId[answer.RequestId] = new Entry(playerId ?? string.Empty, answer);
            _order.Enqueue(answer.RequestId);
            while (_order.Count > Capacity) _byId.Remove(_order.Dequeue());
        }

        public RoomRequestLedgerSaveData ToSaveData()
        {
            var records = new RoomRequestRecordSaveData[_order.Count];
            int i = 0;
            foreach (var id in _order)
            {
                var e = _byId[id];
                records[i++] = new RoomRequestRecordSaveData
                {
                    PlayerId = e.PlayerId, Piece = e.Answer.Piece, RequestId = id, Outcome = e.Answer.Outcome, Amount = e.Answer.Amount,
                };
            }

            return new RoomRequestLedgerSaveData { Version = RoomRequestLedgerSaveData.KnownVersion, Records = records };
        }

        public static RoomRequestLedger FromSaveData(RoomRequestLedgerSaveData data)
        {
            var ledger = new RoomRequestLedger();
            if (data?.Records == null) return ledger;
            foreach (var r in data.Records)
                if (r != null) ledger.Record(r.PlayerId, new RoomAnswer(r.Piece, r.RequestId, r.Outcome, r.Amount));
            return ledger;
        }

        private readonly struct Entry
        {
            public Entry(string playerId, RoomAnswer answer)
            {
                PlayerId = playerId;
                Answer = answer;
            }

            public string PlayerId { get; }
            public RoomAnswer Answer { get; }
        }
    }

    [Serializable]
    public sealed class RoomRequestLedgerSaveData : IVersionedSave
    {
        public const int KnownVersion = 1;

        public int Version;
        public RoomRequestRecordSaveData[] Records = Array.Empty<RoomRequestRecordSaveData>();

        int IVersionedSave.Version { get => Version; set => Version = value; }
        int IVersionedSave.KnownVersion => KnownVersion;
    }

    [Serializable]
    public sealed class RoomRequestRecordSaveData
    {
        public string PlayerId;
        public string Piece;
        public string RequestId;
        public string Outcome;
        public int Amount;
    }

    /// <summary>
    /// 보내는 쪽(같이 하는 방의 플레이어)이 방에 보냈고 아직 답을 받지 않은 요청들. 그 플레이어의 세이브에 같이 저장된다
    /// (<see cref="SaveFileNames.RoomOutbox"/>): 방은 저장을 받을 때 여기 든 요청을 반영하므로 개인 진행과 방의 것이 한 번에 바뀐다.
    /// 끊겼다 다시 붙어도, 꺼졌다 켜져도 답을 받을 때까지 남아 있다가 다시 간다(같은 id라 한 번만 반영된다).
    /// </summary>
    public sealed class RoomOutbox
    {
        private readonly List<RoomRequestSaveData> _requests = new List<RoomRequestSaveData>(8);
        private readonly Func<string> _newId;

        /// <param name="newId">새 요청 id(겹치지 않는 값). 없으면 GUID.</param>
        public RoomOutbox(Func<string> newId = null)
        {
            _newId = newId ?? (() => Guid.NewGuid().ToString("N"));
        }

        public IReadOnlyList<RoomRequestSaveData> Requests => _requests;

        public string NewId() => _newId();

        public void Add(RoomRequestSaveData request)
        {
            if (request == null || string.IsNullOrEmpty(request.RequestId)) throw new ArgumentException("요청 id가 없다", nameof(request));
            if (Find(request.RequestId) >= 0) return;
            _requests.Add(request);
        }

        public bool Contains(string requestId) => Find(requestId) >= 0;

        /// <summary>답을 받았다: 그 요청을 지운다. 없던 요청이면 false.</summary>
        public bool Answered(string requestId)
        {
            int index = Find(requestId);
            if (index < 0) return false;
            _requests.RemoveAt(index);
            return true;
        }

        public RoomOutboxSaveData ToSaveData()
        {
            var copy = new RoomRequestSaveData[_requests.Count];
            for (int i = 0; i < copy.Length; i++) copy[i] = _requests[i].Copy();
            return new RoomOutboxSaveData { Version = RoomOutboxSaveData.KnownVersion, Requests = copy };
        }

        public void LoadFromSaveData(RoomOutboxSaveData data)
        {
            _requests.Clear();
            if (data?.Requests == null) return;
            foreach (var r in data.Requests)
                if (r != null && !string.IsNullOrEmpty(r.RequestId) && Find(r.RequestId) < 0) _requests.Add(r.Copy());
        }

        private int Find(string requestId)
        {
            for (int i = 0; i < _requests.Count; i++)
                if (string.Equals(_requests[i].RequestId, requestId, StringComparison.Ordinal)) return i;
            return -1;
        }
    }

    [Serializable]
    public sealed class RoomOutboxSaveData : IVersionedSave
    {
        public const int KnownVersion = 1;

        public int Version;
        public RoomRequestSaveData[] Requests = Array.Empty<RoomRequestSaveData>();

        int IVersionedSave.Version { get => Version; set => Version = value; }
        int IVersionedSave.KnownVersion => KnownVersion;
    }
}
