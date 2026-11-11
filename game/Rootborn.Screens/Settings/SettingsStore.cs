using System;
using Rootborn.Core.Save;
using Rootborn.Core.Settings;

namespace Rootborn.Screens.Settings
{
    /// <summary>
    /// 슬롯과 무관한 설정(언어, 켬/끔). Core GameSettings를 들고 있고, 저장소가 있으면 Core 설정 파일(_settings/settings.json)로 읽고 쓴다.
    /// 저장소가 없으면(세이브 폴더 없이 띄운 월드·견본) 메모리에만 둔다. 읽을 수 없는 설정 파일은 기본값으로 시작한다(LoadError에 남긴다).
    /// </summary>
    public sealed class SettingsStore
    {
        private readonly ISaveStore _store;

        public SettingsStore(ISaveStore store)
        {
            _store = store;
            if (store == null) { Settings = GameSettings.CreateDefault(); return; }
            try { Settings = GameSettingsSaveFiles.LoadOrCreate(store); }
            catch (Exception e) when (!(e is OutOfMemoryException))
            {
                LoadError = e.Message;
                Settings = GameSettings.CreateDefault();
            }
        }

        /// <summary>저장하지 않는 설정(기본값).</summary>
        public static SettingsStore InMemory() => new SettingsStore(null);

        public GameSettings Settings { get; }

        /// <summary>설정 파일을 읽지 못했으면 그 까닭(읽었거나 파일이 없으면 null).</summary>
        public string LoadError { get; }

        /// <summary>저장소가 있으면 쓴다.</summary>
        public void Save()
        {
            if (_store != null) GameSettingsSaveFiles.Save(_store, Settings);
        }
    }
}
