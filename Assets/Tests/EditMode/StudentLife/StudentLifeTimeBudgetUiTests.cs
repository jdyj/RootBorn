using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Localization;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.UI.StudentLife;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.Tests.EditMode.StudentLife
{
 public sealed class StudentLifeTimeBudgetUiTests
 {
  private readonly List<UnityEngine.Object> _objects = new List<UnityEngine.Object>();
  private StudentLifeProgressComponent _student;
  private GameDataRegistry _registry;
  private string _language;
  private SaveSlotMetadata _save;
  [SetUp] public void SetUp()
  {
   _save=ActiveSaveContext.Metadata;ActiveSaveContext.Clear();_language=GameText.Language; GameText.Language="en";
   _registry=Make<GameDataRegistry>();
   var calendar=Make<LifeCalendarDefinition>();calendar.ConfigureForTests(2,3);Set(_registry,"_studentLifeCalendar",calendar);
   var text=Make<GameTextCatalog>();
   text.ConfigureJson("{\"entries\":[{\"key\":\"life.tp.summary\",\"ko\":\"{0}일차 {1} TP {2}/{3}\",\"en\":\"Day {0} {1} TP {2}/{3}\"},{\"key\":\"life.weekday.0\",\"ko\":\"월요일\",\"en\":\"Monday\"},{\"key\":\"life.weekday.5\",\"ko\":\"토요일\",\"en\":\"Saturday\"},{\"key\":\"life.tp.cost\",\"ko\":\"{0} TP\",\"en\":\"{0} TP\"},{\"key\":\"life.tp.free\",\"ko\":\"무료\",\"en\":\"No TP cost\"},{\"key\":\"life.tp.applied\",\"ko\":\"완료 {0}/{1}\",\"en\":\"Done {0}/{1}\"}]}");
   Set(_registry,"_textCatalog",text);GameDataRegistryProvider.SetOverrideForTests(_registry);
   _student=Go("calendar-ui-player").AddComponent<StudentLifeProgressComponent>();
   _student.ConfigureForTests("calendar-ui-test","calendar-ui-player",100,100,0,480);
   _student.Progress.ConfigureCalendar(calendar);
  }
  [TearDown] public void TearDown(){GameText.Language=_language;GameDataRegistryProvider.ClearOverrideForTests();GameDataRegistryProvider.ResetCacheForTests();for(int i=_objects.Count-1;i>=0;i--)if(_objects[i]!=null)UnityEngine.Object.DestroyImmediate(_objects[i]);_objects.Clear();ActiveSaveContext.Set(_save);}
  [Test] public void Diary_RebindDisplaysCurrentDayAndRemainingBudget()
  {
   var panel=Go("Diary").AddComponent<DailyPlanPanel>(); panel.Bind(null,null,_student);
   Assert.That(Label(panel,"TimeBudget").text,Is.EqualTo("Day 1 Monday TP 2/2"));
   _student.Progress.TrySpendTimePoints(1); panel.Bind(null,null,_student);
   Assert.That(Label(panel,"TimeBudget").text,Is.EqualTo("Day 1 Monday TP 1/2"));
  }
  [Test] public void LocationAction_RefreshesBudgetAndDisablesAfterSecondPaidActivity()
  {
   var panel=Show(1,false,127); var button=panel.GetComponentsInChildren<Button>(true).Single(x=>x.name=="LocationActivityButton_0");
   Assert.That(button.GetComponentInChildren<Text>().text,Does.Contain("1 TP"));
   button.onClick.Invoke(); Assert.That(Label(panel,"TimeBudget").text,Does.Contain("1/2"));
   button.onClick.Invoke(); Assert.That(button.interactable,Is.False);Assert.That(Label(panel,"TimeBudget").text,Does.Contain("0/2"));
   int minutes=_student.Progress.TimeMinutes;button.onClick.Invoke();Assert.That(_student.Progress.TimeMinutes,Is.EqualTo(minutes));
  }
  [Test] public void FreeClass_DisablesAfterDailyUseWithoutConsumingBudget()
  {
   var panel=Show(0,true,31);var button=panel.GetComponentsInChildren<Button>(true).Single(x=>x.name=="LocationActivityButton_0");
   Assert.That(button.GetComponentInChildren<Text>().text,Does.Contain("No TP cost"));
   button.onClick.Invoke();Assert.That(_student.Progress.TimePointsRemaining,Is.EqualTo(2));Assert.That(button.interactable,Is.False);
  }
  [Test] public void WeekendDiary_ShowsSaturdayAndThreePoints()
  {
   var data=_student.Progress.ToSaveData();data.CurrentDay=6;_student.RestoreFromSaveData(data,null,null,null);
   var panel=Go("Diary").AddComponent<DailyPlanPanel>();panel.Bind(null,null,_student);
   Assert.That(Label(panel,"TimeBudget").text,Is.EqualTo("Day 6 Saturday TP 3/3"));
  }
  [Test] public void Location_WithSevenActivitiesMakesEveryChoiceReachableInScrollList()
  {
   var activities=new LocationActivityDefinition[7];
   for(int i=0;i<activities.Length;i++){activities[i]=Make<LocationActivityDefinition>();activities[i].ConfigureForTests("choice"+i,"choice"+i,null,LocationGrowthRoute.Exploration,0,0,0,0,null,null);}
   var identity=Make<LocationIdentityDefinition>();identity.ConfigureForTests("many","many",null,"many",activities,null,0,1440);
   var panel=Go("Many").AddComponent<LocationIdentityPanel>();panel.Show(identity,_student.gameObject);
   var buttons=panel.GetComponentsInChildren<Button>(true).Where(x=>x.name.StartsWith("LocationActivityButton_")).ToArray();
   Assert.That(buttons.Length,Is.EqualTo(7));Assert.That(panel.GetComponentInChildren<ScrollRect>(true),Is.Not.Null);
   buttons[6].onClick.Invoke();Assert.That(_student.Progress.GetActivityLogIds(),Does.Contain("choice6"));
  }
  private LocationIdentityPanel Show(int cost,bool once,int mask)
  {
   var policy=Make<ActivityTimePolicyDefinition>();policy.ConfigureForTests("ui-policy",cost,mask,once);
   var activity=Make<LocationActivityDefinition>();activity.ConfigureForTests("ui-activity","activity.test",null,LocationGrowthRoute.SelfStudy,15,0,0,0,null,null);Set(activity,"_timePolicy",policy);
   var identity=Make<LocationIdentityDefinition>();identity.ConfigureForTests("ui-place","place.test",null,"place.description",new[]{activity},null,0,1440);
   var panel=Go("LocationPanel").AddComponent<LocationIdentityPanel>();panel.Show(identity,_student.gameObject);return panel;
  }
  private Text Label(Component root,string name){var text=root.GetComponentsInChildren<Text>(true).FirstOrDefault(x=>x.name==name);Assert.That(text,Is.Not.Null,"Missing visible "+name);return text;}
  private T Make<T>() where T:ScriptableObject{var value=ScriptableObject.CreateInstance<T>();_objects.Add(value);return value;}
  private GameObject Go(string name){var value=new GameObject(name);_objects.Add(value);return value;}
  private void Set(UnityEngine.Object obj,string name,UnityEngine.Object value){var so=new SerializedObject(obj);var p=so.FindProperty(name);Assert.That(p,Is.Not.Null,name);p.objectReferenceValue=value;so.ApplyModifiedPropertiesWithoutUndo();}
 }
}
