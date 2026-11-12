using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Rootborn.Screens;

namespace Rootborn.Simulation.Rooms
{
    /// <summary>
    /// 같이 하는 방 한 판의 기록을 output/simulation/room-&lt;이름&gt;/에 쓴다:
    /// room.txt(씨앗·방 설정·정책과 방의 일 기록 — 걸음 순서, 끊김, 나가기·들어오기, 서버 끄고 켜기), &lt;자리&gt;/play.txt(그 사람의 행동 — PlayRecord),
    /// summary.md(넓이), failure.md(실패했을 때). <see cref="Replay"/>가 이것을 그대로 다시 돌린다.
    /// </summary>
    public static class RoomRecord
    {
        public static string Directory(string repoRoot, string name) => PlayRecord.Directory(repoRoot, FolderName(name));

        public static string FolderName(string name) => "room-" + name;

        /// <summary>자리 한 사람의 판 이름(PlayRecord가 그 이름의 폴더에 쓴다).</summary>
        public static string PlayerName(string room, string playerId) => Path.Combine(FolderName(room), playerId);

        public static string Write(RoomPlayResult result, PlayAtlas atlas)
        {
            RoomPlaySettings s = result.Settings;
            string dir = Directory(atlas.RepoRoot, s.Name);
            System.IO.Directory.CreateDirectory(dir);
            var screens = ScreenCatalog.Create().Screens.Select(x => x.Id).ToArray();
            var playSettings = new PlaySettings { Days = s.Days, StepsPerDay = s.StepsPerDay, MaxSteps = s.MaxRounds };
            foreach (PlayResult p in result.Players) PlayRecord.Write(p, atlas.RepoRoot, playSettings, screens, atlas);

            var room = new StringBuilder();
            room.AppendLine("# room " + s.Name);
            room.AppendLine("# seed " + s.Seed.ToString(CultureInfo.InvariantCulture));
            room.AppendLine("# players " + s.Players.ToString(CultureInfo.InvariantCulture));
            room.AppendLine("# shared " + string.Join(",", s.SharedPieces));
            room.AppendLine("# policies " + string.Join(",", result.Players.Select(p => p.Policy)));
            room.AppendLine("# days " + s.Days + " steps-per-day " + s.StepsPerDay + " max-rounds " + s.MaxRounds);
            room.AppendLine("# disconnect " + Num(s.DisconnectChance) + " restart-each-day " + s.RestartEachDay + " sit-out " + s.OneSitsOutADay);
            room.AppendLine("# network latency " + Num(s.LatencySeconds) + " jitter " + Num(s.JitterSeconds) + " loss " + Num(s.LossRate));
            room.AppendLine("# columns: 일, 자리(0부터, 방 전체면 -1), 자세히");
            foreach (RoomEvent e in result.Events) room.AppendLine(e.Line);
            File.WriteAllText(Path.Combine(dir, "room.txt"), room.ToString());
            File.WriteAllText(Path.Combine(dir, "summary.md"), Summary(result));
            File.WriteAllLines(Path.Combine(dir, "notes.txt"), result.Notes); // 방의 로그와 추적(ROOTBORN_SIM_ROOM_TRACE=1) 전부
            string failure = Path.Combine(dir, "failure.md");
            PlayFailure f = result.FirstFailure;
            if (f != null) File.WriteAllText(failure, "# " + f.What + "\n\n```\n" + f.Report + "\n```\n");
            else if (File.Exists(failure)) File.Delete(failure);
            result.OutputDir = dir;
            return dir;
        }

        private static string Num(double v) => v.ToString("0.######", CultureInfo.InvariantCulture);

