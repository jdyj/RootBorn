using System;using System.Reflection;using NUnit.Framework;using UnityEngine;using UnityEngine.UI;using UnityEditor;using Rootborn.UI.Modern;
namespace Rootborn.Tests.EditMode {
public sealed class UrbanUiIconTests {
 GameObject root;UrbanUiThemeDefinition theme;
 static Type IconType=>typeof(UrbanUiThemeRuntime).Assembly.GetType("Rootborn.UI.Modern.UrbanUiIcon");
 static void Publish(UrbanUiThemeDefinition value){typeof(UrbanUiThemeRuntime).GetProperty("Current").GetSetMethod(true).Invoke(null,new object[]{value});var f=typeof(UrbanUiThemeRuntime).GetField("Changed",BindingFlags.NonPublic|BindingFlags.Static);(f.GetValue(null) as Action)?.Invoke();}
 [TearDown] public void Cleanup(){if(root!=null)UnityEngine.Object.DestroyImmediate(root);Publish(null);}
 [Test] public void LateThemeReadiness_UsesSameNonBlockingImageAndClearsOnFallback(){
  var t=IconType;Assert.That(t,Is.Not.Null,"Shared icon renderer required");root=new GameObject("icon",typeof(RectTransform),typeof(Image));var c=root.AddComponent(t);t.GetMethod("Configure").Invoke(c,new object[]{"journal"});var img=root.GetComponent<Image>();int id=img.GetInstanceID();Assert.That(img.raycastTarget,Is.False);Assert.That(img.sprite,Is.Null);
  theme=AssetDatabase.LoadAssetAtPath<UrbanUiThemeDefinition>("Assets/Data/UI/UrbanMoldingTheme.asset");Publish(theme);Assert.That(img.sprite,Is.Not.Null);Assert.That(img.sprite.name,Is.EqualTo("urban_icon_journal"));Assert.That(img.GetInstanceID(),Is.EqualTo(id));Assert.That(img.preserveAspect,Is.True);
  Publish(null);Assert.That(img.sprite,Is.Null);Assert.That(img.enabled,Is.False);
 }
 [Test] public void DisabledAndDestroyedIcons_RemoveReadinessSubscription(){
  var t=IconType;Assert.That(t,Is.Not.Null);root=new GameObject("icon",typeof(RectTransform),typeof(Image));var c=(Behaviour)root.AddComponent(t);t.GetMethod("Configure").Invoke(c,new object[]{"plan"});var f=typeof(UrbanUiThemeRuntime).GetField("Changed",BindingFlags.NonPublic|BindingFlags.Static);
  c.enabled=false;var a=f.GetValue(null) as Action;Assert.That(a==null||Array.TrueForAll(a.GetInvocationList(),x=>!ReferenceEquals(x.Target,c)),Is.True);
  UnityEngine.Object.DestroyImmediate(root);root=null;a=f.GetValue(null) as Action;Assert.That(a==null||Array.TrueForAll(a.GetInvocationList(),x=>!ReferenceEquals(x.Target,c)),Is.True);
 }
}}
