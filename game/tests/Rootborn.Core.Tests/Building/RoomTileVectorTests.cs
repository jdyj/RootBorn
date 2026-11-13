using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using Rootborn.Core.Building;

namespace Rootborn.Core.Tests.Building
{
    /// <summary>C# 고르기 규칙(RoomTilePlan)이 키트(Python room_tiles.plan_sources)가 낸 시험 표와 칸·부분·픽셀 단위로 같은지. 자동 테스트.
    /// 시험 표만 바뀌고 C#을 고치지 않으면 여기가 깨진다(계획 Review Focus 4).</summary>
    public sealed class RoomTileVectorTests
    {
        private static IEnumerable<TestCaseData> AllCases()
        {
            foreach (var c in RoomVectors.Cases())
                yield return new TestCaseData(c.Id).SetName("EveryCase_MatchesThePythonTable(" + c.Id + ")");
        }

        [TestCaseSource(nameof(AllCases))]
        public void EveryCase_MatchesThePythonTable(string id)
        {
            var expected = RoomVectors.Find(id);
            var plan = expected.Build();

            var errors = new StringBuilder();
            var seen = new HashSet<(int, int)>();
            foreach (var cell in expected.Cells)
            {
                seen.Add((cell.X, cell.Y));
                var got = plan.At(cell.X, cell.Y);
                if (got == null)
                {
                    errors.AppendLine($"({cell.X},{cell.Y}): C# 출처표가 없다");
                    continue;
                }
                if (got.Kind != cell.Kind)
                    errors.AppendLine($"({cell.X},{cell.Y}): kind {got.Kind} != {cell.Kind}");
                int n = System.Math.Max(got.Parts.Count, cell.Parts.Count);
                for (int i = 0; i < n; i++)
                {
                    string want = i < cell.Parts.Count ? cell.Parts[i].ToString() : "(없음)";
                    string have = i < got.Parts.Count ? Describe(got.Parts[i]) : "(없음)";
                    if (want != have)
                    {
                        errors.AppendLine($"({cell.X},{cell.Y}) 부분 {i}: C# {have} / 시험 표 {want}");
                        break;
                    }
                }
            }
            foreach (var c in plan.Cells)
                if (!seen.Contains((c.Cell.X, c.Cell.Y)))
                    errors.AppendLine($"({c.Cell.X},{c.Cell.Y}): 시험 표에 없는 칸을 C#이 냈다");

            Assert.That(errors.Length, Is.EqualTo(0), "사례 " + id + ":\n" + errors);
        }

        [Test]
        public void SourceKinds_MatchTheTableHeader()
        {
            var table = RoomVectors.Load();
            Assert.That(table.CellSize, Is.EqualTo(RoomTilePlan.CellSize));
            var mine = new Dictionary<string, (string, bool)>();
            foreach (var k in TileSources.All)
                mine[k.Name] = (k.Field, k.HasOwner);
            Assert.That(mine, Is.EquivalentTo(table.Sources));
        }

        [Test]
        public void TheTableHasEveryKindOfCase()
        {
            // 시험 표가 비거나 줄면 위 견주기가 아무것도 지키지 않는다.
            Assert.That(RoomVectors.Load().Cases.Count, Is.GreaterThanOrEqualTo(23));
        }

        internal static string Describe(TilePart p) =>
            p.Source + " " + (p.Shade ?? p.Piece) + (p.HasOwner ? " own(" + p.Owner.X + "," + p.Owner.Y + ")" : "") + " px=" + p.Pixels;
    }
}
