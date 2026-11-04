using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.StudentLife;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
namespace Rootborn.Tests.EditMode.Minigames
{
 public sealed class MinigameSessionTests
 {
  private readonly List<Object> _assets=new List<Object>();
  [TearDown]public void TearDown(){foreach(var a in _assets)Object.DestroyImmediate(a);_assets.Clear();}
  [Test]public void Sequence_PreviewRejectsInput_ThenCorrectOrderCompletes(){var d=Sequence();var s=Session(d);Assert.That(Call(s,"Choose",0),Is.False);Call(s,"AdvanceTime",2f);Assert.That(Call(s,"Choose",0),Is.True);Call(s,"Choose",1);Call(s,"Choose",2);Assert.That(Get(s,"IsComplete"),Is.True);Assert.That(Get(s,"Score"),Is.EqualTo(100));}
  [Test]public void Sequence_WrongOrderCannotEarnPerfectScore(){var d=Sequence();var s=Session(d);Call(s,"AdvanceTime",2f);Call(s,"Choose",2);Call(s,"Choose",0);Call(s,"Choose",1);Call(s,"Choose",2);Assert.That((int)Get(s,"Score"),Is.LessThan(100));}
  [Test]public void Choices_RecordWrongAnswersAndScoreActualSuccesses(){var d=New("ChoiceChallengeDefinition");Ints(d,"_answers",0,1);Strings(d,"_prompts","p0","p1");Strings(d,"_symbols","a","b");var s=Session(d);Call(s,"Choose",1);Call(s,"Choose",1);Assert.That(Get(s,"IsComplete"),Is.True);Assert.That(Get(s,"Score"),Is.EqualTo(50));}
  [Test]public void CompletedSession_RejectsFurtherInputsAndPreservesScore(){var d=New("ChoiceChallengeDefinition");Ints(d,"_answers",0);Strings(d,"_prompts","p");Strings(d,"_symbols","a","b");var s=Session(d);Call(s,"Choose",0);Assert.That(Call(s,"Choose",1),Is.False);Call(s,"AdvanceTime",100f);Assert.That(Get(s,"Score"),Is.EqualTo(100));}
  [Test]public void CancelledSession_NeverBecomesRewardable(){var s=Session(Sequence());Call(s,"Cancel");Call(s,"AdvanceTime",5f);Assert.That(Call(s,"Choose",0),Is.False);Assert.That(Get(s,"IsCancelled"),Is.True);Assert.That(Get(s,"Succeeded"),Is.False);}
  [Test]public void TimeoutWithoutInput_IsFailure(){var d=Sequence();Set(d,"_timeLimitSeconds",5f);var s=Session(d);Call(s,"AdvanceTime",10f);Assert.That(Get(s,"IsComplete"),Is.True);Assert.That(Get(s,"Succeeded"),Is.False);Assert.That(Get(s,"Score"),Is.EqualTo(0));}
  [Test]public void Timing_OnBeatsCompletesAtMaximumScore(){var d=Timing();var s=Session(d);Call(s,"AdvanceTime",1f);Call(s,"Tap");Call(s,"AdvanceTime",1f);Call(s,"Tap");Call(s,"AdvanceTime",1f);Assert.That(Get(s,"IsComplete"),Is.True);Assert.That(Get(s,"Score"),Is.EqualTo(100));}
  [Test]public void Timing_OutsideWindowDoesNotCount(){var s=Session(Timing());Call(s,"AdvanceTime",.25f);Call(s,"Tap");Call(s,"AdvanceTime",3f);Assert.That(Get(s,"Succeeded"),Is.False);Assert.That(Get(s,"Score"),Is.EqualTo(0));}
  [Test]public void Timing_RepeatedTapCannotCreditSameBeatTwice(){var s=Session(Timing());Call(s,"AdvanceTime",1f);Call(s,"Tap");Assert.That(Call(s,"Tap"),Is.False);Call(s,"AdvanceTime",3f);Assert.That((int)Get(s,"Score"),Is.LessThanOrEqualTo(50));}
  [Test]public void Placement_BoundsAndOverlapAreRejectedWithoutChangingBoard(){var d=Placement();var s=Session(d);Assert.That(Call(s,"Place",0,new Vector2Int(-1,0),0),Is.False);Assert.That(Call(s,"Place",0,new Vector2Int(0,1),0),Is.True);Assert.That(Call(s,"Place",1,new Vector2Int(1,1),0),Is.False);Assert.That(Get(s,"IsComplete"),Is.False);}
  [Test]public void Placement_RotationFitsNarrowSpaceAndCompletes(){var d=Placement();Set(d,"_boardSize",new Vector2Int(2,3));var s=Session(d);Assert.That(Call(s,"Place",0,new Vector2Int(0,1),1),Is.True);Assert.That(Call(s,"Place",1,new Vector2Int(1,1),0),Is.True);Assert.That(Get(s,"IsComplete"),Is.True);Assert.That(Get(s,"Score"),Is.EqualTo(100));}
  [Test]public void Placement_ReservedEntryCellsStayClear(){var d=Placement();Vectors(d,"_blockedCells",new Vector2Int(0,0));var s=Session(d);Assert.That(Call(s,"Place",0,new Vector2Int(0,0),0),Is.False);}
  [Test]public void TargetDodge_RequiresRealAimAndAvoidanceInputs(){var d=Targets();var s=Session(d);Assert.That(Call(s,"Aim",new Vector2(.1f,.1f)),Is.False);Assert.That(Call(s,"Aim",new Vector2(.5f,.5f)),Is.True);Call(s,"MoveLane",1);Call(s,"AdvanceTime",3f);Assert.That(Get(s,"IsComplete"),Is.True);Assert.That((int)Get(s,"Score"),Is.GreaterThan(0));}
  [Test]public void TargetDodge_ObstacleCollisionReducesScore(){var d=Targets();var hit=Session(d);var safe=Session(d);Call(hit,"Aim",new Vector2(.5f,.5f));Call(safe,"Aim",new Vector2(.5f,.5f));Call(safe,"MoveLane",1);Call(hit,"AdvanceTime",3f);Call(safe,"AdvanceTime",3f);Assert.That((int)Get(hit,"Score"),Is.LessThan((int)Get(safe,"Score")));}
  [Test]public void TraitBonus_WidensTimingWindowWithoutChangingTrait(){var d=Timing();var trait=ScriptableObject.CreateInstance<TraitDefinition>();_assets.Add(trait);trait.ConfigureForTests("fine","fine");var rule=New("GrowthMinigameBonusRule");Set(rule,"_trait",trait);Set(rule,"_target",0);Set(rule,"_amountPerPoint",.01f);Set(rule,"_maximumBonus",.3f);var p=new StudentLifeProgress("mini","p",10,10);p.AddTrait(trait,10);var game=Game(d);Refs(game,"_bonuses",rule);var s=SessionFrom(game,p);Assert.That((float)Get(Get(s,"Current"),"TimingWindow"),Is.EqualTo(.25f).Within(.001f));Assert.That(p.GetTraitValue(trait),Is.EqualTo(10));}
  [Test]public void MultiStageGame_RequiresEveryChallengeBeforeReward(){var first=New("ChoiceChallengeDefinition");Ints(first,"_answers",0);Strings(first,"_prompts","p");Strings(first,"_symbols","a","b");var game=Game(first);Refs(game,"_challenges",first,Sequence());var s=SessionFrom(game,null);Call(s,"Choose",0);Assert.That(Get(s,"IsComplete"),Is.False);Call(s,"AdvanceTime",2f);Call(s,"Choose",0);Call(s,"Choose",1);Call(s,"Choose",2);Assert.That(Get(s,"Succeeded"),Is.True);}
  [Test]public void EmptyDefinition_FailsClosed(){var game=New("MinigameDefinition");var error=Assert.Throws<TargetInvocationException>(()=>SessionFrom(game,null));Assert.That(error.InnerException,Is.TypeOf<ArgumentException>());}
  [Test]public void NegativeTimeCannotRewindTheChallenge(){var s=Session(Sequence());Call(s,"AdvanceTime",2f);Assert.That(Call(s,"AdvanceTime",-1f),Is.False);Assert.That(Call(s,"Choose",0),Is.True);}
  private ScriptableObject Sequence(){var d=New("SequenceChallengeDefinition");Ints(d,"_answers",0,1,2);Strings(d,"_symbols","a","b","c");Set(d,"_revealSeconds",2f);Set(d,"_timeLimitSeconds",20f);Set(d,"_mistakeAllowance",3);return d;}
  private ScriptableObject Timing(){var d=New("TimingChallengeDefinition");Floats(d,"_beatTimes",1f,2f);Set(d,"_windowSeconds",.15f);Set(d,"_timeLimitSeconds",3f);return d;}
  private ScriptableObject Placement(){var d=New("PlacementChallengeDefinition");Set(d,"_boardSize",new Vector2Int(4,3));Vectors(d,"_pieceSizes",new Vector2Int(2,1),new Vector2Int(1,2));Set(d,"_timeLimitSeconds",30f);return d;}
  private ScriptableObject Targets(){var d=New("TargetDodgeChallengeDefinition");var so=new SerializedObject(d);var a=so.FindProperty("_targets");Assert.That(a,Is.Not.Null);a.arraySize=1;a.GetArrayElementAtIndex(0).vector2Value=new Vector2(.5f,.5f);so.ApplyModifiedPropertiesWithoutUndo();Ints(d,"_obstacleLanes",0);Set(d,"_obstacleIntervalSeconds",1f);Set(d,"_timeLimitSeconds",2f);Set(d,"_targetRadius",.1f);Set(d,"_targetWobble",0f);Set(d,"_laneCount",3);return d;}
  private ScriptableObject Game(ScriptableObject challenge){var d=New("MinigameDefinition");Refs(d,"_challenges",challenge);return d;}
  private object Session(ScriptableObject challenge)=>SessionFrom(Game(challenge),null);
  private object SessionFrom(ScriptableObject game,StudentLifeProgress p)=>Activator.CreateInstance(Type("MinigameSession"),game,p,123);
  private Type Type(string name){var type=typeof(StudentLifeProgress).Assembly.GetType("Rootborn.Game.Minigames."+name);Assert.That(type,Is.Not.Null,"Missing minigame type "+name);return type;}
  private ScriptableObject New(string name){var d=ScriptableObject.CreateInstance(Type(name));_assets.Add(d);return d;}
  private object Call(object obj,string name,params object[] args){var method=obj.GetType().GetMethod(name,BindingFlags.Public|BindingFlags.Instance);Assert.That(method,Is.Not.Null,"Missing method "+name);return method.Invoke(obj,args);}
  private object Get(object obj,string name){var p=obj.GetType().GetProperty(name);Assert.That(p,Is.Not.Null,"Missing property "+name);return p.GetValue(obj);}
  private void Set(Object obj,string field,object value){var so=new SerializedObject(obj);var p=so.FindProperty(field);Assert.That(p,Is.Not.Null,field);if(value is int i)p.intValue=i;else if(value is float f)p.floatValue=f;else if(value is Vector2Int v)p.vector2IntValue=v;else p.objectReferenceValue=(Object)value;so.ApplyModifiedPropertiesWithoutUndo();}
  private void Array(Object obj,string field,int count,Action<SerializedProperty,int> assign){var so=new SerializedObject(obj);var p=so.FindProperty(field);Assert.That(p,Is.Not.Null,field);p.arraySize=count;for(int i=0;i<count;i++)assign(p.GetArrayElementAtIndex(i),i);so.ApplyModifiedPropertiesWithoutUndo();}
  private void Ints(Object o,string f,params int[] v)=>Array(o,f,v.Length,(p,i)=>p.intValue=v[i]);
  private void Floats(Object o,string f,params float[] v)=>Array(o,f,v.Length,(p,i)=>p.floatValue=v[i]);
  private void Strings(Object o,string f,params string[] v)=>Array(o,f,v.Length,(p,i)=>p.stringValue=v[i]);
  private void Vectors(Object o,string f,params Vector2Int[] v)=>Array(o,f,v.Length,(p,i)=>p.vector2IntValue=v[i]);
  private void Refs(Object o,string f,params Object[] v)=>Array(o,f,v.Length,(p,i)=>p.objectReferenceValue=v[i]);
 }
}
