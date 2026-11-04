using System;
using System.Collections.Generic;
using Rootborn.Game.Player;
using Rootborn.Game.StudentLife;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rootborn.Game.Time
{
    // Read-only presentation of the same aggregate used by student and adult commands.
    // Scene scope prevents Town time from changing legacy Farm clocks.
    [DisallowMultipleComponent]
    public sealed class LifeTimePresentation : MonoBehaviour
    {
        private static readonly List<LifeTimePresentation> Sources = new List<LifeTimePresentation>();
        private StudentLifeProgressComponent _owner;
        private bool _suspended;
        private bool _wasAvailable;
        public static event Action AvailabilityChanged;
        public event Action Changed;
        public StudentLifeProgressComponent Owner => _owner;
        public bool HasTime => isActiveAndEnabled && !_suspended && _owner != null && _owner.isActiveAndEnabled && _owner.Progress != null;
        public int Day => HasTime ? _owner.Progress.CurrentDay : 0;
        public int Minutes => HasTime ? _owner.Progress.TimeMinutes : 0;
        public float Hour => GameClock.WrapHour(Minutes / 60f);

        public static LifeTimePresentation FindInScene(Scene scene)
        {
            for (int i = 0; i < Sources.Count; i++)
                if (Sources[i] != null && Sources[i].isActiveAndEnabled && Sources[i].gameObject.scene == scene)
                    return Sources[i];
            return null;
        }

        public void Bind(StudentLifeProgressComponent owner)
        {
            if (_owner == owner) { Publish(); return; }
            if (_owner != null) _owner.TimeChanged -= Publish;
            _owner = owner;
            if (_owner != null) _owner.TimeChanged += Publish;
            Publish();
        }

        public void SetSuspended(bool suspended)
        {
            if (_suspended == suspended) return;
            _suspended = suspended;
            Publish();
        }

        private void OnEnable()
        {
            if (!Sources.Contains(this)) Sources.Add(this);
            StudentLifeProgressComponent.AvailabilityChanged += ResolveOwner;
            PlayerController.AvailabilityChanged += ResolveOwner;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            ResolveOwner();
            AvailabilityChanged?.Invoke();
        }

        private void OnDisable()
        {
            StudentLifeProgressComponent.AvailabilityChanged -= ResolveOwner;
            PlayerController.AvailabilityChanged -= ResolveOwner;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            if (_owner != null) _owner.TimeChanged -= Publish;
            _owner = null;
            Sources.Remove(this);
            Publish();
            AvailabilityChanged?.Invoke();
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene == gameObject.scene) ResolveOwner();
        }

        private void ResolveOwner()
        {
            StudentLifeProgressComponent candidate = null;
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                foreach (var owner in root.GetComponentsInChildren<StudentLifeProgressComponent>(false))
                {
                    var controller = owner.GetComponent<PlayerController>();
                    if (!owner.isActiveAndEnabled || controller == null || !controller.isActiveAndEnabled) continue;
                    // Do not arbitrarily select a player when ownership is ambiguous.
                    if (candidate != null) { Bind(null); return; }
                    candidate = owner;
                }
            }
            Bind(candidate);
        }

        private void Publish()
        {
            Changed?.Invoke();
            bool available = HasTime;
            if (_wasAvailable == available) return;
            _wasAvailable = available;
            AvailabilityChanged?.Invoke();
        }
    }
}
