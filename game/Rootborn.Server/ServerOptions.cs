using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using Rootborn.Net.Room;

namespace Rootborn.Server
{
    /// <summary>
    /// 서버 실행 값. Host(HostOptions)와 같은 방식: 실행 인자가 먼저, 다음이 환경 변수. 이름 규칙도 같다(--save-root ↔ ROOTBORN_SAVE_ROOT).
    /// 세이브 폴더·슬롯·듣는 주소·포트는 기본값이 없다 — 잘못된 곳에 방을 열거나 남의 세이브를 덮지 않게 반드시 받는다.
    /// 경로·주소·포트를 코드에 적지 않는다.
    /// </summary>
    public sealed class ServerOptions
    {
        public const string RootVariable = "ROOTBORN_REPO_ROOT";
        public const string DataVariable = "ROOTBORN_DATA_DIR";
        public const string SaveRootVariable = "ROOTBORN_SAVE_ROOT";
        public const string SlotVariable = "ROOTBORN_SAVE_SLOT";
        public const string BindVariable = "ROOTBORN_SERVER_BIND";
        public const string PortVariable = "ROOTBORN_SERVER_PORT";
        public const string MaxPlayersVariable = "ROOTBORN_MAX_PLAYERS";
        public const string ShareVariable = "ROOTBORN_ROOM_SHARE";
        public const string DayCopiesVariable = "ROOTBORN_DAY_COPIES";

        /// <summary>--share 값 "none": 시간만 같이 쓴다.</summary>
        public const string ShareNothing = "none";

        public static readonly string HelpText = string.Join(Environment.NewLine, new[]
        {
            "Rootborn.Server — 화면 없는 데디케이티드 서버(방 하나).",
            "",
            "인자(환경 변수도 같은 값, 인자가 먼저):",
            "  --save-root <세이브 폴더>   " + SaveRootVariable + "   반드시 준다",
            "  --slot <슬롯 id>            " + SlotVariable + "   반드시 준다. 없으면 새 방을 만들고, 있으면 이어서 연다",
            "  --bind <IP 주소>            " + BindVariable + "   반드시 준다. 듣는 주소(이 기계의 LAN 주소 등)",
            "  --port <포트>               " + PortVariable + "   반드시 준다. 0이면 운영체제가 빈 포트를 준다(로그에 찍힌다)",
            "  --max-players <정원>        " + MaxPlayersVariable + "   1~" + RoomServerSettings.MaxPlayersLimit + ", 기본 4",
            "  --share <조각,...>          " + ShareVariable + "   같이 쓰는 것(방을 처음 만들 때만): " + string.Join(", ", Rootborn.Core.Room.RoomPieceCatalog.CreateDefault().Names)
                + " 중 쉼표로, 또는 " + ShareNothing + ". 기본 " + ShareNothing + "(시간만). 시간은 항상, 가방·개인 성장은 항상 각자",
            "  --day-copies <N>            " + DayCopiesVariable + "   날이 바뀔 때 남기는 슬롯 사본 수(방 파일과 손님 세이브), 기본 " + RoomServerSettings.DefaultDayCopies + ", 0이면 안 남김",
            "  --root <저장소>             " + RootVariable + "   데이터는 <저장소>/data. 없으면 실행 파일 위치에서 위로 찾는다",
            "  --data <데이터 폴더>        " + DataVariable + "   --root 대신 데이터 폴더를 바로 준다",
            "  --help                      이 도움말",
            "",
            "끝내기: Ctrl+C, 종료 신호, 또는 콘솔에 stop 입력. 방 상태를 저장하고 닫는다.",
        });

        public string RepoRoot;
        public string DataDirectory;
        public string SaveRoot;
        public string SaveSlot;
        public IPAddress BindAddress;
        public int Port;
        public int MaxPlayers = 4;
        public int DayCopies = RoomServerSettings.DefaultDayCopies;

        /// <summary>같이 쓰는 조각 이름(방을 처음 만들 때만 쓴다). 비어 있으면 시간만.</summary>
        public string[] SharedPieces = Array.Empty<string>();

        public bool HelpRequested;

        /// <param name="environment">환경 변수 읽기. 테스트가 바꿔 넣는다.</param>
        /// <param name="searchFrom">저장소를 찾아 올라가기 시작할 폴더. 기본은 실행 파일 위치.</param>
        public static ServerOptions Resolve(string[] args, Func<string, string> environment = null, string searchFrom = null)
        {
            environment ??= Environment.GetEnvironmentVariable;
            args ??= Array.Empty<string>();
            var o = new ServerOptions();
            string root = environment(RootVariable);
            string data = environment(DataVariable);
            string saveRoot = environment(SaveRootVariable);
            string slot = environment(SlotVariable);
            string bind = environment(BindVariable);
            string port = environment(PortVariable);
            string maxPlayers = environment(MaxPlayersVariable);
            string share = environment(ShareVariable);
            string dayCopies = environment(DayCopiesVariable);

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (arg == "--help" || arg == "-h")
                {
                    o.HelpRequested = true;
                    return o;
                }

                if (i + 1 >= args.Length) throw new ServerOptionsException("값이 빠진 인자: " + arg);
                string value = args[++i];
                switch (arg)
                {
                    case "--root": root = value; break;
                    case "--data": data = value; break;
                    case "--save-root": saveRoot = value; break;
                    case "--slot": slot = value; break;
                    case "--bind": bind = value; break;
                    case "--port": port = value; break;
                    case "--max-players": maxPlayers = value; break;
                    case "--share": share = value; break;
                    case "--day-copies": dayCopies = value; break;
                    default: throw new ServerOptionsException("모르는 인자: " + arg);
                }
            }

