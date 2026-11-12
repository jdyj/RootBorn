using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Rootborn.Net.Session;

namespace Rootborn.Server
{
    /// <summary>
    /// 서버 콘솔 명령 등록 표(표준 입력 한 줄 = 명령 하나). 명령 종류를 switch로 닫지 않는다 — <see cref="Add"/> 한 줄로 더한다.
    /// 명령은 루프 스레드에서 돈다(<see cref="ServerRunner.Enqueue"/>로 넣은 줄을 틱 사이에 꺼낸다). 출력은 운영 로그(현지화 대상 아님).
    /// </summary>
    public sealed class ServerCommands
    {
        private readonly Dictionary<string, Entry> _byName = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        private readonly List<Entry> _order = new List<Entry>(8);

        private sealed class Entry
        {
            public string Name;
            public string Summary;
            public Action<ServerRunner, string> Run;
        }

        public IReadOnlyCollection<string> Names
        {
            get
            {
                var names = new List<string>(_order.Count);
                foreach (var e in _order) names.Add(e.Name);
                return names;
            }
        }

        /// <param name="name">명령 이름(대소문자 무관, 공백 없이).</param>
        /// <param name="summary">help에 보일 한 줄.</param>
        /// <param name="run">(서버, 이름 뒤의 나머지 글자).</param>
        public ServerCommands Add(string name, string summary, Action<ServerRunner, string> run)
        {
            if (string.IsNullOrWhiteSpace(name) || name.IndexOf(' ') >= 0) throw new ArgumentException("명령 이름은 공백 없는 한 낱말이다: " + name, nameof(name));
            if (run == null) throw new ArgumentNullException(nameof(run));
            if (_byName.ContainsKey(name)) throw new ArgumentException("이미 있는 명령: " + name, nameof(name));
            var entry = new Entry { Name = name, Summary = summary ?? string.Empty, Run = run };
            _byName[name] = entry;
            _order.Add(entry);
            return this;
        }

        /// <summary>한 줄을 명령으로 돌린다. 빈 줄은 무시하고, 모르는 명령은 도움말을 가리킨다.</summary>
        public void Run(ServerRunner runner, string line)
        {
            if (runner == null) throw new ArgumentNullException(nameof(runner));
            string text = (line ?? string.Empty).Trim();
            if (text.Length == 0) return;
            int space = text.IndexOf(' ');
            string name = space < 0 ? text : text.Substring(0, space);
            string rest = space < 0 ? string.Empty : text.Substring(space + 1).Trim();
            if (!_byName.TryGetValue(name, out var entry))
            {
                runner.Log("모르는 명령: " + name + " — help 로 명령 목록을 본다");
                return;
            }

            entry.Run(runner, rest);
        }

        /// <summary>help가 찍는 줄들.</summary>
        public IEnumerable<string> HelpLines()
        {
            yield return "명령(한 줄에 하나):";
            foreach (var e in _order) yield return "  " + e.Name.PadRight(8) + " " + e.Summary;
        }

        /// <summary>기본 명령: status, players, save, help, stop.</summary>
        public static ServerCommands CreateDefault()
        {
            var r = new ServerCommands();
            r.Add("status", "방의 날·시각, 들어와 있는 사람, 같이 쓰는 것, 가동 시간, 마지막 저장 시각", (s, _) => s.LogLines(StatusLines(s)));
            r.Add("players", "들어와 있는 사람(이름·신원)", (s, _) => s.LogLines(PlayerLines(s)));
            r.Add("save", "지금 방 상태를 저장한다", (s, _) => s.SaveNow("save 명령"));
            r.Add("help", "이 목록", (s, _) => s.LogLines(s.Commands.HelpLines()));
            r.Add("stop", "저장하고 끝낸다", (s, _) => s.RequestStop("stop 명령"));
            return r;
        }

        public static IEnumerable<string> StatusLines(ServerRunner s)
        {
            var room = s.Room;
            yield return "방: 슬롯 " + s.SaveSlot + ", " + room.DayAndTime + ", 정원 " + room.MaxPlayers + ", 같이 쓰는 것 " + room.ShareSettings;
            yield return "들어와 있는 사람: " + room.Players.Count + "명" + (room.Players.Count == 0 ? " (아무도 없으면 방의 시계가 멈춘다)" : "");
            foreach (var line in PlayerRows(s)) yield return line;
            yield return "가동 시간: " + Duration(s.UptimeSeconds) + " (틱 " + s.Ticks.ToString(CultureInfo.InvariantCulture) + ")";
            var last = s.LastSaved;
            yield return "마지막 저장: " + (last.HasValue ? last.Value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) : "아직 없음")
                + (s.AutosaveSeconds > 0 ? ", 주기 저장 " + s.AutosaveSeconds.ToString("0.###", CultureInfo.InvariantCulture) + "초마다" : ", 주기 저장 꺼짐");
        }

        public static IEnumerable<string> PlayerLines(ServerRunner s)
        {
            yield return "들어와 있는 사람: " + s.Room.Players.Count + "명 / 정원 " + s.Room.MaxPlayers;
            foreach (var line in PlayerRows(s)) yield return line;
        }

        private static IEnumerable<string> PlayerRows(ServerRunner s)
        {
            var players = new List<ServerPlayer>(s.Room.Players);
            players.Sort((a, b) => a.JoinIndex.CompareTo(b.JoinIndex));
            foreach (var p in players)
            {
                var sb = new StringBuilder(64);
                sb.Append("  ").Append(PlayerLabel(p)).Append(" (번호 ").Append(p.NetId.ToString(CultureInfo.InvariantCulture)).Append(')');
                if (p.HasPose) sb.Append(", 맵 ").Append(p.LastPose.MapId);
                yield return sb.ToString();
            }
        }

        /// <summary>들어올 때 보낸 이름이 있으면 "이름 [신원]", 없으면 신원만(이름은 화면 표시용이라 신원과 겹칠 수 있다).</summary>
        public static string PlayerLabel(ServerPlayer player)
            => string.IsNullOrWhiteSpace(player.Name) ? player.PlayerId : player.Name + " [" + player.PlayerId + "]";

        public static string Duration(double seconds)
        {
            long total = (long)Math.Max(0, Math.Floor(seconds));
            long days = total / 86400, hours = total / 3600 % 24, minutes = total / 60 % 60, secs = total % 60;
            return (days > 0 ? days.ToString(CultureInfo.InvariantCulture) + "일 " : "")
                   + hours.ToString("00", CultureInfo.InvariantCulture) + ":" + minutes.ToString("00", CultureInfo.InvariantCulture) + ":" + secs.ToString("00", CultureInfo.InvariantCulture);
        }
    }
}
