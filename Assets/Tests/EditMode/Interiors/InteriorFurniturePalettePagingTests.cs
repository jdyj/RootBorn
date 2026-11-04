using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Interiors;
using Rootborn.UI.Interiors;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

namespace Rootborn.Tests.EditMode.Interiors
{
    public sealed class InteriorFurniturePalettePagingTests
    {
        private GameObject _host;
        private RectTransform _palette;
        private InteriorFurniturePlacementPanel _panel;
        private Tile _tile;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("PaletteFixture", typeof(RectTransform), typeof(InteriorFurniturePlacementPanel));
            _panel = _host.GetComponent<InteriorFurniturePlacementPanel>();
            var palette = new GameObject("FurniturePalette", typeof(RectTransform));
            palette.transform.SetParent(_host.transform, false);
            _palette = palette.GetComponent<RectTransform>();
            _palette.sizeDelta = new Vector2(430f, 860f);
            _tile = ScriptableObject.CreateInstance<Tile>();
            var furniture = (List<InteriorFurnitureDefinition>)typeof(InteriorFurniturePlacementPanel)
                .GetField("_furniture", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_panel);
            for (int i = 0; i < 87; i++) furniture.Add(new InteriorFurnitureDefinition("qa.fixture." + i.ToString("D3"),
                i == 86 ? "마지막 장난감 주방" : "테스트 가구 " + i,
                InteriorObjectKind.OfficeProp, true, InteriorPlacementPreference.AvoidCorridor,
                new[] { new InteriorFurnitureTile(Vector2Int.zero, _tile) }, new[] { Vector2Int.zero }));
            typeof(InteriorFurniturePlacementPanel).GetMethod("CreateFurnitureCategoryButtons", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(_panel, new object[] { _palette });
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_host);
            Object.DestroyImmediate(_tile);
        }

        [Test]
        public void CatalogOverSixtyItems_HasBoundedVisibleButtonsAndReachableFinalPage()
        {
            Assert.AreEqual(20, ItemButtons(false).Length);
            var next = Find<Button>("FurniturePaletteNextPage");
            var previous = Find<Button>("FurniturePalettePreviousPage");
            Assert.IsNotNull(next);
            Assert.IsNotNull(previous);
            Assert.IsFalse(previous.interactable);
            var all = new HashSet<string>();
            for (int page = 0; page < 5; page++)
            {
                foreach (var button in ItemButtons(false))
                {
                    all.Add(button.name);
                    var center = _palette.InverseTransformPoint(button.GetComponent<RectTransform>().position);
                    Assert.IsTrue(_palette.rect.Contains(center), button.name + " must stay within the visible palette.");
                }
                Assert.LessOrEqual(ItemButtons(false).Length, 20);
                Assert.LessOrEqual(ItemButtons(true).Length, 20, "Paging must reuse at most20item buttons, not instantiate the whole catalog.");
                if (page < 4) { Assert.IsTrue(next.interactable); next.onClick.Invoke(); }
            }
            Assert.AreEqual(87, all.Count, "Every entry including indexes above60must be reachable.");
            Assert.AreEqual(7, ItemButtons(false).Length);
            Assert.IsFalse(next.interactable);
            Assert.IsNotNull(Find<Button>("PaletteFurniture_qa.fixture.086"));
        }

        [Test]
        public void Search_FindsLateDisplayNameOrCaseInsensitiveStableId_AndReusesButtonPool()
        {
            var search = Find<InputField>("FurniturePaletteSearchInput");
            Assert.IsNotNull(search);
            var pool = ItemButtons(true).Select(button => button.GetInstanceID()).OrderBy(id => id).ToArray();
            search.text = "장난감 주방";
            Assert.AreEqual(1, ItemButtons(false).Length);
            Assert.AreEqual("PaletteFurniture_qa.fixture.086", ItemButtons(false)[0].name);
            search.text = "QA.FIXTURE.086";
            Assert.AreEqual("PaletteFurniture_qa.fixture.086", ItemButtons(false).Single().name);
            search.text = "no such furniture";
            Assert.IsEmpty(ItemButtons(false));
            Assert.IsFalse(Find<Button>("FurniturePaletteNextPage").interactable);
            search.text = string.Empty;
            Assert.AreEqual(20, ItemButtons(false).Length);
            CollectionAssert.AreEqual(pool, ItemButtons(true).Select(button => button.GetInstanceID()).OrderBy(id => id).ToArray());
        }

        [Test]
        public void ReboundButton_SelectsTheVisibleFurnitureAndSearchKeepsThatSelection()
        {
            var search = Find<InputField>("FurniturePaletteSearchInput");
            Assert.IsNotNull(search);
            search.text = "qa.fixture.086";
            ItemButtons(false).Single().onClick.Invoke();
            var selected = typeof(InteriorFurniturePlacementPanel).GetField("_activeFurniture", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.AreEqual("qa.fixture.086", ((InteriorFurnitureDefinition)selected.GetValue(_panel)).Id);
            search.text = string.Empty;
            Find<Button>("FurniturePaletteNextPage").onClick.Invoke();
            Assert.AreEqual("qa.fixture.086", ((InteriorFurnitureDefinition)selected.GetValue(_panel)).Id,
                "Browsing another page must not silently change the chosen furniture or settings.");
            ItemButtons(false).First().onClick.Invoke();
            Assert.AreEqual("qa.fixture.020", ((InteriorFurnitureDefinition)selected.GetValue(_panel)).Id,
                "Pooled listeners must select the new page's item, not a stale captured definition.");
        }

        private Button[] ItemButtons(bool includeInactive) => _palette.GetComponentsInChildren<Button>(includeInactive)
            .Where(button => button.name.StartsWith("PaletteFurniture_")).ToArray();

        private T Find<T>(string objectName) where T : Component => _palette.GetComponentsInChildren<T>(true)
            .FirstOrDefault(component => component.name == objectName);
    }
}
