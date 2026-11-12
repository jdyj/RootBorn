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
            PausesClock = layer == ScreenLayer.Panel || layer == ScreenLayer.Modal;
        }

        public string Id { get; }
        public ScreenLayer Layer { get; }
        public bool BlocksMovement { get; }
        public Func<ScreenServices, GameScreen> Create { get; }
        /// <summary>
        /// 떠 있는 동안 혼자 하기의 시계를 멈춘다(흐르는 시간 설계 3.1: 패널·모달·대화). 기본은 패널·모달 층이면 true, HUD·알림은 false.
        /// 미니게임처럼 떠 있어도 시간이 흘러야 하는 화면은 등록 때 KeepsClockRunning으로 끈다. 멈출지 판단은 GameSession이 한다(같이 하기는 멈추지 않는다).
        /// </summary>
        public bool PausesClock { get; internal set; }
        /// <summary>월드(HUD) 없이 뜨는 화면(슬롯 선택처럼 게임 한 판 전). HUD와 함께 떠 있지 않는다.</summary>
        public bool WithoutWorld { get; internal set; }
        /// <summary>여는 키가 시작 화면(WithoutWorld) 위에서도 듣는다.</summary>
        public bool KeyWorksOnStartScreens { get; internal set; }
        /// <summary>이 화면 안에서만 듣는 키의 단축키 안내 줄(장치 키 이름표를 받아 만든다). 없으면 null.</summary>
        public Func<Rootborn.Screens.KeyGuide.KeyGuideKeys, IReadOnlyList<Rootborn.Screens.KeyGuide.KeyGuideRow>> KeyGuide { get; internal set; }
        /// <summary>
        /// Esc로 닫지 않는 까닭(고르기를 끝내야 하는 화면). null이면 Esc가 이 화면을 닫는다(패널·모달). Esc 규칙은 ScreenStack.Key.
        /// </summary>
        public string EscapeKeepsOpenReason { get; internal set; }
        /// <summary>월드의 상호작용이 여는 화면이다(OpenedByInteraction). 떠 있는 동안 상호작용 안내를 숨긴다.</summary>
        public bool OpenedByInteraction { get; internal set; }
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
        private readonly List<KeyValuePair<ScreenKey, string>> _keyOrder = new List<KeyValuePair<ScreenKey, string>>();

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
        /// <summary>키 → 화면 배정, 등록한 순서(단축키 안내가 읽는다).</summary>
        public IReadOnlyList<KeyValuePair<ScreenKey, string>> KeyBindings => _keyOrder;
        public bool TryGetByInteraction(string kind, out string screenId) => _interactions.TryGetValue(kind ?? string.Empty, out screenId);
        /// <summary>등록된 상호작용 종류 → 그 종류가 여는 화면 id(OpenedByInteraction으로 등록한 것 전부).</summary>
        public IReadOnlyDictionary<string, string> InteractionKinds => _interactions;

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
                _owner._keyOrder.Add(new KeyValuePair<ScreenKey, string>(key, _id));
                return this;
            }

            /// <summary>
            /// 이 화면의 키는 시작 화면(WithoutWorld) 위에서도 연다(시작 화면은 이동을 막는 화면이라 보통은 키 배정이 막힌다).
            /// 게임 한 판 없이도 뜨는 화면에만 쓴다(단축키 안내).
            /// </summary>
            public Registration KeyWorksOnStartScreens()
            {
                _owner._screens[_id].KeyWorksOnStartScreens = true;
                return this;
            }

            /// <summary>이 화면 안에서만 듣는 키를 단축키 안내에 넘긴다(키를 처리하는 화면이 줄을 만든다).</summary>
            public Registration GuidesKeys(Func<Rootborn.Screens.KeyGuide.KeyGuideKeys, IReadOnlyList<Rootborn.Screens.KeyGuide.KeyGuideRow>> rows)
            {
                _owner._screens[_id].KeyGuide = rows ?? throw new ArgumentNullException(nameof(rows));
                return this;
            }

            /// <summary>
            /// Esc로 닫지 않는다(고르기를 끝내야 하는 모달, 시작 화면). 이유를 적는다 — 단축키 안내와 시험이 읽는다.
            /// 기본은 Esc가 맨 위의 패널·모달을 닫는다.
            /// </summary>
            public Registration KeepsOpenOnEscape(string reason)
            {
                if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Esc로 닫지 않는 이유가 비었다", nameof(reason));
                _owner._screens[_id].EscapeKeepsOpenReason = reason;
                return this;
            }

            /// <summary>떠 있어도 혼자 하기의 시계를 멈추지 않는다(미니게임 — 하는 동안 시간이 흐른다).</summary>
            public Registration KeepsClockRunning()
            {
                _owner._screens[_id].PausesClock = false;
                return this;
            }

            /// <summary>월드(HUD) 없이 뜨는 화면이다(게임 한 판 전의 시작 화면).</summary>
            public Registration WithoutWorld()
            {
                _owner._screens[_id].WithoutWorld = true;
                return this;
            }

            /// <summary>이 종류의 상호작용(월드 InteractionEvent.Kind)이 화면을 연다.</summary>
            public Registration OpenedByInteraction(string kind)
            {
                if (string.IsNullOrEmpty(kind)) throw new ArgumentException("상호작용 종류가 비었다", nameof(kind));
                if (_owner._interactions.TryGetValue(kind, out string other)) throw new InvalidOperationException($"상호작용 {kind}는 이미 화면 {other}에 배정됐다");
                _owner._interactions[kind] = _id;
                _owner._screens[_id].OpenedByInteraction = true;
                return this;
            }
        }
    }
}
