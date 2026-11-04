using Rootborn.Game.Localization;
using Rootborn.Game.Common;
using Rootborn.Game.StudentLife;
using Rootborn.Game.Time;
using Rootborn.Network.Time;
using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.UI.HUD
{
    public sealed class TimeHud : MonoBehaviour
    {
        [SerializeField] private GameClock _clock;
        [SerializeField] private Text _label;
        private StudentLifeProgressComponent _lifeOwner;
        private LifeTimePresentation _lifeSource;
        private NetworkWorldTimeState _worldTime;
        private string _lastRenderedText;
        private enum DisplaySource { None, Loading, Network, Life, Clock }
        private DisplaySource _displaySource;
        private int _displayDay;
        private int _displayValue;
        private int _displayPhase;
        private string _displayLanguage;
        private GameTextCatalog _displayCatalog;

        public void BindLabelForRuntime(Text label)
        {
            _label = label;
            _lastRenderedText = null;
            _displaySource = DisplaySource.None;
            Refresh();
        }

        public void BindLifeProgress(StudentLifeProgressComponent owner)
        {
            if (_lifeOwner != null) _lifeOwner.TimeChanged -= Refresh;
            _lifeOwner = owner;
            if (_lifeOwner != null) _lifeOwner.TimeChanged += Refresh;
            Refresh();
        }

        public void BindLifeTimePresentation(LifeTimePresentation source)
        {
            if (_lifeSource != null) _lifeSource.Changed -= HandleLifeSourceChanged;
            _lifeSource = source;
            if (_lifeSource != null)
            {
                _lifeSource.Changed += HandleLifeSourceChanged;
                _lifeSource.SetSuspended(_worldTime != null);
            }
            HandleLifeSourceChanged();
        }

        private void HandleLifeSourceChanged() => BindLifeProgress(_lifeSource != null && _lifeSource.HasTime ? _lifeSource.Owner : null);

        private void OnEnable()
        {
            NetworkWorldTimeState.ActiveChanged += BindNetwork;
            if (_clock != null) _clock.OnTimeChanged += HandleClockChanged;
            if (_lifeSource != null) _lifeSource.Changed += HandleLifeSourceChanged;
            if (_lifeOwner != null) _lifeOwner.TimeChanged += Refresh;
            BindNetwork();
            if (_lifeSource != null) HandleLifeSourceChanged();
        }

        private void OnDisable()
        {
            NetworkWorldTimeState.ActiveChanged -= BindNetwork;
            if (_clock != null) _clock.OnTimeChanged -= HandleClockChanged;
            if (_worldTime != null) _worldTime.Changed -= Refresh;
            if (_lifeSource != null) _lifeSource.Changed -= HandleLifeSourceChanged;
            if (_lifeOwner != null) _lifeOwner.TimeChanged -= Refresh;
        }

        private void BindNetwork()
        {
            if (_worldTime != null) _worldTime.Changed -= Refresh;
            _worldTime = NetworkWorldTimeState.Active;
            if (_worldTime != null) _worldTime.Changed += Refresh;
            if (_lifeSource != null) _lifeSource.SetSuspended(_worldTime != null);
            Refresh();
        }

        private void HandleClockChanged(float hour)
        {
            if (_worldTime != null || (_lifeOwner != null && _lifeOwner.isActiveAndEnabled && _lifeOwner.Progress != null)) return;
            Refresh();
        }

        private void CheckLocalizationCatalog()
        {
            GameDataRegistryProvider.TryGet(out var registry);
            var catalog = registry != null ? registry.TextCatalog : null;
            if (_displayCatalog == catalog) return;
            _displayCatalog = catalog;
            _displaySource = DisplaySource.None;
        }

        private bool IsSameDisplay(DisplaySource source, int day, int value, int phase, string language)
        {
            if (_displaySource == source && _displayDay == day && _displayValue == value && _displayPhase == phase && _displayLanguage == language) return true;
            _displaySource = source;
            _displayDay = day;
            _displayValue = value;
            _displayPhase = phase;
            _displayLanguage = language;
            return false;
        }

        private void Refresh()
        {
            if (_label == null) return;
            if (_worldTime != null)
            {
                if (IsSameDisplay(DisplaySource.Network, _worldTime.CurrentDay, (int)_worldTime.TimeOfDay, (int)_worldTime.SchedulePhase, null)) return;
                SetLabelText("Day " + _worldTime.CurrentDay + " " + _worldTime.WeekdayName + " " + _worldTime.TimeOfDay + " " + _worldTime.SchedulePhase);
                return;
            }
            var progress = _lifeOwner != null && _lifeOwner.isActiveAndEnabled ? _lifeOwner.Progress : null;
            if (progress != null)
            {
                CheckLocalizationCatalog();
                int minutes = progress.TimeMinutes % (24 * 60);
                if (IsSameDisplay(DisplaySource.Life, progress.CurrentDay, minutes, 0, GameText.Language)) return;
                SetLabelText(GameText.Format("life.time.hud", progress.CurrentDay, minutes / 60, minutes % 60));
                return;
            }
            if (_clock != null)
            {
                int percent = Mathf.RoundToInt(_clock.DayProgress01 * 100f);
                if (IsSameDisplay(DisplaySource.Clock, _clock.Day, percent, 0, null)) return;
                SetLabelText("Day " + _clock.Day + " (" + percent + "%)");
            }
            else
            {
                CheckLocalizationCatalog();
                if (IsSameDisplay(DisplaySource.Loading, 0, 0, 0, GameText.Language)) return;
                SetLabelText(GameText.Get("life.tp.loading"));
            }
        }

        private void SetLabelText(string value)
        {
            if (_lastRenderedText == value) return;
            _lastRenderedText = value;
            _label.text = value;
        }
    }
}

