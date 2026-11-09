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
        private Rootborn.Game.Player.PlayerInventory _boundInventory;
        private ItemDefinition _shopCurrency;
        private enum DisplaySource { None, Loading, Network, Life, Clock }
        private static readonly string[] EnglishWeekdays = { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
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
            if (_boundInventory != null && _boundInventory.Inventory != null) _boundInventory.Inventory.OnChanged -= Refresh;
            _boundInventory = null;
        }

        private void BindNetwork()
        {
            if (_worldTime != null) _worldTime.Changed -= Refresh;
            _worldTime = NetworkWorldTimeState.Active;
            if (_worldTime != null) _worldTime.Changed += Refresh;
            if (_lifeSource != null) _lifeSource.SetSuspended(_worldTime != null);
            Refresh();
        }

        // Game data (and its text catalog) can arrive after the first draw, for example on a client that joined straight into
        // Town. Until it does, check once a frame; when it appears, draw again so the label switches to the player's language.
        private void Update()
        {
            if (_displayCatalog != null || _label == null) return;
            GameDataRegistryProvider.TryGet(out var registry);
            if (registry == null || registry.TextCatalog == null) return;
            CheckLocalizationCatalog(); // remembers the catalog, so this stops after one redraw
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

        // The currency is whatever the shop charges, read once from data.
        private static ItemDefinition FindShopCurrency()
        {
            if (!GameDataRegistryProvider.TryGet(out var registry) || registry == null || registry.FurnitureDefinitions == null) return null;
            var furniture = registry.FurnitureDefinitions;
            for (int i = 0; i < furniture.Length; i++) if (furniture[i] != null && furniture[i].IsForSale && furniture[i].PriceItem != null) return furniture[i].PriceItem;
            return null;
        }

        private static int AllowanceCount(Rootborn.Game.Player.PlayerInventory inventory, ItemDefinition currency)
            => inventory != null && inventory.Inventory != null && currency != null ? inventory.Inventory.CountOf(currency) : -1;

        public static string AllowanceText(Rootborn.Game.Player.PlayerInventory inventory) => FormatAllowance(AllowanceCount(inventory, FindShopCurrency()));

        private static string FormatAllowance(int count) => count < 0 ? string.Empty : GameText.FormatLocalized("dream.shop.balance", " \u00b7 Allowance {0}", count);

        private void BindInventory()
        {
            var next = _lifeOwner != null ? _lifeOwner.GetComponent<Rootborn.Game.Player.PlayerInventory>() : null;
            if (next == _boundInventory) return;
            if (_boundInventory != null && _boundInventory.Inventory != null) _boundInventory.Inventory.OnChanged -= Refresh;
            _boundInventory = next;
            if (_boundInventory != null && _boundInventory.Inventory != null && isActiveAndEnabled) _boundInventory.Inventory.OnChanged += Refresh;
            if (_shopCurrency == null) _shopCurrency = FindShopCurrency();
        }

        private void Refresh()
        {
            if (_label == null) return;
            if (_worldTime != null)
            {
                // Shared world time (multiplayer): day, weekday and part of day in the player's language. The schedule phase
                // (DayStart, ...) is a sync detail, not something to show; it stays in the logs (worldTime.SnapshotText).
                CheckLocalizationCatalog();
                BindInventory();
                int sharedAllowance = AllowanceCount(_boundInventory, _shopCurrency);
                if (IsSameDisplay(DisplaySource.Network, _worldTime.CurrentDay, (int)_worldTime.TimeOfDay * 100000 + _worldTime.WeekdayIndex, sharedAllowance, GameText.Language)) return;
                string weekday = GameText.Localized("daily.weekday." + _worldTime.WeekdayIndex, EnglishWeekdays[_worldTime.WeekdayIndex]);
                string partOfDay = GameText.Localized("daily.time-of-day." + _worldTime.TimeOfDay.ToString().ToLowerInvariant(), _worldTime.TimeOfDay.ToString());
                SetLabelText(GameText.FormatLocalized("daily.time.shared-hud", "Day {0} {1} \u00b7 {2}", _worldTime.CurrentDay, weekday, partOfDay) + FormatAllowance(sharedAllowance));
                return;
            }
            var progress = _lifeOwner != null && _lifeOwner.isActiveAndEnabled ? _lifeOwner.Progress : null;
            if (progress != null)
            {
                CheckLocalizationCatalog();
                int minutes = progress.TimeMinutes % (24 * 60);
                BindInventory();
                int allowance = AllowanceCount(_boundInventory, _shopCurrency);
                if (IsSameDisplay(DisplaySource.Life, progress.CurrentDay, minutes, allowance, GameText.Language)) return;
                SetLabelText(GameText.Format("life.time.hud", progress.CurrentDay, minutes / 60, minutes % 60) + FormatAllowance(allowance));
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

