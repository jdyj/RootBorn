using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Rootborn.Game.Player;
using Rootborn.Game.StudentLife;
using Rootborn.UI.StudentLife;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rootborn.Tests.EditMode.Player
{
    public sealed class ContrastPlainTarget : MonoBehaviour, IPlayerInteractable
    {
        public string InteractionPrompt => "[E] Plain target";
        public Vector3 InteractionPromptOffset => new Vector3(0f, 1.1f, 0f);
        public Transform InteractionTransform => transform;
        public bool CanInteract(GameObject player) => true;
        public bool TryInteract(GameObject player) => false;
    }

    public sealed class ExplainingTarget : MonoBehaviour, IPlayerInteractable, IPrioritizedPlayerInteractable, IPlayerInteractionFailureExplainer
    {
        public string InteractionPrompt => "[E] Explaining target";
        public Vector3 InteractionPromptOffset => Vector3.up;
        public Transform InteractionTransform => transform;
        public int InteractionPriority => 500;
        public bool CanInteract(GameObject player) => true;
        public bool TryInteract(GameObject player) => false;
        public string ExplainFailure(GameObject player) => "no time left";
    }

    public sealed class SchoolPromptAppearanceTests
    {
        const string ProviderName = "Rootborn.Game.Player.IPlayerInteractionPromptAppearance";
        const string EnabledField = "_overrideInteractionPromptColor";
        const string ColorField = "_interactionPromptColor";
        static readonly Color32 Navy = new Color32(38, 59, 70, 255);
        static readonly Color32 Default = new Color32(255, 245, 173, 255);
        readonly List<GameObject> _owned = new List<GameObject>();
        GameObject _actor, _boardObject, _plainObject;
        PlayerInteractionRouter _router;
        DailyPlanBoardInteractor _board;
        Camera _camera;

        GameObject Own(string name, params Type[] components)
        {
            var go = new GameObject("SchoolContrastFixture-" + name, components); _owned.Add(go); return go;
        }

        [SetUp]
        public void SetUpOwnedObjectsOnly()
        {
            _actor = Own("Actor", typeof(PlayerInteractionRouter)); _actor.transform.position = new Vector3(10000f, 10000f, 0f);
            _router = _actor.GetComponent<PlayerInteractionRouter>();
            var cameraObject = Own("Camera", typeof(Camera)); _camera = cameraObject.GetComponent<Camera>();
            _camera.orthographic = true; _camera.orthographicSize = 5.5f; _camera.pixelRect = new Rect(0, 0, 1470, 714);
            cameraObject.transform.position = _actor.transform.position + new Vector3(0, 0, -10);
            // Use only this fixture camera. Do not retag or disable the user's MainCamera.
            typeof(PlayerInteractionRouter).GetField("_promptCamera", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_router, _camera);
            _boardObject = Own("Board", typeof(BoxCollider2D), typeof(DailyPlanBoardInteractor));
            _boardObject.transform.position = _actor.transform.position + Vector3.right * .5f;
            _boardObject.GetComponent<BoxCollider2D>().isTrigger = true;
            _board = _boardObject.GetComponent<DailyPlanBoardInteractor>();
            var panel = Own("Panel", typeof(DailyPlanPanel)).GetComponent<DailyPlanPanel>();
            _board.Bind(panel, Array.Empty<DailyPlanRouteSetDefinition>(), null, null);
            _plainObject = Own("Plain", typeof(BoxCollider2D), typeof(ContrastPlainTarget));
            _plainObject.transform.position = _boardObject.transform.position;
            _plainObject.GetComponent<BoxCollider2D>().isTrigger = true; _plainObject.SetActive(false);
            Physics2D.SyncTransforms();
        }

        [TearDown]
        public void DestroyOnlyOwnedObjects()
        {
            for (int i = _owned.Count - 1; i >= 0; i--) if (_owned[i] != null) Object.DestroyImmediate(_owned[i]);
            _owned.Clear();
        }

        static Type Provider()
        {
            var type = typeof(PlayerInteractionRouter).Assembly.GetType(ProviderName, false);
            Assert.That(type, Is.Not.Null, "Optional appearance API is absent: expected pre-implementation RED."); return type;
        }
        static bool ColorFromProvider(DailyPlanBoardInteractor board, out Color color)
        {
            var type = Provider(); Assert.That(type.IsInstanceOfType(board), Is.True);
            var method = type.GetMethod("TryGetInteractionPromptColor", new[] { typeof(Color).MakeByRefType() });
            Assert.That(method, Is.Not.Null); Assert.That(method.ReturnType, Is.EqualTo(typeof(bool)));
            object[] args = { default(Color) }; var value = method.Invoke(board, args); color = (Color)args[0]; return (bool)value;
        }
        static void Configure(DailyPlanBoardInteractor board, bool enabled, Color color)
        {
            var serialized = new SerializedObject(board); var flag = serialized.FindProperty(EnabledField); var ink = serialized.FindProperty(ColorField);
            Assert.That(flag, Is.Not.Null, "Missing optional per-instance color override; expected RED.");
            Assert.That(ink, Is.Not.Null, "Missing authored prompt color; expected RED.");
            flag.boolValue = enabled; ink.colorValue = color; serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        [Test]
        public void FailedInteraction_ShowsTheTargetsExplanationInThePrompt()
        {
            _plainObject.SetActive(false);
            var explainerObject = Own("Explainer", typeof(BoxCollider2D), typeof(ExplainingTarget));
            explainerObject.transform.position = _actor.transform.position + Vector3.right * .1f;
            explainerObject.GetComponent<BoxCollider2D>().isTrigger = true;
            Physics2D.SyncTransforms();
            Assert.That(_router.TryInteractWithNearest(), Is.False);
            Assert.That(_router.PromptText, Is.EqualTo("no time left"));
            _router.RefreshPromptNow();
            Assert.That(_router.PromptText, Is.EqualTo("no time left"), "The explanation stays long enough to be read.");
        }

        TextMesh Refresh()
        {
            Physics2D.SyncTransforms(); _router.RefreshPromptNow();
            Assert.That(_router.PromptVisible, Is.True); var text = _actor.GetComponentInChildren<TextMesh>(); Assert.That(text, Is.Not.Null); return text;
        }
        static void Rgba(Color actual, Color32 expected)
        {
            Color32 bytes = actual; Assert.That(bytes.r, Is.EqualTo(expected.r)); Assert.That(bytes.g, Is.EqualTo(expected.g));
            Assert.That(bytes.b, Is.EqualTo(expected.b)); Assert.That(bytes.a, Is.EqualTo(expected.a));
        }

        [Test]
        public void OptionalProviderDoesNotChangeTheRequiredInteractableContract()
        {
            var type = Provider(); Assert.That(type.IsInterface && type.IsPublic, Is.True);
            Assert.That(typeof(IPlayerInteractable).IsAssignableFrom(type), Is.False, "Appearance stays optional for existing interactables.");
            Assert.That(type.GetMethods().Length, Is.EqualTo(1));
            var method = type.GetMethod("TryGetInteractionPromptColor", new[] { typeof(Color).MakeByRefType() });
            Assert.That(method, Is.Not.Null); Assert.That(method.ReturnType, Is.EqualTo(typeof(bool)));
            Assert.That(method.GetParameters()[0].IsOut, Is.True);
        }

        [Test]
        public void UnconfiguredBoardDeclinesAppearanceAndKeepsExistingDefaults()
        {
            Color ignored; Assert.That(ColorFromProvider(_board, out ignored), Is.False);
            var flag = new SerializedObject(_board).FindProperty(EnabledField); Assert.That(flag, Is.Not.Null); Assert.That(flag.boolValue, Is.False);
            Rgba(Refresh().color, Default);
            Assert.That(_board.InteractionPromptOffset, Is.EqualTo(new Vector3(0, 1.1f, 0)));
            Assert.That(_board.InteractionPriority, Is.EqualTo(80)); Assert.That(_board.InteractionPrompt, Is.EqualTo("[E] Plan Day"));
        }

        [Test]
        public void EnabledBoardRendersAuthoredOpaqueNavy()
        {
            Configure(_board, true, Navy); Color color; Assert.That(ColorFromProvider(_board, out color), Is.True); Rgba(color, Navy);
            Rgba(Refresh().color, Navy); Assert.That(_router.CurrentInteractable, Is.SameAs(_board));
        }

        [Test]
        public void SameVisibleTargetResolvesChangedColorOnEveryRefresh()
        {
            Configure(_board, true, Navy); var first = Refresh(); Rgba(first.color, Navy);
            var replacement = new Color32(54, 70, 82, 255); Configure(_board, true, replacement);
            var next = Refresh(); Assert.That(next, Is.SameAs(first)); Rgba(next.color, replacement);
        }

        [Test]
        public void DisablingOverrideOnTheSameTargetRestoresDefaultWithoutLeak()
        {
            Configure(_board, true, Navy); Rgba(Refresh().color, Navy);
            Configure(_board, false, Navy); Color ignored; Assert.That(ColorFromProvider(_board, out ignored), Is.False);
            Rgba(Refresh().color, Default);
        }

        [Test]
        public void SwitchingToPlainTargetAndReopeningNeverLeaksBoardColor()
        {
            Configure(_board, true, Navy); var text = Refresh(); Rgba(text.color, Navy);
            _boardObject.SetActive(false); _plainObject.SetActive(true); Rgba(Refresh().color, Default);
            Assert.That(_router.CurrentInteractable, Is.SameAs(_plainObject.GetComponent<ContrastPlainTarget>()));
            _plainObject.SetActive(false); Physics2D.SyncTransforms(); _router.RefreshPromptNow(); Assert.That(_router.PromptVisible, Is.False);
            _plainObject.SetActive(true); Assert.That(Refresh(), Is.SameAs(text)); Rgba(text.color, Default);
        }

        [Test]
        public void EnabledProviderPreservesValidAuthoredAlpha()
        {
            var translucent = new Color32(38, 59, 70, 128); Configure(_board, true, translucent);
            Color color; Assert.That(ColorFromProvider(_board, out color), Is.True); Rgba(color, translucent); Rgba(Refresh().color, translucent);
        }

        [Test]
        public void AppearanceOnlyChangeKeepsPromptGeometryFontSortingAndOffset()
        {
            var before = Refresh(); var renderer = before.GetComponent<MeshRenderer>(); var position = before.transform.position;
            var scale = before.transform.localScale; var bounds = renderer.bounds; var font = before.font; int fontSize = before.fontSize;
            float characterSize = before.characterSize; int order = renderer.sortingOrder; int layer = renderer.sortingLayerID;
            Configure(_board, true, Navy); var after = Refresh(); Rgba(after.color, Navy);
            Assert.That(Vector3.Distance(after.transform.position, position), Is.LessThan(.0001f));
            Assert.That(Vector3.Distance(after.transform.localScale, scale), Is.LessThan(.0001f));
            Assert.That(Vector3.Distance(renderer.bounds.center, bounds.center), Is.LessThan(.002f));
            Assert.That(Vector3.Distance(renderer.bounds.size, bounds.size), Is.LessThan(.002f));
            Assert.That(after.font == font, Is.True); Assert.That(after.fontSize, Is.EqualTo(fontSize)); Assert.That(after.characterSize, Is.EqualTo(characterSize));
            Assert.That(renderer.sortingOrder, Is.EqualTo(order)); Assert.That(renderer.sortingLayerID, Is.EqualTo(layer));
            Assert.That(_board.InteractionPromptOffset, Is.EqualTo(new Vector3(0, 1.1f, 0)));
        }

        [Test]
        public void SavedSchoolAloneAuthorsOpaqueNavyWhileHouseTownAndOffsetsRemainExact()
        {
            string school = File.ReadAllText("Assets/Scenes/School.unity").Replace("\r\n", "\n");
            var body = Regex.Match(school, @"(?ms)^--- !u!114 &224293929\n.*?(?=^--- !u!|\z)"); Assert.That(body.Success, Is.True);
            Assert.That(Regex.Matches(school, @"(?m)^  " + EnabledField + ": 1$").Count, Is.EqualTo(1));
            Assert.That(body.Value, Does.Contain("  " + EnabledField + ": 1\n"));
            var ink = Regex.Match(body.Value, @"(?m)^  " + ColorField + @": \{r: ([^,]+), g: ([^,]+), b: ([^,]+), a: ([^}]+)\}$");
            Assert.That(ink.Success, Is.True, "School must persist its explicit appearance");
            Rgba(new Color(Parse(ink.Groups[1].Value), Parse(ink.Groups[2].Value), Parse(ink.Groups[3].Value), Parse(ink.Groups[4].Value)), Navy);
            Assert.That(body.Value, Does.Contain("_interactionPromptOffset: {x: 0, y: 1.6, z: 0}"));
            string house = File.ReadAllText("Assets/Scenes/House.unity"); Assert.That(house, Does.Contain("_interactionPromptOffset: {x: 0, y: 1.7, z: 0}"));
            foreach (string path in new[] { "Assets/Scenes/House.unity", "Assets/Scenes/Town.unity" })
            {
                // A scene re-save may write the default (override off) values out; what must hold is
                // that House and Town never turn the color override on.
                string source = File.ReadAllText(path);
                Assert.That(Regex.IsMatch(source, @"(?m)^  " + EnabledField + @": [^0\s]"), Is.False, path + " must not override the prompt color.");
            }
        }

        [Test]
        public void PlainInteractableWithoutAppearanceProviderKeepsOriginalPaleColor()
        {
            _boardObject.SetActive(false); _plainObject.SetActive(true); Rgba(Refresh().color, Default);
            Assert.That(_router.PromptText, Is.EqualTo("[E] Plain target"));
        }
        static float Parse(string value) => float.Parse(value, CultureInfo.InvariantCulture);
    }
}
