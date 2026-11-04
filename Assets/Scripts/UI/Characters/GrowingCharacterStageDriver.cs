using System.Collections;
using Rootborn.Game.Characters.Growing;
using System.Collections.Generic;
using Rootborn.Game.Common;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.UI.Careers;
using UnityEngine;

namespace Rootborn.UI.Characters
{
    /// <summary>
    /// Updates the character when the day advances or the adult career changes. The look is derived from saved
    /// progress, so loading a save restores it with no extra data. Works from events after binding, not per frame.
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

        public bool IsLocalPlayer => _view != null && !_view.IsRemote;
        public GrowingCharacterAppearanceDefinition CurrentLook => _view != null ? _view.Appearance : null;

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
            StopAllCoroutines();
            if (isActiveAndEnabled) StartCoroutine(BindWhenReady());
        }

        /// <summary>Applies the look for the given progress and returns it. Does nothing when already correct.</summary>
        public GrowingCharacterAppearanceDefinition Apply(int studentDay, bool isAdult, string facilityId)
        {
            if (_view == null || _registry == null || _view.IsRemote) return null;
            var look = GrowingCharacterStageSelector.Select(_registry.CharacterAppearances, studentDay, isAdult, facilityId);
            if (look != null) look = GrowingCharacterStageSelector.ApplyChoice(_registry.CharacterAppearances, look, Wardrobe()?.GetChoice(look.Body != null ? look.Body.Id : null));
            if (look != null && look != _view.Appearance) _view.Apply(look);
            return look;
        }

        public List<GrowingCharacterAppearanceDefinition> WardrobeOptions()
        {
            return GrowingCharacterStageSelector.Options(_registry != null ? _registry.CharacterAppearances : null, CurrentLook);
        }

        /// <summary>Wears and remembers an outfit for the current body. Saved at once; a failed write keeps the outfit for this session.</summary>
        public void ChooseOutfit(string appearanceId)
        {
            var current = CurrentLook;
            if (current == null || current.Body == null || _view.IsRemote) return;
            var look = GrowingCharacterStageSelector.ApplyChoice(_registry.CharacterAppearances, current, appearanceId);
            if (look == null || look.Id != appearanceId) return;
            var wardrobe = Wardrobe() ?? (_wardrobe = new GrowingWardrobeStore());
            wardrobe.SetChoice(current.Body.Id, appearanceId);
            if (_boundProgress != null) wardrobe.Save(new SaveService(_boundProgress.SaveSlot), _boundProgress.PlayerId);
            _view.Apply(look);
        }

        private GrowingWardrobeStore Wardrobe()
        {
            if (_boundProgress == null) return _wardrobe;
            string owner = _boundProgress.SaveSlot + "/" + _boundProgress.PlayerId;
            if (_wardrobe == null || _wardrobeOwner != owner)
            {
                _wardrobe = GrowingWardrobeStore.Load(new SaveService(_boundProgress.SaveSlot), _boundProgress.PlayerId);
                _wardrobeOwner = owner;
            }

            return _wardrobe;
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
