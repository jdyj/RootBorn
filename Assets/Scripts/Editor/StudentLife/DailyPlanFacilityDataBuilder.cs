using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Rootborn.Game.Common;
using Rootborn.Game.StudentLife;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rootborn.Editor.StudentLife
{
    /// <summary>Bind diary plans to real authored minigame definitions; preserve existing alternatives.</summary>
    public static class DailyPlanFacilityDataBuilder
    {
        private const string Root = "Assets/Data/StudentLife/DailyPlans/FacilityPracticeRoutes/";
        private const string DataRoot = "Assets/Data/StudentLife/";
        private static readonly HashSet<string> Created = new HashSet<string>(StringComparer.Ordinal);
        private static readonly HashSet<string> Touched = new HashSet<string>(StringComparer.Ordinal);
        private static readonly HashSet<string> BackedUp = new HashSet<string>(StringComparer.Ordinal);
        private static string s_runDirectory;
        [Serializable] private sealed class AssetRecord { public string Path; public string Id; public string Guid; public string Address; public string Group; }
        [Serializable] private sealed class ImportManifest { public string RunDirectory; public string[] NewAssets; public string[] TouchedAssets; public AssetRecord[] Routes; }
        private sealed class Plan
        {
            public string Path, Id;
            public LocationDefinition Location;
            public CareerPracticeDefinition Practice;
            public PartTimeWorkDefinition Work;
            public string Address => "data/" + Path.Substring("Assets/Data/".Length, Path.Length - "Assets/Data/".Length - ".asset".Length).Replace('_', '-').ToLowerInvariant();
        }

        [MenuItem("Rootborn/Student Life/Build Facility Daily Plans")]
        public static void Build()
        {
            Created.Clear(); Touched.Clear(); BackedUp.Clear();
            s_runDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "output/full-design-20261027/growth-audit/daily-plan-authoring",
                DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff", System.Globalization.CultureInfo.InvariantCulture)));
            var registry = Required<GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset");
            var set = Required<DailyPlanRouteSetDefinition>(DataRoot + "DailyPlans/DefaultDailyPlanRouteSet.asset");
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var group = settings != null ? settings.FindGroup("Data") : null;
            var schema = group != null ? group.GetSchema<BundledAssetGroupSchema>() : null;
            if (schema == null) throw new InvalidOperationException("Existing local Data Addressables group is required.");
            var plans = new[]
            {
                Practice("Chef", "chef", "Restaurant"), Practice("EmergencyCare", "emergency-care", "MedicalClinic"),
                Practice("Interior", "interior", "DesignStudio"), Practice("Soldier", "soldier", "TrainingGround"),
                Work("CornerStore", "CornerStoreShift", "corner-store", registry), Work("Workshop", "WorkshopHelper", "workshop-helper", registry)
            };
            Validate(plans, registry, settings);
            var records = new List<AssetRecord>();
            try
            {
                foreach (var plan in plans)
                {
                    var route = Asset<DailyPlanRouteDefinition>(plan.Path);
                    Edit(route, so =>
                    {
                        Field(so, "_id").stringValue = plan.Id;
                        Field(so, "_displayNameKey").stringValue = plan.Id + ".name";
                        Field(so, "_targetLocation").objectReferenceValue = plan.Location;
                        Field(so, "_growthRoute").intValue = (int)(plan.Work != null ? LocationGrowthRoute.Work : LocationGrowthRoute.Exploration);
                        References(so, "_matchingActivities", Array.Empty<Object>());
                        References(so, "_matchingPractices", plan.Practice != null ? new Object[] { plan.Practice } : Array.Empty<Object>());
                        References(so, "_matchingWorks", plan.Work != null ? new Object[] { plan.Work } : Array.Empty<Object>());
                        References(so, "_requirements", Array.Empty<Object>());
                        References(so, "_outcomes", Array.Empty<Object>());
                        Field(so, "_objectiveTitleKey").stringValue = plan.Id + ".name";
                        Field(so, "_objectiveDetailKey").stringValue = plan.Id + ".detail";
                        Field(so, "_nextHintKey").stringValue = plan.Id + ".next";
                    });
                    Edit(set, so => Append(so, "_routes", route));
                    BeforeMutation(settings); BeforeMutation(group); BeforeMutation(schema);
                    schema.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
                    schema.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
                    EditorUtility.SetDirty(schema);
                    string guid = AssetDatabase.AssetPathToGUID(plan.Path);
                    var previous = settings.FindAssetEntry(guid);
                    if (previous != null && previous.parentGroup != null) BeforeMutation(previous.parentGroup);
                    var entry = settings.CreateOrMoveEntry(guid, group);
                    entry.address = plan.Address;
                    entry.SetLabel("DailyPlan", true, true);
                    EditorUtility.SetDirty(group); EditorUtility.SetDirty(settings);
                    records.Add(new AssetRecord { Path = plan.Path, Id = plan.Id, Guid = guid, Address = entry.address, Group = group.Name });
                }
                Edit(registry, so => Append(so, "_dailyPlanRouteSets", set));
                AssetDatabase.SaveAssets();
                Debug.Log("[ROOTBORN] Six typed facility daily plans authored; existing route alternatives preserved.");
            }
            finally
            {
                Directory.CreateDirectory(s_runDirectory);
                var manifest = new ImportManifest { RunDirectory = s_runDirectory, NewAssets = Created.OrderBy(x => x, StringComparer.Ordinal).ToArray(),
                    TouchedAssets = Touched.OrderBy(x => x, StringComparer.Ordinal).ToArray(), Routes = records.ToArray() };
                string json = JsonUtility.ToJson(manifest, true);
                File.WriteAllText(Path.Combine(s_runDirectory, "assets-manifest.json"), json, new UTF8Encoding(false));
                File.WriteAllText(Path.Combine(Path.GetDirectoryName(s_runDirectory), "latest-assets-manifest.json"), json, new UTF8Encoding(false));
            }
        }

        private static Plan Practice(string career, string id, string location) => new Plan
        {
            Path = Root + "DailyPlan_Practice_" + career + ".asset", Id = "daily.practice." + id,
            Practice = Required<CareerPracticeDefinition>(DataRoot + "CareerPractices/PracticeS/Practice_" + career + ".asset"),
            Location = Required<LocationDefinition>(DataRoot + "Locations/Location_" + location + ".asset")
        };
        private static Plan Work(string file, string workName, string id, GameDataRegistry registry)
        {
            var work = Required<PartTimeWorkDefinition>(DataRoot + "PartTimeWork/PartTimeWork_" + workName + ".asset");
            var location = work.Workplace != null ? registry.Locations.FirstOrDefault(x => x != null && x.Id == work.Workplace.LocationId) : null;
            if (location == null) throw new InvalidOperationException("Workplace must resolve to a registered location: " + workName);
            return new Plan { Path = Root + "DailyPlan_Work_" + file + ".asset", Id = "daily.work." + id, Work = work, Location = location };
        }
        private static void Validate(Plan[] plans, GameDataRegistry registry, AddressableAssetSettings settings)
        {
            var registeredRoutes = registry.DailyPlanRouteSets.Where(x => x != null).SelectMany(x => x.Routes).Where(x => x != null).ToArray();
            foreach (var plan in plans)
            {
                if (!registry.Locations.Contains(plan.Location)) throw new InvalidOperationException("Unregistered location: " + plan.Id);
                if (plan.Practice != null && (plan.Practice.Minigame == null || !registry.CareerPractices.Contains(plan.Practice)))
                    throw new InvalidOperationException("Practice must have a registered real minigame: " + plan.Id);
                if (plan.Work != null && (plan.Work.Minigame == null || !registry.PartTimeWorks.Contains(plan.Work) || !plan.Work.Rewards.Any(x => x.Count > 0)))
                    throw new InvalidOperationException("Work must have a real minigame and payout: " + plan.Id);
                var existing = AssetDatabase.LoadMainAssetAtPath(plan.Path);
                if (existing != null && !(existing is DailyPlanRouteDefinition)) throw new InvalidOperationException("Asset type conflict: " + plan.Path);
                if (registeredRoutes.Any(x => x.Id == plan.Id && AssetDatabase.GetAssetPath(x) != plan.Path))
                    throw new InvalidOperationException("Duplicate route ID: " + plan.Id);
                string guid = AssetDatabase.AssetPathToGUID(plan.Path);
                foreach (var group in settings.groups)
                    if (group != null && group.entries.Any(x => x.address == plan.Address && x.guid != guid))
                        throw new InvalidOperationException("Address already belongs to another asset: " + plan.Address);
            }
        }
        private static T Required<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path)
            ?? throw new InvalidOperationException("Required authored asset missing: " + path);
        private static T Asset<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) { BeforeMutation(existing); return existing; }
            EnsureFolder(path.Substring(0, path.LastIndexOf('/')));
            var value = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(value, path);
            Created.Add(path); Touched.Add(path); return value;
        }
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/'); EnsureFolder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
        private static void BeforeMutation(Object asset)
        {
            string path = AssetDatabase.GetAssetPath(asset); if (string.IsNullOrEmpty(path)) return;
            Touched.Add(path); if (Created.Contains(path) || !BackedUp.Add(path)) return;
            string backup = Path.Combine(s_runDirectory, "before", path); Directory.CreateDirectory(Path.GetDirectoryName(backup));
            if (File.Exists(path)) File.Copy(path, backup, false);
            if (File.Exists(path + ".meta")) File.Copy(path + ".meta", backup + ".meta", false);
        }
        private static void Edit(Object asset, Action<SerializedObject> apply)
        {
            BeforeMutation(asset); var so = new SerializedObject(asset); apply(so); so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(asset);
        }
        private static SerializedProperty Field(SerializedObject so, string field) => so.FindProperty(field)
            ?? throw new InvalidOperationException(so.targetObject.name + " missing " + field);
        private static void References(SerializedObject so, string field, Object[] values)
        {
            var array = Field(so, field); array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
        private static void Append(SerializedObject so, string field, Object value)
        {
            var array = Field(so, field);
            for (int i = 0; i < array.arraySize; i++) if (array.GetArrayElementAtIndex(i).objectReferenceValue == value) return;
            int index = array.arraySize; array.arraySize++; array.GetArrayElementAtIndex(index).objectReferenceValue = value;
        }
    }
}