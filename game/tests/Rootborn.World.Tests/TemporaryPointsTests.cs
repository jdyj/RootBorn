using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Rootborn.World.Tests
{
    /// <summary>
    /// 자동 테스트(창 없음): 새 맵 전까지 둔 임시 지점(interactions.json의 "temporary": true)끼리의 간격.
    /// 화면 근거가 아니다.
    /// </summary>
    public sealed class TemporaryPointsTests
    {
        private static InteractionData Interactions => InteractionData.Load(Path.Combine(Repo.Root, InteractionData.DefaultRelativePath));

        /// <summary>
        /// 자리가 다른 두 임시 지점은 상호작용 범위(radiusCells)보다 멀리 둔다. 그래서 한 지점 칸에 서면 다른 임시 지점은 범위 밖이라
        /// 대상을 다투지 않는다(서로 가리지 않는다). 같은 자리에 겹쳐 둔 것(장소 방문 + 장소 활동)은 우선순위로 고르도록 일부러 둔 것이라 뺀다.
        /// </summary>
        [Test]
        public void TemporaryPointsAtDifferentSpots_AreFartherApartThanTheInteractionRange()
        {
            InteractionData data = Interactions;
            WorldMapLoader loader = Repo.Loader();
            var tooClose = new List<string>();
            foreach (string mapId in data.MapIds)
            {
                var points = new List<InteractionThing>();
                foreach (InteractionThing t in data.Map(mapId).Things)
                    if (t.Temporary) points.Add(t);
                if (points.Count < 2) continue;
                float reach = data.RadiusCells * loader.LoadMap(mapId).World.Grid.CellSize;
                for (int i = 0; i < points.Count; i++)
                {
                    for (int j = i + 1; j < points.Count; j++)
                    {
                        InteractionThing a = points[i], b = points[j];
                        float distance = MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
                        if (distance == 0f || distance > reach) continue;
                        tooClose.Add(mapId + ": " + a.Kind + "/" + a.Id + " (" + a.X + ", " + a.Y + ")와 " + b.Kind + "/" + b.Id + " (" + b.X + ", " + b.Y + ")가 "
                            + distance + "px — 상호작용 범위 " + reach + "px 안");
                    }
                }
            }
            Assert.That(tooClose, Is.Empty, string.Join("\n", tooClose));
        }

        /// <summary>
        /// game/world/README.md 8절 "임시 연결 목록"이 데이터의 임시 표시와 같다: 자리가 있는 항목(상호작용 지점, 빛 지점, 도착 지점)은
        /// README에 `이름` (x, y)로 적고 그 집합이 데이터와 같아야 하며, 자리가 없는 항목(문 경로, 이동 구역)은 `이름`이 적혀 있어야 한다.
        /// 데이터에 임시 항목을 더하거나 옮기거나 지우고 목록을 그대로 두면 실패한다.
        /// </summary>
        [Test]
        public void TheReadmeTemporaryList_MatchesTheTemporaryMarksInTheData()
        {
            string readme = File.ReadAllText(Path.Combine(Repo.Root, "game", "world", "README.md"));
            int start = readme.IndexOf("## 8.", StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), "README에 8절이 없다");
            int end = readme.IndexOf("\n## ", start + 1, StringComparison.Ordinal);
            string section = end < 0 ? readme.Substring(start) : readme.Substring(start, end - start);

            var listed = new SortedSet<string>(StringComparer.Ordinal);
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(section, @"`([^`]+)`\s*\((-?[0-9.]+),\s*(-?[0-9.]+)\)"))
                listed.Add(Point(m.Groups[1].Value, float.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture), float.Parse(m.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture)));

            var data = new SortedSet<string>(StringComparer.Ordinal);
            var names = new List<string>();
            InteractionData interactions = Interactions;
            foreach (string mapId in interactions.MapIds)
                foreach (InteractionThing t in interactions.Map(mapId).Things)
                    if (t.Temporary) data.Add(Point(t.Id, t.X, t.Y));
            WorldLightingData lights = WorldLightingData.Load(Path.Combine(Repo.Root, WorldLightingData.DefaultRelativePath));
            foreach (PointLightDef l in lights.Temporary) data.Add(Point(l.Name, l.X, l.Y));
            foreach (MapEntry map in Repo.Loader().Catalog.Maps)
                foreach (TemporaryLink link in map.Temporary)
                {
                    if (map.Spawns.TryGetValue(link.Name, out var at) && link.Kind == "spawn") data.Add(Point(link.Name, at.X, at.Y));
                    else names.Add(link.Name);
                }

            Assert.Multiple(() =>
            {
                Assert.That(data.Except(listed), Is.Empty, "데이터에는 임시인데 README 8절에 (자리와 함께) 없다");
                Assert.That(listed.Except(data), Is.Empty, "README 8절에는 있는데 데이터의 임시 항목이 아니다(또는 자리가 다르다)");
                foreach (string name in names) Assert.That(section, Does.Contain("`" + name + "`"), "README 8절에 임시 연결 " + name + "이 없다");
            });
        }

        private static string Point(string name, float x, float y)
            => name + " (" + x.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + ", " + y.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + ")";
    }
}
