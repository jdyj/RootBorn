using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Rootborn.Game.Common;
using Rootborn.Game.Dialogue;
using Rootborn.Game.Placement;
using Rootborn.Game.Quests;
using Rootborn.Game.Quests.Objectives;
using Rootborn.Game.StudentLife;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace Rootborn.Editor.StudentLife
{
    /// <summary>Author the common opening only. Does not choose the town's past or place world objects.</summary>
    public static class MainStoryOpeningDataBuilder
    {
        private const string Root = "Assets/Data/StudentLife/MainStory/";
        private static GameDataRegistry s_registry;
        private static readonly HashSet<string> Touched = new HashSet<string>(StringComparer.Ordinal);
        private static readonly HashSet<string> Created = new HashSet<string>(StringComparer.Ordinal);
        private static readonly HashSet<string> BackedUp = new HashSet<string>(StringComparer.Ordinal);
        private static string s_runDirectory;
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
            s_runDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "output/full-design-20261027/growth-audit/authoring",
                DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff", System.Globalization.CultureInfo.InvariantCulture)));
            s_addressables = AddressableAssetSettingsDefaultObject.Settings;
            if (s_addressables == null || s_addressables.FindGroup("Data")?.GetSchema<BundledAssetGroupSchema>() == null
                || s_addressables.FindGroup("Tiles")?.GetSchema<BundledAssetGroupSchema>() == null)
                throw new InvalidOperationException("Existing local Data and Tiles Addressables groups are required before this import.");
            var photoSprite = Required<Sprite>("Assets/Art/HousePixels/Story31/story_town_photo.png");
            var deskSource = Required<FurnitureDefinition>("Assets/Data/Interiors/Furniture/Furniture_Desk_Basic.asset");
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
                var archive = BuildClue("InspectArchiveDoor", "archive-door", library, paid);
                var ledger = BuildClue("ReadRepairLedger", "repair-ledger", workshop, paid);
                AppendIdentityActivity("Library", archive);
                AppendIdentityActivity("Workshop", ledger);
    
                var photo = BuildPhotoBoard(photoSprite);
                var desk = CopyFurniture(deskSource, "StudyDesk", "story.study-desk", "나의 조립 책상", "unlock.furniture.story.study-desk");
                var teacherObjective = Objective<TalkQuestObjective>("Teacher", "opening.objective.teacher");
                Edit(teacherObjective, so => Reference(so, "_targetNpc", teacher));
                var planObjective = Objective<DailyPlanSelectedQuestObjective>("DailyPlan", "opening.objective.plan");
                var visits = new QuestObjectiveBase[locations.Length];
                for (int i = 0; i < locations.Length; i++)
                {
                    var visit = Objective<LocationVisitQuestObjective>("Visit_" + locations[i].name, "opening.objective.visit." + i);
                    Edit(visit, so => Reference(so, "_targetLocation", locations[i]));
                    visits[i] = visit;
                }
                var workObjective = Objective<PartTimeWorkQuestObjective>("PaidWork", "opening.objective.work");
                // Preserve later facility mini-game work references added by world-content integration.
                Edit(workObjective, so => { Append(so, "_acceptedWorks", storeWork); Append(so, "_acceptedWorks", work); });
                var archiveObjective = Objective<LocationActivityQuestObjective>("ArchiveDoor", "opening.objective.archive");
                archiveObjective.ConfigureForRuntime("opening.objective.archive", 1, archive);
                EditorUtility.SetDirty(archiveObjective);
                var ledgerObjective = Objective<LocationActivityQuestObjective>("RepairLedger", "opening.objective.ledger");
                ledgerObjective.ConfigureForRuntime("opening.objective.ledger", 1, ledger);
                EditorUtility.SetDirty(ledgerObjective);
                var clues = Objective<AnyOfQuestObjective>("ClueAlternative", "opening.objective.clue-choice");
                Edit(clues, so => References(so, "_alternatives", new Object[] { archiveObjective, ledgerObjective }));
                var room = Objective<FurniturePlacedQuestObjective>("MyRoom", "opening.objective.room");
                Edit(room, so => { References(so, "_acceptedFurniture", new Object[] { photo, desk }); Text(so, "_requiredSurfaceId", "house"); });
                var steps = new[]
                {
                    Step("BackToSchool", new QuestObjectiveBase[] { teacherObjective, planObjective }),
                    Step("AfterSchoolTour", visits),
                    Step("FirstPartTimeJob", new QuestObjectiveBase[] { workObjective }),
                    Step("DustySecrets", new QuestObjectiveBase[] { clues }),
                    Step("MyOwnSpace", new QuestObjectiveBase[] { room })
                };
                var chain = Asset<QuestChainDefinition>(Root + "QuestChain_SchoolOpening.asset");
                chain.ConfigureForTests("main-story.school-opening", "opening.chain.name", "opening.chain.description", string.Empty,
                    steps.Cast<ScriptableObject>().ToArray(), null, null, null, null, 100);
                Edit(chain, so => Reference(so, "_startNpc", guide));
                Register("_questChains", chain);
    
                BuildCampaign(teacher, storeWork, work, archive, ledger);
                AddNpcOpeningLine(guide, "opening.dialogue.guide");
                AddNpcOpeningLine(teacher, "opening.dialogue.teacher");
                AddNpcOpeningLine(Required<NpcDefinition>("Assets/Data/NPCs/Npc_Librarian.asset"), "opening.dialogue.librarian");
                AddNpcOpeningLine(Required<NpcDefinition>("Assets/Data/NPCs/Npc_WorkManager.asset"), "opening.dialogue.manager");
                addresses = RegisterAddressables();
                AssetDatabase.SaveAssets();
                Debug.Log("[ROOTBORN] Common opening data saved. Remaining player gates: visible facility targets/heads, real mini-game work, furniture purchase offers, photo-board Game View verification, Boot input journey.");
            }
            finally { WriteManifest(addresses); }
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
                var hint = Asset<LocationActivityCareerHintOutcome>(Root + "Activities/Outcomes/CareerIntroduction_" + careers[i] + ".asset");
                hint.ConfigureForTests(Required<CareerDefinition>("Assets/Data/StudentLife/CareerPractices/Careers/Career_" + careers[i] + ".asset"));
                EditorUtility.SetDirty(hint);
                var introduction = Asset<LocationActivityDefinition>(Root + "Activities/LocationActivity_Introduce" + careers[i] + ".asset");
                introduction.ConfigureForTests("opening.introduce." + ids[i], "opening.facility." + ids[i] + ".introduction", location,
                    LocationGrowthRoute.Exploration, 0, 0, 0, 0, null, new LocationActivityOutcomeBase[] { hint });
                Edit(introduction, so => Reference(so, "_timePolicy", free)); Register("_locationActivities", introduction);
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

        private static LocationActivityDefinition BuildClue(string name, string id, LocationDefinition location, ActivityTimePolicyDefinition paid)
        {
            var observation = Asset<LocationActivityTraitDeltaOutcome>(Root + "Activities/Outcomes/Observation_" + name + ".asset");
            observation.ConfigureForTests(Required<TraitDefinition>("Assets/Data/StudentLife/CareerPractices/Traits/Trait_Observation.asset"), 1);
            EditorUtility.SetDirty(observation);
            var activity = Asset<LocationActivityDefinition>(Root + "Activities/LocationActivity_" + name + ".asset");
            activity.ConfigureForTests("opening.clue." + id, "opening.clue." + id + ".action", location,
                LocationGrowthRoute.Exploration, 20, 0, 1, 0, null, new LocationActivityOutcomeBase[] { observation });
            Edit(activity, so => { Reference(so, "_timePolicy", paid); Text(so, "_resultTextKey", "opening.clue." + id + ".result"); }); Register("_locationActivities", activity);
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
        private static QuestStepDefinition Step(string name, QuestObjectiveBase[] objectives)
        {
            var step = Asset<QuestStepDefinition>(Root + "Steps/MainQuest_" + name + ".asset");
            step.ConfigureForTests("mainquest." + name, "opening.quest." + name + ".name", "opening.quest." + name + ".description",
                objectives, null, null, null, null); EditorUtility.SetDirty(step); return step;
        }

        private static void BuildCampaign(NpcDefinition teacher, PartTimeWorkDefinition store, PartTimeWorkDefinition workshop, LocationActivityDefinition archive, LocationActivityDefinition ledger)
        {
            var days = new CampaignDayDefinition[3];
            var dayRoutes = new[]
            {
                new[] { Route("Teacher", "npc:" + teacher.Id + ":met"), Route("SelfStudy", Required<LocationActivityDefinition>("Assets/Data/StudentLife/LocationIdentities/Activities/LocationActivity_Library_HardBook.asset").Id) },
                new[] { Route("StoreWork", store.Id), Route("WorkshopWork", workshop.Id) },
                new[] { Route("Archive", archive.Id), Route("Ledger", ledger.Id) }
            };
            for (int i = 0; i < days.Length; i++)
            {
                days[i] = Asset<CampaignDayDefinition>(Root + "Campaigns/Day_" + (i + 1) + ".asset");
                days[i].ConfigureForTests("opening.day." + (i + 1), i + 1, "opening.day." + (i + 1) + ".name",
                    "opening.day." + (i + 1) + ".direction", "opening.day." + (i + 1) + ".next", dayRoutes[i]);
                EditorUtility.SetDirty(days[i]); Register("_campaignDays", days[i]);
            }
            var campaign = Asset<CampaignDefinition>(Root + "Campaigns/Campaign_SchoolOpeningThreeDays.asset");
            campaign.ConfigureForTests("campaign.school-opening-three-days", "opening.campaign.name", "opening.campaign.description", days, null);
            EditorUtility.SetDirty(campaign); Register("_campaigns", campaign);
            Edit(s_registry, so => Reference(so, "_defaultStudentCampaign", campaign));
        }
        private static CampaignRouteDefinition Route(string name, string prefix)
        {
            var objective = Asset<CampaignActivityLogObjective>(Root + "Campaigns/Objective_" + name + ".asset");
            objective.ConfigureForTests("opening.campaign.objective." + name, prefix, 1, false);
            Edit(objective, so => Text(so, "_displayNameKey", "opening.route." + name)); Register("_campaignObjectives", objective);
            var route = Asset<CampaignRouteDefinition>(Root + "Campaigns/Route_" + name + ".asset");
            route.ConfigureForTests("opening.route." + name, "opening.route." + name, "opening.route." + name + ".description", new CampaignObjectiveBase[] { objective });
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
            var lines = dialogue.LineKeys.Where(x => x != key).ToList(); lines.Insert(0, key);
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
