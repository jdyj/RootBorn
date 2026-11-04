using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Placement;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Rootborn.Tests.EditMode.Placement
{
    public sealed class StudentLifePixelAssetIntegrationTests
    {
        private const string Art = "Assets/Art/StudentLifePixels";
        private const string Data = "Assets/Data/Interiors/Furniture/StudentLifePixels";
        private static readonly string[] Names = { "archive_door", "old_town_ledger", "snack_delivery_crate", "assembly_kit", "toy_kitchen", "town_record_photo" };
        private static readonly Vector2Int[] Sizes = { new Vector2Int(64,96),new Vector2Int(24,20),new Vector2Int(48,40),new Vector2Int(64,48),new Vector2Int(80,64),new Vector2Int(40,32) };
        private static readonly string[] NativeSha256 = {
            "3d54faca2833a15b434240a044e9ffe1efd3997d3deb862fa787d824264a3400",
            "811e9c2e808413d6acaf3c0c4f73e9011af634438a1ef25490c980be2e3d2bda",
            "d620f076a355b59fe60e8ce4bcd7e12c4286c1b1afe83d8b71ce90464a649e88",
            "8f88da17ba83f98da26ed8b11a25ac18287e7935ab46c1e6e5b4c60d3c97e1be",
            "6eaaee552675c4d3e191d97d8435e979e51cfb9710ae6d8402cf8e32621e0a9b",
            "16969c46d1be26ba6cf7b1276333f298f1b970784cbaf22ddea0fcb812af813c" };

        [Test]
        public void NativeSprites_PreserveReviewedBytesAndBottomCenterPivots()
        {
            for (int i=0;i<Names.Length;i++)
            {
                string path=Art+"/Sprites/"+Names[i]+".png";
                Assert.That(File.Exists(path),Is.True,"Import missing: "+path);
                using(var hash=SHA256.Create())
                    Assert.AreEqual(NativeSha256[i],BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-",string.Empty).ToLowerInvariant(),path);
                CheckImporter(path,Sizes[i],new Vector2(Sizes[i].x/2f,2f));
            }
        }

        [TestCase("snack_delivery_crate","StudentLife_snack_delivery_crate_1x1",48,96,0,34)]
        [TestCase("toy_kitchen","StudentLife_toy_kitchen_2x1",96,96,8,10)]
        public void FloorAdapters_AddTransparentPaddingWithoutResampling(string native,string adapter,int width,int height,int x,int top)
        {
            string path=Art+"/Furniture48/"+adapter+".png";
            CheckImporter(path,new Vector2Int(width,height),new Vector2(24,24));
            var original=new Texture2D(2,2,TextureFormat.RGBA32,false);
            var padded=new Texture2D(2,2,TextureFormat.RGBA32,false);
            try
            {
                Assert.IsTrue(ImageConversion.LoadImage(original,File.ReadAllBytes(Art+"/Sprites/"+native+".png")));
                Assert.IsTrue(ImageConversion.LoadImage(padded,File.ReadAllBytes(path)));
                var src=original.GetPixels32();var dst=padded.GetPixels32();int bottom=height-top-original.height;
                for (int py=0;py<height;py++) for (int px=0;px<width;px++)
                {
                    var actual=dst[py*width+px];
                    if (px>=x && px<x+original.width && py>=bottom && py<bottom+original.height)
                        Assert.AreEqual(src[(py-bottom)*original.width+(px-x)],actual,"Source pixel changed at "+px+","+py);
                    else Assert.AreEqual(new Color32(0,0,0,0),actual,"Padding must be transparent black.");
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(original);UnityEngine.Object.DestroyImmediate(padded); }
        }

        [Test]
        public void Furniture_RegistersOnlyFloorPropsExactlyOnceWithExactFootprints()
        {
            Assert.IsTrue(AssetDatabase.IsValidFolder(Data),"StudentLife furniture import is missing.");
            var definitions=AssetDatabase.FindAssets("t:FurnitureDefinition",new[]{Data}).Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<FurnitureDefinition>).ToArray();
            Assert.AreEqual(2,definitions.Length,"Tabletop kit/books/photo and wall door must not become floor blockers.");
            var registry=AssetDatabase.LoadAssetAtPath<GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset");
            Assert.IsNotNull(registry);
            foreach(var definition in definitions)
            {
                Assert.AreEqual(1,registry.FurnitureDefinitions.Count(d=>d==definition),definition.StableId);
                Assert.IsTrue(definition.BlocksMovement);
                Assert.AreEqual(1,definition.TileParts.Count);
                Assert.AreEqual(Vector2Int.zero,definition.TileParts[0].LocalCell);
                var tile=definition.TileParts[0].Tile as Tile;
                Assert.IsNotNull(tile);Assert.IsNotNull(tile.sprite);
                Assert.AreEqual(Tile.ColliderType.None,tile.colliderType,"Collision uses explicit footprint data.");
                CollectionAssert.AreEquivalent(new[]{"house","town-test"},definition.AllowedSurfaceIds);
            }
            var crate=definitions.Single(d=>d.StableId=="studentlife.snack.delivery.crate");
            var kitchen=definitions.Single(d=>d.StableId=="studentlife.toy.kitchen");
            CollectionAssert.AreEquivalent(new[]{Vector2Int.zero},crate.FootprintCells);
            CollectionAssert.AreEquivalent(new[]{Vector2Int.zero,Vector2Int.right},kitchen.FootprintCells);
            StringAssert.EndsWith("StudentLife_snack_delivery_crate_1x1.png",AssetDatabase.GetAssetPath(((Tile)crate.TileParts[0].Tile).sprite));
            StringAssert.EndsWith("StudentLife_toy_kitchen_2x1.png",AssetDatabase.GetAssetPath(((Tile)kitchen.TileParts[0].Tile).sprite));
        }

        [Test]
        public void Addressables_ExposeAllTwelveAssetsWithUniqueLocalAddressesAndLabel()
        {
            Assert.IsTrue(AssetDatabase.IsValidFolder(Art));Assert.IsTrue(AssetDatabase.IsValidFolder(Data));
            var settings=AddressableAssetSettingsDefaultObject.Settings;Assert.IsNotNull(settings);
            var paths=AssetDatabase.FindAssets("",new[]{Art,Data}).Select(AssetDatabase.GUIDToAssetPath).Where(p=>p.EndsWith(".png")||p.EndsWith(".asset")).ToArray();
            Assert.AreEqual(12,paths.Length,"Eight PNGs, two FurnitureDefinitions and two tiles expected.");
            foreach(var path in paths)
            {
                var entry=settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(path));Assert.IsNotNull(entry,path);
                Assert.IsTrue(entry.labels.Contains("StudentLifePixels"),path);
                string group=path.EndsWith(".png")?"Sprites":AssetDatabase.LoadAssetAtPath<FurnitureDefinition>(path)!=null?"Data":"Tiles";
                Assert.AreEqual(group,entry.parentGroup.Name,path);
                Assert.AreEqual(1,settings.groups.Where(g=>g!=null).SelectMany(g=>g.entries).Count(e=>e.address==entry.address),entry.address);
                var schema=entry.parentGroup.GetSchema<BundledAssetGroupSchema>();Assert.IsNotNull(schema);
                StringAssert.Contains("RuntimePath",schema.LoadPath.GetValue(settings),entry.address);
            }
            var registryEntry=settings.FindAssetEntry(AssetDatabase.AssetPathToGUID("Assets/Data/Registry/GameDataRegistry.asset"));
            Assert.IsNotNull(registryEntry);Assert.AreEqual("data/registry",registryEntry.address);
        }

        private static void CheckImporter(string path,Vector2Int size,Vector2 pivot)
        {
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;Assert.IsNotNull(importer,path);
            Assert.AreEqual(TextureImporterType.Sprite,importer.textureType);Assert.AreEqual(SpriteImportMode.Single,importer.spriteImportMode);
            Assert.AreEqual(48f,importer.spritePixelsPerUnit);Assert.AreEqual(FilterMode.Point,importer.filterMode);
            Assert.AreEqual(TextureImporterCompression.Uncompressed,importer.textureCompression);Assert.IsFalse(importer.mipmapEnabled);
            Assert.AreEqual(TextureImporterNPOTScale.None,importer.npotScale);
            importer.GetSourceTextureWidthAndHeight(out int w,out int h);Assert.AreEqual(size,new Vector2Int(w,h));
            var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);Assert.IsNotNull(sprite,path);
            Assert.AreEqual(new Vector2(w,h),sprite.rect.size);Assert.Less(Vector2.Distance(pivot,sprite.pivot),0.001f,path);
        }
    }
}
