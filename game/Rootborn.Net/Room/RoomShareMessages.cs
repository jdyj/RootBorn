using System;
using System.Collections.Generic;
using System.Text;
using Rootborn.Net.Messages;

namespace Rootborn.Net.Room
{
    /// <summary>
    /// 방 → 들어온 사람: 이 방이 같이 쓰는 조각 이름(Core Room RoomShareSettings)과, 손님 저장을 받는 방인지(GuestSaves — 받으면 바로 뒤에
    /// 그 사람의 세이브가 온다). 들어오자마자 한 번 보낸다.
    /// </summary>
    public sealed class RoomSettingsMessage
    {
        public string[] SharedPieces = Array.Empty<string>();
        public bool GuestSaves;
    }

    /// <summary>
    /// 세이브 파일 묶음을 나눈 조각 하나(<see cref="RoomTransfer"/>). 방 → 들어온 사람은 그 사람의 세이브(들어올 때 한 번),
    /// 사람 → 방은 저장 한 번에 바뀐 파일이다. 같은 TransferId의 Index 0..Count-1을 차례로 모으면 묶음 하나다.
    /// </summary>
    public sealed class SaveChunkMessage
    {
        public uint TransferId;
        public ushort Index;
        public ushort Count;
        public byte[] Bytes = Array.Empty<byte>();
    }

    /// <summary>방 → 사람: 보낸 저장을 받아 썼는지(Accepted). 안 받았으면 이유 코드(<see cref="GuestSaveRefusal"/>) — 방의 세이브는 그대로다.</summary>
    public sealed class SaveTransferAnswerMessage
    {
        public uint TransferId;
        public bool Accepted;
        public string Reason = string.Empty;
    }

    /// <summary>방 → 모두: 공유 조각 하나의 지금 상태(Core Room RoomPieceState).</summary>
    public sealed class RoomPieceStateMessage
    {
        public string Piece = string.Empty;
        public int Amount;
        public string[] Ids = Array.Empty<string>();
    }

    /// <summary>방 → 보낸 사람: 요청 하나의 답(Core Room RoomAnswer).</summary>
    public sealed class RoomAnswerMessage
    {
        public string Piece = string.Empty;
        public string RequestId = string.Empty;
        public string Outcome = string.Empty;
        public int Amount;
    }

    /// <summary>방이 보낸 저장을 받지 않은 이유 코드. 한 번 쓴 코드는 뜻을 바꾸지 않는다.</summary>
    public static class GuestSaveRefusal
    {
        /// <summary>묶음이 크기 한도를 넘는다.</summary>
        public const string TooLarge = "save too large";
        /// <summary>묶음을 풀 수 없다(조각이 빠졌거나 순서가 틀림, 깨진 바이트).</summary>
        public const string Malformed = "save malformed";
        /// <summary>세이브 파일 이름이 아닌 것이 있다.</summary>
        public const string BadFileName = "save bad file name";
        /// <summary>겹쳐 본 세이브로 게임을 시작할 수 없다(깨진 파일, 더 새 형식 버전, 다른 플레이어의 진행 — 손상 슬롯 검사와 같은 규칙).</summary>
        public const string Damaged = "save damaged";
        /// <summary>이 방은 손님 저장을 받지 않는다.</summary>
        public const string NotAccepted = "save not accepted";
    }

    /// <summary>
    /// 세이브 파일 묶음(이름 → 원문, 지울 이름). 방과 사람 사이에 오가는 저장의 모양이다. 내용(JSON)은 해석하지 않는다.
    /// </summary>
    public sealed class SaveFileSet
    {
        public List<KeyValuePair<string, string>> Writes { get; } = new List<KeyValuePair<string, string>>();
        public List<string> Deletes { get; } = new List<string>();

        public bool IsEmpty => Writes.Count == 0 && Deletes.Count == 0;

        /// <summary>저장소가 알린 바뀐 파일(이름 → 원문, 지운 이름)로 만든다.</summary>
        public static SaveFileSet From(IEnumerable<KeyValuePair<string, string>> writes, IEnumerable<string> deletes)
        {
            var set = new SaveFileSet();
            if (writes != null) set.Writes.AddRange(writes);
            if (deletes != null) set.Deletes.AddRange(deletes);
            return set;
        }

