using System;
using System.Linq;
using NUnit.Framework;
using Rootborn.Game.Careers;
using Rootborn.Game.Common;
using Rootborn.Game.Minigames;
using Rootborn.Game.StudentLife;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
namespace Rootborn.Tests.EditMode.Minigames
{
    public sealed class CareerMinigameAssetTests
    {
        private static readonly string[] Careers={"Chef","EmergencyCare","Interior","Soldier"};
        private GameDataRegistry Registry=>AssetDatabase.LoadAssetAtPath<GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset");
        private T Asset<T>(string path)where T:UnityEngine.Object{var a=AssetDatabase.LoadAssetAtPath<T>(path);Assert.That(a,Is.Not.Null,path);return a;}
        private MinigameDefinition Game(string name)=>Asset<MinigameDefinition>("Assets/Data/StudentLife/Minigames/Game_"+name+".asset");
        private UnityEngine.Object Ref(UnityEngine.Object obj,string field){var p=new SerializedObject(obj).FindProperty(field);Assert.That(p,Is.Not.Null,field);return p.objectReferenceValue;}
        [Test]public void FourPracticeGames_ArePlayableAndWiredToRegisteredPractices()
        {
            foreach(var career in Careers){var game=Game(career+"_Practice");Assert.That(game.IsValid,Is.True,career);var p=Registry.CareerPractices.Single(x=>x.CareerHint.name=="Career_"+career);Assert.That(Ref(p,"_minigame"),Is.SameAs(game));}
        }
        [Test]public void CareerMechanics_MatchDesignAndEmergencyHighLevelAddsTravel()
        {
            Func<MinigameDefinition,Type[]> types=g=>{var p=new SerializedObject(g).FindProperty("_challenges");return Enumerable.Range(0,p.arraySize).Select(i=>p.GetArrayElementAtIndex(i).objectReferenceValue.GetType()).ToArray();};
            CollectionAssert.Contains(types(Game("Chef_Practice")),typeof(SequenceChallengeDefinition));CollectionAssert.Contains(types(Game("Chef_Practice")),typeof(TimingChallengeDefinition));
            CollectionAssert.Contains(types(Game("EmergencyCare_Practice")),typeof(ChoiceChallengeDefinition));CollectionAssert.Contains(types(Game("Interior_Practice")),typeof(PlacementChallengeDefinition));CollectionAssert.Contains(types(Game("Soldier_Practice")),typeof(TargetDodgeChallengeDefinition));
            CollectionAssert.Contains(types(Game("EmergencyCare_Work2")),typeof(TargetDodgeChallengeDefinition));
        }
        [Test]public void FourFacilities_HaveThreeValidLevelsAndAuthoredWorkAtEveryLevel()
        {
            Assert.That(Registry.FacilityDefinitions.Length,Is.EqualTo(4));
            foreach(var facility in Registry.FacilityDefinitions){Assert.That(facility.IsValid,Is.True,facility.name);for(int level=1;level<=3;level++){var work=facility.Works.Single(w=>w.MinimumFacilityLevel==level);Assert.That(work.IsValid,Is.True);Assert.That(Ref(work,"_minigame"),Is.Not.Null);Assert.That(((MinigameDefinition)Ref(work,"_minigame")).IsValid,Is.True);Assert.That(work.MinimumSuccessScore,Is.EqualTo(60));}Assert.That(facility.GetLevel(3).CompletesRoute,Is.True);}
        }
        [Test]public void UpgradesConsumeSharedAllowanceAndMaterials_AndCreateWorldChanges()
        {
            Assert.That(Registry.FacilityDefinitions.Length,Is.EqualTo(4));var allowance=Registry.Items.Single(x=>x.Id=="item.allowance");
            foreach(var f in Registry.FacilityDefinitions)for(int level=2;level<=3;level++){var l=f.GetLevel(level);Assert.That(l.BuildCosts().Length,Is.GreaterThanOrEqualTo(2));Assert.That(l.BuildCosts().Any(x=>x.Item==allowance),Is.True);Assert.That(l.WorldFlags.Count,Is.GreaterThan(0));Assert.That(l.RewardPercent,Is.GreaterThan(f.GetLevel(level-1).RewardPercent));}
        }
        [Test]public void AutoSkipRequiresMasteryRepeatedSuccessAndFacilityEquipmentCoverage()
        {
            Assert.That(Registry.FacilityWorks.Length,Is.EqualTo(12));foreach(var w in Registry.FacilityWorks){var p=new SerializedObject(w).FindProperty("_autoSkipRequirements");AutoSkipRequirementCoverage coverage=AutoSkipRequirementCoverage.None;for(int i=0;i<p.arraySize;i++)coverage|=((AutoSkipRequirementBase)p.GetArrayElementAtIndex(i).objectReferenceValue).Coverage;Assert.That(coverage,Is.EqualTo(AutoSkipRequirementCoverage.All));}
        }
        [Test]public void PracticeSuccessesRevealAnEligibleCareerWithoutSchoolAttendance()
        {
            Assert.That(Registry.FacilityDefinitions.Length,Is.EqualTo(4));foreach(var f in Registry.FacilityDefinitions){var student=new StudentLifeProgress("asset-test","p",100,100);var candidates=new CareerCandidateProgress("asset-test","p");var practice=Registry.CareerPractices.Single(p=>p.CareerHint==f.Career);for(int i=0;i<3;i++){practice.Activity.ApplyEffects(student);student.RecordActivityCompleted(practice.Activity.Id,Array.Empty<string>());candidates.ApplyFirstMatchingHint(Registry.CareerHints,student,"practice"+i,new[]{practice.Activity.Id});}Assert.That(f.Candidate.GetState(candidates),Is.EqualTo(CareerCandidateState.ReadyForChoice));Assert.That(student.IsCareerHintUnlocked(f.Career),Is.True);}
        }
        [Test]public void RegistryAndAddressables_ContainEveryMinigameChallengeAndBonus()
        {
            var so=new SerializedObject(Registry);foreach(string field in new[]{"_minigames","_minigameChallenges","_minigameBonuses"}){var p=so.FindProperty(field);Assert.That(p,Is.Not.Null,field);Assert.That(p.arraySize,Is.GreaterThan(0));var seen=new System.Collections.Generic.HashSet<UnityEngine.Object>();for(int i=0;i<p.arraySize;i++){var obj=p.GetArrayElementAtIndex(i).objectReferenceValue;Assert.That(obj,Is.Not.Null);Assert.That(seen.Add(obj),Is.True);var entry=AddressableAssetSettingsDefaultObject.Settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(obj)));Assert.That(entry,Is.Not.Null,obj.name);Assert.That(entry.parentGroup.Name,Is.EqualTo("Data"));}}
        }
        [Test]public void IntroJobsRequireAuthoredMinigames_AndAllNewTitlesAreBilingual()
        {
            foreach(string suffix in new[]{"CornerStoreShift","WorkshopHelper"}){var w=Asset<PartTimeWorkDefinition>("Assets/Data/StudentLife/PartTimeWork/PartTimeWork_"+suffix+".asset");Assert.That(Ref(w,"_minigame"),Is.Not.Null);}
            foreach(var career in Careers){var g=Game(career+"_Practice");foreach(string language in new[]{"ko","en"})Assert.That(Registry.TextCatalog.Get(g.DisplayNameKey,language),Is.Not.EqualTo(g.DisplayNameKey));}
        }
    }
}
