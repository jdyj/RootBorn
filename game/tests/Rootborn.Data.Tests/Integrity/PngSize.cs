using System;
using System.IO;

namespace Rootborn.Data.Tests.Integrity
{
    /// <summary>PNG 파일 머리(서명 8바이트 + IHDR)에서 너비와 높이만 읽는다. 그림을 풀지 않는다.</summary>
    internal static class PngSize
    {
        private static readonly byte[] Signature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        /// <summary>PNG가 아니거나 읽을 수 없으면 false.</summary>
        public static bool TryRead(string path, out int width, out int height)
        {
            width = 0;
            height = 0;
            var head = new byte[24];
            try
            {
                using var stream = File.OpenRead(path);
                var read = 0;
                while (read < head.Length)
                {
                    var n = stream.Read(head, read, head.Length - read);
                    if (n == 0) return false;
                    read += n;
                }
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }

            for (var i = 0; i < Signature.Length; i++)
                if (head[i] != Signature[i]) return false;
            // 8..11 길이, 12..15 "IHDR", 16..19 너비, 20..23 높이(빅엔디언)
            if (head[12] != (byte)'I' || head[13] != (byte)'H' || head[14] != (byte)'D' || head[15] != (byte)'R') return false;
            width = BigEndian(head, 16);
            height = BigEndian(head, 20);
            return width > 0 && height > 0;
        }

        private static int BigEndian(byte[] b, int at) => (b[at] << 24) | (b[at + 1] << 16) | (b[at + 2] << 8) | b[at + 3];
    }
}
