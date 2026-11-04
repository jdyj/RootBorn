using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Localization;
using Rootborn.Game.StudentLife;
using Rootborn.UI.StudentLife;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Rootborn.Tests.EditMode.UI.StudentLife
{
    // Only existing public UI/domain APIs are referenced: this fixture compiles before the new helper.
    public sealed class ExplorationTextLocalizationTests
    {
        private readonly List<Object> _owned = new List<Object>();
        private readonly Dictionary<FieldInfo, object> _statics = new Dictionary<FieldInfo, object>();
        private GameDataRegistry _registry;
        private ExplorationInteractionDefinition _definition;
        private StudentLifeProgress _student;
        private ExplorationProgress _progress;
        private Inventory _inventory;
        private ExplorationChoicePanel _panel;
        private readonly string[] _ids = { "choice.test.inspect", "choice.test.careful", "choice.test.discovery", "choice.test.leave" };

        [SetUp]
        public void SetUp()
        {
            Snapshot(typeof(GameText), "_language");
            foreach (string name in new[] { "s_provider", "s_overrideRegistry", "s_cachedRegistry" }) Snapshot(typeof(GameDataRegistryProvider), name);
            var source = AssetDatabase.LoadAssetAtPath<GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset");
            Assert.That(source, Is.Not.Null);
            _registry = Own(Object.Instantiate(source));
            GameDataRegistryProvider.SetOverrideForTests(_registry);
            _definition = AssetDatabase.LoadAssetAtPath<ExplorationInteractionDefinition>("Assets/Data/StudentLife/ExplorationInteractions/Exploration_TestAlleyCrate.asset");
            Assert.That(_definition, Is.Not.Null);
            _student = new StudentLifeProgress("localization-fixture", "localization-player", 12, 10, 0, 480);
            _progress = new ExplorationProgress("localization-fixture", "localization-player");
            _inventory = new Inventory();
            var root = Own(new GameObject("OwnedExplorationLocalizationCanvas", typeof(RectTransform), typeof(Canvas)));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            _panel = ExplorationChoicePanel.EnsureInScene(root.GetComponent<Canvas>());
            Assert.That(_panel != null && _panel.transform.IsChildOf(root.transform), Is.True, "Never Open a host/foreign panel.");
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                if (_panel != null) _panel.Close();
                for (int i = _owned.Count - 1; i >= 0; i--) if (_owned[i] != null) Object.DestroyImmediate(_owned[i]);
                _owned.Clear();
            }
            finally { foreach (var entry in _statics) entry.Key.SetValue(null, entry.Value); _statics.Clear(); }
        }

        [Test]
        public void CatalogAppendsExactlyReviewed38AndPreservesIndependent555And383Baselines()
        {
            var all = Rows(_registry.TextCatalog);
            Assert.That(all.Select(r => r.Key).Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(all.Length));
            // Wardrobe text is the one reviewed addition after this baseline. It sits behind the protected rows.
            Assert.That(all.Skip(593).Select(r => r.Key).Where(k => !k.StartsWith("ui.wardrobe.", StringComparison.Ordinal)), Is.Empty);
            var rows = all.Take(593).ToArray();
            Assert.That(rows.Length, Is.EqualTo(593));
            Assert.That(Hash(string.Join("\n", rows.Take(555).Select(r => r.Key + "\t" + r.Ko + "\t" + r.En))),
                Is.EqualTo("418C9CDA813794BAD4E8615B156085ADBE989B6BCE3483E88DCC34E660FE9A45"), "Every original555 ordered key/KO/EN value is protected.");
            var appended = rows.Skip(555).OrderBy(r => r.Key, StringComparer.Ordinal).ToArray();
            Assert.That(Hash(string.Join("\n", appended.Select(r => r.Key + "\t" + r.Ko + "\t" + r.En))),
                Is.EqualTo("E3BD82722F7E435D5FB84CC47C47FEA93A978949C64ED2C595910BC19445555A"), "Reviewed38 entries, including 체력 wording, must match exactly.");
            foreach (string language in new[] { "ko", "en" })
            {
                var json = FlatStrings(File.ReadAllText("Assets/Localization/" + language + ".json", Encoding.UTF8));
                Assert.That(json.Count, Is.EqualTo(421));
                foreach (var row in appended) Assert.That(json[row.Key], Is.EqualTo(language == "ko" ? row.Ko : row.En));
                var addedKeys = new HashSet<string>(appended.Select(r => r.Key), StringComparer.Ordinal);
                var original = json.Where(r => !addedKeys.Contains(r.Key)).OrderBy(r => r.Key, StringComparer.Ordinal).ToArray();
                Assert.That(original.Length, Is.EqualTo(383), "Do not backfill172 historical JSON omissions.");
                string expected = language == "ko" ? "0FDF73240E0B9B3A00B05199893308BF552A1E7F519E02FDE99BA5DFA41D0262" : "94949D13F4DC16F76F12B3C1BB4D985B697D3CC4847E997892BCDE0FAA21B85A";
                Assert.That(Hash(string.Join("\n", original.Select(r => r.Key + "\t" + r.Value))), Is.EqualTo(expected));
            }
        }

        [Test]
        public void CurrentFourDefinitionsRetainActualFocusWoodChefDiscoveryAndDismissalSemantics()
        {
            Assert.That(_definition.Choices.Select(c => c.Id), Is.EqualTo(_ids));
            Assert.That(_definition.DisplayNameKey, Is.EqualTo("Exploration Test Alley Crate"), "Catalog compatibility alias must not rewrite the SO.");
            var choices = _definition.Choices;
            Assert.That(choices[0].Outcomes.Select(o => o.GetType()), Is.EqualTo(new[] { typeof(ExplorationItemGrantOutcome), typeof(ExplorationTraitDeltaOutcome) }));
            var grant = (ExplorationItemGrantOutcome)choices[0].Outcomes[0];
            Assert.That(grant.Item.Id, Is.EqualTo("Wood")); Assert.That(grant.Count, Is.EqualTo(1));
            Assert.That(_registry.Items.Any(item => item != null && item.Id == "Wood" && item.DisplayKey == "item.wood"), Is.True);
            Assert.That(choices[1].Conditions.Count, Is.EqualTo(1));
            var condition = new SerializedObject(choices[1].Conditions[0]);
            Assert.That(condition.FindProperty("_minimum").intValue, Is.EqualTo(1));
            Assert.That(((TraitDefinition)condition.FindProperty("_trait").objectReferenceValue).Id, Is.EqualTo("trait.focus"));
            Assert.That(choices[1].Outcomes.Select(o => o.GetType()), Is.EqualTo(new[] { typeof(ExplorationCareerHintOutcome), typeof(ExplorationTraitDeltaOutcome) }), "Careful grants no wood.");
            foreach (var outcome in new[] { choices[0].Outcomes[1], choices[1].Outcomes[1] })
            {
                Assert.That(ReferencedId(outcome, "_trait"), Is.EqualTo("trait.focus"));
                Assert.That(new SerializedObject(outcome).FindProperty("_delta").intValue, Is.EqualTo(1));
            }
            var career = new SerializedObject(choices[1].Outcomes[0]).FindProperty("_career").objectReferenceValue as CareerDefinition;
            Assert.That(career.Id, Is.EqualTo("career.chef"));
            Assert.That(choices[2].Outcomes.Select(o => o.GetType()), Is.EqualTo(new[] { typeof(ExplorationClueOutcome), typeof(ExplorationEncyclopediaOutcome), typeof(ExplorationFollowUpQuestOutcome) }));
            Assert.That(ReferencedId(choices[2].Outcomes[0], "_clue"), Is.EqualTo("clue.discovery.library-archive-rumor"));
            Assert.That(ReferencedId(choices[2].Outcomes[1], "_entry"), Is.EqualTo("ency.location.first-discovery"));
            Assert.That(ReferencedId(choices[2].Outcomes[2], "_quest"), Is.EqualTo("quest.gatherWood"));
            foreach (var choice in choices.Take(3))
            {
                Assert.That(choice.RiskPolicy.EnergyCost, Is.EqualTo(1)); Assert.That(choice.RiskPolicy.FocusCost, Is.Zero); Assert.That(choice.RiskPolicy.StressDelta, Is.EqualTo(1));
                Assert.That(choice.DismissesInteraction, Is.False);
            }
            Assert.That(choices[3].DismissesInteraction, Is.True); Assert.That(choices[3].Outcomes, Is.Empty); Assert.That(choices[3].RiskPolicy, Is.Null);
        }

        [TestCase("ko")]
        [TestCase("en")]
        public void ActualPanelLocalizesFourChoicesAndFocusLockWithoutChangingSourceProgressOrCallbacks(string language)
        {
            GameText.Language = language;
            string sourceBefore = SourceSnapshot(); string progressBefore = StateSnapshot();
            _panel.Open(Model(), _progress, id => Assert.Fail("Rendering cannot execute a choice."));
            Assert.That(Text("ExplorationTitle").text, Is.EqualTo(language == "ko" ? "골목의 낡은 상자" : "Old alley crate"));
            string[] names = language == "ko" ? new[] { "살펴보기", "조심히 조사하기", "흔적 기록하기", "그만두기" } : new[] { "Inspect", "Look closely", "Record findings", "Leave" };
            var buttons = Choices();
            Assert.That(buttons.Length, Is.EqualTo(4));
            for (int i = 0; i < buttons.Length; i++)
            {
                string label = buttons[i].GetComponentInChildren<Text>().text;
                Assert.That(label, Does.StartWith(names[i] + " — "));
                NoInternals(label); Fits(buttons[i].GetComponentInChildren<Text>());
            }
            Assert.That(buttons[1].interactable, Is.False);
            Assert.That(buttons[1].GetComponentInChildren<Text>().text, Does.EndWith(language == "ko" ? "집중력 1 필요" : "Focus 1 required"));
            Assert.That(StateSnapshot(), Is.EqualTo(progressBefore)); Assert.That(SourceSnapshot(), Is.EqualTo(sourceBefore));
            var focus = AssetDatabase.LoadAssetAtPath<TraitDefinition>("Assets/Data/StudentLife/CareerPractices/Traits/Trait_Focus.asset");
            _student.AddTrait(focus, 1); // Fixture preparation, not a presentation effect.
            progressBefore = StateSnapshot(); var received = new List<string>();
            _panel.Open(Model(), _progress, id => received.Add(id));
            buttons = Choices(); Assert.That(buttons.All(b => b.interactable), Is.True);
            foreach (var button in buttons) Fits(button.GetComponentInChildren<Text>());
            foreach (var button in buttons) button.onClick.Invoke();
            Assert.That(received, Is.EqualTo(_ids), "Callbacks retain original IDs/order and fire once; fake callbacks do not run the domain.");
            Assert.That(StateSnapshot(), Is.EqualTo(progressBefore)); Assert.That(SourceSnapshot(), Is.EqualTo(sourceBefore));
            Assert.That(buttons[3].GetComponentInChildren<Text>().text, Does.EndWith(language == "ko" ? "변화 없이 닫기" : "Close without changes"));
        }

        [Test]
        public void ReusedPanelRefreshesCloseLanguageAndKeepsTwentyTwoPixelInsetAndTitleSeparation()
        {
            GameText.Language = "ko"; _panel.Open(Model(), _progress, id => { });
            var close = _panel.GetComponentsInChildren<Button>().Single(b => b.name == "Close");
            Assert.That(close.GetComponentInChildren<Text>().text, Is.EqualTo("닫기"));
            close.onClick.Invoke(); Assert.That(_panel.IsOpen, Is.False);
            GameText.Language = "en"; _panel.Open(Model(), _progress, id => { });
            Assert.That(close.GetComponentInChildren<Text>().text, Is.EqualTo("Close"));
            var rect = (RectTransform)close.transform; var title = Text("ExplorationTitle").rectTransform;
            Assert.That(rect.sizeDelta, Is.EqualTo(new Vector2(64, 32)));
            Assert.That(((RectTransform)_panel.transform).rect.xMax - (rect.anchoredPosition.x + rect.rect.xMax), Is.EqualTo(22).Within(.01));
            Assert.That(rect.anchoredPosition.x + rect.rect.xMin - (title.anchoredPosition.x + title.rect.xMax), Is.GreaterThanOrEqualTo(4));
            foreach (var text in _panel.GetComponentsInChildren<Text>()) { Fits(text); Assert.That(text.raycastTarget, Is.False); }
        }

        [TestCase("ko")]
        [TestCase("en")]
        public void RealPromptGetterUsesLocalizedTitleAndSafeUnknownPlace(string language)
        {
            GameText.Language = language;
            var point = Own(new GameObject("OwnedPrompt")).AddComponent<ExplorationInteractionPointInteractor>();
            point.Bind(_definition);
            Assert.That(point.InteractionPrompt, Is.EqualTo(language == "ko" ? "[E] 골목의 낡은 상자 살펴보기" : "[E] Inspect Old alley crate"));
            var unknown = Own(ScriptableObject.CreateInstance<ExplorationInteractionDefinition>());
            unknown.ConfigureForTests("secret.unregistered", "secret.unregistered.title", "desc", Vector2.zero, 1, false, 0, null, Array.Empty<ExplorationChoiceDefinition>());
            point.Bind(unknown);
            Assert.That(point.InteractionPrompt, Is.EqualTo(language == "ko" ? "[E] 이곳 살펴보기" : "[E] Inspect this place"));
            point.Bind(null); NoInternals(point.InteractionPrompt);
        }

        [Test]
        public void UnknownFieldsAndMissingCatalogNeverEchoInputOrFallbackKeys()
        {
            GameText.Language = "en";
            var model = new ExplorationSummaryModel("secret.id", "secret.title", "secret.description", false,
                new[] { new ExplorationChoiceSummaryModel("secret.one", "secret.name", "secret.preview", true, ""), new ExplorationChoiceSummaryModel("secret.two", "secret.name", "", false, "secret.lock") });
            _panel.Open(model, _progress, id => { });
            Assert.That(Text("ExplorationTitle").text, Is.EqualTo("Investigation"));
            Assert.That(Choices()[0].GetComponentInChildren<Text>().text, Is.EqualTo("Choose — Choose an approach"));
            Assert.That(Choices()[1].GetComponentInChildren<Text>().text, Is.EqualTo("Choose — Requirements not met"));
            Set(_registry, "_textCatalog", null);
            _panel.Open(model, _progress, id => { });
            foreach (var text in _panel.GetComponentsInChildren<Text>()) { Assert.That(text.text, Does.Not.Contain("secret").And.Not.Contain("exploration.").And.Not.Contain("facility.")); }
        }

        [TestCase("ko")]
        [TestCase("en")]
        public void ActualEffectGroupsAndEveryResultKindAreLocalizedBoundedAndReadOnly(string language)
        {
            GameText.Language = language; _panel.Open(Model(), _progress, id => { });
            for (int scenario = 0; scenario < 3; scenario++)
            {
                var collector = ActualCollector(scenario);
                var result = new ExplorationChoiceResult(ExplorationChoiceResultKind.Applied, _definition.Id, _ids[scenario], "", collector);
                string before = StateSnapshot(); string ids = string.Join("|", result.OutcomeIds) + string.Join("|", result.ItemGrantIds);
                _panel.ShowResult(result); string text = _panel.ResultText;
                Assert.That(text, Does.StartWith(language == "ko" ? "조사를 마쳤어요" : "Investigation complete"));
                Assert.That(text, Does.Contain(language == "ko" ? "체력 -1" : "Energy -1").And.Contain(language == "ko" ? "스트레스 +1" : "Stress +1"));
                if (scenario == 0) Assert.That(text, Does.Contain(language == "ko" ? "+목재 ×1" : "+Wood ×1"));
                if (scenario == 1) { Assert.That(text, Does.Contain(language == "ko" ? "진로 힌트 1개" : "Career hints: 1")); Assert.That(text, Does.Not.Contain("Wood").And.Not.Contain("목재")); }
                if (scenario < 2) Assert.That(text, Does.Contain(language == "ko" ? "성장 기록 1건" : "Growth updates: 1"));
                if (scenario == 2) Assert.That(text, Does.Contain(language == "ko" ? "단서 1건" : "Clues: 1").And.Contain(language == "ko" ? "도감 1건" : "Journal: 1").And.Contain(language == "ko" ? "후속 퀘스트 1건" : "Follow-up: 1"));
                NoInternals(text); ResultFits();
                Assert.That(StateSnapshot(), Is.EqualTo(before)); Assert.That(string.Join("|", result.OutcomeIds) + string.Join("|", result.ItemGrantIds), Is.EqualTo(ids));
            }
            foreach (ExplorationChoiceResultKind kind in Enum.GetValues(typeof(ExplorationChoiceResultKind)))
            {
                _panel.ShowResult(new ExplorationChoiceResult(kind, "secret.interaction", "secret.choice", "secret.reason", null));
                string value = _panel.ResultText; Assert.That(value, Is.Not.Empty); NoInternals(value); Assert.That(value, Does.Not.Contain(kind.ToString()).And.Not.Contain("secret")); ResultFits();
            }
            _panel.ShowResult(new ExplorationChoiceResult((ExplorationChoiceResultKind)999, "secret", "secret", "secret", null));
            Assert.That(_panel.ResultText, Is.EqualTo(language == "ko" ? "조사 결과를 확인해 주세요" : "Check the investigation result"));
        }

        [Test]
        public void UnknownMalformedPacketsAndAmbiguousItemIdsUseNeutralTextWithoutDoubleCounting()
        {
            GameText.Language = "en"; _panel.Open(Model(), _progress, id => { });
            var collector = new ExplorationOutcomeCollector(); collector.AddItemGrant("Wood:1"); collector.AddOutcome("item-grant:Wood");
            _panel.ShowResult(new ExplorationChoiceResult(ExplorationChoiceResultKind.Applied, "", "", "", collector));
            Assert.That(_panel.ResultText, Does.Contain("+Wood ×1").And.Not.Contain("Other changes").And.Not.Contain("item-grant"));
            collector.AddOutcome("item-grant:secret.orphan");
            _panel.ShowResult(new ExplorationChoiceResult(ExplorationChoiceResultKind.Applied, "", "", "", collector));
            Assert.That(_panel.ResultText, Does.Contain("Other changes: 1").And.Not.Contain("secret"), "A typed item fact must not suppress an unrelated outcome of the same category.");
            var wrongCase = new ExplorationOutcomeCollector(); wrongCase.AddItemGrant("wood:2");
            _panel.ShowResult(new ExplorationChoiceResult(ExplorationChoiceResultKind.Applied, "", "", "", wrongCase));
            Assert.That(_panel.ResultText, Does.Contain("+Item ×2").And.Not.Contain("Wood"), "Authored Wood ID lookup is exact and case-sensitive.");
            var duplicate = Own(ScriptableObject.CreateInstance<ItemDefinition>()); Set(duplicate, "_id", "Wood"); Set(duplicate, "_displayKey", "career.chef");
            Set(_registry, "_items", _registry.Items.Concat(new[] { duplicate }).ToArray());
            _panel.ShowResult(new ExplorationChoiceResult(ExplorationChoiceResultKind.Applied, "", "", "", collector));
            Assert.That(_panel.ResultText, Does.Contain("+Item ×1").And.Not.Contain("Chef").And.Not.Contain("Wood"));
            collector = new ExplorationOutcomeCollector(); collector.AddItemGrant("secret.unregistered:2"); collector.AddItemSpend("secret.bad:not-a-count"); collector.AddOutcome("secret.raw-protocol"); collector.AddFatigueStatus("secret.status:bad");
            _panel.ShowResult(new ExplorationChoiceResult(ExplorationChoiceResultKind.Applied, "", "", "", collector));
            Assert.That(_panel.ResultText, Does.Contain("Item").And.Not.Contain("secret").And.Not.Contain("not-a-count")); NoInternals(_panel.ResultText); ResultFits();
        }

        [Test]
        public void LargeFutureEffectListsRemainInsideResultRectUsingNeutralAggregateCounts()
        {
            GameText.Language = "en"; _panel.Open(Model(), _progress, id => { });
            var collector = new ExplorationOutcomeCollector();
            for (int i = 0; i < 40; i++) { collector.AddItemGrant("secret.long-item-" + i + ":1"); collector.AddOutcome("unrecognized:" + i); collector.AddFatigueStatus("unrecognized-status:" + i); }
            _panel.ShowResult(new ExplorationChoiceResult(ExplorationChoiceResultKind.Applied, "", "", "", collector));
            Assert.That(_panel.ResultText, Does.Not.Contain("secret").And.Not.Contain("unrecognized")); ResultFits();
        }

        private ExplorationSummaryModel Model() => ExplorationSummaryBuilder.Build(_definition.Id, new ExplorationLookupCache(new[] { _definition }), _progress, _student, _inventory, null, null, 1, 480);
        private Button[] Choices() => _panel.GetComponentsInChildren<Button>().Where(b => b.name.StartsWith("ChoiceButton_", StringComparison.Ordinal)).ToArray();
        private Text Text(string name) => _panel.GetComponentsInChildren<Text>(true).Single(t => t.name == name);
        private string StateSnapshot() => JsonUtility.ToJson(_student.ToSaveData()) + JsonUtility.ToJson(_progress.ToSaveData());
        private string SourceSnapshot() => EditorJsonUtility.ToJson(_definition) + string.Join("|", _definition.Choices.Select(c => EditorJsonUtility.ToJson(c)));
        private void ResultFits()
        {
            var text = Text("ExplorationResultText"); Fits(text);
            using (var generator = new TextGenerator())
            {
                generator.Populate(text.text, text.GetGenerationSettings(text.rectTransform.rect.size));
                Assert.That(generator.lineCount, Is.LessThanOrEqualTo(3));
            }
        }
        private static void Fits(Text text) => Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + .5f), text.name + " localized text overflows: " + text.text);
        private static void NoInternals(string value) { foreach (string token in new[] { "Exploration Test", "choice.test.", "exploration.", "item-grant:", "trait:", "career:", "clue:", "quest:" }) Assert.That(value, Does.Not.Contain(token)); }
        private static ExplorationOutcomeCollector ActualCollector(int scenario)
        {
            var c = new ExplorationOutcomeCollector();
            if (scenario == 0) { c.AddItemGrant("Wood:1"); c.AddOutcome("item-grant:Wood"); }
            if (scenario < 2) c.AddOutcome("trait:trait.focus");
            if (scenario == 1) { c.AddCareerHint("career.chef"); c.AddOutcome("career:career.chef"); }
            if (scenario == 2) { c.AddClue("clue.discovery.library-archive-rumor"); c.AddEncyclopediaEntry("ency.location.first-discovery"); c.AddFollowUpQuest("quest.gatherWood"); c.AddOutcome("clue:clue.discovery.library-archive-rumor"); c.AddOutcome("encyclopedia:ency.location.first-discovery"); c.AddOutcome("quest:quest.gatherWood"); }
            c.AddFatigueStatus("energy:-1"); c.AddFatigueStatus("stress:1"); return c;
        }
        private void Snapshot(Type type, string name) { var field = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Static); Assert.That(field, Is.Not.Null, name); _statics.Add(field, field.GetValue(null)); }
        private static void Set(object target, string name, object value) { var field = target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance); Assert.That(field, Is.Not.Null, name); field.SetValue(target, value); }
        private T Own<T>(T value) where T : Object { _owned.Add(value); return value; }
        private sealed class Row { public string Key, Ko, En; }
        private static Row[] Rows(GameTextCatalog catalog)
        {
            var p = new SerializedObject(catalog).FindProperty("_entries");
            return Enumerable.Range(0, p.arraySize).Select(i => { var r = p.GetArrayElementAtIndex(i); return new Row { Key = r.FindPropertyRelative("key").stringValue, Ko = r.FindPropertyRelative("ko").stringValue, En = r.FindPropertyRelative("en").stringValue }; }).ToArray();
        }
        private static string Hash(string value)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "");
        }
        private static string ReferencedId(Object source, string field)
        {
            var target = new SerializedObject(source).FindProperty(field).objectReferenceValue;
            Assert.That(target, Is.Not.Null, field);
            var id = target.GetType().GetProperty("Id"); Assert.That(id, Is.Not.Null);
            return (string)id.GetValue(target);
        }
        [Serializable] private sealed class Scalar { public string value; }
        private static Dictionary<string, string> FlatStrings(string json)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            string quoted = "\"(?:\\\\.|[^\"\\\\])*\"";
            var regex = new Regex("(?<key>" + quoted + ")\\s*:\\s*(?<value>" + quoted + ")");
            foreach (Match match in regex.Matches(json))
            {
                string key = JsonUtility.FromJson<Scalar>("{\"value\":" + match.Groups["key"].Value + "}").value;
                string value = JsonUtility.FromJson<Scalar>("{\"value\":" + match.Groups["value"].Value + "}").value;
                result.Add(key, value);
            }
            Assert.That(Regex.Replace(regex.Replace(json, ""), @"[\s{},]", ""), Is.Empty, "Expected flat JSON string map.");
            return result;
        }
    }
}
