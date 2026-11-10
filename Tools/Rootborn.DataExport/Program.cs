using System;
using System.IO;

namespace Rootborn.DataExport
{
    public static class Program
    {
        // 사용법: dotnet run --project Tools/Rootborn.DataExport -- [--repo <저장소 루트>] [--out <출력 폴더>] [--allow-unresolved]
        // 종료 코드: 0 정상, 1 풀지 못한 참조 있음(--allow-unresolved면 0), 2 읽지 못한 파일 있음.
        public static int Main(string[] args)
        {
            var options = new ExportOptions();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--allow-unresolved") options.AllowUnresolved = true;
                else if (args[i] == "--repo" && i + 1 < args.Length) options.RepoRoot = Path.GetFullPath(args[++i]);
                else if (args[i] == "--out" && i + 1 < args.Length) options.OutDir = Path.GetFullPath(args[++i]);
                else { Console.Error.WriteLine("알 수 없는 인자: " + args[i]); return 64; }
            }

            options.RepoRoot = options.RepoRoot ?? FindRepoRoot(Directory.GetCurrentDirectory(), options.DataDir);
            if (options.RepoRoot == null) { Console.Error.WriteLine("Assets/Data가 있는 저장소 루트를 찾지 못했다. --repo로 지정한다."); return 64; }
            options.OutDir = options.OutDir ?? Path.Combine(options.RepoRoot, "data");

            ExportResult result = Exporter.Run(options);
            foreach (string message in result.Messages) Console.WriteLine(message);
            Console.WriteLine("보고서: " + Path.Combine(options.OutDir, "export-report.json") + " (종료 코드 " + result.ExitCode + ")");
            return result.ExitCode;
        }

        private static string FindRepoRoot(string start, string dataDir)
        {
            for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
                if (Directory.Exists(Path.Combine(dir.FullName, dataDir))) return dir.FullName;
            return null;
        }
    }
}
