using System;
using System.Collections.Generic;
using System.Text;

namespace Rootborn.Host.Tests.TwoGames
{
    /// <summary>
    /// 두 게임 테스트에서 부모(테스트 프로세스)와 자식(두 번째 게임 프로세스)이 표준 입출력으로 주고받는 줄.
    /// - 부모 → 자식: 동사 한 줄. 칸은 탭으로 나눈다(<see cref="Encode"/>). 자식이 프레임마다 묻는 <see cref="Tick"/>에는 <see cref="Go"/>로 답한다.
    /// - 자식 → 부모: <see cref="Ok"/>(+ 값) 또는 <see cref="Error"/>(+ 이유)가 동사 하나의 끝이다. 동사를 하는 동안 한 프레임을 돌릴 때마다
    ///   <see cref="Tick"/>을 보내고 <see cref="Go"/>를 기다린다(그 사이 부모가 자기 게임을 한 프레임 돌린다 — 두 게임이 번갈아 한 프레임씩 간다).
    ///   이 접두어가 없는 줄은 게임의 로그다(부모가 테스트 출력에 옮겨 적는다).
    /// 주소·포트·경로는 이 줄에 적지 않는다 — 부모가 테스트 안에서 얻은 값을 동사의 칸으로 넘긴다.
    /// </summary>
    public static class PeerProtocol
    {
        /// <summary>자식을 두 번째 게임으로 띄우는 인자(테스트 어셈블리의 진입점이 이것을 보고 동사 루프를 돈다).</summary>
        public const string PeerArgument = "--two-games-peer";

        public const string Tick = "@@tick";
        public const string Go = "go";
        public const string Ok = "@@ok";
        public const string Error = "@@err";

        public static string Encode(IReadOnlyList<string> parts)
        {
            var b = new StringBuilder();
            for (int i = 0; i < parts.Count; i++)
            {
                if (i > 0) b.Append('\t');
                Escape(b, parts[i] ?? string.Empty);
            }
            return b.ToString();
        }

        public static string[] Decode(string line)
        {
            var parts = new List<string>();
            var b = new StringBuilder();
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '\t') { parts.Add(b.ToString()); b.Clear(); continue; }
                if (c == '\\' && i + 1 < line.Length)
                {
                    char n = line[++i];
                    b.Append(n == 't' ? '\t' : n == 'n' ? '\n' : n == 'r' ? '\r' : n);
                    continue;
                }
                b.Append(c);
            }
            parts.Add(b.ToString());
            return parts.ToArray();
        }

        /// <summary>한 칸을 줄바꿈·탭 없이 적는다.</summary>
        public static string EscapeOne(string value)
        {
            var b = new StringBuilder();
            Escape(b, value ?? string.Empty);
            return b.ToString();
        }

        private static void Escape(StringBuilder b, string value)
        {
            foreach (char c in value)
            {
                switch (c)
                {
                    case '\\': b.Append("\\\\"); break;
                    case '\t': b.Append("\\t"); break;
                    case '\n': b.Append("\\n"); break;
                    case '\r': b.Append("\\r"); break;
                    default: b.Append(c); break;
                }
            }
        }
    }

    /// <summary>자식 게임이 동사를 하다 실패했다(그 이유를 담는다).</summary>
    public sealed class PeerException : Exception
    {
        public PeerException(string message) : base(message) { }
    }
}