            var missing = new List<string>(4);
            if (string.IsNullOrWhiteSpace(saveRoot)) missing.Add("--save-root <세이브 폴더> (또는 " + SaveRootVariable + ")");
            if (string.IsNullOrWhiteSpace(slot)) missing.Add("--slot <슬롯 id> (또는 " + SlotVariable + ")");
            if (string.IsNullOrWhiteSpace(bind)) missing.Add("--bind <IP 주소> (또는 " + BindVariable + ")");
            if (string.IsNullOrWhiteSpace(port)) missing.Add("--port <포트> (또는 " + PortVariable + ")");
            if (missing.Count > 0) throw new ServerOptionsException("빠진 값:" + Environment.NewLine + "  " + string.Join(Environment.NewLine + "  ", missing), missing);

            o.SaveRoot = saveRoot;
            o.SaveSlot = slot;
            if (!IPAddress.TryParse(bind, out o.BindAddress))
                throw new ServerOptionsException("--bind 는 IP 주소다(예: 이 기계의 LAN 주소): " + bind);
            if (!int.TryParse(port, NumberStyles.Integer, CultureInfo.InvariantCulture, out o.Port) || o.Port < 0 || o.Port > 65535)
                throw new ServerOptionsException("--port 는 0~65535 정수다: " + port);
            if (!string.IsNullOrWhiteSpace(maxPlayers)
                && (!int.TryParse(maxPlayers, NumberStyles.Integer, CultureInfo.InvariantCulture, out o.MaxPlayers) || o.MaxPlayers < 1 || o.MaxPlayers > RoomServerSettings.MaxPlayersLimit))
                throw new ServerOptionsException("--max-players 는 1~" + RoomServerSettings.MaxPlayersLimit + " 정수다: " + maxPlayers);

            if (!string.IsNullOrWhiteSpace(dayCopies)
                && (!int.TryParse(dayCopies, NumberStyles.Integer, CultureInfo.InvariantCulture, out o.DayCopies) || o.DayCopies < 0))
                throw new ServerOptionsException("--day-copies 는 0 이상 정수다: " + dayCopies);
            o.SharedPieces = ParseShare(share);

            if (string.IsNullOrWhiteSpace(data))
            {
                if (string.IsNullOrWhiteSpace(root)) root = FindRepoRoot(searchFrom ?? AppContext.BaseDirectory);
                if (string.IsNullOrWhiteSpace(root))
                    throw new ServerOptionsException($"게임 데이터 위치를 모른다. --root <저장소>(또는 {RootVariable}) 또는 --data <데이터 폴더>(또는 {DataVariable})로 준다.");
                data = Path.Combine(root, "data");
            }

            if (!Directory.Exists(data)) throw new ServerOptionsException("데이터 폴더가 없다: " + data + " (--root 또는 --data)");
            o.RepoRoot = root;
            o.DataDirectory = data;
            return o;
        }

        // 쉼표로 나눈 조각 이름. "none"이나 빈 값은 시간만. 모르는 이름은 오류(아는 이름을 알려 준다).
        private static string[] ParseShare(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || string.Equals(value.Trim(), ShareNothing, StringComparison.Ordinal)) return Array.Empty<string>();
            var catalog = Rootborn.Core.Room.RoomPieceCatalog.CreateDefault();
            var names = new List<string>();
            foreach (var part in value.Split(','))
            {
                var name = part.Trim();
                if (name.Length == 0) continue;
                if (!catalog.Knows(name))
                    throw new ServerOptionsException("--share 에 모르는 조각: " + name + " (" + string.Join(", ", catalog.Names) + " 또는 " + ShareNothing + ")");
                if (!names.Contains(name)) names.Add(name);
            }

            return names.ToArray();
        }

        private static string FindRepoRoot(string from)
        {
            for (var d = new DirectoryInfo(from); d != null; d = d.Parent)
                if (File.Exists(Path.Combine(d.FullName, "data", "registry.json"))) return d.FullName;
            return null;
        }
    }

    public sealed class ServerOptionsException : Exception
    {
        public ServerOptionsException(string message, IReadOnlyList<string> missing = null) : base(message)
        {
            Missing = missing ?? Array.Empty<string>();
        }

        /// <summary>빠진 값마다 한 줄.</summary>
        public IReadOnlyList<string> Missing { get; }
    }
}
