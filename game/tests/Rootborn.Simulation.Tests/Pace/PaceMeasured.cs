using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Rootborn.Simulation.Pace
{
    /// <summary>
    /// 가까운 층(창 없는 몸, 프레임 단위)에서 잰 값을 먼 층(GameSession 명령 단위)에 넘기는 파일(pace-measured.json):
    /// 상호작용 종류마다 그 지점까지 걸은 실제 초(평균, "어디서 어디까지"의 줄 포함)와 그 지점이 연 화면에서 새로 나온 글자 수·고르기 횟수(평균).
    /// 걷기는 실제로 돈 프레임에서 잰 값이고, 글자·고르기는 가까운 층의 관찰에서 센 어림이다. 엔티티 id·종류 이름은 데이터에서 온 그대로다.
    /// 이 파일은 Simulation과 Balance 테스트가 같이 쓴다(Balance는 링크로 컴파일한다).
    /// </summary>
    public sealed class PaceMeasured
    {
        public const string FileName = "pace-measured.json";
        /// <summary>먼 층이 읽을 파일(없으면 기본 자리 <see cref="DefaultPath"/>).</summary>
        public const string PathVariable = "ROOTBORN_PACE_MEASURED";
        /// <summary>그날 첫 걷기의 출발(그 전에 들른 지점이 없다).</summary>
        public const string DayStart = "(그날 처음)";

        public sealed class Mean
        {
            public double Value;
            public int Count;
        }

        public sealed class Screen
        {
            public double Chars, Choices;
            public int Count;
        }

        public sealed class Walk
        {
            public string From, To, Kind;
            public double Seconds;
            public int Count;
        }

        public string Source = string.Empty;
        public SortedDictionary<string, Mean> WalkSecondsByKind = new SortedDictionary<string, Mean>(StringComparer.Ordinal);
        public SortedDictionary<string, Screen> ScreenByKind = new SortedDictionary<string, Screen>(StringComparer.Ordinal);
        public List<Walk> Walks = new List<Walk>();

        public static string DefaultPath(string repoRoot) => Path.Combine(repoRoot, "output", "simulation", "pace", "measured", FileName);

        /// <summary>환경 변수가 있으면 그 파일, 없으면 기본 자리.</summary>
        public static string PathFromEnvironment(string repoRoot)
        {
            string v = Environment.GetEnvironmentVariable(PathVariable);
            return string.IsNullOrWhiteSpace(v) ? DefaultPath(repoRoot) : v;
        }

        public double? WalkSeconds(string kind) => kind != null && WalkSecondsByKind.TryGetValue(kind, out Mean m) && m.Count > 0 ? m.Value : (double?)null;

        public Screen ScreenFor(string kind) => kind != null && ScreenByKind.TryGetValue(kind, out Screen s) && s.Count > 0 ? s : null;

        /// <summary>
        /// 가까운 층 판들의 걸음에서 잰다. 걷기: 걷기 분류의 걸음(문 포함)을 다음 상호작용 걸음까지 더해 "앞 지점 → 그 지점" 한 줄(날이 바뀌면 출발은 그날 처음).
        /// 화면: 상호작용 걸음 뒤 다음 걷기까지(같은 날)의 새 글자·고르기(미니게임 걸음 빼고)를 그 종류의 한 번으로.
        /// </summary>
        public static PaceMeasured FromRuns(IEnumerable<(string Name, IReadOnlyList<PaceStep> Steps)> runs, double secondsPerFrame)
        {
            var m = new PaceMeasured();
            var walkSum = new Dictionary<string, (double Sum, int Count)>(StringComparer.Ordinal);
            var pairSum = new Dictionary<(string, string, string), (double Sum, int Count)>();
            var screenSum = new Dictionary<string, (double Chars, double Choices, int Count)>(StringComparer.Ordinal);
            var names = new List<string>();
            foreach (var (name, steps) in runs)
            {
                names.Add(name);
                string from = DayStart, openKind = null;
                double walked = 0, chars = 0, choices = 0;
                int day = int.MinValue;
                void CloseScreen()
                {
                    if (openKind == null) return;
                    var s = screenSum.TryGetValue(openKind, out var v) ? v : (0, 0, 0);
                    screenSum[openKind] = (s.Item1 + chars, s.Item2 + choices, s.Item3 + 1);
                    openKind = null; chars = 0; choices = 0;
                }
                foreach (PaceStep st in steps)
                {
                    if (st.Day != day) { CloseScreen(); day = st.Day; from = DayStart; walked = 0; }
                    if (st.Category == PaceCategories.Walk)
                    {
                        CloseScreen();
                        walked += st.GameSeconds(secondsPerFrame);
                        bool arrives = st.InteractionKind != null && st.Key.StartsWith("walk:", StringComparison.Ordinal);
                        if (!arrives) continue;
                        var w = walkSum.TryGetValue(st.InteractionKind, out var a) ? a : (0, 0);
                        walkSum[st.InteractionKind] = (w.Item1 + walked, w.Item2 + 1);
                        var key = (from, st.Subject ?? string.Empty, st.InteractionKind);
                        var p = pairSum.TryGetValue(key, out var b) ? b : (0, 0);
                        pairSum[key] = (p.Item1 + walked, p.Item2 + 1);
                        from = st.Subject ?? st.InteractionKind;
                        walked = 0;
                        openKind = st.InteractionKind;
                        continue;
                    }
                    if (openKind != null && st.Category != PaceCategories.Minigame) { chars += st.NewChars; choices += st.Choices; }
                }
                CloseScreen();
            }
            m.Source = string.Join(", ", names);
            foreach (var kv in walkSum) m.WalkSecondsByKind[kv.Key] = new Mean { Value = kv.Value.Sum / kv.Value.Count, Count = kv.Value.Count };
            foreach (var kv in screenSum) m.ScreenByKind[kv.Key] = new Screen { Chars = kv.Value.Chars / kv.Value.Count, Choices = kv.Value.Choices / kv.Value.Count, Count = kv.Value.Count };
            foreach (var kv in pairSum.OrderBy(k => k.Key.Item3, StringComparer.Ordinal).ThenBy(k => k.Key.Item1, StringComparer.Ordinal).ThenBy(k => k.Key.Item2, StringComparer.Ordinal))
                m.Walks.Add(new Walk { From = kv.Key.Item1, To = kv.Key.Item2, Kind = kv.Key.Item3, Seconds = kv.Value.Sum / kv.Value.Count, Count = kv.Value.Count });
            return m;
        }

        private static readonly JsonSerializerOptions Json = new JsonSerializerOptions { IncludeFields = true, WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

        public void Write(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            File.WriteAllText(path, JsonSerializer.Serialize(this, Json));
        }

        /// <summary>파일이 없으면 null.</summary>
        public static PaceMeasured Read(string path) => File.Exists(path) ? JsonSerializer.Deserialize<PaceMeasured>(File.ReadAllText(path), Json) : null;
    }
}
