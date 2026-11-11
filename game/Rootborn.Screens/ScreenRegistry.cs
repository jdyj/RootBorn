using System;
using System.Collections.Generic;

namespace Rootborn.Screens
{
    /// <summary>등록된 화면 하나: 층, 이동을 막는지, 만드는 법.</summary>
    public sealed class ScreenEntry
    {
        internal ScreenEntry(string id, ScreenLayer layer, bool blocksMovement, Func<ScreenServices, GameScreen> create)
        {
            Id = id; Layer = layer; BlocksMovement = blocksMovement; Create = create;
        }

        public string Id { get; }
        public ScreenLayer Layer { get; }
        public bool BlocksMovement { get; }
        public Func<ScreenServices, GameScreen> Create { get; }
    }

    /// <summary>
    /// 등록 표: 화면 id → 화면, 키 → 화면, 상호작용 종류 → 화면. 엔티티 id로 화면을 고르지 않는다.
    /// 새 화면은 ScreenCatalog에 한 줄로 더한다: r.Add(id, 층, 이동을 막는지, 만드는 법).OpenedBy(키).OpenedByInteraction(종류).
    /// </summary>
    public sealed class ScreenRegistry
    {
        private readonly Dictionary<string, ScreenEntry> _screens = new Dictionary<string, ScreenEntry>(StringComparer.Ordinal);
        private readonly Dictionary<ScreenKey, string> _keys = new Dictionary<ScreenKey, string>();
        private readonly Dictionary<string, string> _interactions = new Dictionary<string, string>(StringComparer.Ordinal);

        public Registration Add(string id, ScreenLayer layer, bool blocksMovement, Func<ScreenServices, GameScreen> create)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("화면 id가 비었다", nameof(id));
            if (create == null) throw new ArgumentNullException(nameof(create));
            if (_screens.ContainsKey(id)) throw new InvalidOperationException("이미 등록된 화면: " + id);
            _screens[id] = new ScreenEntry(id, layer, blocksMovement, create);
            return new Registration(this, id);
        }

        private readonly List<Action<ScreenServices>> _listeners = new List<Action<ScreenServices>>();

        /// <summary>
        /// 화면 쌓기가 만들어질 때 한 번 불릴 것(화면이 열리기 전부터 알림을 들어야 하는 것: 예를 들어 장소 상호작용 뒤 하루 이벤트를 여는 쪽).
        /// Services.Screens가 채워진 뒤 불린다.
        /// </summary>
        public void AddListener(Action<ScreenServices> attach)
        {
            if (attach == null) throw new ArgumentNullException(nameof(attach));
            _listeners.Add(attach);
        }

        public IReadOnlyList<Action<ScreenServices>> Listeners => _listeners;

        public IEnumerable<ScreenEntry> Screens => _screens.Values;
        public bool Has(string id) => id != null && _screens.ContainsKey(id);
        public bool TryGet(string id, out ScreenEntry entry) => _screens.TryGetValue(id ?? string.Empty, out entry);
        public bool TryGetByKey(ScreenKey key, out string screenId) => _keys.TryGetValue(key, out screenId);
        public bool TryGetByInteraction(string kind, out string screenId) => _interactions.TryGetValue(kind ?? string.Empty, out screenId);

        public readonly struct Registration
        {
            private readonly ScreenRegistry _owner;
            private readonly string _id;

            internal Registration(ScreenRegistry owner, string id) { _owner = owner; _id = id; }

            /// <summary>이 키를 누르면 화면을 열고 닫는다(키 하나에 화면 하나).</summary>
            public Registration OpenedBy(ScreenKey key)
            {
                if (_owner._keys.TryGetValue(key, out string other)) throw new InvalidOperationException($"키 {key}는 이미 화면 {other}에 배정됐다");
                _owner._keys[key] = _id;
                return this;
            }

            /// <summary>이 종류의 상호작용(월드 InteractionEvent.Kind)이 화면을 연다.</summary>
            public Registration OpenedByInteraction(string kind)
            {
                if (string.IsNullOrEmpty(kind)) throw new ArgumentException("상호작용 종류가 비었다", nameof(kind));
                if (_owner._interactions.TryGetValue(kind, out string other)) throw new InvalidOperationException($"상호작용 {kind}는 이미 화면 {other}에 배정됐다");
                _owner._interactions[kind] = _id;
                return this;
            }
        }
    }
}
