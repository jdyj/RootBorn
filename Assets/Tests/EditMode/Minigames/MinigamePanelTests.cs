using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Minigames;
using Rootborn.UI.StudentLife;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
namespace Rootborn.Tests.EditMode.Minigames
{
    public sealed class MinigamePanelTests
    {
        private readonly List<Object> _objects = new List<Object>();
        [TearDown] public void TearDown() { for (int i=_objects.Count-1;i>=0;i--) if (_objects[i]!=null) Object.DestroyImmediate(_objects[i]); _objects.Clear(); }
        [Test] public void Panel_HostedOnDisplayOnlyCanvasEnablesPointerInput()
        {
            var canvas=Canvas();
            var prior=canvas.GetComponent<GraphicRaycaster>();
            if(prior!=null) Object.DestroyImmediate(prior);
            Panel(canvas); Panel(canvas);
            Assert.That(canvas.GetComponents<GraphicRaycaster>().Length,Is.EqualTo(1));
            Assert.That(canvas.GetComponent<GraphicRaycaster>().enabled,Is.True);
        }
        [Test] public void Panel_IsInitiallyHidden_AndUsesExistingCanvas()
        {
            var canvas=Canvas(); var p=Panel(canvas); Assert.That(Get(p,"IsOpen"),Is.False);
            Assert.That(((Component)p).transform.IsChildOf(canvas.transform),Is.True);
            Assert.That(Panel(canvas),Is.SameAs(p));
        }
        [Test] public void ChoiceButton_SubmitsActualAnswer_AndDeliversResultOnce()
        {
            var p=Panel(Canvas()); var session=Choice(); int results=0;
            Call(p,"Show",session,(Action<MinigameSession>)(s=>{results++;Assert.That(s.Score,Is.EqualTo(100));}));
            Button(p,"Choice_0").onClick.Invoke(); Call(p,"Close"); Call(p,"Close");
            Assert.That(results,Is.EqualTo(1)); Assert.That(session.IsComplete,Is.True);
        }
        [Test] public void ClosingActiveGame_ForfeitsAndReleasesGameplayInput()
        {
            var p=Panel(Canvas()); var session=Choice(); int results=0;
            Call(p,"Show",session,(Action<MinigameSession>)(s=>results++));
            Assert.That(Blocked(),Is.True); Call(p,"Close");
            Assert.That(session.IsCancelled,Is.True); Assert.That(Blocked(),Is.False); Assert.That(results,Is.EqualTo(1));
        }
        [Test] public void DisablingPanel_CancelsOnceAndDoesNotLeaveInputLocked()
        {
            var p=Panel(Canvas()); var session=Choice();int results=0;
            Call(p,"Show",session,(Action<MinigameSession>)(s=>results++));
            ((Component)p).gameObject.SetActive(false); Call(p,"Close");
            Assert.That(session.IsCancelled,Is.True);Assert.That(results,Is.EqualTo(1));Assert.That(Blocked(),Is.False);
        }
        [Test] public void OpenPanel_RejectsReplacingAnUnsettledSession()
        {
            var p=Panel(Canvas());var first=Choice();Call(p,"Show",first,(Action<MinigameSession>)(_=>{}));
            Assert.That(Call(p,"Show",Choice(),(Action<MinigameSession>)(_=>{})),Is.False);
            Assert.That(Get(p,"Session"),Is.SameAs(first));
        }
        [Test] public void SequencePreview_DisablesAnswerButtonsUntilMemorizationEnds()
        {
            var d=New<SequenceChallengeDefinition>(); SetArray(d,"_answers",new[]{0,1});SetArray(d,"_symbols",new[]{"a","b"}); Set(d,"_revealSeconds",2f);
            var s=Session(d);var p=Panel(Canvas());Call(p,"Show",s,(Action<MinigameSession>)(_=>{}));
            Assert.That(Button(p,"Choice_0").interactable,Is.False); s.AdvanceTime(2f);Call(p,"RefreshView");Assert.That(Button(p,"Choice_0").interactable,Is.True);
        }
        [Test] public void TimingPanel_ShowsAuthoredBeatsAndRespondsToTapButton()
        {
            var d=New<TimingChallengeDefinition>();SetArray(d,"_beatTimes",new[]{1f,2f});Set(d,"_timeLimitSeconds",3f);
            var s=Session(d);var p=Panel(Canvas());Call(p,"Show",s,(Action<MinigameSession>)(_=>{}));
            Assert.That(Find(p,"Beat_0"),Is.Not.Null); Assert.That(Find(p,"Beat_1"),Is.Not.Null);s.AdvanceTime(1f);Button(p,"Tap").onClick.Invoke();Assert.That(s.Current.ProgressIndex,Is.EqualTo(1));
        }
        [Test] public void PlacementPanel_CellClickUsesSelectedPieceAndRotation()
        {
            var d=New<PlacementChallengeDefinition>();Set(d,"_boardSize",new Vector2Int(2,3));SetArray(d,"_pieceSizes",new[]{new Vector2Int(2,1),new Vector2Int(1,2)});
            var s=Session(d);var p=Panel(Canvas());Call(p,"Show",s,(Action<MinigameSession>)(_=>{}));
            Button(p,"Rotate").onClick.Invoke(); Button(p,"Cell_0_1").onClick.Invoke();
            Assert.That(s.Current.TryGetPieceRect(0,out var rect),Is.True);Assert.That(rect.size,Is.EqualTo(new Vector2Int(1,2)));
        }
        [Test] public void TargetPanel_ContainsPointerSurfaceAndLaneChoices()
        {
            var d=New<TargetDodgeChallengeDefinition>();SetArray(d,"_targets",new[]{new Vector2(.5f,.5f)});SetArray(d,"_obstacleLanes",new[]{0});
            var s=Session(d);var p=Panel(Canvas());Call(p,"Show",s,(Action<MinigameSession>)(_=>{}));
            Assert.That(Find(p,"AimSurface"),Is.Not.Null);Button(p,"Lane_1").onClick.Invoke();Assert.That(s.Current.Lane,Is.EqualTo(1));
        }
        [Test] public void InputLock_OverlappingOwnersRequireBothToRelease()
        {
            var a=Canvas();var b=Canvas(); var t=LockType();t.GetMethod("Acquire").Invoke(null,new object[]{a});t.GetMethod("Acquire").Invoke(null,new object[]{b});
            t.GetMethod("Release").Invoke(null,new object[]{a});Assert.That(Blocked(),Is.True);t.GetMethod("Release").Invoke(null,new object[]{b});Assert.That(Blocked(),Is.False);
        }
        [Test] public void InputLock_DestroyedOwnerCannotPermanentlyBlockPlay()
        {
            var a=Canvas();LockType().GetMethod("Acquire").Invoke(null,new object[]{a});Object.DestroyImmediate(a.gameObject);Assert.That(Blocked(),Is.False);
        }
        [Test] public void InputLock_StopsResidualPlayerMovementInPhysicsTick()
        {
            var owner=Canvas();LockType().GetMethod("Acquire").Invoke(null,new object[]{owner});
            var go=new GameObject("Player",typeof(Rigidbody2D),typeof(Rootborn.Game.Player.PlayerController));_objects.Add(go);
            var controller=go.GetComponent<Rootborn.Game.Player.PlayerController>();
            typeof(Rootborn.Game.Player.PlayerController).GetField("_input",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(controller,Vector2.right);
            typeof(Rootborn.Game.Player.PlayerController).GetMethod("FixedUpdate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(controller,null);
            Assert.That(go.GetComponent<Rigidbody2D>().linearVelocity,Is.EqualTo(Vector2.zero));
        }
        [Test] public void FailedSettlement_KeepsCompletedResultOpenForRetry()
        {
            var p=Panel(Canvas());var s=Choice();
            Call(p,"Show",s,(Action<MinigameSession>)(_=>Call(p,"SetSettlementFailure","save failed",(Func<bool>)(()=>false))));
            Button(p,"Choice_0").onClick.Invoke();Call(p,"Close");
            Assert.That(Get(p,"IsOpen"),Is.True);Assert.That(Blocked(),Is.True);Assert.That(s.Score,Is.EqualTo(100));
            Button(p,"RetrySave").onClick.Invoke();Assert.That(Get(p,"IsOpen"),Is.True);
        }
        [Test] public void SuccessfulSettlementRetry_AllowsClosingWithoutReplayingGame()
        {
            var p=Panel(Canvas());var s=Choice();int retries=0;
            Call(p,"Show",s,(Action<MinigameSession>)(_=>Call(p,"SetSettlementFailure","save failed",(Func<bool>)(()=>{retries++;return true;}))));
            Button(p,"Choice_0").onClick.Invoke();Button(p,"RetrySave").onClick.Invoke();Call(p,"Close");
            Assert.That(retries,Is.EqualTo(1));Assert.That(Get(p,"IsOpen"),Is.False);Assert.That(Blocked(),Is.False);Assert.That(s.Score,Is.EqualTo(100));
        }
        [Test] public void AuthoredChoiceSprites_AppearBesideLocalizedLabels_WithoutInterceptingClicks()
        {
            var d=New<ChoiceChallengeDefinition>();SetArray(d,"_answers",new[]{0});SetArray(d,"_symbols",new[]{"a","b"});SetArray(d,"_prompts",new[]{"prompt"});
            var texture=new Texture2D(48,48);_objects.Add(texture);var sprite=Sprite.Create(texture,new Rect(0,0,48,48),new Vector2(.5f,.5f));_objects.Add(sprite);
            var so=new SerializedObject(d);var icons=so.FindProperty("_symbolSprites");Assert.IsNotNull(icons,"Challenges must author optional choice sprites through data.");icons.arraySize=2;icons.GetArrayElementAtIndex(0).objectReferenceValue=sprite;so.ApplyModifiedPropertiesWithoutUndo();
            var p=Panel(Canvas());var session=Session(d);Call(p,"Show",session,(Action<MinigameSession>)(_=>{}));
            var first=Button(p,"Choice_0");Assert.IsNotNull(first.GetComponent<Rootborn.UI.Modern.ModernUiTileImage>(),"Choice cards must use the approved dimensional UI skin.");var icon=first.transform.Find("SymbolIcon")?.GetComponent<Image>();Assert.IsNotNull(icon);Assert.AreSame(sprite,icon.sprite);Assert.IsFalse(icon.raycastTarget);Assert.IsTrue(icon.preserveAspect);
            Assert.IsNotEmpty(first.GetComponentInChildren<Text>().text);
            Assert.IsNull(Button(p,"Choice_1").transform.Find("SymbolIcon"),"Missing art must retain the text choice.");
            first.onClick.Invoke();Assert.IsTrue(session.IsComplete);Assert.AreEqual(100,session.Score);
        }
        [Test] public void SequencePreview_UsesAuthoredSpritesInAnswerOrder_AndRemovesThemWhenRecallBegins()
        {
            var d=New<SequenceChallengeDefinition>();SetArray(d,"_answers",new[]{1,0,1});SetArray(d,"_symbols",new[]{"a","b"});Set(d,"_revealSeconds",2f);
            var t=new Texture2D(96,48);_objects.Add(t);var a=Sprite.Create(t,new Rect(0,0,48,48),Vector2.one*.5f);var b=Sprite.Create(t,new Rect(48,0,48,48),Vector2.one*.5f);_objects.Add(a);_objects.Add(b);
            var so=new SerializedObject(d);var icons=so.FindProperty("_symbolSprites");Assert.IsNotNull(icons);icons.arraySize=2;icons.GetArrayElementAtIndex(0).objectReferenceValue=a;icons.GetArrayElementAtIndex(1).objectReferenceValue=b;so.ApplyModifiedPropertiesWithoutUndo();
            var p=Panel(Canvas());var session=Session(d);Call(p,"Show",session,(Action<MinigameSession>)(_=>{}));
            Assert.AreSame(b,Find(p,"PreviewSymbol_0")?.GetComponent<Image>().sprite);Assert.AreSame(a,Find(p,"PreviewSymbol_1")?.GetComponent<Image>().sprite);Assert.AreSame(b,Find(p,"PreviewSymbol_2")?.GetComponent<Image>().sprite);
            Assert.IsFalse(Button(p,"Choice_0").interactable);session.AdvanceTime(2f);Call(p,"RefreshView");Assert.IsFalse(Find(p,"PreviewSymbol_0").gameObject.activeSelf);Assert.IsTrue(Button(p,"Choice_0").interactable);
        }
        [Test] public void PlacementSprites_ShowOnChoicesAndPlacedCells_WithoutBlockingInput()
        {
            var d=New<PlacementChallengeDefinition>();Set(d,"_boardSize",new Vector2Int(3,2));SetArray(d,"_pieceSizes",new[]{new Vector2Int(2,1),new Vector2Int(1,2)});
            var a=ArtSprite();var b=ArtSprite();SetArray(d,"_symbolSprites",new[]{a,b});var session=Session(d);var p=Panel(Canvas());Call(p,"Show",session,(Action<MinigameSession>)(_=>{}));
            var choice=Find(p,"PieceIcon_0")?.GetComponent<Image>();Assert.IsNotNull(choice);Assert.AreSame(a,choice.sprite);Assert.IsFalse(choice.raycastTarget);Assert.IsTrue(choice.preserveAspect);
            Assert.IsNotNull(Button(p,"Piece_0").GetComponent<Rootborn.UI.Modern.ModernUiTileImage>());
            var placed=Find(p,"PlacedPiece_0")?.GetComponent<Image>();Assert.IsNotNull(placed);Assert.IsFalse(placed.gameObject.activeSelf);
            Button(p,"Cell_0_0").onClick.Invoke();Assert.IsTrue(placed.gameObject.activeSelf);Assert.AreSame(a,placed.sprite);Assert.IsFalse(placed.raycastTarget);
            Assert.That(placed.rectTransform.sizeDelta,Is.EqualTo(new Vector2(92,42)));Assert.That(placed.rectTransform.anchoredPosition,Is.EqualTo(new Vector2(-120,-20)));
            Button(p,"Cell_2_0").onClick.Invoke();Assert.IsTrue(session.IsComplete);Assert.AreEqual(100,session.Score);Assert.IsTrue(Find(p,"PlacedPiece_1").gameObject.activeSelf,"Final furniture piece must remain visible on the completed board");
        }
        [Test] public void PlacementSprites_FollowRotatedFootprintsAndReuseVisuals()
        {
            var d=New<PlacementChallengeDefinition>();Set(d,"_boardSize",new Vector2Int(3,3));SetArray(d,"_pieceSizes",new[]{new Vector2Int(2,1),Vector2Int.one});SetArray(d,"_symbolSprites",new[]{ArtSprite()});
            var session=Session(d);var p=Panel(Canvas());Call(p,"Show",session,(Action<MinigameSession>)(_=>{}));Button(p,"Rotate").onClick.Invoke();Button(p,"Cell_0_0").onClick.Invoke();
            var image=Find(p,"PlacedPiece_0")?.GetComponent<Image>();Assert.IsNotNull(image);Assert.That(image.rectTransform.sizeDelta,Is.EqualTo(new Vector2(42,92)));int identity=image.GetInstanceID();
            Button(p,"Piece_0").onClick.Invoke();Button(p,"Cell_1_0").onClick.Invoke();Call(p,"RefreshView");Assert.That(Find(p,"PlacedPiece_0").GetComponent<Image>().GetInstanceID(),Is.EqualTo(identity));Assert.That(image.rectTransform.sizeDelta,Is.EqualTo(new Vector2(92,42)));
            Assert.IsNull(Find(p,"PieceIcon_1"),"Missing optional art keeps the normal text piece choice");
        }
        [Test] public void TargetSprites_ReplacePlaceholderArtAndPreservePointerSurfaceAndLanes()
        {
            var d=New<TargetDodgeChallengeDefinition>();SetArray(d,"_targets",new[]{new Vector2(.5f,.5f)});SetArray(d,"_obstacleLanes",new[]{0});var target=ArtSprite();var obstacle=ArtSprite();SetArray(d,"_symbolSprites",new[]{target,obstacle});
            var session=Session(d);var p=Panel(Canvas());Call(p,"Show",session,(Action<MinigameSession>)(_=>{}));var targetImage=Find(p,"Target").GetComponent<Image>();var obstacleImage=Find(p,"Obstacle").GetComponent<Image>();
            Assert.AreSame(target,targetImage.sprite);Assert.AreSame(obstacle,obstacleImage.sprite);Assert.IsFalse(targetImage.raycastTarget);Assert.IsFalse(obstacleImage.raycastTarget);Assert.IsTrue(targetImage.preserveAspect);Assert.IsNull(Find(p,"TargetMark"));Assert.IsTrue(Find(p,"AimSurface").GetComponent<Image>().raycastTarget);
            Button(p,"Lane_1").onClick.Invoke();Assert.AreEqual(1,session.Current.Lane);
        }
        [Test] public void MissingTargetSprites_KeepOriginalTargetGlyphAndObstacle()
        {
            var d=New<TargetDodgeChallengeDefinition>();SetArray(d,"_targets",new[]{new Vector2(.5f,.5f)});SetArray(d,"_obstacleLanes",new[]{0});var p=Panel(Canvas());Call(p,"Show",Session(d),(Action<MinigameSession>)(_=>{}));
            Assert.IsNotNull(Find(p,"TargetMark"));Assert.IsNull(Find(p,"Target").GetComponent<Image>().sprite);Assert.IsNull(Find(p,"Obstacle").GetComponent<Image>().sprite);
        }
        private Sprite ArtSprite(){var t=new Texture2D(48,48);_objects.Add(t);var s=Sprite.Create(t,new Rect(0,0,48,48),Vector2.one*.5f);_objects.Add(s);return s;}
        private object Panel(Canvas c){var t=typeof(LocationIdentityPanel).Assembly.GetType("Rootborn.UI.Minigames.MinigamePanel");Assert.That(t,Is.Not.Null,"Missing playable minigame panel");return t.GetMethod("EnsureInScene").Invoke(null,new object[]{c});}
        private Canvas Canvas(){var go=new GameObject("MiniTestCanvas",typeof(Canvas));_objects.Add(go);return go.GetComponent<Canvas>();}
        private T New<T>()where T:ScriptableObject{var a=ScriptableObject.CreateInstance<T>();_objects.Add(a);return a;}
        private MinigameSession Choice(){var d=New<ChoiceChallengeDefinition>();SetArray(d,"_answers",new[]{0});SetArray(d,"_symbols",new[]{"a","b"});SetArray(d,"_prompts",new[]{"prompt"});return Session(d);}
        private MinigameSession Session(ChallengeDefinitionBase d){var g=New<MinigameDefinition>();SetArray(g,"_challenges",new[]{d});return new MinigameSession(g,null,123);}
        private Type LockType(){var t=typeof(MinigameSession).Assembly.GetType("Rootborn.Game.Common.UiGameplayInputLock");Assert.That(t,Is.Not.Null,"Missing gameplay input lock");return t;}
        private bool Blocked()=>(bool)LockType().GetProperty("IsBlocked").GetValue(null);
        private object Call(object o,string method,params object[] args){var m=o.GetType().GetMethod(method);Assert.That(m,Is.Not.Null,method);return m.Invoke(o,args);}
        private object Get(object o,string prop)=>o.GetType().GetProperty(prop).GetValue(o);
        private Transform Find(object p,string name){foreach(var t in ((Component)p).GetComponentsInChildren<Transform>(true))if(t.name==name)return t;return null;}
        private Button Button(object p,string name){var t=Find(p,name);Assert.That(t,Is.Not.Null,name);return t.GetComponent<Button>();}
        private void Set(Object o,string name,object v){var so=new SerializedObject(o);var p=so.FindProperty(name);if(v is float f)p.floatValue=f;else if(v is Vector2Int vi)p.vector2IntValue=vi;so.ApplyModifiedPropertiesWithoutUndo();}
        private void SetArray<T>(Object o,string name,T[] values){var so=new SerializedObject(o);var p=so.FindProperty(name);p.arraySize=values.Length;for(int i=0;i<values.Length;i++){var e=p.GetArrayElementAtIndex(i);object v=values[i];if(v is int n)e.intValue=n;else if(v is float f)e.floatValue=f;else if(v is string s)e.stringValue=s;else if(v is Vector2Int vi)e.vector2IntValue=vi;else if(v is Vector2 vf)e.vector2Value=vf;else e.objectReferenceValue=(Object)v;}so.ApplyModifiedPropertiesWithoutUndo();}
    }
}
