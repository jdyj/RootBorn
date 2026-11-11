using System;
using System.Collections.Generic;

namespace Rootborn.Net.Messages
{
    /// <summary>몸통을 읽는다. version은 받은 메시지의 형식 버전(등록한 버전 이하)이라 옛 모양도 읽을 수 있다.</summary>
    public delegate bool MessageBodyReader<T>(NetReader reader, int version, out T message);

    /// <summary>메시지 하나를 풀어 본 결과.</summary>
    public enum DecodeOutcome
    {
        Ok,
        /// <summary>머리(종류 2바이트 + 버전 1바이트)나 몸통이 모자라거나 맞지 않는다.</summary>
        Malformed,
        UnknownKind,
        /// <summary>이 프로그램이 아는 것보다 높은 형식 버전.</summary>
        TooNewVersion,
        TooLarge,
    }

    /// <summary>버린 메시지 수. 세션은 버리기만 하고 계속 돈다.</summary>
    public sealed class DropCounters
    {
        public int Malformed { get; private set; }
        public int UnknownKind { get; private set; }
        public int TooNewVersion { get; private set; }
        public int TooLarge { get; private set; }

        /// <summary>뜻은 맞지만 지금 받을 수 없는 메시지(들어오기 전의 위치, 남의 것인 척한 것, 받는 쪽이 처리하지 않는 종류 등).</summary>
        public int Unexpected { get; private set; }

        public int Total => Malformed + UnknownKind + TooNewVersion + TooLarge + Unexpected;

        public void Count(DecodeOutcome outcome)
        {
            switch (outcome)
            {
                case DecodeOutcome.Malformed: Malformed++; break;
                case DecodeOutcome.UnknownKind: UnknownKind++; break;
                case DecodeOutcome.TooNewVersion: TooNewVersion++; break;
                case DecodeOutcome.TooLarge: TooLarge++; break;
            }
        }

        public void CountUnexpected() => Unexpected++;
    }

    /// <summary>
    /// 메시지 종류 등록 표. 메시지 바이트 = 종류 번호(ushort, 리틀 엔디언) + 형식 버전(byte) + 몸통.
    /// 새 종류는 <see cref="Add{T}"/> 한 줄로 더한다(종류를 enum으로 닫지 않는다). 번호는 한 번 쓰면 다른 뜻으로 다시 쓰지 않는다.
    /// 몸통 뒤에 남는 바이트는 무시한다(같은 버전 안에서 뒤에 필드를 덧붙여도 옛 프로그램이 읽는다).
    /// 최대 크기를 넘는 메시지는 보내지도 받지도 않는다.
    /// </summary>
    public sealed class MessageCatalog
    {
        public const int HeaderBytes = 3;

        private readonly Dictionary<ushort, Entry> _byKind = new Dictionary<ushort, Entry>(32);
        private readonly Dictionary<Type, Entry> _byType = new Dictionary<Type, Entry>(32);
        private readonly NetWriter _writer = new NetWriter(256);

        public MessageCatalog(int maxMessageBytes)
        {
            if (maxMessageBytes <= HeaderBytes) throw new ArgumentException("최대 크기가 너무 작다", nameof(maxMessageBytes));
            MaxMessageBytes = maxMessageBytes;
        }

        public int MaxMessageBytes { get; }

        public void Add<T>(ushort kind, byte version, Action<NetWriter, T> write, MessageBodyReader<T> read) where T : class
        {
            if (write == null) throw new ArgumentNullException(nameof(write));
            if (read == null) throw new ArgumentNullException(nameof(read));
            if (_byKind.ContainsKey(kind)) throw new ArgumentException("이미 쓰인 종류 번호: " + kind, nameof(kind));
            if (_byType.ContainsKey(typeof(T))) throw new ArgumentException("이미 등록한 메시지: " + typeof(T).Name);
            var entry = new Entry(kind, version, (w, m) => write(w, (T)m), (NetReader r, int v, out object m) =>
            {
                bool ok = read(r, v, out T typed);
                m = typed;
                return ok && typed != null;
            });
            _byKind.Add(kind, entry);
            _byType.Add(typeof(T), entry);
        }

        public bool TryGetKind(Type messageType, out ushort kind, out byte version)
        {
            kind = 0;
            version = 0;
            if (messageType == null || !_byType.TryGetValue(messageType, out var entry)) return false;
            kind = entry.Kind;
            version = entry.Version;
            return true;
        }

        /// <returns>등록하지 않은 메시지이거나 최대 크기를 넘으면 false.</returns>
        public bool TryEncode(object message, out byte[] bytes)
        {
            bytes = null;
            if (message == null || !_byType.TryGetValue(message.GetType(), out var entry)) return false;
            _writer.Clear();
            _writer.WriteUInt16(entry.Kind);
            _writer.WriteByte(entry.Version);
            entry.Write(_writer, message);
            if (_writer.Length > MaxMessageBytes) return false;
            bytes = _writer.ToArray();
            return true;
        }

        public DecodeOutcome Decode(byte[] bytes, out object message)
        {
            message = null;
            if (bytes == null) return DecodeOutcome.Malformed;
            if (bytes.Length > MaxMessageBytes) return DecodeOutcome.TooLarge;
            var reader = new NetReader(bytes);
            if (!reader.TryReadUInt16(out ushort kind) || !reader.TryReadByte(out byte version)) return DecodeOutcome.Malformed;
            if (!_byKind.TryGetValue(kind, out var entry)) return DecodeOutcome.UnknownKind;
            if (version > entry.Version) return DecodeOutcome.TooNewVersion;
            if (!entry.Read(reader, version, out message))
            {
                message = null;
                return DecodeOutcome.Malformed;
            }

            return DecodeOutcome.Ok;
        }

        private delegate bool BoxedReader(NetReader reader, int version, out object message);

        private sealed class Entry
        {
            public Entry(ushort kind, byte version, Action<NetWriter, object> write, BoxedReader read)
            {
                Kind = kind;
                Version = version;
                Write = write;
                Read = read;
            }

            public ushort Kind { get; }
            public byte Version { get; }
            public Action<NetWriter, object> Write { get; }
            public BoxedReader Read { get; }
        }
    }

    /// <summary>받은 메시지를 형식별 처리기로 넘긴다. 처리기는 형식마다 한 줄로 등록한다.</summary>
    public sealed class MessageHandlers<TContext>
    {
        private readonly Dictionary<Type, Action<TContext, object>> _handlers = new Dictionary<Type, Action<TContext, object>>(32);

        public MessageHandlers<TContext> On<T>(Action<TContext, T> handler) where T : class
        {
            _handlers[typeof(T)] = (c, m) => handler(c, (T)m);
            return this;
        }

        /// <returns>처리기가 없으면 false.</returns>
        public bool Dispatch(TContext context, object message)
        {
            if (message == null || !_handlers.TryGetValue(message.GetType(), out var handler)) return false;
            handler(context, message);
            return true;
        }
    }
}