        /// <summary>넓이: 며칠·몇 명·방 설정, 자리마다 지나간 화면·상호작용 종류, 넣고 꺼낸 횟수, 먼저 온 것만 된 횟수, 끊김 횟수.</summary>
        public static string Summary(RoomPlayResult r)
        {
            RoomPlaySettings s = r.Settings;
            var sb = new StringBuilder();
            sb.AppendLine("# 같이 하는 방 " + s.Name);
            sb.AppendLine();
            sb.AppendLine("- 씨앗 " + s.Seed + ", " + s.Describe());
            sb.AppendLine("- 방의 시계 끝 " + r.ServerDays + "일째, 서버 다시 켜기 " + r.Restarts + "번, 차례 " + r.Rounds + ", 프레임 " + r.Frames + ", 방의 일 " + r.Events.Count);
            PlayFailure f = r.FirstFailure;
            sb.AppendLine("- 결과: " + (f == null ? "통과" : "실패 — " + f.What.Split('\n')[0]));
            sb.AppendLine();
            sb.AppendLine("## 센 것");
            foreach (var pair in r.Counts) sb.AppendLine("- " + pair.Key + ": " + pair.Value);
            sb.AppendLine();
            sb.AppendLine("## 자리");
            foreach (PlayResult p in r.Players)
            {
                sb.AppendLine("- " + Path.GetFileName(p.Name) + " `" + p.Policy + "`: 걸음 " + p.Steps.Count + ", 넘긴 날 " + p.DaysCompleted + ", 다시 읽기 " + p.Reloads
                    + ", 끝낸 퀘스트 " + p.QuestsCompleted + ", 결과 " + (p.Failure == null ? "통과" : "실패 — " + p.Failure.What.Split('\n')[0]));
                sb.AppendLine("  - 화면 (" + p.ScreensOpened.Count + "): " + string.Join(", ", p.ScreensOpened));
                sb.AppendLine("  - 상호작용 종류 (" + p.InteractionKinds.Count + "): " + string.Join(", ", p.InteractionKinds));
                sb.AppendLine("  - 끝 가방: " + string.Join(", ", p.FinalState.Where(l => l.StartsWith("item ", StringComparison.Ordinal)).Select(l => l.Substring(5))));
            }
            var screens = new SortedSet<string>(r.Players.SelectMany(p => p.ScreensOpened), StringComparer.Ordinal);
            var kinds = new SortedSet<string>(r.Players.SelectMany(p => p.InteractionKinds), StringComparer.Ordinal);
            sb.AppendLine();
            sb.AppendLine("## 모두 합쳐");
            sb.AppendLine("- 화면 (" + screens.Count + "): " + string.Join(", ", screens));
            sb.AppendLine("- 상호작용 종류 (" + kinds.Count + "): " + string.Join(", ", kinds));
            if (r.Notes.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("## 메모");
                foreach (var g in r.Notes.GroupBy(n => n).OrderByDescending(g => g.Count()).Take(30)) sb.AppendLine("- " + g.Key + (g.Count() > 1 ? " ×" + g.Count() : ""));
            }
            return sb.ToString();
        }

        /// <summary>기록 폴더의 방 설정과 일 기록, 자리마다의 행동을 읽어 같은 판을 다시 돈다(같은 몸 종류 — 창 없는 몸).</summary>
        public static RoomPlayResult Replay(PlayAtlas atlas, string dir, string newName)
        {
            string file = Path.Combine(dir, "room.txt");
            if (!File.Exists(file)) throw new FileNotFoundException("방 기록이 없다", file);
            var settings = new RoomPlaySettings { Name = newName };
            var events = new List<RoomEvent>();
            foreach (string line in File.ReadAllLines(file))
            {
                if (line.StartsWith("# ", StringComparison.Ordinal)) { Header(settings, line.Substring(2).Split(' ')); continue; }
                if (RoomEvent.TryParse(line, out RoomEvent e)) events.Add(e);
            }
            var policies = new List<IPlayPolicy>();
            for (int i = 0; i < settings.Players; i++)
            {
                string play = Path.Combine(dir, "p" + (i + 1).ToString(CultureInfo.InvariantCulture), "play.txt");
                policies.Add(new ReplayPolicy(PlayRecord.ReadKeys(play)));
            }
            settings.Days = 1000; // 재생은 일 기록이 끝을 정한다
            using var room = new RoomPlay(atlas, settings, policies, events);
            RoomPlayResult result = room.Run();
            result.Notes.Add("재생한 기록: " + dir + " (일 " + events.Count + ")");
            return result;
        }

        private static void Header(RoomPlaySettings s, string[] w)
        {
            double D(int i) => double.Parse(w[i], CultureInfo.InvariantCulture);
            int I(int i) => int.Parse(w[i], CultureInfo.InvariantCulture);
            switch (w[0])
            {
                case "seed": s.Seed = I(1); break;
                case "players": s.Players = I(1); break;
                case "shared": s.SharedPieces = w.Length > 1 ? w[1].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries) : Array.Empty<string>(); break;
                case "days": s.Days = I(1); s.StepsPerDay = I(3); s.MaxRounds = I(5); break;
                case "disconnect": s.DisconnectChance = D(1); s.RestartEachDay = bool.Parse(w[3]); s.OneSitsOutADay = bool.Parse(w[5]); break;
                case "network": s.LatencySeconds = D(2); s.JitterSeconds = D(4); s.LossRate = D(6); break;
            }
        }
    }
}
