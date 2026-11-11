using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Rootborn.Server
{
    /// <summary>
    /// 게임 데이터의 지문: 데이터 폴더 아래 모든 .json의 (상대 경로, 내용)을 이름 순으로 SHA-256에 넣은 앞 16자리.
    /// 줄바꿈(CRLF/LF)은 맞춰서 넣는다 — Windows와 Linux 체크아웃이 같은 데이터로 보이게.
    /// 들어오는 클라이언트도 같은 방법으로 셈해야 한다(지금은 서버에만 있다. Host가 방에 들어가게 될 때 Rootborn.Data로 옮긴다).
    /// </summary>
    public static class DataFingerprint
    {
        public static string Compute(string dataDirectory)
        {
            if (!Directory.Exists(dataDirectory)) throw new DirectoryNotFoundException("데이터 폴더가 없다: " + dataDirectory);
            string root = Path.GetFullPath(dataDirectory);
            var files = Directory.GetFiles(root, "*.json", SearchOption.AllDirectories)
                .Select(path => (Relative: Path.GetRelativePath(root, path).Replace('\\', '/'), Path: path))
                .OrderBy(f => f.Relative, StringComparer.Ordinal)
                .ToArray();

            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            foreach (var file in files)
            {
                hash.AppendData(Encoding.UTF8.GetBytes(file.Relative));
                hash.AppendData(new byte[] { 0 });
                byte[] bytes = File.ReadAllBytes(file.Path);
                hash.AppendData(WithoutCarriageReturnsBeforeNewlines(bytes));
                hash.AppendData(new byte[] { 0 });
            }

            return Convert.ToHexString(hash.GetHashAndReset()).Substring(0, 16).ToLowerInvariant();
        }

        private static byte[] WithoutCarriageReturnsBeforeNewlines(byte[] bytes)
        {
            int crlf = 0;
            for (int i = 0; i + 1 < bytes.Length; i++)
                if (bytes[i] == (byte)'\r' && bytes[i + 1] == (byte)'\n') crlf++;
            if (crlf == 0) return bytes;
            var result = new byte[bytes.Length - crlf];
            int n = 0;
            for (int i = 0; i < bytes.Length; i++)
            {
                if (bytes[i] == (byte)'\r' && i + 1 < bytes.Length && bytes[i + 1] == (byte)'\n') continue;
                result[n++] = bytes[i];
            }

            return result;
        }
    }
}
