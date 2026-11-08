using System.Collections.Generic;
using NUnit.Framework;
using Rootborn.Game.Characters;
using Rootborn.Game.Characters.Growing;
using UnityEngine;

namespace Rootborn.Tests.EditMode.Characters
{
    public sealed class GrowingCharacterVisualViewTests
    {
        private readonly List<Object> _objects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var item in _objects) Object.DestroyImmediate(item);
            _objects.Clear();
        }

        private GrowingCharacterSheetDefinition Sheet(string id)
        {
            var texture = new Texture2D(192, 192);
            _objects.Add(texture);
            var frames = new Sprite[GrowingCharacterSheetDefinition.FrameCount];
            for (int i = 0; i < frames.Length; i++)
            {
                frames[i] = Sprite.Create(texture, new Rect(i % 6 * 32, i / 6 * 48, 32, 48), new Vector2(0.5f, 0f), 48f);
                frames[i].name = id + i;
                _objects.Add(frames[i]);
            }
            var sheet = ScriptableObject.CreateInstance<GrowingCharacterSheetDefinition>();
            sheet.ConfigureForTests(id, frames);
            _objects.Add(sheet);
            return sheet;
        }

        private GrowingCharacterVisualView View(out GrowingCharacterAppearanceDefinition appearance)
        {
            var host = new GameObject("host");
            _objects.Add(host);
            var view = host.AddComponent<GrowingCharacterVisualView>();
            appearance = ScriptableObject.CreateInstance<GrowingCharacterAppearanceDefinition>();
            _objects.Add(appearance);
            appearance.ConfigureForTests("fixture", Sheet("body"), Sheet("head"), Sheet("outfit"));
            view.Apply(appearance);
            return view;
        }

        [TestCase(0f, -1f, 0)]
        [TestCase(-1f, 0f, 1)]
        [TestCase(1f, 0f, 2)]
        [TestCase(0f, 1f, 3)]
        public void GROW_VIEW_001_FacingSelectsSheetRow(float x, float y, int row)
        {
            var view = View(out _);
            view.SetMotion(Vector2.zero, new Vector2(x, y));
            Assert.That(view.CurrentRow, Is.EqualTo(row));
        }

        [Test]
        public void GROW_VIEW_002_IdleUsesStandingColumnOnAllLayers()
        {
            var view = View(out _);
            view.SetMotion(Vector2.zero, Vector2.down);
            Assert.That(view.CurrentColumn, Is.EqualTo(1));
            Assert.That(view.BodyRenderer.sprite.name, Is.EqualTo("body1"));
            Assert.That(view.OutfitRenderer.sprite.name, Is.EqualTo("outfit1"));
            Assert.That(view.HeadRenderer.sprite.name, Is.EqualTo("head1"));
        }

        [Test]
        public void GROW_VIEW_003_WalkingCyclesWalkColumnsOnly()
        {
            var view = View(out _);
            var seen = new List<int>();
            for (int i = 0; i < 8; i++)
            {
                view.Advance(Vector2.up, Vector2.up, 1f / GrowingCharacterVisualView.WalkFramesPerSecond);
                seen.Add(view.CurrentColumn);
            }
            Assert.That(seen, Is.EqualTo(new[] { 1, 2, 1, 0, 1, 2, 1, 0 }));
        }

        [Test]
        public void GROW_VIEW_004_StandingStillDoesNotReassignSprites()
        {
            var view = View(out _);
            view.SetMotion(Vector2.zero, Vector2.down);
            int changes = view.FrameChangeCount;
            for (int i = 0; i < 20; i++) view.SetMotion(Vector2.zero, Vector2.down);
            Assert.That(view.FrameChangeCount, Is.EqualTo(changes));
        }

        [Test]
        public void GROW_VIEW_005_RightFacingUsesOwnRowWithoutFlip()
        {
            var view = View(out _);
            view.SetFlipX(true);
            view.SetMotion(Vector2.zero, Vector2.right);
            Assert.That(view.BodyRenderer.flipX, Is.False);
            Assert.That(view.BodyRenderer.sprite.name, Is.EqualTo("body13"));
        }

        [Test]
        public void GROW_VIEW_006_MissingOutfitLeavesBodyVisible()
        {
            var view = View(out var appearance);
            appearance.ConfigureForTests("bare", appearance.Body, appearance.Head, null);
            view.Apply(appearance);
            view.SetMotion(Vector2.zero, Vector2.down);
            Assert.That(view.OutfitRenderer.sprite, Is.Null);
            Assert.That(view.BodyRenderer.sprite, Is.Not.Null);
        }

        [Test]
        public void GROW_VIEW_007_LayersDrawBodyOutfitHeadInOrder()
        {
            var view = View(out _);
            Assert.That(view.OutfitRenderer.sortingOrder, Is.GreaterThan(view.BodyRenderer.sortingOrder));
            Assert.That(view.HeadRenderer.sortingOrder, Is.GreaterThan(view.OutfitRenderer.sortingOrder));
        }

        [Test]
        public void GROW_VIEW_008_AttackPlaysActionColumnsThenReturns()
        {
            var view = View(out _);
            view.SetMotion(Vector2.zero, Vector2.down);
            view.PlayAction(CharacterVisualAction.Attack);
            view.Advance(Vector2.zero, Vector2.down, 0.01f);
            Assert.That(view.CurrentColumn, Is.EqualTo(3));
            view.Advance(Vector2.zero, Vector2.down, GrowingCharacterVisualView.ActionSeconds + 0.01f);
            Assert.That(view.CurrentColumn, Is.EqualTo(1));
        }

        [Test]
        public void GROW_VIEW_LAYER_001_AnyNumberOfLayersDrawOverTheBodyInOrder()
        {
            var host = new GameObject("host");
            _objects.Add(host);
            var view = host.AddComponent<GrowingCharacterVisualView>();
            var source = host.AddComponent<SpriteRenderer>();
            source.sortingOrder = 40;

            view.ApplyLayers(Sheet("body"), new[] { Sheet("outfit"), Sheet("hair"), Sheet("hat") });
            view.MatchSorting(source);
            view.SetMotion(Vector2.zero, Vector2.down);

            Assert.That(view.LayerCount, Is.EqualTo(3));
            Assert.That(view.BodyRenderer.sprite.name, Is.EqualTo("body1"));
            Assert.That(view.LayerRenderer(0).sprite.name, Is.EqualTo("outfit1"));
            Assert.That(view.LayerRenderer(1).sprite.name, Is.EqualTo("hair1"));
            Assert.That(view.LayerRenderer(2).sprite.name, Is.EqualTo("hat1"));
            Assert.That(view.BodyRenderer.sortingOrder, Is.EqualTo(40));
            Assert.That(view.LayerRenderer(0).sortingOrder, Is.EqualTo(41));
            Assert.That(view.LayerRenderer(2).sortingOrder, Is.EqualTo(43));
            Assert.That(view.Appearance, Is.Null, "a layered look is not a legacy appearance");
        }

        [Test]
        public void GROW_VIEW_LAYER_002_FewerLayersClearTheLeftoverRenderers()
        {
            var host = new GameObject("host");
            _objects.Add(host);
            var view = host.AddComponent<GrowingCharacterVisualView>();
            view.ApplyLayers(Sheet("body"), new[] { Sheet("outfit"), Sheet("hair"), Sheet("hat") });
            int changes = 0;
            view.LookChanged += () => changes++;

            view.ApplyLayers(Sheet("body2"), new[] { Sheet("outfit2") });
            view.SetMotion(Vector2.zero, Vector2.down);

            Assert.That(view.LayerCount, Is.EqualTo(1));
            Assert.That(view.LayerRenderer(0).sprite.name, Is.EqualTo("outfit21"));
            Assert.That(view.HeadRenderer.sprite, Is.Null);
            Assert.That(view.LayerRenderer(2).sprite, Is.Null, "the extra renderer stays but draws nothing");
            Assert.That(changes, Is.EqualTo(1));
        }

        [Test]
        public void GROW_VIEW_LAYER_003_LegacyAppearanceIsBodyOutfitHead()
        {
            var view = View(out var appearance);
            view.SetMotion(Vector2.zero, Vector2.down);
            Assert.That(view.Appearance, Is.SameAs(appearance));
            Assert.That(view.LayerCount, Is.EqualTo(2));
            Assert.That(view.LayerRenderer(0), Is.SameAs(view.OutfitRenderer));
            Assert.That(view.LayerRenderer(1), Is.SameAs(view.HeadRenderer));
        }

        [Test]
        public void GROW_SHEET_001_RejectsWrongFrameCount()
        {
            var sheet = ScriptableObject.CreateInstance<GrowingCharacterSheetDefinition>();
            _objects.Add(sheet);
            sheet.ConfigureForTests("short", new Sprite[3]);
            Assert.That(sheet.IsValid, Is.False);
            Assert.That(sheet.GetFrame(0, 0), Is.Null);
            Assert.That(sheet.GetFrame(9, 9), Is.Null);
        }
    }
}
