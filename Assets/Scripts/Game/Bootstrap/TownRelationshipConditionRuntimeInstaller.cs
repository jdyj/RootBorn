using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Rootborn.Game.StudentLife;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rootborn.Game.Bootstrap
{
    public static class TownRelationshipConditionRuntimeInstaller
    {
        private const string TownSceneName = "Town";
        private static readonly FieldInfo ActivityEffectsField = typeof(LifeActivityDefinition).GetField("_effects", BindingFlags.Instance | BindingFlags.NonPublic);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            EnsureForActiveScene();
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == TownSceneName) StartRunner(scene);
        }

        private static void EnsureForActiveScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.name == TownSceneName) StartRunner(scene);
        }

        private static void StartRunner(Scene scene)
        {
            if (FindRoot(scene, "[TownRelationshipConditionRuntimeInstaller]") != null) return;
            var runner = new GameObject("[TownRelationshipConditionRuntimeInstaller]");
            SceneManager.MoveGameObjectToScene(runner, scene);
            Debug.Log("[ROOTBORN] relationship condition installer started scene=" + scene.name);
            runner.AddComponent<Runner>();
        }

        private sealed class Runner : MonoBehaviour
        {
            private IEnumerator Start()
            {
                float elapsed = 0f;
                float nextWaitingLogAt = 0f;
                while (elapsed < 60f)
                {
                    string diagnostics;
                    if (TryAttachEffects(gameObject.scene, out diagnostics))
                    {
                        Debug.Log("[ROOTBORN] relationship condition effects attached scene=" + gameObject.scene.name + " " + diagnostics);
                    }
                    else if (elapsed >= nextWaitingLogAt)
                    {
                        Debug.Log("[ROOTBORN] relationship condition attach waiting scene=" + gameObject.scene.name + " " + diagnostics);
                        nextWaitingLogAt = elapsed + 5f;
                    }

                    elapsed += UnityEngine.Time.unscaledDeltaTime;
                    yield return null;
                }
            }
        }

        private static bool TryAttachEffects(Scene scene, out string diagnostics)
        {
            var registry = Rootborn.Game.Managers.Managers.Data != null ? Rootborn.Game.Managers.Managers.Data.Registry : null;
            int relationshipCount = registry != null && registry.Relationships != null ? registry.Relationships.Length : 0;
            int statusCount = registry != null && registry.StudentConditionStatuses != null ? registry.StudentConditionStatuses.Length : 0;
            var relationship = relationshipCount > 0 ? registry.Relationships[0] : null;
            var status = statusCount > 0 ? registry.StudentConditionStatuses[0] : null;
            var interactors = CollectActivityInteractorsAcrossLoadedScenes();
            diagnostics = "registryRelationships=" + relationshipCount + " registryStatuses=" + statusCount + " interactors=" + interactors.Count + " hasEffectsField=" + (ActivityEffectsField != null);
            if (relationship == null || status == null || ActivityEffectsField == null) return false;

            if (interactors.Count == 0) return false;

            bool attached = false;
            for (int i = 0; i < interactors.Count; i++)
            {
                var activity = interactors[i] != null ? interactors[i].Activity : null;
                if (activity != null) attached |= AttachEffects(activity, relationship, status);
            }

            return attached;
        }

        private static List<StudentLifeActivityInteractor> CollectActivityInteractorsAcrossLoadedScenes()
        {
            var interactors = new List<StudentLifeActivityInteractor>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                CollectActivityInteractors(SceneManager.GetSceneAt(i), interactors);
            }

            return interactors;
        }

        private static void CollectActivityInteractors(Scene scene, List<StudentLifeActivityInteractor> interactors)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;

            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i] == null) continue;
                roots[i].GetComponentsInChildren(true, interactors);
            }
        }

        private static bool AttachEffects(LifeActivityDefinition activity, RelationshipDefinition relationship, StatusDefinition status)
        {
            var current = ActivityEffectsField.GetValue(activity) as LifeActivityEffectBase[];
            bool hasRelationship = HasRelationshipEffect(current, relationship);
            bool hasStatus = HasStatusEffect(current, status);
            if (hasRelationship && hasStatus) return false;

            int baseLength = current != null ? current.Length : 0;
            int addCount = (hasRelationship ? 0 : 1) + (hasStatus ? 0 : 1);
            var effects = new LifeActivityEffectBase[baseLength + addCount];
            for (int i = 0; i < baseLength; i++) effects[i] = current[i];

            int index = baseLength;
            if (!hasRelationship)
            {
                var relationshipEffect = ScriptableObject.CreateInstance<RelationshipDeltaEffect>();
                relationshipEffect.ConfigureForTests(relationship, 2);
                effects[index++] = relationshipEffect;
            }

            if (!hasStatus)
            {
                var statusEffect = ScriptableObject.CreateInstance<StatusDeltaEffect>();
                statusEffect.ConfigureForTests(status, 3);
                effects[index] = statusEffect;
            }

            ActivityEffectsField.SetValue(activity, effects);
            return true;
        }

        private static bool HasRelationshipEffect(LifeActivityEffectBase[] effects, RelationshipDefinition relationship)
        {
            if (effects == null || relationship == null) return false;
            for (int i = 0; i < effects.Length; i++)
            {
                var effect = effects[i] as RelationshipDeltaEffect;
                if (effect != null && effect.Relationship != null && effect.Relationship.Id == relationship.Id) return true;
            }
            return false;
        }

        private static bool HasStatusEffect(LifeActivityEffectBase[] effects, StatusDefinition status)
        {
            if (effects == null || status == null) return false;
            for (int i = 0; i < effects.Length; i++)
            {
                var effect = effects[i] as StatusDeltaEffect;
                if (effect != null && effect.Status != null && effect.Status.Id == status.Id) return true;
            }
            return false;
        }

        private static GameObject FindRoot(Scene scene, string rootName)
        {
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++) if (roots[i].name == rootName) return roots[i];
            return null;
        }
    }
}
