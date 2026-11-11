using System;
using System.Collections.Generic;

namespace Rootborn.Screens.KeyGuide
{
    /// <summary>
    /// 키 모양 하나에 쓰는 글자. 키 이름("Tab", "Esc", "F1")은 번역하지 않는 글자 그대로(Localized=false),
    /// "방향 키"처럼 말로 된 것은 현지화 키(Localized=true)다.
    /// </summary>
    public readonly struct KeyCap : IEquatable<KeyCap>
    {
        private KeyCap(string text, bool localized) { Text = text ?? string.Empty; Localized = localized; }

        public string Text { get; }
        public bool Localized { get; }

        /// <summary>번역하지 않는 키 이름(장치의 키 글자).</summary>
        public static KeyCap Name(string name) => new KeyCap(name, false);

        /// <summary>현지화 키로 받는 말(예: ui.keys.name.arrows).</summary>
        public static KeyCap Phrase(string textKey) => new KeyCap(textKey, true);

        public bool Equals(KeyCap other) => Localized == other.Localized && string.Equals(Text, other.Text, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is KeyCap other && Equals(other);
        public override int GetHashCode() => (Text.GetHashCode() * 397) ^ (Localized ? 1 : 0);
        public override string ToString() => Localized ? "[" + Text + "]" : Text;
    }

    /// <summary>함께 누르는 키 모양들(예: Shift + 방향 키). 하나면 키 하나.</summary>
    public sealed class KeyChord
    {
        public KeyChord(params KeyCap[] caps)
        {
            if (caps == null || caps.Length == 0) throw new ArgumentException("키가 하나는 있어야 한다", nameof(caps));
            Caps = caps;
        }

        public IReadOnlyList<KeyCap> Caps { get; }

        public static KeyChord Of(string name) => new KeyChord(KeyCap.Name(name));

        /// <summary>앞에 키 하나를 더 누른 조합(예: Shift + 이 조합).</summary>
        public KeyChord With(KeyCap first)
        {
            var caps = new KeyCap[Caps.Count + 1];
            caps[0] = first;
            for (int i = 0; i < Caps.Count; i++) caps[i + 1] = Caps[i];
            return new KeyChord(caps);
        }

        public override string ToString() => string.Join("+", Caps);
    }

    /// <summary>안내 한 줄: 묶음, 하는 일(현지화 키), 같은 일을 하는 키 조합들(어느 것이든 된다).</summary>
    public sealed class KeyGuideRow
    {
        public KeyGuideRow(string group, string actionKey, IReadOnlyList<KeyChord> alternatives)
        {
            if (string.IsNullOrEmpty(group)) throw new ArgumentException("묶음이 비었다", nameof(group));
            if (string.IsNullOrEmpty(actionKey)) throw new ArgumentException("하는 일 키가 비었다", nameof(actionKey));
            if (alternatives == null || alternatives.Count == 0) throw new ArgumentException("키가 하나는 있어야 한다", nameof(alternatives));
            Group = group; ActionKey = actionKey; Alternatives = alternatives;
        }

        public string Group { get; }
        public string ActionKey { get; }
        public IReadOnlyList<KeyChord> Alternatives { get; }

        public override string ToString() => Group + " | " + ActionKey + " | " + string.Join(" / ", Alternatives);
    }

    /// <summary>안내의 묶음 이름. 제목 문구 키는 ui.keys.group.&lt;이름&gt;. 새 묶음은 상수 하나와 Order에 한 자리.</summary>
    public static class KeyGuideGroups
    {
        public const string Movement = "movement";
        public const string Interact = "interact";
        public const string Screens = "screens";
        public const string Navigation = "navigation";
        /// <summary>미니게임 안(미니게임 화면이 등록 때 넘긴다).</summary>
        public const string Minigame = "minigame";
        /// <summary>가구 배치 카메라(Host가 넘긴다).</summary>
        public const string Furniture = "furniture";
        public const string System = "system";

        /// <summary>보이는 순서. 여기 없는 묶음은 맨 뒤(넘겨준 순서대로).</summary>
        public static readonly string[] Order = { Movement, Interact, Screens, Navigation, Minigame, Furniture, System };

        public static string TitleKey(string group) => "ui.keys.group." + group;
    }

    /// <summary>
    /// 장치 키 이름표. Screens는 장치를 모르므로 Host가 채운다(Host Input/Devices.cs의 배정에서): 화면 쌓기가 받는 키(ScreenKey)마다
    /// 그 키를 내는 장치 키 이름, "방향 키"·Shift 모양, 그리고 화면이 아닌 키(걷기, 상호작용, F11 등)의 안내 줄.
    /// 채우지 않은 키는 ScreenKey 이름 그대로 보인다(빠졌다는 것이 보이게).
    /// </summary>
    public sealed class KeyGuideKeys
    {
        private readonly Dictionary<ScreenKey, KeyChord[]> _names = new Dictionary<ScreenKey, KeyChord[]>();
        private readonly List<KeyGuideRow> _rows = new List<KeyGuideRow>();

        /// <summary>네 방향 키를 한 모양으로 부를 때(기본: 현지화 키 ui.keys.name.arrows).</summary>
        public KeyChord Directions { get; set; } = new KeyChord(KeyCap.Phrase("ui.keys.name.arrows"));

        /// <summary>Shift 키 모양.</summary>
        public KeyCap Shift { get; set; } = KeyCap.Name("Shift");

        /// <summary>이 ScreenKey를 내는 장치 키(조합)들. 다시 부르면 바꾼다.</summary>
        public KeyGuideKeys Name(ScreenKey key, params KeyChord[] alternatives)
        {
            if (alternatives == null || alternatives.Length == 0) throw new ArgumentException("키가 하나는 있어야 한다", nameof(alternatives));
            _names[key] = alternatives;
            return this;
        }

        /// <summary>화면이 아닌 키의 안내 줄(Host만 아는 키).</summary>
        public KeyGuideKeys AddRow(KeyGuideRow row)
        {
            _rows.Add(row ?? throw new ArgumentNullException(nameof(row)));
            return this;
        }

        public bool Has(ScreenKey key) => _names.ContainsKey(key);

        /// <summary>이 키의 장치 이름들. 없으면 ScreenKey 이름 하나.</summary>
        public IReadOnlyList<KeyChord> For(ScreenKey key) =>
            _names.TryGetValue(key, out KeyChord[] names) ? names : new[] { KeyChord.Of(key.ToString()) };

        public IReadOnlyList<KeyGuideRow> Rows => _rows;
    }

    /// <summary>
    /// 단축키 안내의 표시 모델: 묶음 순서대로 줄. 화면을 여는 키는 등록 표(ScreenRegistry.KeyBindings)에서, 화면 안 버튼 옮기기는
    /// 그 키를 처리하는 화면 쌓기(ScreenStack.NavigationGuide)에서, 화면 안에서만 듣는 키(미니게임)는 그 화면이 등록 때 넘긴 줄(ScreenEntry.KeyGuide)에서,
    /// 걷기·상호작용·전체 화면·가구 배치 카메라처럼 Host만 아는 키는 KeyGuideKeys.Rows에서 온다.
    /// 손으로 적은 목록이 없다: 화면을 키와 함께 등록하면 줄이 저절로 생긴다. 화면 줄의 하는 일 키는 ui.keys.&lt;화면 id&gt;.
    /// </summary>
    public sealed class KeyGuideModel
    {
        private KeyGuideModel(IReadOnlyList<KeyGuideRow> rows) { Rows = rows; }

        public IReadOnlyList<KeyGuideRow> Rows { get; }

        /// <summary>화면 줄의 하는 일 문구 키(규칙 키). 등록에 따로 이름이 없으므로 화면 id에서 만든다.</summary>
        public static string ScreenActionKey(string screenId) => "ui.keys." + screenId;

        public static KeyGuideModel From(ScreenRegistry registry, KeyGuideKeys keys)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            keys ??= new KeyGuideKeys();
            var all = new List<KeyGuideRow>(keys.Rows.Count + 16);
            all.AddRange(keys.Rows);
            foreach (KeyValuePair<ScreenKey, string> binding in registry.KeyBindings)
                all.Add(new KeyGuideRow(KeyGuideGroups.Screens, ScreenActionKey(binding.Value), keys.For(binding.Key)));
            all.AddRange(ScreenStack.NavigationGuide(keys));
            foreach (ScreenEntry entry in registry.Screens)
                if (entry.KeyGuide != null) all.AddRange(entry.KeyGuide(keys));

            var ordered = new List<KeyGuideRow>(all.Count);
            foreach (string group in KeyGuideGroups.Order)
                foreach (KeyGuideRow row in all)
                    if (row.Group == group) ordered.Add(row);
            foreach (KeyGuideRow row in all)
                if (Array.IndexOf(KeyGuideGroups.Order, row.Group) < 0) ordered.Add(row);
            return new KeyGuideModel(ordered);
        }
    }
}
