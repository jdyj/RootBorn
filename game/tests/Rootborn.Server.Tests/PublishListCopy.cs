using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Rootborn.Server.Tests
{
    /// <summary>
    /// 배포 목록(Tools/publish/content.txt, content-server.txt)이 고르는 파일 가운데 그림·글꼴을 뺀 것(데이터·설정·맵)만 임시 폴더에 같은 상대 경로로 복사한다.
    /// 목록 문법은 Tools/publish/publish.sh 와 같다: 평범한 줄(bash globstar 글롭, 폴더면 안의 파일 전부), `!` 줄(앞에서 모인 것에서 뺀다 — bash [[ == ]] 처럼 *가 /도 넘는다),
    /// `@ldtk`(.ldtk 와 레벨 파일 .ldtkl — 타일셋 PNG 는 그림이라 뺀다). `@refs`·`@png` 줄과 자동으로 따라오는 좌표표는 그림 몫이라 보지 않는다.
    /// 그림 수천 개를 복사하지 않으려고 그림을 뺀다 — 이 복사본은 "시작할 때 읽는 데이터가 목록에 다 있는가"만 본다.
    /// </summary>
    internal static class PublishListCopy
    {
        private static readonly string[] PictureExtensions = { ".png", ".ttf", ".otf", ".ttc" };

        /// <summary>목록 파일(Tools/publish/&lt;이름&gt;)로 고른 파일을 target 아래에 복사하고 복사한 상대 경로를 돌려준다.</summary>
        public static IReadOnlyCollection<string> Copy(string repoRoot, string listName, string target, Func<string, string> editList = null)
        {
            string text = File.ReadAllText(Path.Combine(repoRoot, "Tools", "publish", listName)).Replace("\r\n", "\n");
            if (editList != null) text = editList(text);
            var chosen = Select(repoRoot, text);
            foreach (string file in chosen)
            {
                string to = Path.Combine(target, file);
                Directory.CreateDirectory(Path.GetDirectoryName(to));
                File.Copy(Path.Combine(repoRoot, file), to);
            }

            return chosen;
        }

        public static SortedSet<string> Select(string repoRoot, string listText)
        {
            var chosen = new SortedSet<string>(StringComparer.Ordinal);
            foreach (string raw in listText.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;
                if (line.StartsWith("!", StringComparison.Ordinal))
                {
                    var remove = RemoveRegex(line.Substring(1));
                    chosen.RemoveWhere(f => remove.IsMatch(f));
                }
                else if (line.StartsWith("@ldtk ", StringComparison.Ordinal))
                {
                    foreach (string ldtk in Matching(repoRoot, line.Substring(6).Trim()).Where(f => f.EndsWith(".ldtk", StringComparison.OrdinalIgnoreCase)))
                    {
                        chosen.Add(ldtk);
                        string levels = Path.Combine(repoRoot, Path.ChangeExtension(ldtk, null));
                        if (!Directory.Exists(levels)) continue;
                        foreach (string level in Directory.GetFiles(levels, "*.ldtkl")) chosen.Add(Relative(repoRoot, level));
                    }
                }
                else if (line.StartsWith("@", StringComparison.Ordinal))
                {
                    // @refs·@png: 그림만 더한다 — 이 복사본은 그림을 보지 않는다.
                }
                else
                {
                    foreach (string file in Matching(repoRoot, line)) chosen.Add(file);
                }
            }

            chosen.RemoveWhere(f => PictureExtensions.Any(e => f.EndsWith(e, StringComparison.OrdinalIgnoreCase)));
            return chosen;
        }

        /// <summary>글롭에 맞는 파일(맞는 폴더면 안의 파일 전부). 글롭 앞의 고정된 폴더 아래만 훑는다.</summary>
        private static IEnumerable<string> Matching(string repoRoot, string glob)
        {
            int wild = glob.IndexOfAny(new[] { '*', '?', '[' });
            if (wild < 0)
            {
                string path = Path.Combine(repoRoot, glob);
                if (File.Exists(path)) return new[] { glob.TrimEnd('/') };
                if (Directory.Exists(path)) return Directory.GetFiles(path, "*", SearchOption.AllDirectories).Select(f => Relative(repoRoot, f));
                return Array.Empty<string>();
            }

            int slash = glob.LastIndexOf('/', wild);
            string baseDir = Path.Combine(repoRoot, slash < 0 ? "." : glob.Substring(0, slash));
            if (!Directory.Exists(baseDir)) return Array.Empty<string>();
            var regex = AddRegex(glob);
            return Directory.GetFiles(baseDir, "*", SearchOption.AllDirectories).Select(f => Relative(repoRoot, f)).Where(f => regex.IsMatch(f));
        }

        private static string Relative(string repoRoot, string path) => Path.GetRelativePath(repoRoot, path).Replace('\\', '/');

        /// <summary>bash globstar: *는 / 를 넘지 않고, 경로 한 마디인 **는 폴더 0개 이상. 글롭이 폴더에 맞으면 그 아래 파일 전부.</summary>
        private static Regex AddRegex(string glob)
        {
            var sb = new StringBuilder("^");
            for (int i = 0; i < glob.Length; i++)
            {
                char c = glob[i];
                if (c == '*' && i + 1 < glob.Length && glob[i + 1] == '*')
                {
                    i++;
                    if (i + 1 < glob.Length && glob[i + 1] == '/')
                    {
                        sb.Append("(?:[^/]+/)*");
                        i++;
                    }
                    else sb.Append(".*");
                }
                else if (c == '*') sb.Append("[^/]*");
                else if (c == '?') sb.Append("[^/]");
                else sb.Append(Regex.Escape(c.ToString()));
            }

            // 끝이 / 인 글롭(game/themes/*/)은 폴더에만 맞는다.
            if (glob.EndsWith("/", StringComparison.Ordinal)) return new Regex(sb.ToString().TrimEnd('/') + "/.+$");
            return new Regex(sb + "(?:/.*)?$");
        }

        /// <summary>bash [[ 파일 == 글롭 ]]: *와 **는 / 도 넘는다.</summary>
        private static Regex RemoveRegex(string glob)
        {
            var sb = new StringBuilder("^");
            foreach (char c in glob)
            {
                if (c == '*') sb.Append(".*");
                else if (c == '?') sb.Append('.');
                else sb.Append(Regex.Escape(c.ToString()));
            }

            return new Regex(sb.Append('$').ToString());
        }
    }
}
