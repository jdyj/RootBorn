using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Rootborn.Data.Tests.Integrity
{
    /// <summary>검사가 찾은 빈 곳 하나. 무엇(Kind)이 어디(Where)에 있는지, 자세한 내용(Detail).</summary>
    internal sealed class DataGap
    {
        public DataGap(string kind, string where, string detail)
        {
            Kind = kind;
            Where = where;
            Detail = detail;
        }

        public string Kind { get; }
        public string Where { get; }
        public string Detail { get; }

        public override string ToString() => Kind + " | " + Where + " | " + Detail;
    }

    /// <summary>지금 데이터에 있고 고치지 않기로 한 빈 곳 하나: 무엇, 어디, 이유.</summary>
    internal sealed class KnownGap
    {
        public KnownGap(string kind, string where, string reason)
        {
            Kind = kind;
            Where = where;
            Reason = reason;
        }

        public string Kind { get; }
        public string Where { get; }
        public string Reason { get; }

        public bool Matches(DataGap gap) => gap.Kind == Kind && gap.Where == Where;

        public override string ToString() => Kind + " | " + Where + " — " + Reason;
    }

    internal static class GapAssert
    {
        /// <summary>
        /// 찾은 빈 곳이 알려진 목록 안에만 있고, 알려진 목록의 그 종류(kinds)는 모두 아직 있는지 본다.
        /// 목록 밖의 새 빈 곳은 데이터를 고치라는 뜻이고, 사라진 항목은 목록에서 지우라는 뜻이다.
        /// </summary>
        public static void OnlyKnown(IReadOnlyCollection<DataGap> found, IEnumerable<KnownGap> known, params string[] kinds)
        {
            var knownOfKinds = known.Where(k => kinds.Contains(k.Kind)).ToList();
            var unexpected = found.Where(g => !knownOfKinds.Any(k => k.Matches(g))).Select(g => g.ToString()).ToList();
            var gone = knownOfKinds.Where(k => !found.Any(k.Matches)).Select(k => k.ToString()).ToList();
            TestContext.WriteLine($"찾은 빈 곳 {found.Count}건(알려진 것 {found.Count - unexpected.Count}건)");
            Assert.That(unexpected, Is.Empty,
                "새 빈 곳(데이터를 고치거나, 고칠 수 없는 이유가 있으면 KnownDataGaps에 이유와 함께 적는다):\n" + string.Join("\n", unexpected));
            Assert.That(gone, Is.Empty, "알려진 빈 곳이 사라졌다(KnownDataGaps에서 지운다):\n" + string.Join("\n", gone));
        }
    }
}
