using System;
using System.Buffers.Binary;
using System.Text;

namespace Rootborn.Net.Messages
{
    /// <summary>메시지 몸통 쓰기. 정수·실수는 리틀 엔디언, 글자는 UTF-8 바이트 수(ushort) + 바이트.</summary>
    public sealed class NetWriter
    {
        private byte[] _buffer;

        public NetWriter(int capacity = 64) => _buffer = new byte[capacity < 8 ? 8 : capacity];

        public int Length { get; private set; }

        public void WriteByte(byte value)
        {
            Ensure(1);
            _buffer[Length++] = value;
        }

        public void WriteBool(bool value) => WriteByte(value ? (byte)1 : (byte)0);

        public void WriteUInt16(ushort value)
        {
            Ensure(2);
            BinaryPrimitives.WriteUInt16LittleEndian(_buffer.AsSpan(Length), value);
            Length += 2;
        }

        public void WriteInt32(int value)
        {
            Ensure(4);
            BinaryPrimitives.WriteInt32LittleEndian(_buffer.AsSpan(Length), value);
            Length += 4;
        }

        public void WriteUInt32(uint value)
        {
            Ensure(4);
            BinaryPrimitives.WriteUInt32LittleEndian(_buffer.AsSpan(Length), value);
            Length += 4;
        }

        public void WriteSingle(float value) => WriteInt32(BitConverter.SingleToInt32Bits(value));

        public void WriteDouble(double value)
        {
            Ensure(8);
            BinaryPrimitives.WriteInt64LittleEndian(_buffer.AsSpan(Length), BitConverter.DoubleToInt64Bits(value));
            Length += 8;
        }

        /// <summary>null은 빈 글자로 쓴다.</summary>
        public void WriteString(string value)
        {
            var bytes = string.IsNullOrEmpty(value) ? Array.Empty<byte>() : Encoding.UTF8.GetBytes(value);
            if (bytes.Length > ushort.MaxValue) throw new ArgumentException("글자가 너무 길다", nameof(value));
            WriteUInt16((ushort)bytes.Length);
            Ensure(bytes.Length);
            bytes.CopyTo(_buffer, Length);
            Length += bytes.Length;
        }

        public void WriteBytes(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return;
            Ensure(bytes.Length);
            bytes.CopyTo(_buffer, Length);
            Length += bytes.Length;
        }

        public byte[] ToArray() => _buffer.AsSpan(0, Length).ToArray();

        public void Clear() => Length = 0;

        private void Ensure(int more)
        {
            if (Length + more <= _buffer.Length) return;
            int size = _buffer.Length * 2;
            while (size < Length + more) size *= 2;
            Array.Resize(ref _buffer, size);
        }
    }
}
