using System;
using System.Collections.Generic;
using System.Linq;

namespace Rootborn.Data
{
    /// <summary>
    /// 만든 정의 전부. id로 조회하고, T로 받을 수 있는 객체(기반 클래스·인터페이스 포함)를 모두 대상으로 한다.
    /// All&lt;T&gt;() 순서: registry.json에서 그 객체들의 id를 가장 많이 담은 목록의 순서, 그 목록에 없는 것은 뒤에 (파일 경로, 배열 순서).
    /// 게임 중에는 이 표 조회만 한다.
    /// </summary>
    public sealed class GameDefinitions
    {
        private readonly List<Entry> _entries;
        private readonly Dictionary<string, List<Entry>> _byId;
        private readonly EntityTable _table;
        private readonly Dictionary<Type, object> _allCache = new Dictionary<Type, object>();

        internal GameDefinitions(List<Entry> entries, EntityTable table)
        {
            _entries = entries;
            _table = table;
            _byId = new Dictionary<string, List<Entry>>(StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                if (!_byId.TryGetValue(entry.Id, out var list)) _byId[entry.Id] = list = new List<Entry>(1);
                list.Add(entry);
            }
        }

        public int Count => _entries.Count;

        /// <summary>지갑으로 가는 통화 아이템(game/economy.json, EconomySettings). 없으면 null — 돈도 가방의 아이템이다.</summary>
        public Rootborn.Core.Inventory.ItemDefinition Currency { get; internal set; }

        /// <summary>흐르는 시간의 규칙(game/time.json + data/time의 하루 길이, TimeSettings). 하루 길이가 없는 데이터면 null.</summary>
        public Rootborn.Core.Time.TimeRules TimeRules { get; internal set; }

        /// <summary>가구 id별 성질 점수 표(모든 FurnitureQualityRule과 가구로 한 번 만든다, WorkspaceTables). 규칙이 없으면 빈 표. 로더를 거치지 않고 만든 정의도 빈 표로 시작한다(null이 아니다).</summary>
        public Rootborn.Core.Workspaces.FurnitureQualityTable FurnitureQuality { get; internal set; } = new Rootborn.Core.Workspaces.FurnitureQualityTable(null, null);

        /// <summary>일 id별 공간 이득과 기술 성장 표(모든 WorkBonusBinding과 WorkSkillGrowth로 한 번 만든다, WorkspaceTables). 연결이 없으면 빈 표. 로더를 거치지 않고 만든 정의도 빈 표로 시작한다(null이 아니다).</summary>
        public Rootborn.Core.Workspaces.WorkLinks WorkLinks { get; internal set; } = new Rootborn.Core.Workspaces.WorkLinks(null, null);

        /// <summary>registry.json의 그 키 아래 id 목록(순서 있음). 키가 없으면 빈 목록. 문자열 하나인 키는 원소 하나짜리 목록.</summary>
        public IReadOnlyList<string> RegistryIds(string registryKey) => _table.RegistryIds(registryKey);

        /// <summary>registry.json의 기본값 키(달력, 하루 끝 규칙, 캠페인 등)의 첫 id. 없으면 빈 문자열.</summary>
        public string RegistryDefault(string registryKey)
        {
            var ids = _table.RegistryIds(registryKey);
            return ids.Count > 0 ? ids[0] : string.Empty;
        }

        public T Get<T>(string id) where T : class
        {
            if (TryGet<T>(id, out var definition)) return definition;
            throw new KeyNotFoundException($"{typeof(T).Name} '{id}' 정의가 없다");
        }

        public bool TryGet<T>(string id, out T definition) where T : class
        {
            definition = null;
            if (id == null || !_byId.TryGetValue(id, out var list)) return false;

            foreach (var entry in list)
            {
                if (!(entry.Value is T typed)) continue;
                if (definition != null)
                    throw new InvalidOperationException($"id '{id}'인 {typeof(T).Name} 정의가 둘 이상이다. 더 좁은 형으로 조회한다");
                definition = typed;
            }

            return definition != null;
        }

        public IReadOnlyList<T> All<T>() where T : class
        {
            if (_allCache.TryGetValue(typeof(T), out var cached)) return (IReadOnlyList<T>)cached;

            var matches = _entries.Where(entry => entry.Value is T).ToList();
            var ordered = OrderByRegistry(matches).Select(entry => (T)entry.Value).ToArray();
            _allCache[typeof(T)] = ordered;
            return ordered;
        }

        private IEnumerable<Entry> OrderByRegistry(List<Entry> matches)
        {
            if (matches.Count == 0) return matches;

            var ids = new HashSet<string>(matches.Select(entry => entry.Id), StringComparer.Ordinal);
            IReadOnlyList<string> best = null;
            var bestCount = 0;
            foreach (var key in _table.RegistryKeys)
            {
                var list = _table.RegistryIds(key);
                var count = list.Count(ids.Contains);
                if (count > bestCount)
                {
                    best = list;
                    bestCount = count;
                }
            }

            if (best == null) return matches;

            var position = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < best.Count; i++)
            {
                if (!position.ContainsKey(best[i])) position.Add(best[i], i);
            }

            // 목록에 있는 것은 목록 순서, 없는 것은 원래 (파일, 배열) 순서로 뒤에. OrderBy는 안정 정렬이다.
            return matches.OrderBy(entry => position.TryGetValue(entry.Id, out var at) ? at : int.MaxValue);
        }

        internal sealed class Entry
        {
            public Entry(string id, string jsonType, object value)
            {
                Id = id;
                JsonType = jsonType;
                Value = value;
            }

            public string Id { get; }
            public string JsonType { get; }
            public object Value { get; }
        }
    }
}
