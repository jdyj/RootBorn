using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Rootborn.Game.Careers;
using Rootborn.Game.Common;
using Rootborn.Game.Minigames;
using Rootborn.Game.StudentLife;
using Rootborn.Game.WorldState;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using Object=UnityEngine.Object;
namespace Rootborn.Editor.Minigames
{
    public static class CareerMinigameDataBuilder
    {
        private const string Root="Assets/Data/StudentLife/Minigames/";
        private static readonly List<Object> Touched=new List<Object>();
        private static readonly HashSet<string> BackedUp=new HashSet<string>();
        private static string _backup;
        private static GameDataRegistry _registry;
        private static ItemDefinition _allowance,_materials;
        [MenuItem("Rootborn/Careers/Build Playable Career Data")]
        public static void Build()
        {
            _backup="output/full-design-20261027/backups/career-data-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff")+"/";
            Touched.Clear();BackedUp.Clear();_registry=AssetDatabase.LoadAssetAtPath<GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset");
            if(_registry==null)throw new InvalidOperationException("Missing registry");
            var settings=AddressableAssetSettingsDefaultObject.Settings;
            if(settings==null||settings.FindGroup("Data")==null)throw new InvalidOperationException("Local Data Addressables group is required");
            Backup(AssetDatabase.GetAssetPath(settings));Backup(AssetDatabase.GetAssetPath(settings.FindGroup("Data")));
            Touch(_registry);_allowance=Load<ItemDefinition>("Assets/Data/Items/Item_Allowance.asset");
            _materials=Asset<ItemDefinition>("Assets/Data/Items/Item_FacilityMaterials.asset");
            Set(_materials,"_id","item.facility-materials");Set(_materials,"_displayKey","item.facility-materials.name");Set(_materials,"_maxStack",9999);
            var taste=Trait("Taste");var delicate=Trait("Delicacy");var relief=Trait("Relief");var aesthetic=Trait("Aesthetic");
            var calm=Load<TraitDefinition>("Assets/Data/StudentLife/CareerPractices/Traits/Trait_Calm.asset");
            var focus=Load<TraitDefinition>("Assets/Data/StudentLife/CareerPractices/Traits/Trait_Focus.asset");
            var fitness=Load<TraitDefinition>("Assets/Data/StudentLife/CareerPractices/Traits/Trait_Fitness.asset");
            BuildRoute("Chef","chef","Restaurant",new[]{Bonus("TasteWarning",taste,MinigameBonusTarget.HintCount,.1f,2),Bonus("DelicateTiming",delicate,MinigameBonusTarget.TimingWindow,.008f,.2f)},new[]{taste,delicate},level=>new ChallengeDefinitionBase[]{Sequence("Chef"+level,"minigame.chef.sequence",new[]{"minigame.ingredient.rice","minigame.ingredient.vegetable","minigame.ingredient.egg","minigame.ingredient.sauce"},level==1?new[]{0,1,2}:level==2?new[]{0,2,1,3}:new[]{0,1,3,2,1},3.5f,35),Timing("Chef"+level,"minigame.chef.timing",Enumerable.Range(1,3+level).Select(i=>i*1.25f).ToArray(),.26f-level*.035f)});
            BuildRoute("EmergencyCare","emergency-care","MedicalClinic",new[]{Bonus("ReliefHints",relief,MinigameBonusTarget.HintCount,.1f,2),Bonus("CalmTime",calm,MinigameBonusTarget.TimeLimit,.5f,12)},new[]{relief},level=>
            {
                var challenges=new List<ChallengeDefinitionBase>{Choice("Care"+level,"minigame.care.instructions",new[]{"minigame.tool.bandage","minigame.tool.thermometer","minigame.tool.water"},new[]{"minigame.care.scrape","minigame.care.temperature","minigame.care.rest"},new[]{0,1,2},35-level*3),Sequence("CareSequence"+level,"minigame.care.sequence",new[]{"minigame.care.check","minigame.care.call","minigame.care.help"},new[]{0,1,2},3,25)};
                if(level>=2)challenges.Add(Target("Travel"+level,"minigame.travel.instructions",System.Array.Empty<Vector2>(),new[]{0,1,2,1,0,2},1.8f,0));return challenges.ToArray();
            });
            BuildRoute("Interior","interior","DesignStudio",new[]{Bonus("AestheticHarmony",aesthetic,MinigameBonusTarget.Harmony,.5f,15)},new[]{aesthetic},level=>new ChallengeDefinitionBase[]{Placement("Interior"+level,"minigame.interior.instructions",new Vector2Int(5,4),level<3?new[]{new Vector2Int(2,1),new Vector2Int(1,2),new Vector2Int(1,1)}:new[]{new Vector2Int(3,1),new Vector2Int(1,2),new Vector2Int(2,1),new Vector2Int(1,1)},new[]{new Vector2Int(2,0)},55-level*5)});
            BuildRoute("Soldier","soldier","TrainingGround",new[]{Bonus("FitnessTime",fitness,MinigameBonusTarget.TimeLimit,.35f,10),Bonus("FocusedAim",focus,MinigameBonusTarget.Steadiness,.04f,1)},System.Array.Empty<TraitDefinition>(),level=>new ChallengeDefinitionBase[]{Target("Training"+level,"minigame.training.instructions",new[]{new Vector2(.25f,.35f),new Vector2(.7f,.65f),new Vector2(.5f,.3f),new Vector2(.2f,.7f)},new[]{0,1,2,0,2,1},2.4f-level*.25f,.025f+level*.01f)});
            var storeGame=Game("CornerStore_Intro",new[]{Sequence("StoreIntro","minigame.store.instructions",new[]{"minigame.goods.bread","minigame.goods.milk","minigame.goods.fruit"},new[]{0,1,2},4,35)},System.Array.Empty<MinigameBonusRuleBase>());
            var workshopGame=Game("Workshop_Intro",new[]{Placement("WorkshopIntro","minigame.workshop.instructions",new Vector2Int(4,3),new[]{new Vector2Int(2,1),new Vector2Int(1,2)},new[]{new Vector2Int(0,0)},45)},System.Array.Empty<MinigameBonusRuleBase>());
            Set(Load<PartTimeWorkDefinition>("Assets/Data/StudentLife/PartTimeWork/PartTimeWork_CornerStoreShift.asset"),"_minigame",storeGame);
            Set(Load<PartTimeWorkDefinition>("Assets/Data/StudentLife/PartTimeWork/PartTimeWork_WorkshopHelper.asset"),"_minigame",workshopGame);
            AppendRegistry("_items",new Object[]{_materials});AppendRegistry("_studentLifeTraits",Touched.OfType<TraitDefinition>());
            AppendRegistry("_minigames",Touched.OfType<MinigameDefinition>());AppendRegistry("_minigameChallenges",Touched.OfType<ChallengeDefinitionBase>());AppendRegistry("_minigameBonuses",Touched.OfType<MinigameBonusRuleBase>());
            AppendRegistry("_facilityDefinitions",Touched.OfType<FacilityDefinition>());AppendRegistry("_facilityWorks",Touched.OfType<FacilityWorkDefinition>());AppendRegistry("_facilityLevels",Touched.OfType<FacilityLevelDefinition>());
            AppendRegistry("_facilityTransitionRequirements",Touched.OfType<FacilityTransitionRequirementBase>());AppendRegistry("_autoSkipRequirements",Touched.OfType<AutoSkipRequirementBase>());
            AppendRegistry("_careerCandidates",Touched.OfType<CareerCandidateDefinition>());AppendRegistry("_careerHints",Touched.OfType<CareerHintDefinition>());AppendRegistry("_worldStateFlags",Touched.OfType<WorldStateFlagDefinition>());
            settings.AddLabel("CareerGameplay");
            foreach(var obj in Touched.Distinct())
            {
                var path=AssetDatabase.GetAssetPath(obj);if(string.IsNullOrEmpty(path)||!path.StartsWith("Assets/Data/",StringComparison.Ordinal))continue;
                var guid=AssetDatabase.AssetPathToGUID(path);var entry=settings.FindAssetEntry(guid);
                if(entry==null){entry=settings.CreateOrMoveEntry(guid,settings.FindGroup("Data"));entry.address="data/"+path.Substring("Assets/Data/".Length).Replace(".asset","").ToLowerInvariant();}
                entry.SetLabel("CareerGameplay",true);
            }
            EditorUtility.SetDirty(settings);AssetDatabase.SaveAssets();Directory.CreateDirectory(_backup);
            File.WriteAllLines(_backup+"touched-assets.txt",Touched.Select(AssetDatabase.GetAssetPath).Distinct());
        }
        private static void BuildRoute(string suffix,string id,string locationSuffix,MinigameBonusRuleBase[] bonuses,TraitDefinition[] extraTraits,Func<int,ChallengeDefinitionBase[]> challenges)
        {
            string folder="Assets/Data/Careers/Facilities/"+suffix+"/";
            var practice=Load<CareerPracticeDefinition>("Assets/Data/StudentLife/CareerPractices/PracticeS/Practice_"+suffix+".asset");
            var career=practice.CareerHint;var location=Load<LocationDefinition>("Assets/Data/StudentLife/Locations/Location_"+locationSuffix+".asset");
            var candidate=Asset<CareerCandidateDefinition>(folder+"Candidate_"+suffix+".asset");Set(candidate,"_id","candidate."+id);Set(candidate,"_displayNameKey","career."+id);Set(candidate,"_descriptionKey","career."+id+".description");Set(candidate,"_revealedHintThreshold",1);Set(candidate,"_deepeningHintThreshold",2);Set(candidate,"_readyHintThreshold",3);Refs(candidate,"_relatedLocations",location);Refs(candidate,"_relatedActivities",practice.Activity);
            var source=Asset<CareerResultLogHintSource>(folder+"Source_Practice.asset");Set(source,"_requiredText",practice.Activity.Id);
            var hint=Asset<CareerHintDefinition>(folder+"Hint_Practice.asset");Set(hint,"_id","hint."+id+".practice");Set(hint,"_displayNameKey","career."+id+".insight");Set(hint,"_candidate",candidate);Set(hint,"_understandingDelta",1);Refs(hint,"_sources",source);
            var facility=Asset<FacilityDefinition>(folder+"Facility_"+suffix+".asset");Set(facility,"_id","facility."+id);Set(facility,"_displayNameKey","facility."+id+".name");Set(facility,"_career",career);Set(facility,"_candidate",candidate);Set(facility,"_location",location);
            var hintRule=Asset<CareerHintTransitionRequirement>(folder+"Transition_Hint.asset");var candidateRule=Asset<CandidateReadyTransitionRequirement>(folder+"Transition_Candidate.asset");var dayRule=Asset<MinimumDayTransitionRequirement>(folder+"Transition_Day.asset");Set(dayRule,"_minimumDay",4);Refs(facility,"_transitionRequirements",hintRule,candidateRule,dayRule);
            var mastery=Asset<MasteryAutoSkipRequirement>(folder+"AutoSkip_Mastery.asset");var successes=Asset<SuccessCountAutoSkipRequirement>(folder+"AutoSkip_Successes.asset");Set(successes,"_minimumSuccesses",5);var equipment=Asset<FacilityEquipmentAutoSkipRequirement>(folder+"AutoSkip_Facility.asset");Set(equipment,"_minimumLevel",2);
            var levels=new List<Object>();var works=new List<Object>();
            for(int i=1;i<=3;i++)
            {
                var level=Asset<FacilityLevelDefinition>(folder+"Level_"+i+".asset");Set(level,"_id","facility."+id+".level"+i);Set(level,"_displayNameKey","facility."+id+".level"+i);Set(level,"_level",i);Set(level,"_rewardPercent",100+(i-1)*50);Set(level,"_completesRoute",i==3);
                if(i>1){Refs(level,"_costItems",_allowance,_materials);Ints(level,"_costCounts",i==2?100:300,i==2?4:10);var flag=Asset<WorldStateFlagDefinition>(folder+"World_Level"+i+".asset");Set(flag,"_id","world."+id+".level"+i);Set(flag,"_displayNameKey","facility."+id+".level"+i);Set(flag,"_relatedLocation",location);Set(flag,"_scope",0);Refs(level,"_worldFlags",flag);}
                levels.Add(level);
                var game=Game(suffix+"_Work"+i,challenges(i),bonuses);
                var work=Asset<FacilityWorkDefinition>(folder+"Work_"+i+".asset");Set(work,"_id","work."+id+".level"+i);Set(work,"_displayNameKey","work."+id+".level"+i);Set(work,"_facility",facility);Set(work,"_minimumFacilityLevel",i);Set(work,"_timePointCost",1);Set(work,"_maximumMastery",100);Set(work,"_masteryPerSuccess",20);Set(work,"_minimumSuccessScore",60);Set(work,"_reputationPerSuccess",i*2);Set(work,"_minigame",game);Refs(work,"_rewardItems",_allowance,_materials);Ints(work,"_maximumRewardCounts",20+i*10,2+i);Refs(work,"_autoSkipRequirements",mastery,successes,equipment);works.Add(work);
            }
            Refs(facility,"_levels",levels.ToArray());Refs(facility,"_works",works.ToArray());
            Set(practice,"_minigame",Game(suffix+"_Practice",challenges(1),bonuses));
            foreach(var trait in extraTraits){var effect=Asset<TraitDeltaActivityEffect>(folder+"Effect_"+trait.name+".asset");Set(effect,"_trait",trait);Set(effect,"_delta",2);Append(practice.Activity,"_effects",new[]{effect});}
        }
        private static TraitDefinition Trait(string suffix){var t=Asset<TraitDefinition>(Root+"Traits/Trait_"+suffix+".asset");Set(t,"_id","trait."+suffix.ToLowerInvariant());Set(t,"_displayNameKey","trait."+suffix.ToLowerInvariant());return t;}
        private static MinigameBonusRuleBase Bonus(string name,TraitDefinition trait,MinigameBonusTarget target,float perPoint,float cap){var b=Asset<GrowthMinigameBonusRule>(Root+"Bonuses/"+name+".asset");Set(b,"_trait",trait);Set(b,"_target",(int)target);Set(b,"_amountPerPoint",perPoint);Set(b,"_maximumBonus",cap);return b;}
        private static MinigameDefinition Game(string name,ChallengeDefinitionBase[] stages,MinigameBonusRuleBase[] bonuses){var g=Asset<MinigameDefinition>(Root+"Game_"+name+".asset");Set(g,"_id","minigame."+name.ToLowerInvariant());Set(g,"_displayNameKey","minigame.name."+name.ToLowerInvariant());Set(g,"_successScore",60);Refs(g,"_challenges",stages);Refs(g,"_bonuses",bonuses);return g;}
        private static T Challenge<T>(string name,string instruction,float duration)where T:ChallengeDefinitionBase{var c=Asset<T>(Root+"Challenges/"+name+".asset");Set(c,"_instructionKey",instruction);Set(c,"_timeLimitSeconds",duration);Set(c,"_mistakeAllowance",6);return c;}
        private static SequenceChallengeDefinition Sequence(string name,string key,string[] symbols,int[] sequence,float reveal,float duration){var c=Challenge<SequenceChallengeDefinition>(name,key,duration);Strings(c,"_symbols",symbols);Ints(c,"_answers",sequence);Set(c,"_revealSeconds",reveal);return c;}
        private static ChoiceChallengeDefinition Choice(string name,string key,string[] symbols,string[] prompts,int[] answers,float duration){var c=Challenge<ChoiceChallengeDefinition>(name,key,duration);Strings(c,"_symbols",symbols);Strings(c,"_prompts",prompts);Ints(c,"_answers",answers);return c;}
        private static TimingChallengeDefinition Timing(string name,string key,float[] beats,float window){var c=Challenge<TimingChallengeDefinition>(name+"Timing",key,beats.Last()+1);Floats(c,"_beatTimes",beats);Set(c,"_windowSeconds",window);return c;}
        private static PlacementChallengeDefinition Placement(string name,string key,Vector2Int size,Vector2Int[] pieces,Vector2Int[] blocked,float duration){var c=Challenge<PlacementChallengeDefinition>(name,key,duration);Set(c,"_boardSize",size);Vectors(c,"_pieceSizes",pieces);Vectors(c,"_blockedCells",blocked);return c;}
        private static TargetDodgeChallengeDefinition Target(string name,string key,Vector2[] targets,int[] lanes,float interval,float wobble){var c=Challenge<TargetDodgeChallengeDefinition>(name,key,lanes.Length*interval+5);Array(c,"_targets",targets.Length,(p,i)=>p.vector2Value=targets[i]);Ints(c,"_obstacleLanes",lanes);Set(c,"_obstacleIntervalSeconds",interval);Set(c,"_targetRadius",.09f);Set(c,"_targetWobble",wobble);Set(c,"_laneCount",3);return c;}
        private static T Load<T>(string path)where T:Object{var obj=AssetDatabase.LoadAssetAtPath<T>(path);if(obj==null)throw new InvalidOperationException("Missing "+path);Touch(obj);return obj;}
        private static T Asset<T>(string path)where T:ScriptableObject{Backup(path);Folder(Path.GetDirectoryName(path).Replace('\\','/'));var obj=AssetDatabase.LoadAssetAtPath<T>(path);if(obj==null){obj=ScriptableObject.CreateInstance<T>();AssetDatabase.CreateAsset(obj,path);}Touch(obj);return obj;}
        private static void Touch(Object obj){if(!Touched.Contains(obj)){Backup(AssetDatabase.GetAssetPath(obj));Touched.Add(obj);}}
        private static void Backup(string path){if(string.IsNullOrEmpty(path)||!BackedUp.Add(path))return;foreach(var p in new[]{path,path+".meta"})if(File.Exists(p)){var dest=_backup+p;Directory.CreateDirectory(Path.GetDirectoryName(dest));File.Copy(p,dest,false);}}
        private static void Folder(string path){if(AssetDatabase.IsValidFolder(path))return;var parent=Path.GetDirectoryName(path).Replace('\\','/');Folder(parent);AssetDatabase.CreateFolder(parent,Path.GetFileName(path));}
        private static void Set(Object obj,string name,object value){Touch(obj);var so=new SerializedObject(obj);var p=so.FindProperty(name);if(p==null)throw new InvalidOperationException(obj.name+" missing "+name);if(value is int i)p.intValue=i;else if(value is float f)p.floatValue=f;else if(value is bool b)p.boolValue=b;else if(value is string s)p.stringValue=s;else if(value is Vector2Int v)p.vector2IntValue=v;else p.objectReferenceValue=(Object)value;so.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(obj);}
        private static void Array(Object obj,string name,int count,Action<SerializedProperty,int> assign){Touch(obj);var so=new SerializedObject(obj);var p=so.FindProperty(name);if(p==null)throw new InvalidOperationException(obj.name+" missing "+name);p.arraySize=count;for(int i=0;i<count;i++)assign(p.GetArrayElementAtIndex(i),i);so.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(obj);}
        private static void Refs(Object obj,string name,params Object[] values)=>Array(obj,name,values.Length,(p,i)=>p.objectReferenceValue=values[i]);
        private static void Ints(Object obj,string name,params int[] values)=>Array(obj,name,values.Length,(p,i)=>p.intValue=values[i]);
        private static void Floats(Object obj,string name,float[] values)=>Array(obj,name,values.Length,(p,i)=>p.floatValue=values[i]);
        private static void Strings(Object obj,string name,string[] values)=>Array(obj,name,values.Length,(p,i)=>p.stringValue=values[i]);
        private static void Vectors(Object obj,string name,Vector2Int[] values)=>Array(obj,name,values.Length,(p,i)=>p.vector2IntValue=values[i]);
        private static void AppendRegistry(string field,IEnumerable<Object> values)=>Append(_registry,field,values);
        private static void Append(Object obj,string field,IEnumerable<Object> values){var so=new SerializedObject(obj);var p=so.FindProperty(field);if(p==null)throw new InvalidOperationException("Missing registry array "+field);var list=new List<Object>();for(int i=0;i<p.arraySize;i++){var v=p.GetArrayElementAtIndex(i).objectReferenceValue;if(v!=null&&!list.Contains(v))list.Add(v);}foreach(var v in values)if(v!=null&&!list.Contains(v))list.Add(v);Refs(obj,field,list.ToArray());}
    }
}
