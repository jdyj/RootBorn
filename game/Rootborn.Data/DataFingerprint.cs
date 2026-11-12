using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Rootborn.Data
{
    /// <summary>
    /// 게임 데이터의 지문: 규칙에 쓰이는 데이터 파일의 (상대 경로, 내용)을 이름 순으로 SHA-256에 넣은 앞 16자리.
    /// 서버(Rootborn.Server)와 들어가는 쪽(Rootborn.Host)이 이 한 곳의 셈법을 쓴다. 셈법을 바꾸면 이미 나눠 준 판과 맞지 않게 된다.
    ///
    /// 셈하는 것 — 서버와 게임이 함께 읽어 규칙을 만드는 파일(GameDataLoader가 읽는 것과 같은 기준):
    ///   - data/registry.json (등록부)
    ///   - data 아래에서 맨 위가 배열인 .json (엔티티 파일. EntityTable이 배열 파일만 엔티티로 읽는다)
    ///   - data 폴더 옆의 game/economy.json (통화 설정, EconomySettings). 없으면 넣지 않는다 — 통화가 없는 판은 규칙이 다르다
    ///   - data 폴더 옆의 game/time.json (하루 시작, 취침, 밤 체력 감소 — TimeSettings). 없으면 넣지 않는다(시간 규칙이 다른 판)
    /// 셈하지 않는 것 — 맨 위가 객체인 그 밖의 data .json: 그림 좌표표(data/atlases), 변환 보고서(export-report.json),
    /// 문구(data/localization). 배포마다 들어가는 몫이 다르고, 문구가 달라도(언어가 다른 두 사람) 같이 할 수 있어야 한다.
    /// 줄바꿈(CRLF/LF)과 맨 앞 UTF-8 BOM은 맞춰서 넣는다 — Windows와 Linux에서 만든 체크아웃·배포가 같은 데이터로 보이게.
    /// </summary>
    public static class DataFingerprint
    {
        private const string RegistryFile = "registry.json";
        private const string EconomyRelative = "../game/" + EconomySettings.FileName;
        private const string TimeRelative = "../game/" + TimeSettings.FileName;

        public static string Compute(string dataDirectory)
        {
            if (!Directory.Exists(dataDirectory)) throw new DirectoryNotFoundException("데이터 폴더가 없다: " + dataDirectory);
            string root = Path.GetFullPath(dataDirectory);
            var files = new List<(string Relative, byte[] Bytes)>();
            foreach (var path in Directory.GetFiles(root, "*.json", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(root, path).Replace('\\', '/');
                byte[] bytes = Normalize(File.ReadAllBytes(path));
                if (relative == RegistryFile || StartsWithArray(bytes)) files.Add((relative, bytes));
            }

            string economy = EconomySettings.PathFor(root);
            if (File.Exists(economy)) files.Add((EconomyRelative, Normalize(File.ReadAllBytes(economy))));
            string time = TimeSettings.PathFor(root);
            if (File.Exists(time)) files.Add((TimeRelative, Normalize(File.ReadAllBytes(time))));

            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            foreach (var file in files.OrderBy(f => f.Relative, StringComparer.Ordinal))
            {
                hash.AppendData(Encoding.UTF8.GetBytes(file.Relative));
                hash.AppendData(new byte[] { 0 });
                hash.AppendData(file.Bytes);
                hash.AppendData(new byte[] { 0 });
            }

            return Convert.ToHexString(hash.GetHashAndReset()).Substring(0, 16).ToLowerInvariant();
        }

        private static bool StartsWithArray(byte[] bytes)
        {
            foreach (byte b in bytes)
            {
                if (b == (byte)' ' || b == (byte)'\t' || b == (byte)'\r' || b == (byte)'\n') continue;
                return b == (byte)'[';
            }

            return false;
        }

        /// <summary>맨 앞 UTF-8 BOM을 떼고 CRLF를 LF로 맞춘다.</summary>
        private static byte[] Normalize(byte[] bytes)
        {
            int start = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
            int crlf = 0;
            for (int i = start; i + 1 < bytes.Length; i++)
                if (bytes[i] == (byte)'\r' && bytes[i + 1] == (byte)'\n') crlf++;
            if (crlf == 0 && start == 0) return bytes;
            var result = new byte[bytes.Length - start - crlf];
            int n = 0;
            for (int i = start; i < bytes.Length; i++)
            {
                if (bytes[i] == (byte)'\r' && i + 1 < bytes.Length && bytes[i + 1] == (byte)'\n') continue;
                result[n++] = bytes[i];
            }

            return result;
        }
    }
}
