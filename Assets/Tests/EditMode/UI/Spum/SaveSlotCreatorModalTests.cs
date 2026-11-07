using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Save;
using Rootborn.UI.MainMenu;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using Object = UnityEngine.Object;

namespace Rootborn.Tests.EditMode.UI.Spum
{
    public sealed class SaveSlotCreatorModalTests
    {
        [Test] public void OpeningCreatorHidesSlotContents_AndCancelRestoresExactPriorActiveStates()
        {
            var host = new GameObject("SaveSlotModalTest");
            var root = new GameObject("SaveSlotSelectRoot", typeof(RectTransform));
            var previouslyInactive = new GameObject("PreviouslyHiddenControl", typeof(RectTransform), typeof(Button));
            var cards = new List<GameObject>();
            SpumCharacterCreatorPanel creator = null;
            var tempRoot = Path.Combine(Application.temporaryCachePath, "rootborn-creator-modal-" + Guid.NewGuid().ToString("N"));
            try
            {
                root.transform.SetParent(host.transform, false);
                ((RectTransform)root.transform).sizeDelta = new Vector2(1920, 1080);
                var owner = host.AddComponent<SaveSlotSelectPanel>();
                typeof(SaveSlotSelectPanel).GetField("_root", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, root);
                for (int i = 0; i < 3; i++)
                {
                    var card = new GameObject("SaveSlotCard_slot-" + i, typeof(RectTransform), typeof(Image), typeof(Button));
                    card.transform.SetParent(root.transform, false); cards.Add(card);
                }
                previouslyInactive.transform.SetParent(root.transform, false); previouslyInactive.SetActive(false);
                var open = typeof(SaveSlotSelectPanel).GetMethod("OpenSpumCreator", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(open, Is.Not.Null);
                open.Invoke(owner, new object[] { new SaveService("slot-0", tempRoot), "slot-0" });
                creator = root.GetComponentInChildren<SpumCharacterCreatorPanel>(true);
                Assert.That(creator, Is.Not.Null); Assert.That(creator.gameObject.activeInHierarchy, Is.True);
                Assert.That(cards.TrueForAll(x => !x.activeInHierarchy), Is.True,
                    "Opening the creator currently leaves every underlying save card visible and interactive.");
                Assert.That(previouslyInactive.activeSelf, Is.False);
                var cancel = creator.transform.Find("SpumCharacterCreatorRoot/Header/BackButton").GetComponent<Button>();
                cancel.onClick.Invoke();
                Assert.That(cards.TrueForAll(x => x.activeSelf && x.activeInHierarchy), Is.True, "Cancel must restore the same existing card instances.");
                Assert.That(previouslyInactive.activeSelf, Is.False, "Cancel must not reveal a control that was already hidden.");
                Assert.That(creator == null || !creator.gameObject.activeInHierarchy, Is.True);
                Assert.That(File.Exists(Path.Combine(tempRoot, "slot-0", "metadata.json")), Is.False, "Opening/canceling must not create a character save.");
            }
            finally
            {
                Object.DestroyImmediate(host);
                if (previouslyInactive != null) Object.DestroyImmediate(previouslyInactive);
                if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
            }
        }
    }
}