        /// <summary>뒤에 온 묶음을 겹친다(같은 이름은 뒤의 것, 지운 것은 쓰기에서 뺀다).</summary>
        public void Overlay(SaveFileSet later)
        {
            if (later == null) return;
            foreach (var pair in later.Writes)
            {
                Deletes.Remove(pair.Key);
                int i = IndexOf(pair.Key);
                if (i >= 0) Writes[i] = pair;
                else Writes.Add(pair);
            }

            foreach (var name in later.Deletes)
            {
                int i = IndexOf(name);
                if (i >= 0) Writes.RemoveAt(i);
                if (!Deletes.Contains(name)) Deletes.Add(name);
            }
        }

        public long ByteCount
        {
            get
            {
                long sum = 0;
                foreach (var pair in Writes) sum += Encoding.UTF8.GetByteCount(pair.Key) + Encoding.UTF8.GetByteCount(pair.Value ?? string.Empty);
                foreach (var name in Deletes) sum += Encoding.UTF8.GetByteCount(name);
                return sum;
            }
        }

        private int IndexOf(string name)
        {
            for (int i = 0; i < Writes.Count; i++) if (Writes[i].Key == name) return i;
            return -1;
        }
    }

    /// <summary>
    /// 세이브 묶음 나눠 보내기와 크기 한도 — 한 곳(설계 7절 4단계). 메시지 하나는 <c>MaxMessageBytes</c>를 넘지 못하므로 묶음을 바이트로 적고
    /// 조각(<see cref="SaveChunkMessage"/>)으로 나눈다. 신뢰 순서 전송으로 보내므로 받는 쪽은 차례로 모으기만 한다(<see cref="ChunkAssembler"/>).
    /// </summary>
    public static class RoomTransfer
    {
        /// <summary>묶음 하나(저장 한 번, 들어올 때 내려 주는 세이브)의 최대 바이트. 지금 세이브 한 슬롯은 수십 KB다.</summary>
        public const int MaxSaveBytes = 4 * 1024 * 1024;

        /// <summary>파일 이름 하나의 최대 바이트.</summary>
        public const int MaxFileNameBytes = 128;

        /// <summary>묶음 하나의 최대 파일 수.</summary>
        public const int MaxFiles = 512;

        // 조각 메시지의 머리(종류·버전 3 + 묶음 id 4 + 번호 2 + 개수 2 + 길이 4)보다 넉넉히 뺀다.
        private const int ChunkOverhead = 32;

        public static int ChunkBytes(int maxMessageBytes) => Math.Max(64, maxMessageBytes - ChunkOverhead);

        /// <summary>묶음을 바이트로 적는다. 크기·개수·이름 한도를 넘으면 false.</summary>
        public static bool TryEncode(SaveFileSet set, out byte[] bytes)
        {
            bytes = null;
            if (set == null || set.Writes.Count + set.Deletes.Count > MaxFiles || set.ByteCount > MaxSaveBytes) return false;
            var w = new NetWriter(1024);
            w.WriteInt32(set.Writes.Count);
            foreach (var pair in set.Writes)
            {
                if (!NameFits(pair.Key)) return false;
                w.WriteString(pair.Key);
                var text = Encoding.UTF8.GetBytes(pair.Value ?? string.Empty);
                w.WriteInt32(text.Length);
                w.WriteBytes(text);
            }

            w.WriteInt32(set.Deletes.Count);
            foreach (var name in set.Deletes)
            {
                if (!NameFits(name)) return false;
                w.WriteString(name);
            }

            bytes = w.ToArray();
            return bytes.Length <= MaxSaveBytes;
        }

