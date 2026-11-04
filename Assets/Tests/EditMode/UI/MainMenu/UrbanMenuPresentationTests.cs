using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Family;
using Rootborn.Game.Player;
using Rootborn.Game.Save;
using Rootborn.UI.MainMenu;
using Rootborn.UI.Modern;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Rootborn.Tests.EditMode.UI.MainMenu
{
    public sealed class UrbanMenuPresentationTests
    {
        private GameObject _canvas;
        private GameObject _host;
        private string _saveRoot;
        private bool _hadEventSystem;
        [SetUp] public void Setup()
        {
            _saveRoot = Path.Combine(Application.temporaryCachePath, "urban-menu-" + Guid.NewGuid().ToString("N"));
            SaveSlotSelectPanel.SetSaveRootForTests(_saveRoot);
            _hadEventSystem = Object.FindFirstObjectByType<EventSystem>() != null;
            _canvas = new GameObject("UrbanMenuTestCanvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            _canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            ((RectTransform)_canvas.transform).sizeDelta = new Vector2(1920,1080);
            _host = new GameObject("UrbanMenuOwner", typeof(RectTransform));
            _host.transform.SetParent(_canvas.transform, false);
        }
        [TearDown] public void Cleanup()
        {
            var slots = Object.FindObjectsByType<SaveSlotSelectPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var slot in slots)
            {
                var field = typeof(SaveSlotSelectPanel).GetField("_root", BindingFlags.Instance|BindingFlags.NonPublic);
                var root = field.GetValue(slot) as GameObject;
                if (root != null) Object.DestroyImmediate(root);
                if (slot != null) Object.DestroyImmediate(slot.gameObject);
            }
            if (_canvas != null) Object.DestroyImmediate(_canvas);
            if (!_hadEventSystem)
            {
                var events = Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var system in events) Object.DestroyImmediate(system.gameObject);
            }
            SaveSlotSelectPanel.SetSaveRootForTests(null);
            if (Directory.Exists(_saveRoot)) Directory.Delete(_saveRoot, true);
        }
        [Test] public void MainMenu_ChromePreservesExistingButtonsAndSingleEntryCallback()
        {
            _host.SetActive(false);
            var mode = _host.AddComponent<ModeSelectPanel>();
            string[] fields = {"_singleButton","_hostButton","_clientButton","_quitButton"};
            var buttons = new Button[fields.Length];
            int suppliedCallbackCount=0;
            for(int i=0;i<fields.Length;i++)
            {
                var go=new GameObject("ExistingButton_"+i,typeof(RectTransform),typeof(Image),typeof(Button));go.transform.SetParent(_host.transform,false);
                buttons[i]=go.GetComponent<Button>();
                typeof(ModeSelectPanel).GetField(fields[i],BindingFlags.Instance|BindingFlags.NonPublic).SetValue(mode,buttons[i]);
            }
            buttons[0].onClick.AddListener(()=>suppliedCallbackCount++);
            typeof(ModeSelectPanel).GetMethod("Awake",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(mode,null);
            typeof(ModeSelectPanel).GetMethod("Start",BindingFlags.Instance|BindingFlags.NonPublic)?.Invoke(mode,null);
            var skin=mode.GetComponent<ModernUiTileImage>();
            Assert.That(skin,Is.Not.Null,"The actual menu root must use approved molding.");
            var root=(RectTransform)mode.transform;
            foreach(var button in buttons)
            {
                Assert.That(button,Is.Not.Null);Assert.That(button.GetComponent<ModernUiTileImage>(),Is.Not.Null);
                AssertInside(root,(RectTransform)button.transform,36);
            }
            // Editor fixtures pre-create the slot owner; the real DontDestroyOnLoad installation is verified in PlayMode.
            _host.AddComponent<SaveSlotSelectPanel>();
            buttons[0].onClick.Invoke();
            Assert.That(suppliedCallbackCount,Is.EqualTo(1),"Styling must not replace supplied callbacks.");
            Assert.That(GameObject.Find("SaveSlotSelectRoot"),Is.Not.Null,"Existing Single callback must still open slot selection.");
        }
        [Test] public void SaveScreen_CardsAndLegacyCustomizationFitFrameAndExposeBack()
        {
            var panel=_host.AddComponent<SaveSlotSelectPanel>();panel.Show();
            var root=GetRoot(panel);var frame=root.transform.Find("SaveSlotFrame") as RectTransform;
            Assert.That(frame,Is.Not.Null,"Save screen needs the shared fixed molding frame.");
            Assert.That(frame.GetComponent<ModernUiTileImage>(),Is.Not.Null);
            for(int i=0;i<3;i++)
            {
                var card=frame.Find("SaveSlotCard_slot-"+i) as RectTransform;
                Assert.That(card,Is.Not.Null);AssertInside(frame,card,36);
                Assert.That(card.GetComponent<ModernUiTileImage>(),Is.Not.Null);
                var icon=card.Find("SaveSlotSymbol");Assert.That(icon,Is.Not.Null);
                Assert.That(icon.GetComponent<Image>().raycastTarget,Is.False);
                Assert.That(card.Find("NewGameButton"),Is.Not.Null);
            }
            var controls=frame.Find("CharacterSelectionPanel") as RectTransform;Assert.That(controls,Is.Not.Null);AssertInside(frame,controls,36);
            foreach(string name in new[]{"BodyNextButton","EyesNextButton","HairNextButton","OutfitNextButton","AccessoryNextButton"}){var option=controls.Find(name) as RectTransform;Assert.That(option,Is.Not.Null);AssertInside(controls,option,6);}
            var back=frame.Find("BackButton");Assert.That(back,Is.Not.Null);back.GetComponent<Button>().onClick.Invoke();
            Assert.That(root.activeSelf,Is.False,"Back must hide the overlay and return to the still-existing menu.");
            panel.Show();Assert.That(GetRoot(panel).activeSelf,Is.True);
        }
        [Test] public void DeleteChoice_CancelKeepsMetadataAndOnlyConfirmDeletes()
        {
            var service=new SaveService("slot-0",_saveRoot);
            service.SaveMetadata(service.CreateUiMetadata("slot-0",new CharacterCustomization(),123,456));
            var panel=_host.AddComponent<SaveSlotSelectPanel>();panel.Show();
            FindButton(GetRoot(panel),"DeleteButton").onClick.Invoke();
            Assert.That(service.LoadMetadata("slot-0"),Is.Not.Null,"Delete must first ask for confirmation.");
            FindButton(GetRoot(panel),"DeleteSlotCancelButton").onClick.Invoke();
            Assert.That(service.LoadMetadata("slot-0"),Is.Not.Null);
            FindButton(GetRoot(panel),"DeleteButton").onClick.Invoke();
            FindButton(GetRoot(panel),"DeleteSlotConfirmButton").onClick.Invoke();
            Assert.That(service.LoadMetadata("slot-0"),Is.Null);
        }
        private static GameObject GetRoot(SaveSlotSelectPanel p)=>(GameObject)typeof(SaveSlotSelectPanel).GetField("_root",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(p);
        private static Button FindButton(GameObject root,string name)
        {
            foreach(var b in root.GetComponentsInChildren<Button>(true))if(b.name==name && b.gameObject.activeInHierarchy)return b;
            Assert.Fail("Missing active button: "+name);return null;
        }
        private static void AssertInside(RectTransform parent,RectTransform child,float inset)
        {
            var bounds=parent.rect;bounds.xMin+=inset;bounds.yMin+=inset;bounds.xMax-=inset;bounds.yMax-=inset;
            var corners=new Vector3[4];child.GetWorldCorners(corners);
            foreach(var corner in corners)Assert.That(bounds.Contains(parent.InverseTransformPoint(corner)),Is.True,child.name+" overlaps molding.");
        }
    }
}
