using System.Collections;
using Rootborn.Game.StudentLife;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rootborn.Game.Bootstrap
{
    public static class TownStudentLifeRuntimeInstaller
    {
        private const string TownSceneName = "Town";
        private const string RunnerName = "[TownStudentLifeRuntimeInstaller]";
        private const string RootName = "[StudentLife]";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            EnsureForActiveScene();
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == TownSceneName)
            {
                Ensure(scene);
            }
        }

        private static void EnsureForActiveScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.name == TownSceneName)
            {
                Ensure(scene);
            }
        }

        private static void Ensure(Scene scene)
        {
            if (FindRoot(scene, RunnerName) != null)
            {
                return;
            }

            var runner = new GameObject(RunnerName);
            SceneManager.MoveGameObjectToScene(runner, scene);
            runner.AddComponent<Runner>();
        }

        private sealed class Runner : MonoBehaviour
        {
            private IEnumerator Start()
            {
                Scene scene = SceneManager.GetActiveScene();
                GameObject player = null;
                float elapsed = 0f;
                while (player == null && elapsed < 5f)
                {
                    player = FindRoot(scene, "Player");
                    if (player == null)
                    {
                        elapsed += UnityEngine.Time.unscaledDeltaTime;
                        yield return null;
                    }
                }

                if (player == null)
                {
                    yield break;
                }

                EnsureProgress(player);
                EnsureActivityObjects(scene);
            }
        }

        private static void EnsureProgress(GameObject player)
        {
            var progress = player.GetComponent<StudentLifeProgressComponent>();
            if (progress == null)
            {
                progress = player.AddComponent<StudentLifeProgressComponent>();
            }

            progress.EnsureProgress();
        }

        private static void EnsureActivityObjects(Scene scene)
        {
            var root = FindRoot(scene, RootName);
            if (root == null)
            {
                root = new GameObject(RootName);
                SceneManager.MoveGameObjectToScene(root, scene);
            }

            var diligence = CreateTrait("trait.diligence", "trait.diligence");
            var curiosity = CreateTrait("trait.curiosity", "trait.curiosity");
            var basicStudy = CreateSkill("skill.basic-study", "skill.basic-study");
            var studyCareer = CreateCareer("career.study-path", "career.study-path");
            EnsureActivity(root.transform, "SchoolEntryActivity", new Vector3(-1.5f, 0f, 0f), CreateAttendSchoolActivity(curiosity), curiosity, basicStudy, studyCareer);
            EnsureActivity(root.transform, "StudyBasicsActivity", new Vector3(-0.5f, 0f, 0f), CreateStudyBasicsActivity(diligence, basicStudy, studyCareer), diligence, basicStudy, studyCareer);
        }

        private static void EnsureActivity(
            Transform root,
            string name,
            Vector3 position,
            LifeActivityDefinition activity,
            TraitDefinition trait,
            SkillDefinition skill,
            CareerDefinition career)
        {
            var existing = root.Find(name);
            GameObject go = existing == null ? new GameObject(name) : existing.gameObject;
            go.transform.SetParent(root, false);
            go.transform.position = position;

            var renderer = go.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                renderer = go.AddComponent<SpriteRenderer>();
            }
            if (renderer.sprite == null)
            {
                renderer.sprite = CreateMarkerSprite(name.Contains("Study") ? new Color(0.2f, 0.45f, 0.9f, 1f) : new Color(0.2f, 0.75f, 0.45f, 1f));
            }
            renderer.sortingOrder = 45;

            var collider = go.GetComponent<BoxCollider2D>();
            if (collider == null)
            {
                collider = go.AddComponent<BoxCollider2D>();
            }
            collider.isTrigger = true;
            collider.size = new Vector2(0.75f, 0.75f);

            var interactor = go.GetComponent<StudentLifeActivityInteractor>();
            if (interactor == null)
            {
                interactor = go.AddComponent<StudentLifeActivityInteractor>();
            }
            interactor.Bind(activity, trait, skill, career);
        }

        private static LifeActivityDefinition CreateAttendSchoolActivity(TraitDefinition curiosity)
        {
            var effect = ScriptableObject.CreateInstance<TraitDeltaActivityEffect>();
            effect.ConfigureForTests(curiosity, 1);
            var activity = ScriptableObject.CreateInstance<LifeActivityDefinition>();
            activity.ConfigureForTests(
                "activity.attend-school",
                "activity.attend-school",
                LifeActivityCategory.School,
                timeCostMinutes: 30,
                energyCost: 1,
                focusCost: 0,
                stressDelta: 0,
                requirements: null,
                effects: new LifeActivityEffectBase[] { effect });
            return activity;
        }

        private static LifeActivityDefinition CreateStudyBasicsActivity(TraitDefinition diligence, SkillDefinition basicStudy, CareerDefinition career)
        {
            var traitEffect = ScriptableObject.CreateInstance<TraitDeltaActivityEffect>();
            traitEffect.ConfigureForTests(diligence, 2);
            var skillEffect = ScriptableObject.CreateInstance<SkillProgressActivityEffect>();
            skillEffect.ConfigureForTests(basicStudy, 3);
            var careerEffect = ScriptableObject.CreateInstance<CareerHintUnlockActivityEffect>();
            careerEffect.ConfigureForTests(career);
            var activity = ScriptableObject.CreateInstance<LifeActivityDefinition>();
            activity.ConfigureForTests(
                "activity.study-basics",
                "activity.study-basics",
                LifeActivityCategory.School,
                timeCostMinutes: 60,
                energyCost: 2,
                focusCost: 2,
                stressDelta: 1,
                requirements: null,
                effects: new LifeActivityEffectBase[] { traitEffect, skillEffect, careerEffect });
            return activity;
        }

        private static TraitDefinition CreateTrait(string id, string displayNameKey)
        {
            var trait = ScriptableObject.CreateInstance<TraitDefinition>();
            trait.ConfigureForTests(id, displayNameKey);
            return trait;
        }

        private static SkillDefinition CreateSkill(string id, string displayNameKey)
        {
            var skill = ScriptableObject.CreateInstance<SkillDefinition>();
            skill.ConfigureForTests(id, displayNameKey);
            return skill;
        }

        private static CareerDefinition CreateCareer(string id, string displayNameKey)
        {
            var career = ScriptableObject.CreateInstance<CareerDefinition>();
            career.ConfigureForTests(id, displayNameKey, null);
            return career;
        }

        private static Sprite CreateMarkerSprite(Color color)
        {
            var texture = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            texture.name = "StudentLifeActivityMarker";
            var pixels = new Color[64];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }
            texture.SetPixels(pixels);
            texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, 8f, 8f), new Vector2(0.5f, 0.5f), 16f);
            sprite.name = texture.name;
            return sprite;
        }

        private static GameObject FindRoot(Scene scene, string rootName)
        {
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].name == rootName)
                {
                    return roots[i];
                }
            }

            return null;
        }
    }
}