        public static bool TryDecode(byte[] bytes, out SaveFileSet set)
        {
            set = null;
            if (bytes == null || bytes.Length > MaxSaveBytes) return false;
            var r = new NetReader(bytes);
            var result = new SaveFileSet();
            if (!r.TryReadInt32(out int writes) || writes < 0 || writes > MaxFiles) return false;
            for (int i = 0; i < writes; i++)
            {
                if (!r.TryReadString(MaxFileNameBytes, out string name) || name.Length == 0) return false;
                if (!r.TryReadInt32(out int length) || !r.TryReadBytes(length, out byte[] text)) return false;
                string value;
                try { value = new UTF8Encoding(false, true).GetString(text); }
                catch (ArgumentException) { return false; }
                result.Writes.Add(new KeyValuePair<string, string>(name, value));
            }

            if (!r.TryReadInt32(out int deletes) || deletes < 0 || writes + deletes > MaxFiles) return false;
            for (int i = 0; i < deletes; i++)
            {
                if (!r.TryReadString(MaxFileNameBytes, out string name) || name.Length == 0) return false;
                result.Deletes.Add(name);
            }

            set = result;
            return r.Remaining == 0;
        }

        /// <summary>바이트를 조각으로 나눈다. 빈 묶음도 조각 하나다(받는 쪽이 끝을 안다).</summary>
        public static List<SaveChunkMessage> Split(uint transferId, byte[] payload, int maxMessageBytes)
        {
            payload ??= Array.Empty<byte>();
            int size = ChunkBytes(maxMessageBytes);
            int count = Math.Max(1, (payload.Length + size - 1) / size);
            if (count > ushort.MaxValue) throw new ArgumentException("묶음이 너무 크다", nameof(payload));
            var chunks = new List<SaveChunkMessage>(count);
            for (int i = 0; i < count; i++)
            {
                int start = i * size;
                int length = Math.Min(size, payload.Length - start);
                var part = new byte[Math.Max(0, length)];
                if (length > 0) Buffer.BlockCopy(payload, start, part, 0, length);
                chunks.Add(new SaveChunkMessage { TransferId = transferId, Index = (ushort)i, Count = (ushort)count, Bytes = part });
            }

            return chunks;
        }

        private static bool NameFits(string name) => !string.IsNullOrEmpty(name) && Encoding.UTF8.GetByteCount(name) <= MaxFileNameBytes;
    }

    /// <summary>
    /// 한 보내는 쪽의 조각을 차례로 모은다. 새 묶음 id가 오면 앞의 덜 모인 묶음은 버린다(끊겼다 다시 보낸 것). 번호가 맞지 않거나
    /// 크기 한도를 넘으면 그 묶음을 버리고 <see cref="Failed"/>를 세운다.
    /// </summary>
    public sealed class ChunkAssembler
    {
        private uint _id;
        private int _expected;
        private int _count;
        private byte[] _buffer = Array.Empty<byte>();
        private int _length;
        private bool _active;

        /// <summary>마지막으로 버린 묶음 id(없으면 null).</summary>
        public uint? Failed { get; private set; }

        /// <returns>묶음이 다 모였으면 true와 그 바이트.</returns>
        public bool Add(SaveChunkMessage chunk, out uint transferId, out byte[] payload)
        {
            transferId = chunk?.TransferId ?? 0;
            payload = null;
            if (chunk == null) return false;
            if (!_active || chunk.TransferId != _id)
            {
                if (chunk.Index != 0) return Fail(chunk.TransferId);
                _active = true;
                _id = chunk.TransferId;
                _expected = 0;
                _count = chunk.Count;
                _length = 0;
            }

            if (chunk.Index != _expected || chunk.Count != _count || chunk.Count == 0) return Fail(chunk.TransferId);
            var bytes = chunk.Bytes ?? Array.Empty<byte>();
            if ((long)_length + bytes.Length > RoomTransfer.MaxSaveBytes) return Fail(chunk.TransferId);
            if (_buffer.Length < _length + bytes.Length) Array.Resize(ref _buffer, Math.Max(_length + bytes.Length, _buffer.Length * 2));
            Buffer.BlockCopy(bytes, 0, _buffer, _length, bytes.Length);
            _length += bytes.Length;
            _expected++;
            if (_expected < _count) return false;

            _active = false;
            payload = new byte[_length];
            Buffer.BlockCopy(_buffer, 0, payload, 0, _length);
            _length = 0;
            if (_buffer.Length > 64 * 1024) _buffer = Array.Empty<byte>();
            return true;
        }

        private bool Fail(uint id)
        {
            _active = false;
            _length = 0;
            Failed = id;
            return false;
        }
    }

