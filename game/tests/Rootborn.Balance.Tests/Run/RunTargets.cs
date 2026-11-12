using System;
using System.Collections.Generic;
using System.Linq;
using Rootborn.Core.Careers;
using Rootborn.Core.StudentLife;
using Rootborn.Data;

namespace Rootborn.Balance.Tests
{
    /// <summary>
    /// 전략의 대상 종류("each")의 등록부. 대상은 데이터(GameDefinitions)에서 저절로 나오고 id 순이다. 코드에 대상 id를 적지 않는다.
    /// 새 종류는 <see cref="Kind"/> 한 줄을 등록에 더한다.
    /// </summary>
    internal static class RunTargets
    {
        private sealed class Kind
        {
            public string Name;
            public Func<GameDefinitions, IEnumerable<object>> List;   // 정렬 전 대상들
            public Func<object, string> IdOrNull;                      // 이 종류의 대상이면 id, 아니면 null
        }

        private static readonly Dictionary<string, Kind> Kinds = Register(
            new Kind { Name = "none", List = _ => new object[] { null }, IdOrNull = _ => null },
            new Kind { Name = "facility", List = d => d.All<Facility>(), IdOrNull = t => (t as Facility)?.Id },
            new Kind
            {
                Name = "part-time-work",
                List = d => d.All<IMinigamePartTimeWork>().Where(w => w.Minigame != null && w.TimeCostMinutes > 0),
                IdOrNull = t => (t as IMinigamePartTimeWork)?.Id,
            });

        public static bool Knows(string kind) => kind != null && Kinds.ContainsKey(kind);

        public static IReadOnlyList<object> All(string kind, GameDefinitions definitions)
        {
            if (!Knows(kind)) throw new ArgumentException("모르는 대상 종류다: " + kind, nameof(kind));
            return Kinds[kind].List(definitions).OrderBy(IdOf, StringComparer.Ordinal).ToList();
        }

        /// <summary>대상의 id. 대상이 없으면(null) "".</summary>
        public static string IdOf(object target)
        {
            if (target == null) return "";
            foreach (var kind in Kinds.Values)
            {
                var id = kind.IdOrNull(target);
                if (id != null) return id;
            }
            throw new ArgumentException("등록된 대상 종류가 아니다: " + target.GetType().Name, nameof(target));
        }

        private static Dictionary<string, Kind> Register(params Kind[] kinds)
        {
            var map = new Dictionary<string, Kind>(kinds.Length, StringComparer.Ordinal);
            foreach (var kind in kinds) map.Add(kind.Name, kind);
            return map;
        }
    }
}
