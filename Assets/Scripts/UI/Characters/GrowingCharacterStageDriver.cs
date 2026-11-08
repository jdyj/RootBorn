using System.Collections;
using System.Collections.Generic;
using Rootborn.Core.Characters;
using Rootborn.Game.Characters;
using Rootborn.Game.Characters.Growing;
using Rootborn.Game.Characters.Wearables;
using Rootborn.Game.Common;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.UI.Careers;
using UnityEngine;

namespace Rootborn.UI.Characters
{
    /// <summary>
    /// Updates the character when the day advances or the adult career changes. With wearable data in the registry the
    /// look comes from wearable choices (rules in Rootborn.Core); otherwise from the legacy appearances. The look is derived
    /// from saved progress, so loading a save restores it. Works from events after binding, not per frame.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GrowingCharacterStageDriver : MonoBehaviour
    {
        private const float BindRetrySeconds = 0.5f;

        private GrowingCharacterVisualView _view;
        private GameDataRegistry _registry;
        private StudentLifeProgressComponent _student;
        private StudentLifeProgress _boundProgress;
        private AdultCareerRuntimeComponent _adult;
        private GrowingWardrobeStore _wardrobe;
        private string _wardrobeOwner;
        private WearableLookResolver _resolver;
        private WearableState _wearables;
        private string _wearablesOwner;
        private WearableLook _look;
        private int _lastDay = 1;
        private bool _lastAdult;
        private string _lastFacility;

        public bool IsLocalPlayer => _view != null && !_view.IsRemote;
        public GrowingCharacterAppearanceDefinition CurrentLook => _view != null ? _view.Appearance : null;
        public bool HasLook => _view != null && _view.HasLook;
        public string CurrentLookKey => _view != null ? _view.LookKey : string.Empty;
        public bool UsesWearables => Resolver() != null && Resolver().HasData;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Install()
        {
            GrowingCharacterRuntimeInstaller.Installed -= HandleInstalled;
            GrowingCharacterRuntimeInstaller.Installed += HandleInstalled;
        }

        private static void HandleInstalled(GameObject player, GrowingCharacterVisualView view)
        {
            var driver = player.GetComponent<GrowingCharacterStageDriver>();
            if (driver == null) driver = player.AddComponent<GrowingCharacterStageDriver>();
            GameDataRegistryProvider.TryGet(out var registry);
            driver.Configure(view, registry);
        }

        public void Configure(GrowingCharacterVisualView view, GameDataRegistry registry)
        {
            _view = view;
            _registry = registry;
            _resolver = null;
            _look = null;
            StopAllCoroutines();
            if (isActiveAndEnabled) StartCoroutine(BindWhenReady());
        }

        /// <summary>Applies the look for the given progress and returns its key (wearable look code or legacy id).</summary>
        public string Apply(int studentDay, bool isAdult, string facilityId)
        {
            if (_view == null || _registry == null || _view.IsRemote) return null;
            _lastDay = studentDay;
            _lastAdult = isAdult;
            _lastFacility = facilityId;
            if (UsesWearables)
            {
                var look = Resolver().Resolve(Wearables(), studentDay, isAdult, facilityId);
                if (look == null) return null;
                if (_look == null || !look.SameAs(_look) || _view.LookCode != look.Code) _view.ApplyLayers(look.Body, look.Layers, look.Code);
                _look = look;
                return look.Code;
            }

            var legacy = GrowingCharacterStageSelector.Select(_registry.CharacterAppearances, studentDay, isAdult, facilityId);
            if (legacy != null) legacy = GrowingCharacterStageSelector.ApplyChoice(_registry.CharacterAppearances, legacy, Wardrobe()?.GetChoice(legacy.Body != null ? legacy.Body.Id : null));
            if (legacy != null && legacy != _view.Appearance) _view.Apply(legacy);
            return legacy != null ? legacy.Id : null;
        }

        public List<GrowingCharacterAppearanceDefinition> WardrobeOptions()
        {
            return GrowingCharacterStageSelector.Options(_registry != null ? _registry.CharacterAppearances : null, CurrentLook);
        }

        /// <summary>Wardrobe rows: one per slot with a real choice (wearables), or the legacy outfits for the body.</summary>
        public List<WardrobeSection> WardrobeSections()
        {
            var sections = new List<WardrobeSection>();
            if (!UsesWearables)
            {
                var legacy = new WardrobeSection(string.Empty);
                var current = CurrentLook;
                foreach (var option in WardrobeOptions())
                    legacy.Cards.Add(new WardrobeCard(option.Id, option.DisplayNameKey, option.Body, new[] { option.Outfit, option.Head }, option == current));
                if (legacy.Cards.Count > 0) sections.Add(legacy);
                return sections;
            }

            if (_look == null) return sections;
            var resolver = Resolver();
            // Slots with a real choice come first; when no slot has one (e.g. the first stage), single-item slots are
            // shown so the wardrobe is never empty.
            int minimumChoices = 2;
            bool anyRealChoice = false;
            foreach (var slot in resolver.Catalog.Slots)
                if (resolver.Catalog.Choices(slot.Id, _look.StageId).Count >= 2) anyRealChoice = true;
            if (!anyRealChoice) minimumChoices = 1;
            foreach (var slot in resolver.Catalog.Slots)
            {
                var choices = new HashSet<string>();
                foreach (var choice in resolver.Catalog.Choices(slot.Id, _look.StageId)) choices.Add(choice.Id);
                if (choices.Count < minimumChoices) continue;
                var slotData = FindSlot(slot.Id);
                var section = new WardrobeSection(slotData != null ? slotData.DisplayNameKey : slot.Id);
                // Registry order, so designers decide the card order.
                foreach (var wearable in _registry.Wearables)
                {
                    if (wearable == null || !choices.Contains(wearable.Id)) continue;
                    var preview = WearableState.FromEntries(Wearables().ToEntries());
                    WearableRules.Choose(resolver.Catalog, preview, slot.Id, wearable.Id, _look.StageId);
                    var look = resolver.ResolveStage(preview, _look.StageId, null);
                    section.Cards.Add(new WardrobeCard(wearable.Id, wearable.DisplayNameKey, look.Body, look.Layers, Contains(_look.WearableIds, wearable.Id)));
                }

                sections.Add(section);
            }

            return sections;
        }

        /// <summary>Wears and remembers an item (or a legacy outfit). Saved at once; a failed write keeps it for this session.</summary>
        public void ChooseOutfit(string id)
        {
            if (_view == null || _view.IsRemote || string.IsNullOrEmpty(id)) return;
            if (UsesWearables)
            {
                var wearable = Resolver().FindWearable(id);
                if (wearable == null || wearable.Slot == null || _look == null) return;
                var state = Wearables();
                if (!WearableRules.Choose(Resolver().Catalog, state, wearable.Slot.Id, id, _look.StageId)) return;
                if (_boundProgress != null) WearableWardrobeStore.Save(new SaveService(_boundProgress.SaveSlot), _boundProgress.PlayerId, state);
                _look = null;
                Apply(_lastDay, _lastAdult, _lastFacility);
                return;
            }

            var current = CurrentLook;
            if (current == null || current.Body == null) return;
            var look = GrowingCharacterStageSelector.ApplyChoice(_registry.CharacterAppearances, current, id);
            if (look == null || look.Id != id) return;
            var wardrobe = Wardrobe() ?? (_wardrobe = new GrowingWardrobeStore());
            wardrobe.SetChoice(current.Body.Id, id);
            if (_boundProgress != null) wardrobe.Save(new SaveService(_boundProgress.SaveSlot), _boundProgress.PlayerId);
            _view.Apply(look);
        }

        private WearableLookResolver Resolver()
        {
            if (_resolver == null && _registry != null) _resolver = new WearableLookResolver(_registry);
            return _resolver;
        }

        private WearableSlotDefinition FindSlot(string slotId)
        {
            foreach (var slot in _registry.WearableSlots)
                if (slot != null && slot.Id == slotId) return slot;
            return null;
        }

        private static bool Contains(IReadOnlyList<string> ids, string id)
        {
            for (int i = 0; i < ids.Count; i++) if (ids[i] == id) return true;
            return false;
        }

        // Loaded per save slot and player. The first load moves the legacy wardrobe and the creator's outfit in once.
        private WearableState Wearables()
        {
            if (_boundProgress == null) return _wearables ?? (_wearables = new WearableState());
            string owner = _boundProgress.SaveSlot + "/" + _boundProgress.PlayerId;
            if (_wearables == null || _wearablesOwner != owner)
            {
                var resolver = Resolver();
                var legacyIds = LegacyWardrobe.LookIds(Wardrobe()?.Choices, CreatorParts(_boundProgress.SaveSlot), GrowingCharacterStageSelector.OutfitCategoryPrefix);
                _wearables = WearableWardrobeStore.Load(new SaveService(_boundProgress.SaveSlot), _boundProgress.PlayerId, resolver.Catalog, legacyIds,
                    resolver.LegacyAppearanceMap(_registry.CharacterAppearances));
                _wearablesOwner = owner;
            }

            return _wearables;
        }

        private static List<KeyValuePair<string, string>> CreatorParts(string slot)
        {
            var parts = new List<KeyValuePair<string, string>>();
            var metadata = ActiveSaveContext.Metadata;
            var snapshot = metadata != null && metadata.SlotId == slot ? metadata.CharacterAppearanceSnapshot : null;
            if (snapshot == null) return parts;
            foreach (var selection in snapshot.SelectedParts)
                if (selection != null) parts.Add(new KeyValuePair<string, string>(selection.CategoryId, selection.PartId));
            return parts;
        }

        private GrowingWardrobeStore Wardrobe()
        {
            if (_boundProgress == null) return _wardrobe;
            string owner = _boundProgress.SaveSlot + "/" + _boundProgress.PlayerId;
            if (_wardrobe == null || _wardrobeOwner != owner)
            {
                _wardrobe = GrowingWardrobeStore.Load(new SaveService(_boundProgress.SaveSlot), _boundProgress.PlayerId);
                _wardrobeOwner = owner;
                SeedFromCreator(_wardrobe, _boundProgress.SaveSlot);
            }

            return _wardrobe;
        }

        // The outfit picked in the new-game creator fills bodies the player has not chosen for yet; a wardrobe choice wins.
        // The creator choice lives in the slot metadata, so it is the same after every load and needs no extra save.
        private void SeedFromCreator(GrowingWardrobeStore wardrobe, string slot)
        {
            var metadata = ActiveSaveContext.Metadata;
            if (wardrobe == null || metadata == null || metadata.SlotId != slot || _registry == null || _registry.CharacterAppearances == null) return;
            var appearances = _registry.CharacterAppearances;
            for (int i = 0; i < appearances.Length; i++)
            {
                if (!(appearances[i] is GrowingCharacterAppearanceDefinition look) || look.Body == null || !string.IsNullOrEmpty(wardrobe.GetChoice(look.Body.Id))) continue;
                string chosen = GrowingCharacterStageSelector.ChoiceFromSnapshot(metadata.CharacterAppearanceSnapshot, look.Body.Id);
                if (!string.IsNullOrEmpty(chosen)) wardrobe.SetChoice(look.Body.Id, chosen);
            }
        }

        private IEnumerator BindWhenReady()
        {
            var wait = new WaitForSecondsRealtime(BindRetrySeconds);
            // Students may never get a career component, so apply what is known while waiting for it.
            while (!TryBind())
            {
                Refresh();
                yield return wait;
            }

            Refresh();
        }

        private bool TryBind()
        {
            if (_student == null) _student = GetComponent<StudentLifeProgressComponent>();
            if (_adult == null)
            {
                _adult = GetComponent<AdultCareerRuntimeComponent>();
                if (_adult != null) _adult.Committed += Refresh;
            }

            var progress = _student != null ? _student.Progress : null;
            if (progress != _boundProgress)
            {
                if (_boundProgress != null) _boundProgress.TimeChanged -= Refresh;
                _boundProgress = progress;
                if (_boundProgress != null) _boundProgress.TimeChanged += Refresh;
            }

            return _boundProgress != null && _adult != null;
        }

        private void Refresh()
        {
            TryBind();
            if (_boundProgress == null) return;
            var adult = _adult != null ? _adult.Progress : null;
            Apply(_boundProgress.CurrentDay, adult != null && adult.IsAdult, adult != null ? adult.FacilityId : null);
        }

        private void OnDestroy()
        {
            if (_boundProgress != null) _boundProgress.TimeChanged -= Refresh;
            if (_adult != null) _adult.Committed -= Refresh;
        }
    }
}
