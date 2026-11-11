using System;
using System.Buffers.Binary;
using System.Text;

namespace Rootborn.Net.Messages
{
    /// <summary>
    /// 메시지 몸통 읽기. 모자라거나 맞지 않는 바이트는 예외 없이 false다(깨진 메시지가 세션을 죽이지 않게).
    /// 글자는 최대 바이트 수를 넘거나 UTF-8이 아니면 false.
    /// </summary>
    public sealed class NetReader
    {
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, throwOnInvalidBytes: true);
        private readonly byte[] _data;
        private int _position;

        public NetReader(byte[] data, int offset = 0)
        {
            _data = data ?? Array.Empty<byte>();
            _position = offset < 0 ? 0 : offset;
        }

        public int Remaining => _data.Length - _position;

        public bool TryReadByte(out byte value)
        {
            value = 0;
            if (Remaining < 1) return false;
            value = _data[_position++];
            return true;
        }

        public bool TryReadBool(out bool value)
        {
            value = false;
            if (!TryReadByte(out byte b) || b > 1) return false;
            value = b == 1;
            return true;
        }

        public bool TryReadUInt16(out ushort value)
        {
            value = 0;
            if (Remaining < 2) return false;
            value = BinaryPrimitives.ReadUInt16LittleEndian(_data.AsSpan(_position));
            _position += 2;
            return true;
        }

        public bool TryReadInt32(out int value)
        {
            value = 0;
            if (Remaining < 4) return false;
            value = BinaryPrimitives.ReadInt32LittleEndian(_data.AsSpan(_position));
            _position += 4;
            return true;
        }

        public bool TryReadUInt32(out uint value)
        {
            value = 0;
            if (Remaining < 4) return false;
            value = BinaryPrimitives.ReadUInt32LittleEndian(_data.AsSpan(_position));
            _position += 4;
            return true;
        }

        /// <summary>NaN·무한대는 false(위치로 쓸 수 없다).</summary>
        public bool TryReadSingle(out float value)
        {
            value = 0;
            if (!TryReadInt32(out int bits)) return false;
            value = BitConverter.Int32BitsToSingle(bits);
            return float.IsFinite(value);
        }

        public bool TryReadDouble(out double value)
        {
            value = 0;
            if (Remaining < 8) return false;
            value = BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64LittleEndian(_data.AsSpan(_position)));
            _position += 8;
            return double.IsFinite(value);
        }

        public bool TryReadString(int maxBytes, out string value)
        {
            value = string.Empty;
            if (!TryReadUInt16(out ushort length)) return false;
            if (length > maxBytes || length > Remaining) return false;
            if (length == 0) return true;
            try
            {
                value = StrictUtf8.GetString(_data, _position, length);
            }
            catch (ArgumentException)
            {
                return false;
            }

            _position += length;
            return true;
        }
    }
}
