using System.Linq;
using NUnit.Framework;
using Rootborn.Game.Common;
using UnityEditor;
using UnityEngine;
namespace Rootborn.Tests.EditMode.Family
{
    public sealed class CharacterFrameGeometryTests
    {
        [Test] public void RegisteredModernParts_KeepCompleteSixteenByThirtyTwoFrames()
        {
            var registry=AssetDatabase.LoadAssetAtPath<GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset");
            Assert.That(registry.CharacterParts.Length,Is.GreaterThanOrEqualTo(10));
            foreach(var part in registry.CharacterParts)
            {
                var sprite=AssetDatabase.LoadAllAssetsAtPath(part.EditorAssetPath).OfType<Sprite>().Single(x=>x.name==part.SubSpriteName);
                Assert.That(sprite.rect.size,Is.EqualTo(new Vector2(16,32)),part.name);
                Assert.That(sprite.rect.y,Is.EqualTo(sprite.texture.height-32),part.name);
                Assert.That(part.PreviewSprite,Is.SameAs(sprite),"The saved preview must retain its real subasset reference.");
            }
        }
        [Test] public void BodyFrame_ContainsTheLowerHalfOfTheAuthoredCharacter()
        {
            var registry=AssetDatabase.LoadAssetAtPath<GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset");
            var part=registry.CharacterParts.First(x=>x.CategoryId=="body");
            var sprite=part.PreviewSprite;Assert.That(sprite,Is.Not.Null);
            var raw=new Texture2D(2,2,TextureFormat.RGBA32,false);
            try
            {
                raw.LoadImage(System.IO.File.ReadAllBytes(part.EditorAssetPath));
                int lowerOpaque=0;for(int y=0;y<10;y++)for(int x=0;x<16;x++)if(raw.GetPixel((int)sprite.rect.x+x,(int)sprite.rect.y+y).a>.5f)lowerOpaque++;
                Assert.That(lowerOpaque,Is.GreaterThan(5),"A head-only slice cannot represent the body/feet in the game and preview.");
            }
            finally{Object.DestroyImmediate(raw);}
        }
        [Test] public void PartImports_KeepPixelsAndIdenticalFrameAlignment()
        {
            var registry=AssetDatabase.LoadAssetAtPath<GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset");
            foreach(var part in registry.CharacterParts)
            {
                var importer=(TextureImporter)AssetImporter.GetAtPath(part.EditorAssetPath);
                Assert.That(importer.filterMode,Is.EqualTo(FilterMode.Point));
                Assert.That(importer.spritePixelsPerUnit,Is.EqualTo(16));
                Assert.That(part.PreviewSprite.pivot,Is.EqualTo(new Vector2(8,0)),part.name);
            }
        }
    }
}
