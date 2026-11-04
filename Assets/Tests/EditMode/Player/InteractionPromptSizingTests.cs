using System.Collections.Generic;
using NUnit.Framework;
using Rootborn.Game.Player;
using UnityEngine;

namespace Rootborn.Tests.EditMode.Player
{
    public sealed class PromptSizingTarget : MonoBehaviour, IPlayerInteractable
    {
        public string Label = "[E] Read book";
        public string InteractionPrompt => Label;
        public Vector3 InteractionPromptOffset => Vector3.up;
        public Transform InteractionTransform => transform;
        public bool CanInteract(GameObject player) => true;
        public bool TryInteract(GameObject player) => true;
    }

    public sealed class InteractionPromptSizingTests
    {
        private GameObject _player, _target, _cameraObject;
        private Camera _camera;
        private readonly List<GameObject> _oldMain = new List<GameObject>();
        [SetUp] public void SetUp()
        {
            foreach (var go in GameObject.FindGameObjectsWithTag("MainCamera")) { _oldMain.Add(go); go.tag = "Untagged"; }
            _cameraObject = new GameObject("PromptTestCamera", typeof(Camera));
            _cameraObject.tag = "MainCamera";
            _camera = _cameraObject.GetComponent<Camera>();
            _camera.orthographic = true; _camera.orthographicSize = 4;
            _camera.pixelRect = new Rect(0, 0, 1280, 720);
            _camera.transform.position = new Vector3(1000, 1000, -10);
            _player = new GameObject("PromptPlayer", typeof(PlayerInteractionRouter));
            _player.transform.position = new Vector3(1000, 1000, 0);
            _target = new GameObject("PromptTarget", typeof(BoxCollider2D), typeof(PromptSizingTarget));
            _target.transform.position = _player.transform.position + Vector3.right * .5f;
            _target.GetComponent<BoxCollider2D>().isTrigger = true;
            Physics2D.SyncTransforms();
        }
        [TearDown] public void TearDown()
        {
            Object.DestroyImmediate(_player); Object.DestroyImmediate(_target); Object.DestroyImmediate(_cameraObject);
            foreach (var go in _oldMain) if(go != null) go.tag = "MainCamera";
            _oldMain.Clear();
        }
        private Vector2 PixelSize()
        {
            _player.GetComponent<PlayerInteractionRouter>().RefreshPromptNow();
            var bounds = _player.GetComponentInChildren<TextMesh>().GetComponent<MeshRenderer>().bounds;
            return (Vector2)(_camera.WorldToScreenPoint(bounds.max) - _camera.WorldToScreenPoint(bounds.min));
        }
        [Test] public void Prompt_StaysReadableAcrossHouseAndTownZoom()
        {
            var close = PixelSize(); _camera.orthographicSize = 9; var far = PixelSize();
            Assert.That(close.y, Is.InRange(20f, 26f));
            Assert.That(far.y, Is.EqualTo(close.y).Within(1f));
        }
        [Test] public void Prompt_DoesNotInheritActorVisualScale()
        {
            var normal = PixelSize(); _player.transform.localScale = Vector3.one * 2; var scaled = PixelSize();
            Assert.That(scaled.y, Is.EqualTo(normal.y).Within(1f));
        }
        [Test] public void Prompt_LongLabelFitsViewport()
        {
            _target.GetComponent<PromptSizingTarget>().Label = new string('W', 90);
            Assert.That(PixelSize().x, Is.LessThanOrEqualTo(361f));
        }
    }
}
