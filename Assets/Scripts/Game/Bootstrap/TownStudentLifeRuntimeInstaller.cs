using System.Collections;
using System.Collections.Generic;
using Rootborn.Game.Common;
using Rootborn.Game.Player;
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
        private const string CareerPracticeBoardName = "CareerPracticeBoard";
        private const string LifeActivityBoardName = "LifeActivityBoard";

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
                var players = new List<GameObject>();
                float elapsed = 0f;
                while (players.Count == 0 && elapsed < 5f)
                {
                    CollectPlayerRoots(scene, players);
                    if (players.Count == 0)
                    {
                        elapsed += UnityEngine.Time.unscaledDeltaTime;
                        yield return null;
                    }
                }

                if (players.Count == 0)
                {
                    yield break;
                }

                EnsureActivityObjects(scene);
                foreach (var candidate in players)
                {
                    EnsureProgress(candidate);
                    RestoreProgress(candidate, scene);
                }

                for (int i = 0; i < 12; i++)
                {
                    yield return null;
                    CollectPlayerRoots(scene, players);
                    foreach (var candidate in players)
                    {
                        EnsureProgress(candidate);
                    }
                }

                elapsed = 0f;
                while (elapsed < 60f)
                {
                    EnsureActivityObjects(scene);
                    if (StudyBasicsHasRelationshipConditionEffects())
                    {
                        Debug.Log("[ROOTBORN] relationship condition activity objects rebound scene=" + scene.name);
                        yield break;
                    }

                    elapsed += UnityEngine.Time.unscaledDeltaTime;
                    yield return null;
                }
            }
        }

        private static void EnsureProgress(GameObject player)
        {
            if (player == null)
            {
                return;
            }

            var progress = player.GetComponent<StudentLifeProgressComponent>();
            if (progress == null)
            {
                progress = player.AddComponent<StudentLifeProgressComponent>();
            }

            progress.EnsureProgress();
        }

        private static void RestoreProgress(GameObject player, Scene scene)
        {
            var progress = player != null ? player.GetComponent<StudentLifeProgressComponent>() : null;
            if (progress == null)
            {
                return;
            }

            var registry = Rootborn.Game.Managers.Managers.Data != null ? Rootborn.Game.Managers.Managers.Data.Registry : null;
            var traits = new List<TraitDefinition>();
            var skills = new List<SkillDefinition>();
            var careers = new List<CareerDefinition>();
            AddRange(traits, registry != null ? registry.StudentLifeTraits : null);
            AddRange(skills, registry != null ? registry.StudentLifeSkills : null);
            AddRange(careers, registry != null ? registry.Careers : null);

            var interactors = new List<StudentLifeActivityInteractor>();
            CollectComponents(scene, interactors);
            for (int i = 0; i < interactors.Count; i++)
            {
                AddUnique(traits, interactors[i].PrimaryTrait);
                AddUnique(skills, interactors[i].PrimarySkill);
                AddUnique(careers, interactors[i].PrimaryCareer);
            }

            var boards = new List<CareerPracticeBoard>();
            CollectComponents(scene, boards);
            for (int i = 0; i < boards.Count; i++)
            {
                var practices = boards[i].Practices;
                for (int j = 0; j < practices.Count; j++)
                {
                    if (practices[j] != null)
                    {
                        AddUnique(careers, practices[j].CareerHint);
                    }
                }
            }

            StudentLifeProgressPersistence.TryLoad(progress, traits, skills, careers);
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
            EnsureActivity(root.transform, "SchoolEntryActivity", new Vector3(-1.5f, 1.25f, 0f), CreateAttendSchoolActivity(curiosity), curiosity, basicStudy, studyCareer);
            EnsureActivity(root.transform, "StudyBasicsActivity", new Vector3(-0.5f, 0f, 0f), CreateStudyBasicsActivity(diligence, basicStudy, studyCareer), diligence, basicStudy, studyCareer);
            EnsureCareerPracticeBoard(root.transform);
            EnsureLifeActivityBoard(root.transform);
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

        private static void EnsureCareerPracticeBoard(Transform root)
        {
            var existing = root.Find(CareerPracticeBoardName);
            GameObject go = existing == null ? new GameObject(CareerPracticeBoardName) : existing.gameObject;
            go.transform.SetParent(root, false);
            go.transform.position = new Vector3(0.75f, 1.9f, 0f);

            var renderer = go.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                renderer = go.AddComponent<SpriteRenderer>();
            }
            if (renderer.sprite == null)
            {
                renderer.sprite = CreateMarkerSprite(new Color(0.95f, 0.75f, 0.25f, 1f));
            }
            renderer.sortingOrder = 46;

            var collider = go.GetComponent<BoxCollider2D>();
            if (collider == null)
            {
                collider = go.AddComponent<BoxCollider2D>();
            }
            collider.isTrigger = true;
            collider.size = new Vector2(0.9f, 0.9f);

            var board = go.GetComponent<CareerPracticeBoard>();
            if (board == null)
            {
                board = go.AddComponent<CareerPracticeBoard>();
            }
            board.Bind(CreateCareerPractices());
        }

        private static void EnsureLifeActivityBoard(Transform root)
        {
            var existing = root.Find(LifeActivityBoardName);
            GameObject go = existing == null ? new GameObject(LifeActivityBoardName) : existing.gameObject;
            go.transform.SetParent(root, false);
            go.transform.position = new Vector3(1.85f, 0f, 0f);

            var renderer = go.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                renderer = go.AddComponent<SpriteRenderer>();
            }
            if (renderer.sprite == null)
            {
                renderer.sprite = CreateMarkerSprite(new Color(0.45f, 0.85f, 0.95f, 1f));
            }
            renderer.sortingOrder = 47;

            var collider = go.GetComponent<BoxCollider2D>();
            if (collider == null)
            {
                collider = go.AddComponent<BoxCollider2D>();
            }
            collider.isTrigger = true;
            collider.size = new Vector2(0.9f, 0.9f);

            var board = go.GetComponent<LifeActivityBoard>();
            if (board == null)
            {
                board = go.AddComponent<LifeActivityBoard>();
            }
            board.Bind(CreateLifestyleActivities());
        }

        private static CareerPracticeDefinition[] CreateCareerPractices()
        {
            var focus = CreateTrait("trait.focus", "trait.focus");
            var service = CreateTrait("trait.service-sense", "trait.service-sense");
            var creativity = CreateTrait("trait.creativity", "trait.creativity");
            var planning = CreateTrait("trait.planning", "trait.planning");
            var fitness = CreateTrait("trait.fitness", "trait.fitness");
            var discipline = CreateTrait("trait.discipline", "trait.discipline");
            var responsibility = CreateTrait("trait.responsibility", "trait.responsibility");
            var calm = CreateTrait("trait.calm", "trait.calm");
            var empathy = CreateTrait("trait.empathy", "trait.empathy");
            var observation = CreateTrait("trait.observation", "trait.observation");

            var cooking = CreateSkill("skill.cooking", "skill.cooking");
            var timing = CreateSkill("skill.timing", "skill.timing");
            var orderJudgment = CreateSkill("skill.order-judgment", "skill.order-judgment");
            var spaceLayout = CreateSkill("skill.space-layout", "skill.space-layout");
            var colorHarmony = CreateSkill("skill.color-harmony", "skill.color-harmony");
            var budgetSense = CreateSkill("skill.budget-sense", "skill.budget-sense");
            var reactionSpeed = CreateSkill("skill.reaction-speed", "skill.reaction-speed");
            var training = CreateSkill("skill.training", "skill.training");
            var teamwork = CreateSkill("skill.teamwork", "skill.teamwork");
            var firstAid = CreateSkill("skill.first-aid", "skill.first-aid");
            var symptomJudgment = CreateSkill("skill.symptom-judgment", "skill.symptom-judgment");
            var care = CreateSkill("skill.care", "skill.care");

            return new[]
            {
                CreatePractice("practice.chef", "practice.chef", CreateCareer("career.chef", "career.chef"), new[] { "practice.chef.ingredients", "practice.chef.sequence", "practice.chef.order-response" }, new LifeActivityEffectBase[] { Trait(focus, 2), Trait(service, 1), Skill(cooking, 3), Skill(orderJudgment, 2), Skill(timing, 2) }),
                CreatePractice("practice.interior", "practice.interior", CreateCareer("career.interior", "career.interior"), new[] { "practice.interior.room-purpose", "practice.interior.layout", "practice.interior.color-budget" }, new LifeActivityEffectBase[] { Trait(creativity, 2), Trait(planning, 2), Skill(spaceLayout, 3), Skill(colorHarmony, 2), Skill(budgetSense, 2) }),
                CreatePractice("practice.soldier", "practice.soldier", CreateCareer("career.soldier", "career.soldier"), new[] { "practice.soldier.fitness", "practice.soldier.orders", "practice.soldier.teamwork" }, new LifeActivityEffectBase[] { Trait(fitness, 2), Trait(discipline, 2), Trait(responsibility, 1), Skill(training, 3), Skill(reactionSpeed, 2), Skill(teamwork, 2) }),
                CreatePractice("practice.emergency-care", "practice.emergency-care", CreateCareer("career.emergency-care", "career.emergency-care"), new[] { "practice.emergency-care.symptom-check", "practice.emergency-care.first-aid", "practice.emergency-care.patient-care" }, new LifeActivityEffectBase[] { Trait(calm, 2), Trait(empathy, 2), Trait(observation, 1), Skill(firstAid, 3), Skill(symptomJudgment, 2), Skill(care, 2) }),
            };
        }

        private static LifeActivityDefinition[] CreateLifestyleActivities()
        {
            var focus = CreateTrait("trait.focus", "trait.focus");
            var service = CreateTrait("trait.service-sense", "trait.service-sense");
            var creativity = CreateTrait("trait.creativity", "trait.creativity");
            var planning = CreateTrait("trait.planning", "trait.planning");
            var fitness = CreateTrait("trait.fitness", "trait.fitness");
            var discipline = CreateTrait("trait.discipline", "trait.discipline");
            var responsibility = CreateTrait("trait.responsibility", "trait.responsibility");
            var calm = CreateTrait("trait.calm", "trait.calm");
            var empathy = CreateTrait("trait.empathy", "trait.empathy");
            var observation = CreateTrait("trait.observation", "trait.observation");

            return new[]
            {
                CreateLifestyleActivity("activity.morning-routine", LifeActivityCategory.SelfStudy, new[]
                {
                    Choice("choice.pack-timetable", Trait(planning, 1), Trait(responsibility, 1)),
                    Choice("choice.leave-on-time", Trait(discipline, 1), Trait(responsibility, 1)),
                    Choice("choice.help-family-breakfast", Trait(empathy, 1), Trait(service, 1)),
                }),
                CreateLifestyleActivity("activity.class-time", LifeActivityCategory.School, new[]
                {
                    Choice("choice.focus-notes", Trait(focus, 2), Trait(observation, 1)),
                    Choice("choice.prepare-presentation", Trait(planning, 2), Trait(calm, 1), Trait(creativity, 1)),
                    Choice("choice.help-friend-question", Trait(empathy, 1), Trait(service, 1)),
                }),
                CreateLifestyleActivity("activity.after-school-club", LifeActivityCategory.Hobby, new[]
                {
                    Choice("choice.art-craft", Trait(creativity, 2), Trait(observation, 1)),
                    Choice("choice.sports-club", Trait(fitness, 2), Trait(discipline, 1)),
                    Choice("choice.volunteer-club", Trait(empathy, 1), Trait(responsibility, 1), Trait(service, 1)),
                }),
                CreateLifestyleActivity("activity.house-chores", LifeActivityCategory.Errand, new[]
                {
                    Choice("choice.clean-room", Trait(planning, 1), Trait(discipline, 1)),
                    Choice("choice.help-meal-prep", Trait(service, 1), Trait(responsibility, 1)),
                    Choice("choice.check-family-mood", Trait(observation, 1), Trait(empathy, 1)),
                }),
                CreateLifestyleActivity("activity.neighborhood-help", LifeActivityCategory.Errand, new[]
                {
                    Choice("choice.guide-lost-neighbor", Trait(observation, 1), Trait(empathy, 1)),
                    Choice("choice.store-errand", Trait(responsibility, 1), Trait(service, 1)),
                    Choice("choice.park-exercise", Trait(fitness, 1), Trait(discipline, 1)),
                }),
                CreateLifestyleActivity("activity.training-practice", LifeActivityCategory.Hobby, new[]
                {
                    Choice("choice.pace-run", Trait(fitness, 1), Trait(calm, 1)),
                    Choice("choice.follow-routine", Trait(discipline, 1), Trait(planning, 1)),
                    Choice("choice.recover-after-mistake", Trait(calm, 1), Trait(responsibility, 1)),
                }),
                CreateLifestyleActivity("activity.sudden-trouble", LifeActivityCategory.Social, new[]
                {
                    Choice("choice.calm-first-aid", Trait(calm, 1), Trait(empathy, 1), Trait(observation, 1)),
                    Choice("choice.prioritize-schedule", Trait(planning, 1), Trait(calm, 1)),
                    Choice("choice.choose-conflict-words", Trait(empathy, 1), Trait(calm, 1)),
                }),
                CreateLifestyleActivity("activity.part-time-shift", LifeActivityCategory.Work, new[]
                {
                    Choice("choice.greet-customers", Trait(service, 1), Trait(observation, 1)),
                    Choice("choice.track-orders", Trait(focus, 1), Trait(responsibility, 1)),
                    Choice("choice.solve-queue-pressure", Trait(calm, 1), Trait(service, 1)),
                }),
            };
        }

        private static CareerPracticeDefinition CreatePractice(string id, string displayNameKey, CareerDefinition career, string[] stepIds, LifeActivityEffectBase[] effects)
        {
            var careerEffect = ScriptableObject.CreateInstance<CareerHintUnlockActivityEffect>();
            careerEffect.ConfigureForTests(career);
            var allEffects = new LifeActivityEffectBase[effects.Length + 1];
            effects.CopyTo(allEffects, 0);
            allEffects[allEffects.Length - 1] = careerEffect;

            var activity = ScriptableObject.CreateInstance<LifeActivityDefinition>();
            activity.ConfigureForTests(id, displayNameKey, LifeActivityCategory.School, 45, 1, 1, 0, null, allEffects);

            var steps = new PracticeStepDefinition[stepIds.Length];
            for (int i = 0; i < stepIds.Length; i++)
            {
                steps[i] = CreateStep(stepIds[i], stepIds[i]);
            }

            var practice = ScriptableObject.CreateInstance<CareerPracticeDefinition>();
            practice.ConfigureForTests(id, displayNameKey, activity, career, steps);
            return practice;
        }

        private static LifeActivityDefinition CreateLifestyleActivity(string id, LifeActivityCategory category, LifeChoiceDefinition[] choices)
        {
            var activity = ScriptableObject.CreateInstance<LifeActivityDefinition>();
            activity.ConfigureForTests(id, id, category, 20, 1, 1, 0, null, null, choices);
            return activity;
        }

        private static LifeChoiceDefinition Choice(string id, params LifeActivityEffectBase[] effects)
        {
            var choice = ScriptableObject.CreateInstance<LifeChoiceDefinition>();
            choice.ConfigureForTests(id, id, effects);
            return choice;
        }

        private static LifeActivityDefinition CreateAttendSchoolActivity(TraitDefinition curiosity)
        {
            var effect = ScriptableObject.CreateInstance<TraitDeltaActivityEffect>();
            effect.ConfigureForTests(curiosity, 1);
            var activity = ScriptableObject.CreateInstance<LifeActivityDefinition>();
            activity.ConfigureForTests("activity.attend-school", "activity.attend-school", LifeActivityCategory.School, 30, 1, 0, 0, null, new LifeActivityEffectBase[] { effect });
            return activity;
        }

        private static LifeActivityDefinition CreateStudyBasicsActivity(TraitDefinition diligence, SkillDefinition basicStudy, CareerDefinition career)
        {
            var effects = new List<LifeActivityEffectBase>();

            var traitEffect = ScriptableObject.CreateInstance<TraitDeltaActivityEffect>();
            traitEffect.ConfigureForTests(diligence, 2);
            effects.Add(traitEffect);

            var skillEffect = ScriptableObject.CreateInstance<SkillProgressActivityEffect>();
            skillEffect.ConfigureForTests(basicStudy, 3);
            effects.Add(skillEffect);

            var careerEffect = ScriptableObject.CreateInstance<CareerHintUnlockActivityEffect>();
            careerEffect.ConfigureForTests(career);
            effects.Add(careerEffect);

            var relationship = FirstRelationshipDefinition();
            if (relationship != null)
            {
                var relationshipEffect = ScriptableObject.CreateInstance<RelationshipDeltaEffect>();
                relationshipEffect.ConfigureForTests(relationship, 2);
                effects.Add(relationshipEffect);
            }

            var status = FirstStatusDefinition();
            if (status != null)
            {
                var statusEffect = ScriptableObject.CreateInstance<StatusDeltaEffect>();
                statusEffect.ConfigureForTests(status, 3);
                effects.Add(statusEffect);
            }

            var activity = ScriptableObject.CreateInstance<LifeActivityDefinition>();
            activity.ConfigureForTests("activity.study-basics", "activity.study-basics", LifeActivityCategory.School, 60, 2, 2, 1, null, effects.ToArray());
            return activity;
        }

        private static bool StudyBasicsHasRelationshipConditionEffects()
        {
            return FirstRelationshipDefinition() != null && FirstStatusDefinition() != null;
        }

        private static RelationshipDefinition FirstRelationshipDefinition()
        {
            var registry = Rootborn.Game.Managers.Managers.Data != null ? Rootborn.Game.Managers.Managers.Data.Registry : null;
            return registry != null && registry.Relationships != null && registry.Relationships.Length > 0 ? registry.Relationships[0] : null;
        }

        private static StatusDefinition FirstStatusDefinition()
        {
            var registry = Rootborn.Game.Managers.Managers.Data != null ? Rootborn.Game.Managers.Managers.Data.Registry : null;
            return registry != null && registry.StudentConditionStatuses != null && registry.StudentConditionStatuses.Length > 0 ? registry.StudentConditionStatuses[0] : null;
        }

        private static PracticeStepDefinition CreateStep(string id, string displayNameKey)
        {
            var step = ScriptableObject.CreateInstance<PracticeStepDefinition>();
            step.ConfigureForTests(id, displayNameKey);
            return step;
        }

        private static TraitDeltaActivityEffect Trait(TraitDefinition trait, int delta)
        {
            var effect = ScriptableObject.CreateInstance<TraitDeltaActivityEffect>();
            effect.ConfigureForTests(trait, delta);
            return effect;
        }

        private static SkillProgressActivityEffect Skill(SkillDefinition skill, int delta)
        {
            var effect = ScriptableObject.CreateInstance<SkillProgressActivityEffect>();
            effect.ConfigureForTests(skill, delta);
            return effect;
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

        private static void CollectPlayerRoots(Scene scene, List<GameObject> players)
        {
            players.Clear();
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (IsPlayerRoot(roots[i]))
                {
                    players.Add(roots[i]);
                }
            }
        }

        private static bool IsPlayerRoot(GameObject root)
        {
            if (root == null)
            {
                return false;
            }

            return root.name == "Player" ||
                   root.name == "Player(Clone)" ||
                   root.GetComponent<PlayerIdentity>() != null ||
                   root.GetComponent<PlayerController>() != null;
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

        private static void CollectComponents<T>(Scene scene, List<T> results) where T : Component
        {
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                CollectComponents(roots[i].transform, results);
            }
        }

        private static void CollectComponents<T>(Transform root, List<T> results) where T : Component
        {
            if (root.TryGetComponent<T>(out var component))
            {
                results.Add(component);
            }

            for (int i = 0; i < root.childCount; i++)
            {
                CollectComponents(root.GetChild(i), results);
            }
        }
    }
}
