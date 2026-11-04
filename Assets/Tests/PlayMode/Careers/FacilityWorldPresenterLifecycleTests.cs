using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using Rootborn.Game.Careers;
using Rootborn.UI.Careers;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Rootborn.Tests.PlayMode.Careers
{
    public sealed class FacilityWorldPresenterLifecycleTests
    {
        private readonly List<Object> _objects = new List<Object>();
        [UnityTearDown] public IEnumerator TearDown()
        {
            for (int i = _objects.Count - 1; i >= 0; i--) if (_objects[i] != null) Object.Destroy(_objects[i]);
            _objects.Clear(); yield return null;
        }

        [UnityTest] public IEnumerator FACILITY_WORLD_PLAY_001_NativeDisableAndDestroyReleaseOneLoadedAddressExactlyOnce()
        {
            Assert.That(Application.isPlaying, Is.True, "This gate verifies real Unity lifecycle delivery.");
            var facility = Make<FacilityDefinition>();
            var visuals = new FacilityVisualDefinition[3];
            for (int i = 0; i < visuals.Length; i++)
            {
                visuals[i] = Make<FacilityVisualDefinition>(); Set(visuals[i], "_facility", facility); Set(visuals[i], "_level", i + 1); Set(visuals[i], "_spriteAddress", "fixture/runtime-facility/" + (i + 1));
                Set(visuals[i], "_siteSize", new Vector2Int(3, 3)); Set(visuals[i], "_collisionCells", new[] { new Vector2Int(0, 1) });
                Set(visuals[i], "_entranceCells", new[] { new Vector2Int(1, 0) }); Set(visuals[i], "_approachCells", new[] { new Vector2Int(1, -1) });
            }
            var site = new GameObject("RuntimeFacilityLifecycle"); _objects.Add(site);
            var visual = Child(site, "Visual").AddComponent<SpriteRenderer>(); var door = Child(site, "Door").AddComponent<CareerFacilityInteractor>(); var collision = Child(site, "Collision").transform;
            var presenter = site.AddComponent<FacilityWorldPresenter>(); Set(presenter, "_facility", facility); Set(presenter, "_visuals", visuals); Set(presenter, "_spriteRenderer", visual); Set(presenter, "_door", door); Set(presenter, "_collisionRoot", collision);
            var texture = new Texture2D(4, 4); _objects.Add(texture); var sprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), Vector2.zero, 4); _objects.Add(sprite);
            var released = new List<string>(); int loads = 0;
            presenter.ConfigureSpriteLoading(address => { loads++; return Task.FromResult(sprite); }, address => released.Add(address));
            var binding = presenter.BindOwner(null); while (!binding.IsCompleted) yield return null;
            Assert.That(binding.Result, Is.True); Assert.That(visual.sprite, Is.SameAs(sprite)); Assert.That(loads, Is.EqualTo(1)); Assert.That(released, Is.Empty);
            yield return null;
            site.SetActive(false);
            CollectionAssert.AreEqual(new[] { visuals[0].SpriteAddress }, released, "Native OnDisable must release without a manual lifecycle call.");
            Object.Destroy(site); yield return null;
            Assert.That(released.Count, Is.EqualTo(1), "The later native OnDestroy cannot release the same handle twice.");
        }
        private T Make<T>() where T : ScriptableObject { var result = ScriptableObject.CreateInstance<T>(); _objects.Add(result); return result; }
        private static GameObject Child(GameObject root, string name) { var child = new GameObject(name); child.transform.SetParent(root.transform, false); return child; }
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
