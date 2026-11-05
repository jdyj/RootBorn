using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Rootborn.Game.Common;
using Rootborn.Game.Dialogue;
using Rootborn.Game.Placement;
using Rootborn.Game.Quests;
using Rootborn.Game.Quests.Conditions;
using Rootborn.Game.Quests.Objectives;
using Rootborn.Game.StudentLife;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace Rootborn.Editor.StudentLife
{
    /// <summary>Author the common opening: school, career tour, chosen part-time work and the player's dream. Does not place world objects.</summary>
    public static class MainStoryOpeningDataBuilder
    {
        private const string Root = "Assets/Data/StudentLife/MainStory/";
        private static GameDataRegistry s_registry;
        private static readonly HashSet<string> Touched = new HashSet<string>(StringComparer.Ordinal);
        private static readonly HashSet<string> Created = new HashSet<string>(StringComparer.Ordinal);
        private static readonly HashSet<string> BackedUp = new HashSet<string>(StringComparer.Ordinal);
        private static string s_runDirectory;
        // Lines the opening once prefixed. A rebuild removes them so the dropped town-past story cannot return.
        private static readonly string[] RetiredOpeningLines = { "opening.dialogue.librarian", "opening.dialogue.manager", "opening.dialogue.teacher" };

        private sealed class DreamJob
        {
            public string Career, CareerId, FacilityId, LocationName, WorkName, WorkId, PlaceName, RouteName, RouteKey, TraitPath, TraitName;
            public float PlacementOffsetX = 2f;
            public PartTimeWorkDefinition Work;
        }

        private static readonly DreamJob[] DreamJobs =
        {
            new DreamJob { Career = "Chef", CareerId = "chef", FacilityId = "restaurant", LocationName = "Restaurant", WorkName = "RestaurantHelper", WorkId = "restaurant-helper", PlaceName = "Restaurant", RouteName = "RestaurantWork", RouteKey = "restaurant-work", TraitPath = "Assets/Data/StudentLife/Minigames/Traits/Trait_Taste.asset", TraitName = "Taste" },
            new DreamJob { Career = "EmergencyCare", CareerId = "emergency-care", FacilityId = "medical-clinic", LocationName = "MedicalClinic", WorkName = "ClinicHelper", WorkId = "clinic-helper", PlaceName = "Clinic", RouteName = "ClinicWork", RouteKey = "clinic-work", TraitPath = "Assets/Data/StudentLife/Minigames/Traits/Trait_Relief.asset", TraitName = "Relief" },
            new DreamJob { Career = "Interior", CareerId = "interior", FacilityId = "design-studio", LocationName = "DesignStudio", WorkName = "StudioHelper", WorkId = "studio-helper", PlaceName = "Studio", RouteName = "StudioWork", RouteKey = "studio-work", TraitPath = "Assets/Data/StudentLife/Minigames/Traits/Trait_Aesthetic.asset", TraitName = "Aesthetic" },
            new DreamJob { Career = "Soldier", CareerId = "soldier", FacilityId = "training-ground", LocationName = "TrainingGround", WorkName = "TrainingHelper", WorkId = "training-helper", PlaceName = "TrainingGround", RouteName = "TrainingWork", RouteKey = "training-work", TraitPath = "Assets/Data/StudentLife/CareerPractices/Traits/Trait_Discipline.asset", TraitName = "Discipline" },
            new DreamJob { Career = "Chef", CareerId = "chef", FacilityId = "restaurant", LocationName = "Restaurant", WorkName = "ErrandHelper", WorkId = "errand-helper", PlaceName = "Restaurant", RouteName = "ErrandWork", RouteKey = "errand-work", TraitPath = "Assets/Data/StudentLife/Minigames/Traits/Trait_Delicacy.asset", TraitName = "Delicacy", PlacementOffsetX = -2f },
            new DreamJob { Career = "EmergencyCare", CareerId = "emergency-care", FacilityId = "medical-clinic", LocationName = "MedicalClinic", WorkName = "SupplyHelper", WorkId = "supply-helper", PlaceName = "Clinic", RouteName = "SupplyWork", RouteKey = "supply-work", TraitPath = "Assets/Data/StudentLife/CareerPractices/Traits/Trait_Calm.asset", TraitName = "Calm", PlacementOffsetX = -2f },
            new DreamJob { Career = "Soldier", CareerId = "soldier", FacilityId = "training-ground", LocationName = "TrainingGround", WorkName = "FitnessHelper", WorkId = "fitness-helper", PlaceName = "TrainingGround", RouteName = "FitnessWork", RouteKey = "fitness-work", TraitPath = "Assets/Data/StudentLife/CareerPractices/Traits/Trait_Fitness.asset", TraitName = "Fitness", PlacementOffsetX = -2f },
        };
        private static AddressableAssetSettings s_addressables;

        [Serializable] private sealed class ImportManifest
        {
            public string RunDirectory;
            public string[] NewAssets;
            public string[] TouchedAssets;
            public AddressRecord[] Addressables;
        }
        [Serializable] private sealed class AddressRecord
        {
            public string Path;
            public string Guid;
            public string Address;
            public string Group;
        }

        [MenuItem("Rootborn/Student Life/Build Common Story Opening")]
        public static void Build()
        {
            Touched.Clear(); Created.Clear(); BackedUp.Clear();
            s_runDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "output/dream-job-20261104/builder-runs",
                DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff", System.Globalization.CultureInfo.InvariantCulture)));
            s_addressables = AddressableAssetSettingsDefaultObject.Settings;
            if (s_addressables == null || s_addressables.FindGroup("Data")?.GetSchema<BundledAssetGroupSchema>() == null
                || s_addressables.FindGroup("Tiles")?.GetSchema<BundledAssetGroupSchema>() == null)
                throw new InvalidOperationException("Existing local Data and Tiles Addressables groups are required before this import.");
            var photoSprite = Required<Sprite>("Assets/Art/HousePixels/Story31/story_town_photo.png");
            var deskSource = Required<FurnitureDefinition>("Assets/Data/Interiors/Furniture/Growth31/Furniture_Growth31_assembly_desk.asset");
            if (deskSource.TileParts.Count == 0) throw new InvalidOperationException("Furniture art must be populated before content import.");
            if (Mathf.Abs(photoSprite.pixelsPerUnit - 48f) > 0.01f) throw new InvalidOperationException("Approved town photo must keep its 48-PPU import.");
            s_registry = Required<GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset");
            if (new SerializedObject(s_registry).FindProperty("_defaultStudentCampaign") == null)
                throw new InvalidOperationException("Import the explicit default campaign registry field first.");
            var paid = Required<ActivityTimePolicyDefinition>("Assets/Data/StudentLife/Calendar/TimePolicy_PaidActivity.asset");
            var free = Required<ActivityTimePolicyDefinition>("Assets/Data/StudentLife/Calendar/TimePolicy_FreeInteraction.asset");
            var teacher = Required<NpcDefinition>("Assets/Data/NPCs/Npc_Teacher.asset");
            var guide = Required<NpcDefinition>("Assets/Data/NPCs/Npc_FirstGuide.asset");
            var library = Required<LocationDefinition>("Assets/Data/StudentLife/Locations/Location_Library.asset");
            var workshop = Required<LocationDefinition>("Assets/Data/StudentLife/Locations/Location_Workshop.asset");
            var storeWork = Required<PartTimeWorkDefinition>("Assets/Data/StudentLife/PartTimeWork/PartTimeWork_CornerStoreShift.asset");
            AddressRecord[] addresses = Array.Empty<AddressRecord>();
            try
            {
                var allowance = Asset<ItemDefinition>("Assets/Data/Items/Item_Allowance.asset");
                Edit(allowance, so => { Text(so, "_id", "item.allowance"); Text(so, "_displayKey", "item.allowance.name"); Number(so, "_maxStack", 9999); Number(so, "_category", (int)ItemCategory.Misc); });
                Register("_items", allowance);
    
                var locations = BuildFacilities(free);
                var work = BuildWork(workshop, storeWork, allowance, paid);
                BuildDreamJobs(allowance, paid);
                BuildDreamInterests();
                // Activity ids stay as they were: old saves' activity logs still count toward Day 3.
                var jobBooks = BuildDreamActivity("ReadJobBooks", "opening.clue.archive-door", "dream.activity.job-books", library, paid);
                var watchWork = BuildDreamActivity("WatchWorkshopWork", "opening.clue.repair-ledger", "dream.activity.watch-work", workshop, paid);
                AppendIdentityActivity("Library", jobBooks);
                AppendIdentityActivity("Workshop", watchWork);
    
                string storeLocationId = Required<LocationDefinition>("Assets/Data/StudentLife/Locations/Location_CornerStore.asset").Id;
                var photo = BuildPhotoBoard(photoSprite);
                var desk = CopyFurniture(deskSource, "StudyDesk", "story.study-desk", "나의 조립 책상", "unlock.furniture.story.study-desk");
                Edit(desk, so => Text(so, "_category", "desk"));
                var teacherObjective = Objective<TalkQuestObjective>("Teacher", "opening.objective.teacher");
                Edit(teacherObjective, so => Reference(so, "_targetNpc", teacher));
                var planObjective = Objective<DailyPlanSelectedQuestObjective>("DailyPlan", "dream.objective.plan");
                var visits = new QuestObjectiveBase[locations.Length];
                for (int i = 0; i < locations.Length; i++)
                {
                    var visit = Objective<LocationVisitQuestObjective>("Visit_" + locations[i].name, "opening.objective.visit." + i);
                    Edit(visit, so => Reference(so, "_targetLocation", locations[i]));
                    visits[i] = visit;
                }
                var workObjective = Objective<PartTimeWorkQuestObjective>("PaidWork", "dream.objective.work");
                // Preserve later facility mini-game work references added by world-content integration.
                Edit(workObjective, so =>
                {
                    Append(so, "_acceptedWorks", storeWork); Append(so, "_acceptedWorks", work);
                    foreach (var job in DreamJobs) Append(so, "_acceptedWorks", job.Work);
                });
                var jobBooksObjective = Objective<LocationActivityQuestObjective>("ReadJobBooks", "dream.objective.job-books");
                jobBooksObjective.ConfigureForRuntime("dream.objective.job-books", 1, jobBooks);
                EditorUtility.SetDirty(jobBooksObjective);
                var watchWorkObjective = Objective<LocationActivityQuestObjective>("WatchWorkshopWork", "dream.objective.watch-work");
                watchWorkObjective.ConfigureForRuntime("dream.objective.watch-work", 1, watchWork);
                EditorUtility.SetDirty(watchWorkObjective);
                var dreamChoice = Objective<AnyOfQuestObjective>("DreamAlternative", "dream.objective.choice");
                // Activities stay as alternatives: a dream picked before this step sends no new event, and school-free paths stay open.
                var selectDream = Objective<CareerInterestSelectedQuestObjective>("SelectDream", "dream.objective.select");
                Edit(dreamChoice, so => References(so, "_alternatives", new Object[] { selectDream, jobBooksObjective, watchWorkObjective }));
                var room = Objective<FurniturePlacedQuestObjective>("MyRoom", "dream.objective.room");
                // Career furniture is free to place, so the chain can always finish, even in a save already on this step.
                Edit(room, so =>
                {
                    References(so, "_acceptedFurniture", new Object[]
                    {
                        photo, desk,
                        Required<FurnitureDefinition>("Assets/Data/Interiors/Furniture/StudentLifePixels/Furniture48/Furniture_StudentLife_toy_kitchen.asset"),
                        Required<FurnitureDefinition>("Assets/Data/Interiors/Furniture/Growth31/Furniture_Growth31_first_aid_play_set.asset"),
                        Required<FurnitureDefinition>("Assets/Data/Interiors/Furniture/Growth31/Furniture_Growth31_assembly_desk.asset"),
                        Required<FurnitureDefinition>("Assets/Data/Interiors/Furniture/Activity31/Furniture_Activity31_training_tire.asset")
                    });
                    Text(so, "_requiredSurfaceId", "house");
                });
                // Sold at the corner store for allowance: two work shifts.
                foreach (var (forSale, nameKey) in new[] { (photo, "dream.furniture.photo-board.name"), (desk, "dream.furniture.study-desk.name") })
                    Edit(forSale, so => { Text(so, "_displayNameKey", nameKey); Reference(so, "_priceItem", allowance); Number(so, "_price", 10); Text(so, "_purchaseLocationId", storeLocationId); });
                var steps = new[]
                {
                    Step("BackToSchool", new QuestObjectiveBase[] { teacherObjective, planObjective }, descriptionKey: "dream.quest.back-to-school.description", completion: new QuestConditionBase[] { DayCondition(2, "dream.wait.next-day") }),
                    Step("AfterSchoolTour", visits),
                    Step("FirstPartTimeJob", new QuestObjectiveBase[] { workObjective }, "opening.quest.FirstPartTimeJob.name", "dream.quest.first-job.description", completion: new QuestConditionBase[] { DayCondition(3, "dream.wait.next-day") }),
                    Step("MyDream", new QuestObjectiveBase[] { dreamChoice }, "dream.quest.my-dream.name", "dream.quest.my-dream.description", new[] { "mainquest.DustySecrets" }),
                    Step("MyOwnSpace", new QuestObjectiveBase[] { room }, "opening.quest.MyOwnSpace.name", "dream.quest.my-own-space.description")
                };
                var chain = Asset<QuestChainDefinition>(Root + "QuestChain_SchoolOpening.asset");
                chain.ConfigureForTests("main-story.school-opening", "opening.chain.name", "dream.chain.description", string.Empty,
                    steps.Cast<ScriptableObject>().ToArray(), null, null, null, null, 100);
                Edit(chain, so => Reference(so, "_startNpc", guide));
                Register("_questChains", chain);
    
                BuildCampaign(teacher, storeWork, work, jobBooks, watchWork);
                AddNpcOpeningLine(guide, "opening.dialogue.guide");
                AddNpcOpeningLine(teacher, "dream.dialogue.teacher");
                AddNpcOpeningLine(Required<NpcDefinition>("Assets/Data/NPCs/Npc_Librarian.asset"), "dream.dialogue.librarian");
                AddNpcOpeningLine(Required<NpcDefinition>("Assets/Data/NPCs/Npc_WorkManager.asset"), "dream.dialogue.manager");
                addresses = RegisterAddressables();
                AssetDatabase.SaveAssets();
                Debug.Log("[ROOTBORN] Common opening data saved. Remaining player gates: visible facility targets/heads, real mini-game work, furniture purchase offers, photo-board Game View verification, Boot input journey.");
            }
            finally { WriteManifest(addresses); }
        }

        /// <summary>Adds one saved work point beside each career facility. Safe to run again: existing points are left alone.</summary>
        [MenuItem("Rootborn/Student Life/Place Dream Jobs In Town")]
        public static void PlaceDreamJobsInTown()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Town.unity", OpenSceneMode.Single);
            var points = Object.FindObjectsByType<PartTimeWorkInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var template = points.FirstOrDefault(x => x.Work != null && x.Work.Id == "work.workshop-helper");
            if (template == null) throw new InvalidOperationException("Town needs the saved workshop work point to copy.");
            // Check everything first, so a missing facility cannot leave only some jobs placed.
            var transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var pending = new List<(PartTimeWorkDefinition work, Transform anchor, float offsetX)>(DreamJobs.Length);
            foreach (var job in DreamJobs)
            {
                var work = Required<PartTimeWorkDefinition>("Assets/Data/StudentLife/PartTimeWork/PartTimeWork_" + job.WorkName + ".asset");
                if (points.Any(x => x.Work == work)) continue;
                string anchorName = "FacilityActivities_location_" + job.FacilityId.Replace('-', '_');
                var anchor = transforms.FirstOrDefault(x => x.name == anchorName);
                if (anchor == null) throw new InvalidOperationException("Town is missing " + anchorName);
                pending.Add((work, anchor, job.PlacementOffsetX));
            }
            if (pending.Count == 0) return;
            // Kept under their own root so the protected original Town objects stay untouched.
            var root = GameObject.Find("DreamJobWorkPoints");
            if (root == null) { root = new GameObject("DreamJobWorkPoints"); SceneManager.MoveGameObjectToScene(root, scene); }
            // Starting offsets only. Check every point in the Game View before keeping it.
            foreach (var (work, anchor, offsetX) in pending)
            {
                var copy = Object.Instantiate(template.gameObject, root.transform);
                copy.name = "PartTimeWork_" + work.Id.Replace('.', '_').Replace('-', '_');
                copy.transform.position = anchor.position + new Vector3(offsetX, 0f, 0f);
                var so = new SerializedObject(copy.GetComponent<PartTimeWorkInteractor>());
                Reference(so, "_work", work);
                so.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log("[ROOTBORN] Placed " + copy.name + " at " + copy.transform.position);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static LocationDefinition[] BuildFacilities(ActivityTimePolicyDefinition free)
        {
            string[] names = { "Restaurant", "MedicalClinic", "DesignStudio", "TrainingGround" };
            string[] ids = { "restaurant", "medical-clinic", "design-studio", "training-ground" };
            string[] careers = { "Chef", "EmergencyCare", "Interior", "Soldier" };
            // Initial distinct data positions; root verifies pathfinding, sprites and interaction overlap in Town.
            Vector2[] positions = { new Vector2(10f, -6f), new Vector2(12.5f, -6f), new Vector2(5f, -8f), new Vector2(7.5f, -8f) };
            var results = new LocationDefinition[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                var location = Asset<LocationDefinition>("Assets/Data/StudentLife/Locations/Location_" + names[i] + ".asset");
                // Id falls back to the asset name, including a newly created asset. Repair the earlier zero-position output; preserve reviewed nonzero map positions.
                bool alreadyPositioned = !Created.Contains(AssetDatabase.GetAssetPath(location)) && location.WorldPosition != Vector2.zero;
                var position = alreadyPositioned ? location.WorldPosition : positions[i];
                location.ConfigureForTests("location." + ids[i], "opening.facility." + ids[i] + ".name", "opening.facility." + ids[i] + ".description",
                    location.Category, "anchor." + ids[i], position, location.RelatedRoles.ToArray(), location.RelatedNpcs.ToArray(), location.Discoveries.ToArray(), location.VisitRules.ToArray());
                EditorUtility.SetDirty(location); Register("_locations", location);
                // Must be a class with its own script file. Unity cannot save a second class of a shared file (m_Script: 0).
                var hint = Asset<LocationActivityProgressDeltaOutcome>(Root + "Activities/Outcomes/CareerIntroduction_" + careers[i] + ".asset");
                hint.ConfigureCareerHintForTests(Required<CareerDefinition>("Assets/Data/StudentLife/CareerPractices/Careers/Career_" + careers[i] + ".asset"));
                EditorUtility.SetDirty(hint);
                Edit(hint, so => Field(so, "_logCareerHintEveryTime").boolValue = true);
                var introduction = Asset<LocationActivityDefinition>(Root + "Activities/LocationActivity_Introduce" + careers[i] + ".asset");
                introduction.ConfigureForTests("opening.introduce." + ids[i], "opening.facility." + ids[i] + ".introduction", location,
                    LocationGrowthRoute.Exploration, 0, 0, 0, 0, null, new LocationActivityOutcomeBase[] { hint });
                Edit(introduction, so => { Reference(so, "_timePolicy", free); Text(so, "_resultTextKey", "dream.facility." + ids[i] + ".result"); }); Register("_locationActivities", introduction);
                var identity = Asset<LocationIdentityDefinition>("Assets/Data/StudentLife/LocationIdentities/LocationIdentity_" + names[i] + ".asset");
                var activities = identity.AvailableActivities.Where(x => x != null).ToList();
                if (!activities.Contains(introduction)) activities.Add(introduction);
                identity.ConfigureForTests("location.identity." + ids[i], "opening.facility." + ids[i] + ".name", location,
                    "opening.facility." + ids[i] + ".description", activities.ToArray(), identity.Hints.ToArray(), 480, 1140);
                EditorUtility.SetDirty(identity); Register("_locationIdentities", identity);
                results[i] = location;
            }
            return results;
        }

        private static PartTimeWorkDefinition BuildWork(LocationDefinition workshop, PartTimeWorkDefinition store, ItemDefinition allowance, ActivityTimePolicyDefinition paid)
        {
            var workplace = Asset<WorkplaceDefinition>("Assets/Data/StudentLife/PartTimeWork/Workplace_Workshop.asset");
            var work = Asset<PartTimeWorkDefinition>("Assets/Data/StudentLife/PartTimeWork/PartTimeWork_WorkshopHelper.asset");
            var growth = Asset<WorkTraitDeltaOutcome>(Root + "Activities/Outcomes/Work_Responsibility.asset");
            growth.ConfigureForTests(Required<TraitDefinition>("Assets/Data/StudentLife/CareerPractices/Traits/Trait_Responsibility.asset"), 1);
            EditorUtility.SetDirty(growth);
            var works = workplace.AvailableWorks.Where(x => x != null).ToList(); if (!works.Contains(work)) works.Add(work);
            workplace.ConfigureForTests("workplace.workshop", "opening.workplace.workshop", workshop.Id,
                Required<NpcDefinition>("Assets/Data/NPCs/Npc_WorkManager.asset").Id, works.ToArray());
            EditorUtility.SetDirty(workplace); Register("_workplaces", workplace);
            work.ConfigureForTests("work.workshop-helper", "opening.work.workshop-helper", workplace, 30, 1, 1, 0,
                null, null, new WorkOutcomeBase[] { growth });
            Edit(work, so => { References(so, "_rewardItems", new Object[] { allowance }); Ints(so, "_rewardCounts", new[] { 5 }); Reference(so, "_timePolicy", paid); });
            // ConfigureForTests caches grants in memory. Reload removes its empty test cache before gameplay.
            var cached = typeof(PartTimeWorkDefinition).GetField("_configuredRewards", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            cached?.SetValue(work, null);
            Register("_partTimeWorks", work);
            // The legacy store workplace pointed at an unregistered location. Resolve to the actual SO graph while preserving a later valid map assignment.
            var storeLocation = Required<LocationDefinition>("Assets/Data/StudentLife/Locations/Location_CornerStore.asset");
            Register("_locations", storeLocation);
            if (store.Workplace == null) throw new InvalidOperationException("Corner store work needs its reviewed workplace.");
            if (!s_registry.Locations.Any(x => x != null && x.Id == store.Workplace.LocationId))
                Edit(store.Workplace, so => Text(so, "_locationId", storeLocation.Id));
            Register("_workplaces", store.Workplace);
            var existing = store.Rewards.ToList();
            if (!existing.Any(x => x.Item == allowance)) existing.Add(new InventoryGrant(allowance, 5));
            Edit(store, so => { References(so, "_rewardItems", existing.Select(x => (Object)x.Item).ToArray()); Ints(so, "_rewardCounts", existing.Select(x => x.Count).ToArray()); Reference(so, "_timePolicy", paid); });
            cached?.SetValue(store, null);
            return work;
        }

        private static void BuildDreamJobs(ItemDefinition allowance, ActivityTimePolicyDefinition paid)
        {
            const string Folder = "Assets/Data/StudentLife/PartTimeWork/";
            var cached = typeof(PartTimeWorkDefinition).GetField("_configuredRewards", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            foreach (var job in DreamJobs)
            {
                var growth = Asset<WorkTraitDeltaOutcome>(Folder + "Outcomes/WorkOutcome_" + job.WorkName + job.TraitName + ".asset");
                growth.ConfigureForTests(Required<TraitDefinition>(job.TraitPath), 1);
                EditorUtility.SetDirty(growth);
                var workplace = Asset<WorkplaceDefinition>(Folder + "Workplace_" + job.PlaceName + ".asset");
                var work = Asset<PartTimeWorkDefinition>(Folder + "PartTimeWork_" + job.WorkName + ".asset");
                // No facility head NPC exists yet, so the workplace names no NPC.
                var placeWorks = workplace.AvailableWorks.Where(x => x != null).ToList(); if (!placeWorks.Contains(work)) placeWorks.Add(work);
                workplace.ConfigureForTests("workplace." + job.FacilityId, "opening.facility." + job.FacilityId + ".name", "location." + job.FacilityId, string.Empty, placeWorks.ToArray());
                EditorUtility.SetDirty(workplace); Register("_workplaces", workplace);
                // Same size as the workshop helper: no design value exists for these jobs yet.
                work.ConfigureForTests("work." + job.WorkId, "dream.work." + job.WorkId, workplace, 30, 1, 1, 0, null, null, new WorkOutcomeBase[] { growth });
                var game = Required<Rootborn.Game.Minigames.MinigameDefinition>("Assets/Data/StudentLife/Minigames/Game_" + job.Career + "_Practice.asset");
                Edit(work, so => { Reference(so, "_minigame", game); References(so, "_rewardItems", new Object[] { allowance }); Ints(so, "_rewardCounts", new[] { 5 }); Reference(so, "_timePolicy", paid); });
                cached?.SetValue(work, null);
                Register("_partTimeWorks", work);
                job.Work = work;
            }
        }

        // The first success at each job counts once toward its career, so jobs alone can reach a career.
        private static void BuildJobHints()
        {
            var jobs = DreamJobs.Select(x => (x.Career, x.CareerId, x.WorkName, x.WorkId)).Append(("Interior", "interior", "WorkshopHelper", "workshop-helper"));
            foreach (var (career, careerId, workName, workId) in jobs)
            {
                string facility = "Assets/Data/Careers/Facilities/" + career + "/";
                var source = Asset<CareerResultLogHintSource>(facility + "Source_Job_" + workName + ".asset");
                source.ConfigureForTests("work." + workId); EditorUtility.SetDirty(source);
                var hint = Asset<CareerHintDefinition>(facility + "Hint_Job_" + workName + ".asset");
                hint.ConfigureForTests("hint." + careerId + ".job." + workId, "career." + careerId + ".insight",
                    Required<CareerCandidateDefinition>(facility + "Candidate_" + career + ".asset"), null, string.Empty, 1, new CareerHintSourceBase[] { source });
                EditorUtility.SetDirty(hint); Register("_careerHints", hint);
                Edit(hint, so => Field(so, "_appliesOnce").boolValue = true);
            }
        }

        private static void BuildDreamInterests()
        {
            const string Folder = "Assets/Data/Careers/Interests/";
            BuildJobHints();
            var teacher = Required<NpcDefinition>("Assets/Data/NPCs/Npc_Teacher.asset");
            var interests = new List<Object>(4);
            foreach (var job in DreamJobs.GroupBy(x => x.Career).Select(x => x.First()))
            {
                string facility = "Assets/Data/Careers/Facilities/" + job.Career + "/";
                var candidate = Required<CareerCandidateDefinition>(facility + "Candidate_" + job.Career + ".asset");
                var source = Asset<CareerResultLogHintSource>(facility + "Source_Introduction.asset");
                source.ConfigureForTests("opening.introduce." + job.FacilityId);
                EditorUtility.SetDirty(source);
                var hint = Asset<CareerHintDefinition>(facility + "Hint_Introduction.asset");
                hint.ConfigureForTests("hint." + job.CareerId + ".introduction", "career." + job.CareerId + ".insight", candidate, null, string.Empty, 1, new CareerHintSourceBase[] { source });
                EditorUtility.SetDirty(hint); Register("_careerHints", hint);
                Edit(hint, so => Field(so, "_appliesOnce").boolValue = true);
                var requirement = Asset<CareerInterestCandidateStateRequirement>(Folder + "Requirements/InterestRequirement_" + job.Career + "_CandidateRevealed.asset");
                requirement.ConfigureForTests(CareerCandidateState.Revealed); EditorUtility.SetDirty(requirement);
                var rule = Asset<CareerInterestDailyChangeLimitRule>(Folder + "Rules/InterestRule_" + job.Career + "_DailyChangeLimit.asset");
                // The dream can change any time. The rule stays only to treat re-picking the same dream as a no-op.
                rule.ConfigureForTests(99, true); EditorUtility.SetDirty(rule);
                var recommendation = Asset<CareerInterestStaticRecommendation>(Folder + "Recommendations/InterestRecommendation_" + job.Career + ".asset");
                recommendation.ConfigureForTests(new[] { "dream.interest." + job.CareerId + ".action" }); EditorUtility.SetDirty(recommendation);
                var reward = Asset<CareerInterestFocusReward>(Folder + "Rewards/InterestReward_" + job.Career + "_Focus.asset");
                reward.ConfigureForTests("interest." + job.CareerId + ":reward.focus", 1); EditorUtility.SetDirty(reward);
                var interest = Asset<CareerInterestDefinition>(Folder + "Interest_" + job.Career + ".asset");
                interest.ConfigureForTests("interest." + job.CareerId, "career." + job.CareerId, "dream.interest." + job.CareerId + ".description", candidate,
                    new[] { Required<LocationDefinition>("Assets/Data/StudentLife/Locations/Location_" + job.LocationName + ".asset") },
                    new ScriptableObject[] { Required<LocationActivityDefinition>(Root + "Activities/LocationActivity_Introduce" + job.Career + ".asset") },
                    new CareerInterestUnlockRequirementBase[] { requirement }, new CareerInterestSelectionRuleBase[] { rule },
                    new CareerInterestRecommendationBase[] { recommendation }, new CareerInterestRewardBase[] { reward });
                interest.ConfigureFollowUpForTests(teacher, "dream.teacher." + job.CareerId, "dream.teacher.changed");
                EditorUtility.SetDirty(interest);
                interests.Add(interest);
            }
            // The four dreams replace the old learning/technical/service/investigation interests.
            Edit(s_registry, so => References(so, "_careerInterests", interests.ToArray()));
        }

        private static LocationActivityDefinition BuildDreamActivity(string name, string id, string keyBase, LocationDefinition location, ActivityTimePolicyDefinition paid)
        {
            var observation = Asset<LocationActivityTraitDeltaOutcome>(Root + "Activities/Outcomes/Observation_" + name + ".asset");
            observation.ConfigureForTests(Required<TraitDefinition>("Assets/Data/StudentLife/CareerPractices/Traits/Trait_Observation.asset"), 1);
            EditorUtility.SetDirty(observation);
            var activity = Asset<LocationActivityDefinition>(Root + "Activities/LocationActivity_" + name + ".asset");
            activity.ConfigureForTests(id, keyBase + ".action", location,
                LocationGrowthRoute.Exploration, 20, 0, 1, 0, null, new LocationActivityOutcomeBase[] { observation });
            Edit(activity, so => { Reference(so, "_timePolicy", paid); Text(so, "_resultTextKey", keyBase + ".result"); }); Register("_locationActivities", activity);
            return activity;
        }

        private static void AppendIdentityActivity(string name, LocationActivityDefinition activity)
        {
            var identity = Required<LocationIdentityDefinition>("Assets/Data/StudentLife/LocationIdentities/LocationIdentity_" + name + ".asset");
            Edit(identity, so => Append(so, "_availableActivities", activity));
        }

        private static FurnitureDefinition CopyFurniture(FurnitureDefinition source, string name, string id, string displayName, string token)
        {
            var target = Asset<FurnitureDefinition>("Assets/Data/Interiors/Furniture/Story/Furniture_Story_" + name + ".asset");
            BeforeMutation(target);
            EditorUtility.CopySerialized(source, target);
            target.name = "Furniture_Story_" + name;
            Edit(target, so => { Text(so, "_stableId", id); Text(so, "_displayName", displayName); Text(so, "_unlockToken", token); Strings(so, "_allowedSurfaceIds", new[] { "house" }); });
            Register("_furnitureDefinitions", target);
            return target;
        }

        private static FurnitureDefinition BuildPhotoBoard(Sprite sprite)
        {
            // Source inspected: 40x32 pixels at 48 PPU, fits a single house cell without rescaling.
            var tile = Asset<Tile>("Assets/Data/Interiors/Furniture/Story/Tiles/Story_PhotoBoard_Tile.asset");
            tile.sprite = sprite; tile.colliderType = Tile.ColliderType.None; EditorUtility.SetDirty(tile);
            var target = Asset<FurnitureDefinition>("Assets/Data/Interiors/Furniture/Story/Furniture_Story_PhotoBoard.asset");
            var configured = FurnitureDefinition.CreateForTests("story.photo-board", "기록 사진 게시판", "Decor", new[] { "house" },
                new[] { new FurnitureTilePart(Vector2Int.zero, tile) }, new[] { Vector2Int.zero }, false, FurniturePlacementPreference.Any);
            BeforeMutation(target);
            EditorUtility.CopySerialized(configured, target); Object.DestroyImmediate(configured);
            target.name = "Furniture_Story_PhotoBoard";
            Edit(target, so => Text(so, "_unlockToken", "unlock.furniture.story.photo-board"));
            Register("_furnitureDefinitions", target); return target;
        }

        private static T Objective<T>(string name, string key) where T : QuestObjectiveBase
        {
            var objective = Asset<T>(Root + "Objectives/Objective_" + name + ".asset");
            objective.ConfigureForRuntime(key, 1); EditorUtility.SetDirty(objective); Register("_questObjectives", objective); return objective;
        }
        private static DayAtLeastCondition DayCondition(int day, string summaryKey)
        {
            var condition = Asset<DayAtLeastCondition>(Root + "Conditions/Condition_DayAtLeast_" + day + ".asset");
            condition.ConfigureForTests(day, summaryKey); EditorUtility.SetDirty(condition);
            Register("_questConditions", condition);
            return condition;
        }

        private static QuestStepDefinition Step(string name, QuestObjectiveBase[] objectives, string nameKey = null, string descriptionKey = null, string[] legacyStepIds = null, QuestConditionBase[] completion = null, QuestRewardBase[] rewards = null)
        {
            var step = Asset<QuestStepDefinition>(Root + "Steps/MainQuest_" + name + ".asset");
            step.ConfigureForTests("mainquest." + name, nameKey ?? "opening.quest." + name + ".name", descriptionKey ?? "opening.quest." + name + ".description",
                objectives, completion, null, rewards, null); EditorUtility.SetDirty(step);
            if (legacyStepIds != null) Edit(step, so => Strings(so, "_legacyStepIds", legacyStepIds));
            return step;
        }

        private static void BuildCampaign(NpcDefinition teacher, PartTimeWorkDefinition store, PartTimeWorkDefinition workshop, LocationActivityDefinition jobBooks, LocationActivityDefinition watchWork)
        {
            var days = new CampaignDayDefinition[3];
            var dayRoutes = new[]
            {
                new[] { Route("Teacher", "npc:" + teacher.Id + ":met"), Route("SelfStudy", Required<LocationActivityDefinition>("Assets/Data/StudentLife/LocationIdentities/Activities/LocationActivity_Library_HardBook.asset").Id) },
                new[] { Route("StoreWork", store.Id), Route("WorkshopWork", workshop.Id) }.Concat(DreamJobs.Select(job => Route(job.RouteName, job.Work.Id, job.RouteName, "dream.route." + job.RouteKey))).ToArray(),
                // Saved route ids stay Archive and Ledger; only the assets and their text changed.
                new[] { Route("ReadJobBooks", jobBooks.Id, "Archive", "dream.route.job-books"), Route("WatchWorkshopWork", watchWork.Id, "Ledger", "dream.route.watch-work") }
            };
            string[] dayNames = { "opening.day.1.name", "opening.day.2.name", "dream.day.3.name" };
            string[] dayDirections = { "dream.day.1.direction", "dream.day.2.direction", "dream.day.3.direction" };
            string[] dayNext = { "opening.day.1.next", "dream.day.2.next", "dream.day.3.next" };
            for (int i = 0; i < days.Length; i++)
            {
                days[i] = Asset<CampaignDayDefinition>(Root + "Campaigns/Day_" + (i + 1) + ".asset");
                days[i].ConfigureForTests("opening.day." + (i + 1), i + 1, dayNames[i], dayDirections[i], dayNext[i], dayRoutes[i]);
                EditorUtility.SetDirty(days[i]); Register("_campaignDays", days[i]);
            }
            var campaign = Asset<CampaignDefinition>(Root + "Campaigns/Campaign_SchoolOpeningThreeDays.asset");
            campaign.ConfigureForTests("campaign.school-opening-three-days", "opening.campaign.name", "dream.campaign.description", days, null);
            EditorUtility.SetDirty(campaign); Register("_campaigns", campaign);
            Edit(s_registry, so => Reference(so, "_defaultStudentCampaign", campaign));
        }
        private static CampaignRouteDefinition Route(string name, string prefix, string stableId = null, string keyBase = null)
        {
            stableId = stableId ?? name; keyBase = keyBase ?? "opening.route." + name;
            var objective = Asset<CampaignActivityLogObjective>(Root + "Campaigns/Objective_" + name + ".asset");
            objective.ConfigureForTests("opening.campaign.objective." + stableId, prefix, 1, false);
            Edit(objective, so => Text(so, "_displayNameKey", keyBase)); Register("_campaignObjectives", objective);
            var route = Asset<CampaignRouteDefinition>(Root + "Campaigns/Route_" + name + ".asset");
            route.ConfigureForTests("opening.route." + stableId, keyBase, keyBase + ".description", new CampaignObjectiveBase[] { objective });
            EditorUtility.SetDirty(route); Register("_campaignRoutes", route); return route;
        }

        private static void AddNpcOpeningLine(NpcDefinition npc, string key)
        {
            var dialogues = new List<DialogueDefinition>(npc.StageDialogues ?? Array.Empty<DialogueDefinition>());
            if (npc.DefaultDialogue != null) dialogues.Add(npc.DefaultDialogue);
            foreach (var dialogue in dialogues.Where(x => x != null).Distinct()) PrefixLine(dialogue, key);
            foreach (var schedule in s_registry.NpcSchedules.Where(x => x != null && x.Npc == npc))
            {
                BeforeMutation(schedule);
                var so = new SerializedObject(schedule); var entries = so.FindProperty("_entries");
                for (int i = 0; i < entries.arraySize; i++)
                {
                    var entry = entries.GetArrayElementAtIndex(i);
                    var dialogue = entry.FindPropertyRelative("_dialogue").objectReferenceValue as DialogueDefinition;
                    if (dialogue == null)
                    {
                        dialogue = Asset<DialogueDefinition>(Root + "Dialogue/" + npc.name + "_Schedule_" + i + ".asset");
                        string original = entry.FindPropertyRelative("_dialogueKey").stringValue;
                        dialogue.ConfigureForTests(string.IsNullOrEmpty(original) ? new[] { key } : new[] { key, original }, null, null);
                        EditorUtility.SetDirty(dialogue); Register("_dialogues", dialogue);
                        entry.FindPropertyRelative("_dialogue").objectReferenceValue = dialogue;
                    }
                    else PrefixLine(dialogue, key);
                }
                so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(schedule);
            }
        }
        private static void PrefixLine(DialogueDefinition dialogue, string key)
        {
            var lines = dialogue.LineKeys.Where(x => x != key && !RetiredOpeningLines.Contains(x)).ToList(); lines.Insert(0, key);
            Edit(dialogue, so => Strings(so, "_lineKeys", lines.ToArray()));
        }

        private static T Required<T>(string path) where T : Object
        {
            var asset = !string.IsNullOrEmpty(path) ? AssetDatabase.LoadAssetAtPath<T>(path) : null;
            if (asset == null) throw new InvalidOperationException("Required reviewed asset missing: " + path); return asset;
        }
        private static T Asset<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path); if (existing != null) { BeforeMutation(existing); return existing; }
            int slash = path.LastIndexOf('/'); EnsureFolder(path.Substring(0, slash));
            var asset = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset, path);
            Created.Add(path); Touched.Add(path); return asset;
        }
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return; int slash = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, slash)); AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
        private static void Register(string array, Object value) => Edit(s_registry, so => Append(so, array, value));
        private static void Edit(Object target, Action<SerializedObject> action) { BeforeMutation(target); var so = new SerializedObject(target); action(so); so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(target); }

        private static void BeforeMutation(Object target)
        {
            string path = AssetDatabase.GetAssetPath(target);
            if (string.IsNullOrEmpty(path)) return;
            Touched.Add(path);
            if (Created.Contains(path) || !BackedUp.Add(path)) return;
            string backup = Path.Combine(s_runDirectory, "before", path);
            Directory.CreateDirectory(Path.GetDirectoryName(backup));
            if (File.Exists(path)) File.Copy(path, backup, false);
            if (File.Exists(path + ".meta")) File.Copy(path + ".meta", backup + ".meta", false);
        }

        private static AddressRecord[] RegisterAddressables()
        {
            // Snapshot the list: backing up settings/groups adds those files to Touched too.
            var paths = Touched.Where(path => path.StartsWith("Assets/Data/", StringComparison.Ordinal)).OrderBy(path => path, StringComparer.Ordinal).ToArray();
            BeforeMutation(s_addressables);
            var data = s_addressables.FindGroup("Data"); var tiles = s_addressables.FindGroup("Tiles");
            foreach (var group in new[] { data, tiles })
            {
                BeforeMutation(group);
                var schema = group.GetSchema<BundledAssetGroupSchema>(); BeforeMutation(schema);
                schema.BuildPath.SetVariableByName(s_addressables, AddressableAssetSettings.kLocalBuildPath);
                schema.LoadPath.SetVariableByName(s_addressables, AddressableAssetSettings.kLocalLoadPath);
                EditorUtility.SetDirty(schema);
            }
            var records = new List<AddressRecord>(paths.Length);
            foreach (string path in paths)
            {
                var asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (!(asset is ScriptableObject)) continue;
                string guid = AssetDatabase.AssetPathToGUID(path);
                var previous = s_addressables.FindAssetEntry(guid);
                if (previous != null && previous.parentGroup != null) BeforeMutation(previous.parentGroup);
                string previousAddress = previous != null ? previous.address : null;
                var group = asset is TileBase ? tiles : data;
                var entry = s_addressables.CreateOrMoveEntry(guid, group);
                if (string.IsNullOrEmpty(previousAddress)) entry.address = AddressFor(path, asset is TileBase);
                entry.SetLabel("MainStory", true, true);
                EditorUtility.SetDirty(group);
                records.Add(new AddressRecord { Path = path, Guid = guid, Address = entry.address, Group = group.Name });
            }
            EditorUtility.SetDirty(s_addressables);
            return records.ToArray();
        }
        private static string AddressFor(string path, bool tile)
        {
            string relative = path.Substring("Assets/Data/".Length);
            relative = relative.Substring(0, relative.LastIndexOf('.')).Replace('_', '-').Replace(' ', '-').ToLowerInvariant();
            return (tile ? "tiles/" : "data/") + relative;
        }
        private static void WriteManifest(AddressRecord[] addresses)
        {
            Directory.CreateDirectory(s_runDirectory);
            var manifest = new ImportManifest { RunDirectory = s_runDirectory,
                NewAssets = Created.OrderBy(x => x, StringComparer.Ordinal).ToArray(),
                TouchedAssets = Touched.OrderBy(x => x, StringComparer.Ordinal).ToArray(), Addressables = addresses };
            string json = JsonUtility.ToJson(manifest, true);
            File.WriteAllText(Path.Combine(s_runDirectory, "assets-manifest.json"), json, new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(s_runDirectory), "latest-assets-manifest.json"), json, new UTF8Encoding(false));
        }
        private static SerializedProperty Field(SerializedObject so, string name) => so.FindProperty(name) ?? throw new InvalidOperationException(so.targetObject.name + " missing " + name);
        private static void Text(SerializedObject so, string name, string value) => Field(so, name).stringValue = value;
        private static void Number(SerializedObject so, string name, int value) => Field(so, name).intValue = value;
        private static void Reference(SerializedObject so, string name, Object value) => Field(so, name).objectReferenceValue = value;
        private static void References(SerializedObject so, string name, Object[] values) { var array = Field(so, name); array.arraySize = values.Length; for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i]; }
        private static void Strings(SerializedObject so, string name, string[] values) { var array = Field(so, name); array.arraySize = values.Length; for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).stringValue = values[i]; }
        private static void Ints(SerializedObject so, string name, int[] values) { var array = Field(so, name); array.arraySize = values.Length; for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).intValue = values[i]; }
        private static void Append(SerializedObject so, string name, Object value)
        {
            var array = Field(so, name); for (int i = 0; i < array.arraySize; i++) if (array.GetArrayElementAtIndex(i).objectReferenceValue == value) return;
            int index = array.arraySize; array.arraySize++; array.GetArrayElementAtIndex(index).objectReferenceValue = value;
        }
    }
}