    /// <summary>위 메시지들의 몸통 읽기·쓰기. 등록은 <see cref="RootbornMessages.Register"/>에 한 줄씩.</summary>
    internal static class RoomShareMessageBodies
    {
        public const int MaxPieceNameBytes = 64;
        public const int MaxIdBytes = 128;
        public const int MaxOutcomeBytes = 32;
        public const int MaxIds = 256;

        public static void WriteSettings(NetWriter w, RoomSettingsMessage m)
        {
            WriteStrings(w, m.SharedPieces);
            w.WriteBool(m.GuestSaves);
        }

        public static bool ReadSettings(NetReader r, int version, out RoomSettingsMessage m)
        {
            m = new RoomSettingsMessage();
            return ReadStrings(r, MaxPieceNameBytes, 64, out m.SharedPieces) && r.TryReadBool(out m.GuestSaves);
        }

        public static void WriteChunk(NetWriter w, SaveChunkMessage m)
        {
            w.WriteUInt32(m.TransferId);
            w.WriteUInt16(m.Index);
            w.WriteUInt16(m.Count);
            var bytes = m.Bytes ?? Array.Empty<byte>();
            w.WriteInt32(bytes.Length);
            w.WriteBytes(bytes);
        }

        public static bool ReadChunk(NetReader r, int version, out SaveChunkMessage m)
        {
            m = new SaveChunkMessage();
            return r.TryReadUInt32(out m.TransferId) && r.TryReadUInt16(out m.Index) && r.TryReadUInt16(out m.Count) && m.Count > 0 && m.Index < m.Count
                   && r.TryReadInt32(out int length) && r.TryReadBytes(length, out m.Bytes);
        }

        public static void WriteTransferAnswer(NetWriter w, SaveTransferAnswerMessage m)
        {
            w.WriteUInt32(m.TransferId);
            w.WriteBool(m.Accepted);
            w.WriteString(m.Reason);
        }

        public static bool ReadTransferAnswer(NetReader r, int version, out SaveTransferAnswerMessage m)
        {
            m = new SaveTransferAnswerMessage();
            return r.TryReadUInt32(out m.TransferId) && r.TryReadBool(out m.Accepted) && r.TryReadString(RootbornMessages.MaxReasonBytes, out m.Reason);
        }

        public static void WritePieceState(NetWriter w, RoomPieceStateMessage m)
        {
            w.WriteString(m.Piece);
            w.WriteInt32(m.Amount);
            WriteStrings(w, m.Ids);
        }

        public static bool ReadPieceState(NetReader r, int version, out RoomPieceStateMessage m)
        {
            m = new RoomPieceStateMessage();
            return r.TryReadString(MaxPieceNameBytes, out m.Piece) && m.Piece.Length > 0 && r.TryReadInt32(out m.Amount) && ReadStrings(r, MaxIdBytes, MaxIds, out m.Ids);
        }

        public static void WriteAnswer(NetWriter w, RoomAnswerMessage m)
        {
            w.WriteString(m.Piece);
            w.WriteString(m.RequestId);
            w.WriteString(m.Outcome);
            w.WriteInt32(m.Amount);
        }

        public static bool ReadAnswer(NetReader r, int version, out RoomAnswerMessage m)
        {
            m = new RoomAnswerMessage();
            return r.TryReadString(MaxPieceNameBytes, out m.Piece) && r.TryReadString(MaxIdBytes, out m.RequestId) && m.RequestId.Length > 0
                   && r.TryReadString(MaxOutcomeBytes, out m.Outcome) && r.TryReadInt32(out m.Amount);
        }

        private static void WriteStrings(NetWriter w, string[] values)
        {
            values ??= Array.Empty<string>();
            int count = Math.Min(values.Length, ushort.MaxValue);
            w.WriteUInt16((ushort)count);
            for (int i = 0; i < count; i++) w.WriteString(values[i]);
        }

        private static bool ReadStrings(NetReader r, int maxBytes, int maxCount, out string[] values)
        {
            values = Array.Empty<string>();
            if (!r.TryReadUInt16(out ushort count) || count > maxCount) return false;
            values = new string[count];
            for (int i = 0; i < count; i++)
                if (!r.TryReadString(maxBytes, out values[i])) return false;
            return true;
        }
    }
}
