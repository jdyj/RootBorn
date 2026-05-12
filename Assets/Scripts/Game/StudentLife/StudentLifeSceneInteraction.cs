using System;
using System.Collections.Generic;
using System.Globalization;
using Rootborn.Game.Common;
using Rootborn.Game.Managers;
using Rootborn.Game.Player;
using Rootborn.Game.Save;
using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [DisallowMultipleComponent]
    public sealed class StudentLifeProgressComponent : MonoBehaviour
    {
        [SerializeField] private string _fallbackPlayerId = "local-player";
        [SerializeField] private int _startingEnergy = 12;
        [SerializeField] private int _startingFocus = 10;
        [SerializeField] private int _startingStress;
        [SerializeField] private int _startingTimeMinutes = 8 * 60;

        public StudentLifeProgress Progress { get; private set; }

        private void Awake()
        {
            EnsureProgress();
        }

        public void ConfigureForTests(string saveSlot, string playerId, int energy, int focus, int stress, int timeMinutes)
        {
            Progress = new StudentLifeProgress(saveSlot, playerId, energy, focus, stress, timeMinutes);
        }

        public void RestoreFromSaveData(
            StudentLifeProgressSaveData saveData,
            IReadOnlyList<TraitDefinition> traits,
            IReadOnlyList<SkillDefinition> skills,
            IReadOnlyList<CareerDefinition> careers)
        {
            Progress = StudentLifeProgress.FromSaveData(saveData, traits, skills, careers);
            SynchronizeWithPlayerIdentity();
        }

        public StudentLifeProgress EnsureProgress()
        {
            if (Progress != null)
            {
                SynchronizeWithPlayerIdentity();
                return Progress;
            }

            string saveSlot = ActiveSaveContext.Metadata != null && !string.IsNullOrEmpty(ActiveSaveContext.Metadata.SlotId)
                ? ActiveSaveContext.Metadata.SlotId
                : "default";
            Progress = new StudentLifeProgress(saveSlot, ResolvePlayerId(), _startingEnergy, _startingFocus, _startingStress, _startingTimeMinutes);
            ApplyDefaultTutorialStage(Rootborn.Game.Managers.Managers.Data != null ? Rootborn.Game.Managers.Managers.Data.Registry : null);
            return Progress;
        }

        public void SynchronizeWithPlayerIdentity()
        {
            if (Progress == null)
            {
                return;
            }

            string playerId = ResolvePlayerId();
            if (string.IsNullOrEmpty(playerId) || Progress.PlayerId == playerId)
            {
                return;
            }

            if (Progress.PlayerId != PlayerIdentity.DefaultPlayerId && Progress.PlayerId != _fallbackPlayerId && Progress.PlayerId != "player")
            {
                return;
            }

            var saveData = Progress.ToSaveData();
            saveData.PlayerId = playerId;
            Progress = StudentLifeProgress.FromSaveData(saveData, null, null, null);
        }

        public void ApplyDefaultTutorialStage(GameDataRegistry registry)
        {
            if (Progress == null || registry == null || registry.DefaultTutorialStage == null || !string.IsNullOrEmpty(Progress.TutorialStageId))
            {
                return;
            }

            Progress.SetTutorialStage(registry.DefaultTutorialStage, string.Empty);
        }

        private string ResolvePlayerId()
        {
            var identity = GetComponent<PlayerIdentity>();
            if (identity != null && !string.IsNullOrEmpty(identity.PlayerId))
            {
                return identity.PlayerId;
            }

            return string.IsNullOrEmpty(_fallbackPlayerId) ? PlayerIdentity.DefaultPlayerId : _fallbackPlayerId;
        }
    }

    public static class StudentLifeProgressPersistence
    {
        private const string StudentLifeFileName = "student-life-progress.json";

        public static bool TryLoad(
            StudentLifeProgressComponent component,
            IReadOnlyList<TraitDefinition> traits,
            IReadOnlyList<SkillDefinition> skills,
            IReadOnlyList<CareerDefinition> careers)
        {
            var metadata = ActiveSaveContext.Metadata;
            if (component == null || metadata == null || string.IsNullOrEmpty(metadata.SlotId))
            {
                return false;
            }

            string playerId = ResolvePlayerId(component.gameObject);
            string json = new SaveService(metadata.SlotId).ReadJson(FileNameFor(playerId));
            if (string.IsNullOrEmpty(json))
            {
                return false;
            }

            component.RestoreFromSaveData(JsonUtility.FromJson<StudentLifeProgressSaveData>(json), traits, skills, careers);
            return true;
        }

        public static void Save(StudentLifeProgressComponent component)
        {
            var metadata = ActiveSaveContext.Metadata;
            if (component == null || metadata == null || string.IsNullOrEmpty(metadata.SlotId))
            {
                return;
            }

            var progress = component.EnsureProgress();
            string json = JsonUtility.ToJson(progress.ToSaveData(), true);
            new SaveService(metadata.SlotId).WriteJson(FileNameFor(progress.PlayerId), json);
        }

        public static TraitDefinition[] CollectTraits(GameDataRegistry registry, params TraitDefinition[] extra)
        {
            var list = new List<TraitDefinition>();
            AddRange(list, registry != null ? registry.StudentLifeTraits : null);
            AddRange(list, extra);
            return list.ToArray();
        }

        public static SkillDefinition[] CollectSkills(GameDataRegistry registry, params SkillDefinition[] extra)
        {
            var list = new List<SkillDefinition>();
            AddRange(list, registry != null ? registry.StudentLifeSkills : null);
            AddRange(list, extra);
            return list.ToArray();
        }

        public static CareerDefinition[] CollectCareers(GameDataRegistry registry, params CareerDefinition[] extra)
        {
            var list = new List<CareerDefinition>();
            AddRange(list, registry != null ? registry.Careers : null);
            AddRange(list, extra);
            return list.ToArray();
        }

        private static void AddRange<T>(List<T> list, IReadOnlyList<T> values) where T : StudentLifeDefinitionBase
        {
            if (values == null)
            {
                return;
            }

            for (int i = 0; i < values.Count; i++)
            {
                AddUnique(list, values[i]);
            }
        }

        private static void AddUnique<T>(List<T> list, T value) where T : StudentLifeDefinitionBase
        {
            if (value == null || string.IsNullOrEmpty(value.Id))
            {
                return;
            }

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null && list[i].Id == value.Id)
                {
                    return;
                }
            }

            list.Add(value);
        }

        private static string ResolvePlayerId(GameObject player)
        {
            var identity = player != null ? player.GetComponent<PlayerIdentity>() : null;
            return identity != null ? identity.PlayerId : PlayerIdentity.DefaultPlayerId;
        }

        private static string FileNameFor(string playerId)
        {
            if (string.IsNullOrEmpty(playerId) || playerId == PlayerIdentity.DefaultPlayerId)
            {
                return StudentLifeFileName;
            }

            return "student-life-progress-" + SanitizeFileName(playerId) + ".json";
        }

        private static string SanitizeFileName(string value)
        {
            var chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                if (!char.IsLetterOrDigit(c) && c != '-' && c != '_')
                {
                    chars[i] = '_';
                }
            }

            return new string(chars);
        }
    }

    [DisallowMultipleComponent]
    public sealed class StudentLifeActivityInteractor : MonoBehaviour, IPlayerInteractable
    {
        [SerializeField] private LifeActivityDefinition _activity;
        [SerializeField] private TraitDefinition _primaryTrait;
        [SerializeField] private SkillDefinition _primarySkill;
        [SerializeField] private CareerDefinition _primaryCareer;

        private readonly LifeActivityRunner _runner = new LifeActivityRunner();
        private int _requestSequence;

        public LifeActivityDefinition Activity => _activity;
        public TraitDefinition PrimaryTrait => _primaryTrait;
        public SkillDefinition PrimarySkill => _primarySkill;
        public CareerDefinition PrimaryCareer => _primaryCareer;
        public LifeActivityResult LastResult { get; private set; }
        public string InteractionPrompt => "[E] " + HumanizeDisplayKey(_activity != null ? _activity.DisplayNameKey : string.Empty);
        public Vector3 InteractionPromptOffset => new Vector3(0f, 1.05f, 0f);
        public Transform InteractionTransform => transform;

        public void Bind(
            LifeActivityDefinition activity,
            TraitDefinition primaryTrait,
            SkillDefinition primarySkill,
            CareerDefinition primaryCareer)
        {
            _activity = activity;
            _primaryTrait = primaryTrait;
            _primarySkill = primarySkill;
            _primaryCareer = primaryCareer;
        }

        public bool CanInteract(GameObject player)
        {
            return _activity != null && player != null && player.GetComponent<StudentLifeProgressComponent>() != null;
        }

        public bool TryInteract(GameObject player)
        {
            if (!CanInteract(player))
            {
                Debug.Log($"[ROOTBORN] Student life activity interact rejected canInteract=false activity={(_activity != null ? _activity.Id : string.Empty)} player={(player != null ? player.name : string.Empty)}");
                return false;
            }

            return Interact(player.GetComponent<StudentLifeProgressComponent>());
        }

        public bool Interact(StudentLifeProgressComponent progressComponent)
        {
            string requestId = _activity != null ? _activity.Id + ":" + _requestSequence++ : "activity:" + _requestSequence++;
            return Interact(progressComponent, requestId);
        }

        public bool Interact(StudentLifeProgressComponent progressComponent, string requestId)
        {
            var progress = progressComponent == null ? null : progressComponent.EnsureProgress();
            bool applied = _runner.TryPerform(_activity, progress, requestId, out var result);
            LastResult = result;
            Debug.Log($"[ROOTBORN] Student life activity interact applied={applied} result={result.Kind} activity={result.ActivityId} player={result.PlayerId} request={result.RequestId} todayCount={(progress != null ? progress.GetTodayActivityIds().Length : 0)}");
            if (applied)
            {
                StudentLifeProgressPersistence.Save(progressComponent);
            }
            return applied;
        }

        private static string HumanizeDisplayKey(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return "Interact";
            }

            int dot = key.LastIndexOf('.');
            string tail = dot >= 0 && dot + 1 < key.Length ? key.Substring(dot + 1) : key;
            string[] parts = tail.Split(new[] { '-', '_', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return key;
            }

            var textInfo = CultureInfo.InvariantCulture.TextInfo;
            for (int i = 0; i < parts.Length; i++)
            {
                parts[i] = textInfo.ToTitleCase(parts[i]);
            }

            return string.Join(" ", parts);
        }
    }

    [DisallowMultipleComponent]
    public sealed class LifeActivityBoard : MonoBehaviour
    {
        [SerializeField] private LifeActivityDefinition[] _activities = Array.Empty<LifeActivityDefinition>();

        private readonly LifeActivityRunner _runner = new LifeActivityRunner();
        private readonly List<string> _changedTraitIds = new List<string>();

        public IReadOnlyList<LifeActivityDefinition> Activities => _activities;
        public IReadOnlyList<string> ChangedTraitIds => _changedTraitIds;
        public string LastActivityId { get; private set; } = string.Empty;
        public string LastChoiceId { get; private set; } = string.Empty;
        public string LastRequestId { get; private set; } = string.Empty;
        public LifeActivityResultKind LastResultKind { get; private set; } = LifeActivityResultKind.InvalidRequest;

        public void Bind(LifeActivityDefinition[] activities)
        {
            _activities = activities ?? Array.Empty<LifeActivityDefinition>();
            _changedTraitIds.Clear();
            LastActivityId = string.Empty;
            LastChoiceId = string.Empty;
            LastRequestId = string.Empty;
            LastResultKind = LifeActivityResultKind.InvalidRequest;
        }

        public bool RunChoice(StudentLifeProgressComponent progressComponent, int activityIndex, int choiceIndex, string requestId)
        {
            var progress = progressComponent == null ? null : progressComponent.EnsureProgress();
            var activity = GetActivity(activityIndex);
            var choice = activity == null ? null : activity.GetChoice(choiceIndex);
            bool applied = _runner.TryPerformChoice(activity, choice, progress, requestId, out var result);

            LastActivityId = activity == null ? string.Empty : activity.Id;
            LastChoiceId = choice == null ? string.Empty : choice.Id;
            LastRequestId = string.IsNullOrEmpty(requestId) ? string.Empty : requestId;
            LastResultKind = result.Kind;
            _changedTraitIds.Clear();
            for (int i = 0; i < result.ChangedTraitIds.Length; i++)
            {
                _changedTraitIds.Add(result.ChangedTraitIds[i]);
            }

            if (applied)
            {
                StudentLifeProgressPersistence.Save(progressComponent);
            }
            return applied;
        }

        private LifeActivityDefinition GetActivity(int index)
        {
            return index >= 0 && index < _activities.Length ? _activities[index] : null;
        }
    }

    [DisallowMultipleComponent]
    public sealed class CareerPracticeBoard : MonoBehaviour, IPlayerInteractable
    {
        [SerializeField] private CareerPracticeDefinition[] _practices = Array.Empty<CareerPracticeDefinition>();

        private readonly CareerPracticeRunner _runner = new CareerPracticeRunner();
        private readonly List<string> _unlockedCareerHintIds = new List<string>();
        private int _interactionSequence;
        private int _nextPracticeIndex;

        public IReadOnlyList<CareerPracticeDefinition> Practices => _practices;
        public IReadOnlyList<string> UnlockedCareerHintIds => _unlockedCareerHintIds;
        public string LastPracticeId { get; private set; } = string.Empty;
        public string LastCareerHintId { get; private set; } = string.Empty;
        public string LastRequestId { get; private set; } = string.Empty;
        public LifeActivityResultKind LastResultKind { get; private set; } = LifeActivityResultKind.InvalidRequest;
        public string InteractionPrompt => "[E] Career Practice";
        public Vector3 InteractionPromptOffset => new Vector3(0f, 1.15f, 0f);
        public Transform InteractionTransform => transform;

        public void Bind(CareerPracticeDefinition[] practices)
        {
            _practices = practices ?? Array.Empty<CareerPracticeDefinition>();
            _unlockedCareerHintIds.Clear();
            LastPracticeId = string.Empty;
            LastCareerHintId = string.Empty;
            LastRequestId = string.Empty;
            LastResultKind = LifeActivityResultKind.InvalidRequest;
            _interactionSequence = 0;
            _nextPracticeIndex = 0;
        }

        public bool CanInteract(GameObject player)
        {
            return _practices.Length > 0 && player != null && player.GetComponent<StudentLifeProgressComponent>() != null;
        }

        public bool TryInteract(GameObject player)
        {
            if (!CanInteract(player))
            {
                return false;
            }

            int index = _nextPracticeIndex;
            _nextPracticeIndex = (_nextPracticeIndex + 1) % _practices.Length;
            var practice = GetPractice(index);
            string requestId = practice != null ? practice.Id + ":" + _interactionSequence++ : "practice:" + _interactionSequence++;
            return RunPractice(player.GetComponent<StudentLifeProgressComponent>(), index, requestId);
        }

        public bool RunPractice(StudentLifeProgressComponent progressComponent, int practiceIndex, string requestId)
        {
            var progress = progressComponent == null ? null : progressComponent.EnsureProgress();
            var practice = GetPractice(practiceIndex);
            bool applied = _runner.TryPerform(practice, progress, requestId, out var result);

            LastPracticeId = practice == null ? string.Empty : practice.Id;
            LastCareerHintId = practice != null && practice.CareerHint != null ? practice.CareerHint.Id : string.Empty;
            LastRequestId = string.IsNullOrEmpty(requestId) ? string.Empty : requestId;
            LastResultKind = result.Kind;

            if (applied && practice != null && practice.CareerHint != null && progress != null && progress.IsCareerHintUnlocked(practice.CareerHint))
            {
                AddUnlockedCareerHintId(practice.CareerHint.Id);
                StudentLifeProgressPersistence.Save(progressComponent);
            }

            return applied;
        }

        private CareerPracticeDefinition GetPractice(int index)
        {
            return index >= 0 && index < _practices.Length ? _practices[index] : null;
        }

        private void AddUnlockedCareerHintId(string careerHintId)
        {
            if (string.IsNullOrEmpty(careerHintId))
            {
                return;
            }

            for (int i = 0; i < _unlockedCareerHintIds.Count; i++)
            {
                if (_unlockedCareerHintIds[i] == careerHintId)
                {
                    return;
                }
            }

            _unlockedCareerHintIds.Add(careerHintId);
        }
    }
}