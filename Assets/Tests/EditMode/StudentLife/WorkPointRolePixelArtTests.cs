using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Game.Interiors;
using Rootborn.Game.StudentLife;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Rootborn.Tests.EditMode.UI.StudentLife
{
    public sealed class WorkPointRolePixelArtTests
    {
        private const string TownPath = "Assets/Scenes/Town.unity";
        private const string CounterPath = "Assets/Art/UrbanStreet/Buildings31/store_cashier_counter.png";
        private const string BenchPath = "Assets/Art/UrbanStreet/Buildings31/workshop_bench.png";
        private const string TeacherDeskPath = "Assets/Art/SchoolPixels/sprites/teacher_desk.png";
        private const float VisualOffsetY = 0.08333337f;

        [SetUp]
        public void RequireSavedEditorScenes()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
                Assert.That(SceneManager.GetSceneAt(i).isDirty, Is.False, "Never discard an unsaved editor scene for art tests.");
            EditorSceneManager.OpenScene("Assets/Scenes/Boot.unity", OpenSceneMode.Single);
        }

        [TestCase(CounterPath, 64, "sprites/town/comfy31/store-cashier-counter", true)]
        [TestCase(BenchPath, 96, "sprites/town/comfy31/workshop-bench", false)]
        public void WorkSpritePreservesNativeCanvasFloorPivotCrispPixelsAndStableAddress(
            string path, int height, string address, bool requiresTwoPixelClusters)
        {
            var sprite = RequiredAsset<Sprite>(path);
            Assert.That(sprite.rect, Is.EqualTo(new Rect(0, 0, 96, height)));
            Assert.That(sprite.pixelsPerUnit, Is.EqualTo(48f));
            Assert.That(sprite.pivot, Is.EqualTo(new Vector2(48f, 4f)));
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
            Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single));
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            Assert.That(importer.npotScale, Is.EqualTo(TextureImporterNPOTScale.None));
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            Assert.That(settings.spriteMeshType, Is.EqualTo(SpriteMeshType.FullRect));
            AssertAddress(path, address);

            var raw = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.That(raw.LoadImage(File.ReadAllBytes(path)), Is.True);
                Assert.That(new Vector2Int(raw.width, raw.height), Is.EqualTo(new Vector2Int(96, height)));
                var pixels = raw.GetPixels32();
                Assert.That(pixels.All(p => p.a == 0 || p.a == 255), Is.True, "Pixel art must have binary alpha.");
                Assert.That(new HashSet<uint>(pixels.Where(p => p.a != 0).Select(ColorKey)).Count, Is.InRange(1, 32));
                var visible = Enumerable.Range(0, pixels.Length).Where(i => pixels[i].a != 0).ToArray();
                Assert.That(visible, Is.Not.Empty);
                int left = visible.Min(i => i % raw.width), right = visible.Max(i => i % raw.width);
                int bottom = visible.Min(i => i / raw.width), top = visible.Max(i => i / raw.width);
                Assert.That(left, Is.GreaterThanOrEqualTo(4));
                Assert.That(right, Is.LessThanOrEqualTo(91));
                Assert.That(right - left + 1, Is.LessThanOrEqualTo(88));
                Assert.That(top - bottom + 1, Is.LessThanOrEqualTo(56));
                Assert.That(bottom, Is.EqualTo(4), "Four transparent bottom rows preserve the existing visible foot line.");
                // The accepted reused bench is native one-pixel art. Only the new counter has the two-pixel contract.
                if (requiresTwoPixelClusters)
                    for (int y = 0; y < raw.height; y += 2)
                        for (int x = 0; x < raw.width; x += 2)
                        {
                            uint color = ColorKey(pixels[y * raw.width + x]);
                            Assert.That(new[] { ColorKey(pixels[y * raw.width + x + 1]),
                                ColorKey(pixels[(y + 1) * raw.width + x]), ColorKey(pixels[(y + 1) * raw.width + x + 1]) },
                                Is.All.EqualTo(color), "Nonuniform 2x2 cluster at " + x + "," + y);
                        }
            }
            finally { Object.DestroyImmediate(raw); }
        }

        [TestCase("PartTimeWork_work_shift_corner_store", 54f, CounterPath,
            "Assets/Data/StudentLife/PartTimeWork/PartTimeWork_CornerStoreShift.asset",
            "119069712aee6c745ae6c6ebebc67e3c", "work.shift.corner-store")]
        [TestCase("PartTimeWork_work_workshop_helper", 74f, BenchPath,
            "Assets/Data/StudentLife/PartTimeWork/PartTimeWork_WorkshopHelper.asset",
            "b7131be6fdb8cc440b07ec16f5bfccfd", "work.workshop-helper")]
        public void SavedWorkPointUsesItsRoleSpriteWithoutMovingOrReplacingTheWorkInteraction(
            string ownerName, float x, string spritePath, string workPath, string workGuid, string workId)
        {
            var scene = EditorSceneManager.OpenPreviewScene(TownPath);
            try
            {
                var root = AuthoredPoint(scene, ownerName);
                var renderer = AssertPointGeometry(root, new Vector3(x, 2f, 0f), 9904);
                Assert.That(renderer.sprite, Is.SameAs(RequiredAsset<Sprite>(spritePath)));
                Assert.That(renderer.transform.position.y + (4f - renderer.sprite.pivot.y) / renderer.sprite.pixelsPerUnit,
                    Is.EqualTo(2f + VisualOffsetY).Within(0.000001f), "Visible foot line must stay at the existing world height.");
                var interactor = root.GetComponent<PartTimeWorkInteractor>();
                Assert.That(interactor, Is.Not.Null);
                Assert.That(interactor.enabled, Is.True);
                Assert.That(root.GetComponentsInChildren<PartTimeWorkInteractor>(true), Has.Length.EqualTo(1));
                Assert.That(interactor.Work, Is.SameAs(RequiredAsset<PartTimeWorkDefinition>(workPath)));
                Assert.That(AssetDatabase.AssetPathToGUID(workPath), Is.EqualTo(workGuid));
                Assert.That(interactor.Work.Id, Is.EqualTo(workId));
                Assert.That(root.GetComponent<LocationVisitInteractor>(), Is.Null, "A work point must not become a visit point.");
                Assert.That(root.GetComponent<LocationActivityInteractor>(), Is.Null);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void ExistingWorkshopVisitRetainsItsBenchAndSeparateLocationActivityBindings()
        {
            var scene = EditorSceneManager.OpenPreviewScene(TownPath);
            try
            {
                var root = AuthoredPoint(scene, "LocationVisit_location_workshop");
                var renderer = AssertPointGeometry(root, new Vector3(74f, 7f, 0f), 9664);
                Assert.That(renderer.sprite, Is.SameAs(RequiredAsset<Sprite>(BenchPath)));
                var visit = root.GetComponent<LocationVisitInteractor>();
                var activity = root.GetComponent<LocationActivityInteractor>();
                Assert.That(visit, Is.Not.Null);
                Assert.That(activity, Is.Not.Null);
                Assert.That(visit.enabled && activity.enabled, Is.True);
                Assert.That(visit.Location, Is.SameAs(RequiredAsset<LocationDefinition>(
                    "Assets/Data/StudentLife/Locations/Location_Workshop.asset")));
                Assert.That(activity.Identity, Is.SameAs(RequiredAsset<LocationIdentityDefinition>(
                    "Assets/Data/StudentLife/LocationIdentities/LocationIdentity_Workshop.asset")));
                Assert.That(root.GetComponent<PartTimeWorkInteractor>(), Is.Null, "The visit point remains separate from paid work.");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void OriginalSchoolTeacherDeskKeepsItsGuidCanvasPivotAndAddress()
        {
            Assert.That(AssetDatabase.AssetPathToGUID(TeacherDeskPath), Is.EqualTo("0edc5bd1f33d677429a9b54314f8bc62"));
            var original = RequiredAsset<Sprite>(TeacherDeskPath);
            Assert.That(original.name, Is.EqualTo("teacher_desk"));
            Assert.That(original.rect, Is.EqualTo(new Rect(0, 0, 96, 64)));
            Assert.That(original.pixelsPerUnit, Is.EqualTo(48f));
            Assert.That(original.pivot, Is.EqualTo(new Vector2(48f, 4f)));
            AssertAddress(TeacherDeskPath, "sprites/school/sprites/teacher-desk");
            // Exact source PNG/meta preservation belongs to the before/after authoring hash evidence, not this fixture.
        }

        private static Transform AuthoredPoint(Scene scene, string ownerName)
        {
            var parent = scene.GetRootGameObjects().Single(g => g.name == "AuthoredTownBindings").transform;
            Assert.That(parent.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(parent.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(parent.localScale, Is.EqualTo(Vector3.one));
            var root = parent.Cast<Transform>().Single(t => t.name == ownerName);
            Assert.That(root.gameObject.activeSelf, Is.True);
            return root;
        }

        private static SpriteRenderer AssertPointGeometry(Transform root, Vector3 position, int sortingOrder)
        {
            Assert.That(root.localPosition, Is.EqualTo(position));
            Assert.That(root.position, Is.EqualTo(position));
            Assert.That(root.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(root.localScale, Is.EqualTo(Vector3.one));
            Assert.That(root.childCount, Is.EqualTo(1));
            var visual = root.GetChild(0);
            Assert.That(visual.name, Is.EqualTo("Visual"));
            Assert.That(visual.childCount, Is.Zero);
            Assert.That(visual.gameObject.activeSelf, Is.True);
            Assert.That(visual.localPosition.x, Is.Zero);
            Assert.That(visual.localPosition.y, Is.EqualTo(VisualOffsetY).Within(0.000001f));
            Assert.That(visual.localPosition.z, Is.Zero);
            Assert.That(visual.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(visual.localScale, Is.EqualTo(Vector3.one));
            var renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
            Assert.That(renderers, Has.Length.EqualTo(1));
            var renderer = renderers[0];
            Assert.That(renderer.transform, Is.SameAs(visual));
            Assert.That(renderer.enabled, Is.True);
            Assert.That(renderer.drawMode, Is.EqualTo(SpriteDrawMode.Simple));
            Assert.That(renderer.sortingLayerID, Is.Zero);
            Assert.That(renderer.sortingOrder, Is.Zero);
            Assert.That(renderer.spriteSortPoint, Is.EqualTo(SpriteSortPoint.Center));
            Assert.That(renderer.color, Is.EqualTo(Color.white));
            Assert.That(renderer.flipX || renderer.flipY, Is.False);
            var group = root.GetComponent<SortingGroup>();
            Assert.That(group, Is.Not.Null);
            Assert.That(group.enabled, Is.True);
            Assert.That(group.sortingLayerID, Is.Zero);
            Assert.That(group.sortingOrder, Is.EqualTo(sortingOrder));
            Assert.That(renderer.GetComponentsInParent<SortingGroup>(true), Has.Length.EqualTo(1));
            var depth = root.GetComponent<GroundDepthSort>();
            Assert.That(depth, Is.Not.Null);
            Assert.That(depth.enabled, Is.True);
            var depthData = new SerializedObject(depth);
            Assert.That(depthData.FindProperty("_footPointOffset").vector2Value, Is.EqualTo(Vector2.zero));
            Assert.That(depthData.FindProperty("_unitsPerWorld").intValue, Is.EqualTo(48));
            Assert.That(depthData.FindProperty("_baseSortingOrder").intValue, Is.EqualTo(10000));
            var colliders = root.GetComponentsInChildren<Collider2D>(true);
            Assert.That(colliders, Has.Length.EqualTo(1), "Do not add a solid body or another interaction trigger.");
            Assert.That(colliders[0], Is.TypeOf<BoxCollider2D>());
            var box = (BoxCollider2D)colliders[0];
            Assert.That(box.transform, Is.SameAs(root));
            Assert.That(box.enabled && box.isTrigger, Is.True);
            Assert.That(box.offset, Is.EqualTo(Vector2.zero));
            Assert.That(box.size, Is.EqualTo(new Vector2(0.7f, 0.7f)));
            return renderer;
        }

        private static T RequiredAsset<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.That(asset, Is.Not.Null, "Required authored asset missing: " + path);
            return asset;
        }

        private static void AssertAddress(string path, string address)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            Assert.That(settings, Is.Not.Null);
            var entry = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(path));
            Assert.That(entry, Is.Not.Null, path);
            Assert.That(entry.address, Is.EqualTo(address));
        }

        private static uint ColorKey(Color32 p) => p.a == 0 ? 0u : ((uint)p.a << 24) | ((uint)p.r << 16) | ((uint)p.g << 8) | p.b;
    }
}
