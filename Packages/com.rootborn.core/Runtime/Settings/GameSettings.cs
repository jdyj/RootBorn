using System;
using System.Collections.Generic;
using Rootborn.Core.Save;

namespace Rootborn.Core.Settings
{
    /// <summary>
    /// Unity 판 설정 화면(SettingsPanel.MakeSettingsGroup)의 켬/끔 네 칸 이름. Unity 판은 칸에 이름을 붙이지 않아 옆 아이콘 이름을 쓴다
    /// (MakeSpeakerIcon, MakeMutedIcon, MakeMusicIcon, MakeTextGlyph). Unity 판에서 이 칸들은 눌리지 않았고 뜻도 정해지지 않았다 —
    /// 여기서도 값만 저장하고 뜻을 정하지 않는다. 새 설정은 이 목록에 없어도 이름 하나로 저장된다.
    /// </summary>
    public static class GameSettingNames
    {
        public const string Speaker = "Speaker";
        public const string Muted = "Muted";
        public const string Music = "Music";
        public const string Text = "Text";
    }

    /// <summary>설정 파일 모양: 언어 코드와 이름 → 켬/끔 목록. 종류를 닫지 않게 목록으로 둔다.</summary>
    [Serializable]
    public sealed class GameSettingsSaveData : IVersionedSave
    {
        public const int KnownVersion = 1;

        public int Version;
        /// <summary>고른 언어 코드. 빈 글자면 아직 고르지 않았다.</summary>
        public string Language;
        public GameSettingToggleSaveData[] Toggles;

        int IVersionedSave.Version { get => Version; set => Version = value; }
        int IVersionedSave.KnownVersion => KnownVersion;
    }

    [Serializable]
    public sealed class GameSettingToggleSaveData
    {
        public string Name;
        public bool Value;
    }

    /// <summary>
    /// 설정 값. 언어 코드와 켬/끔 값(이름 순서를 지킨다). 파일에 없는 기본 이름은 Unity 판 설정 화면의 처음 값을 갖는다.
    /// 모르는 이름도 그대로 갖고 있다가 다시 쓴다.
    /// </summary>
    public sealed class GameSettings
    {
        // Unity SettingsPanel.MakeSettingsGroup의 MakeToggle 순서와 처음 값.
        private static readonly (string Name, bool Value)[] UnityDefaults =
        {
            (GameSettingNames.Speaker, true),
            (GameSettingNames.Muted, false),
            (GameSettingNames.Music, false),
            (GameSettingNames.Text, true),
        };

        private readonly List<string> _order = new List<string>();
        private readonly Dictionary<string, bool> _toggles = new Dictionary<string, bool>(StringComparer.Ordinal);
        private string _language = string.Empty;

        private GameSettings() { }

        public static GameSettings CreateDefault()
        {
            var settings = new GameSettings();
            for (int i = 0; i < UnityDefaults.Length; i++) settings.SetToggle(UnityDefaults[i].Name, UnityDefaults[i].Value);
            return settings;
        }

        /// <summary>파일 값을 기본값 위에 얹는다. 빈 이름은 버리고, 같은 이름이 여럿이면 뒤의 값을 쓴다.</summary>
        public static GameSettings FromSaveData(GameSettingsSaveData data)
        {
            var settings = CreateDefault();
            if (data == null) return settings;
            settings.Language = data.Language;
            if (data.Toggles != null)
                foreach (var toggle in data.Toggles)
                    if (toggle != null) settings.SetToggle(toggle.Name, toggle.Value);
            return settings;
        }

        /// <summary>고른 언어 코드(소문자로 둔다). 빈 글자면 아직 고르지 않았다.</summary>
        public string Language
        {
            get => _language;
            set => _language = string.IsNullOrEmpty(value) ? string.Empty : value.ToLowerInvariant();
        }

        /// <summary>켬/끔 값이 있는 이름(기본 이름 먼저, 그다음 파일에서 읽은 순서).</summary>
        public IReadOnlyList<string> ToggleNames => _order;

        public bool TryGetToggle(string name, out bool value)
        {
            value = false;
            return !string.IsNullOrEmpty(name) && _toggles.TryGetValue(name, out value);
        }

        public bool GetToggle(string name, bool fallback = false) => TryGetToggle(name, out var value) ? value : fallback;

        /// <summary>이름이 비면 아무것도 하지 않고 false.</summary>
        public bool SetToggle(string name, bool value)
        {
            if (string.IsNullOrEmpty(name)) return false;
            if (!_toggles.ContainsKey(name)) _order.Add(name);
            _toggles[name] = value;
            return true;
        }

        public GameSettingsSaveData ToSaveData()
        {
            var toggles = new GameSettingToggleSaveData[_order.Count];
            for (int i = 0; i < toggles.Length; i++) toggles[i] = new GameSettingToggleSaveData { Name = _order[i], Value = _toggles[_order[i]] };
            return new GameSettingsSaveData { Version = GameSettingsSaveData.KnownVersion, Language = _language, Toggles = toggles };
        }
    }

    /// <summary>설정 파일 읽기·쓰기. 슬롯 자리에는 <see cref="SaveFileNames.SettingsSlot"/>를 넣는다(어느 세이브 슬롯과도 무관).</summary>
    public static class GameSettingsSaveFiles
    {
        /// <summary>파일이 없으면 기본값(쓰지 않는다).</summary>
        public static GameSettings LoadOrCreate(ISaveStore store)
        {
            if (store == null) throw new ArgumentNullException(nameof(store));
            return store.TryRead<GameSettingsSaveData>(SaveFileNames.SettingsSlot, SaveFileNames.Settings, out var data)
                ? GameSettings.FromSaveData(data)
                : GameSettings.CreateDefault();
        }

        public static void Save(ISaveStore store, GameSettings settings)
        {
            if (store == null) throw new ArgumentNullException(nameof(store));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            store.Write(SaveFileNames.SettingsSlot, SaveFileNames.Settings, settings.ToSaveData());
        }
    }
}
