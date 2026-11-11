using System;
using System.Collections.Generic;
using System.IO;

namespace Rootborn.Host
{
    /// <summary>
    /// 실행 환경에서 정해지는 값. 실행 인자가 먼저고 다음이 환경 변수다. 둘 다 없으면 찾아 보고, 못 찾으면 무엇을 주면 되는지 알리며 멈춘다.
    /// 경로를 코드에 적어 두지 않는다.
    /// </summary>
    public sealed class HostOptions
    {
        public const string RootVariable = "ROOTBORN_REPO_ROOT";
        public const string FontVariable = "ROOTBORN_FONT";
        public const string EvidenceVariable = "ROOTBORN_EVIDENCE_DIR";
        public const string WorldMapsVariable = "ROOTBORN_WORLD_MAPS";

        public string RepoRoot;
        public string FontPath;
        public string EvidenceDir;
        public int Width = 1280, Height = 720;

        /// <summary>견본 화면을 띄운다.</summary>
        public bool Sample;

        /// <summary>월드 화면으로 시작할 맵(맵 id 또는 LDtk 레벨 이름, 예: Shop). 없으면 월드 화면을 띄우지 않는다.</summary>
        public string World;
        /// <summary>시작 도착 지점 id. 없으면 맵 목록에서 그 맵의 첫 도착 지점.</summary>
        public string WorldSpawn;
        /// <summary>맵 목록 파일. 없으면 저장소의 game/world/maps.json.</summary>
        public string WorldMapsPath;

        /// <summary>테스트가 한 프레임씩 돌린다: 고정 시간 간격, 수직 동기 끔, 창이 뒤에 있어도 쉬지 않음.</summary>
        public bool TestMode;

        public string UiSheetPath => Path.Combine(RepoRoot, "Assets", "modernuserinterface-win", "16x16", "Modern_UI_Style_2.png");

        /// <summary>월드 화면의 캐릭터 그림. 레이어 캐릭터는 다음 단계라 지금은 Premade 한 장을 쓴다.</summary>
        public string CharacterSheetPath => Path.Combine(RepoRoot, "Assets", "ModernInteriors", "Characters", "Premade32", "Premade_Character_32x32_02.png");

        /// <param name="environment">환경 변수 읽기. 테스트가 바꿔 넣는다.</param>
        /// <param name="searchFrom">저장소를 찾아 올라가기 시작할 폴더. 기본은 실행 파일 위치.</param>
        public static HostOptions Resolve(string[] args, Func<string, string> environment = null, string searchFrom = null)
        {
            environment ??= Environment.GetEnvironmentVariable;
            var o = new HostOptions
            {
                RepoRoot = environment(RootVariable),
                FontPath = environment(FontVariable),
                EvidenceDir = environment(EvidenceVariable),
                WorldMapsPath = environment(WorldMapsVariable),
            };
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (arg == "--sample") { o.Sample = true; continue; }
                if (i + 1 >= args.Length) throw new InvalidOperationException("값이 빠진 인자: " + arg);
                string value = args[++i];
                switch (arg)
                {
                    case "--root": o.RepoRoot = value; break;
                    case "--font": o.FontPath = value; break;
                    case "--evidence": o.EvidenceDir = value; break;
                    case "--world": o.World = value; break;
                    case "--spawn": o.WorldSpawn = value; break;
                    case "--maps": o.WorldMapsPath = value; break;
                    case "--size":
                        string[] wh = value.Split('x');
                        if (wh.Length != 2 || !int.TryParse(wh[0], out o.Width) || !int.TryParse(wh[1], out o.Height) || o.Width < 320 || o.Height < 180)
                            throw new InvalidOperationException("--size 는 1280x720 같은 꼴로 준다(최소 320x180): " + value);
                        break;
                    default: throw new InvalidOperationException("모르는 인자: " + arg);
                }
            }

            if (string.IsNullOrEmpty(o.RepoRoot))
            {
                for (var d = new DirectoryInfo(searchFrom ?? AppContext.BaseDirectory); d != null; d = d.Parent)
                    if (Directory.Exists(Path.Combine(d.FullName, "Assets", "modernuserinterface-win"))) { o.RepoRoot = d.FullName; break; }
            }
            if (string.IsNullOrEmpty(o.RepoRoot))
                throw new InvalidOperationException($"저장소 위치를 모른다. --root 또는 환경 변수 {RootVariable} 로 준다.");
            if (!File.Exists(o.UiSheetPath))
                throw new InvalidOperationException("저장소 위치가 맞지 않다. UI 그림이 없다: " + o.UiSheetPath);

            if (string.IsNullOrEmpty(o.FontPath)) o.FontPath = FindSystemKoreanFont();
            if (string.IsNullOrEmpty(o.FontPath))
                throw new InvalidOperationException($"한글 글꼴을 찾지 못했다. --font 또는 환경 변수 {FontVariable} 로 TTF 경로를 준다.");
            if (!File.Exists(o.FontPath))
                throw new InvalidOperationException("글꼴 파일이 없다: " + o.FontPath);

            if (string.IsNullOrEmpty(o.EvidenceDir)) o.EvidenceDir = Path.Combine(o.RepoRoot, "output", o.World != null ? "host-world-evidence" : "host-ui-evidence");
            if (o.World != null && !File.Exists(o.CharacterSheetPath))
                throw new InvalidOperationException("캐릭터 그림이 없다: " + o.CharacterSheetPath);
            if (o.World != null && o.WorldMapsPath != null && !File.Exists(o.WorldMapsPath))
                throw new InvalidOperationException("맵 목록 파일이 없다: " + o.WorldMapsPath);
            return o;
        }

        /// <summary>
        /// 운영체제에 깔린 한글 글꼴을 찾는다. 저장소에 글꼴 파일이 들어오기 전까지의 임시 방편이다
        /// (배포할 글꼴 선정은 사용자 결정으로 남아 있다).
        /// </summary>
        private static string FindSystemKoreanFont()
        {
            var folders = new List<string>();
            string system = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
            if (!string.IsNullOrEmpty(system)) folders.Add(system);
            folders.AddRange(new[]
            {
                "/usr/share/fonts/truetype/nanum", "/usr/share/fonts/truetype/noto", "/usr/share/fonts/opentype/noto",
                "/usr/share/fonts/noto-cjk", "/System/Library/Fonts", "/System/Library/Fonts/Supplemental",
            });
            string[] names =
            {
                "malgun.ttf", "NanumGothic.ttf", "NotoSansKR-Regular.ttf", "NotoSansCJK-Regular.ttc",
                "NotoSansCJKkr-Regular.otf", "AppleSDGothicNeo.ttc", "AppleGothic.ttf", "gulim.ttc",
            };
            foreach (string name in names)
                foreach (string folder in folders)
                {
                    string path = Path.Combine(folder, name);
                    if (File.Exists(path)) return path;
                }
            return null;
        }
    }
}
