using System.Collections.Generic;
using System.Globalization;
using Rootborn.Core.StudentLife;
using Rootborn.Ui;

namespace Rootborn.Screens.Discoveries
{
    /// <summary>발견으로 바뀐 성장 하나(특성·기술·상태 id와 변화량). 발견 결과 기록(DiscoveryLogCodec)에서 읽는다.</summary>
    public readonly struct DiscoveryChange
    {
        public DiscoveryChange(string targetId, int delta) { TargetId = targetId ?? string.Empty; Delta = delta; }
        public string TargetId { get; }
        public int Delta { get; }
    }

    /// <summary>발견 알림의 표시 모델: 어떤 발견인지(이름 키), 결과 종류의 문구 키, 바뀐 성장(적용됐을 때만).</summary>
    public sealed class DiscoveryNoticeModel
    {
        /// <summary>결과 종류 → 문구 키. 새 종류가 생기면 한 줄 더한다. 표에 없는 종류는 UnavailableKey.</summary>
        private static readonly Dictionary<LifeActivityResultKind, string> ResultKeys = new Dictionary<LifeActivityResultKind, string>
        {
            [LifeActivityResultKind.Applied] = "ui.discovery.found",
            [LifeActivityResultKind.DuplicateRequest] = "ui.discovery.already",
            [LifeActivityResultKind.RequirementFailed] = "ui.discovery.not-yet",
        };

        public const string UnavailableKey = "ui.discovery.unavailable";

        public string DiscoveryId { get; private set; } = string.Empty;
        public string NameKey { get; private set; } = string.Empty;
        public bool Applied { get; private set; }
        public string MessageKey { get; private set; } = UnavailableKey;
        public IReadOnlyList<DiscoveryChange> Changes { get; private set; } = new DiscoveryChange[0];

        public static DiscoveryNoticeModel From(DiscoveryResult result, Rootborn.Data.GameDefinitions definitions)
        {
            var model = new DiscoveryNoticeModel
            {
                DiscoveryId = result.DiscoveryId ?? string.Empty,
                Applied = result.Kind == LifeActivityResultKind.Applied,
                MessageKey = ResultKeys.TryGetValue(result.Kind, out string key) ? key : UnavailableKey,
            };
            if (definitions != null && model.DiscoveryId.Length > 0 && definitions.TryGet(model.DiscoveryId, out Rootborn.Core.StudentLife.Discovery discovery))
                model.NameKey = discovery.DisplayNameKey;
            if (model.Applied)
            {
                DiscoveryGrowthSummaryEntry[] entries = DiscoveryGrowthSummary.FromResultLogs(result.OutcomeLogIds).Entries;
                var changes = new List<DiscoveryChange>(entries.Length);
                foreach (DiscoveryGrowthSummaryEntry entry in entries) changes.Add(new DiscoveryChange(entry.TargetId, entry.Delta));
                model.Changes = changes;
            }
            return model;
        }
    }

    /// <summary>
    /// 발견 지점(상호작용 종류 "discovery")을 살폈을 때의 알림(알림 층, 이동을 막지 않음). 지점에서 E를 누르면 등록 표가 이 화면을 열고,
    /// 화면은 GameSession.Discover(지점 id)를 부른 뒤 결과를 보인다: 발견 이름, 결과 문구(새로 알게 됨, 이미 살펴봄, 아직 알 수 없음),
    /// 바뀐 성장 줄. ShowSeconds가 지나면 저절로 닫힌다(시간은 화면 쌓기의 Tick으로만 흐른다).
    /// 한 번만 적용되는지와 저장은 Core·GameSession이 정한다(화면은 판단하지 않는다).
    /// Unity 판(DiscoveryPointInteractor)은 결과를 로그에만 남기고 화면에 알리지 않았다 — 여기서는 알림 층에 보인다.
    /// </summary>
    public sealed class DiscoveryNoticeScreen : GameScreen
    {
        /// <summary>등록 표에 쓰는 상호작용 종류(interactions.json의 kind).</summary>
        public const string InteractionKind = "discovery";

        /// <summary>바뀐 성장 줄을 몇 개까지 보이는가.</summary>
        public const int MaxChanges = 3;

        /// <summary>배치(1920x1080 기준): 아래 가운데. 새 디자인에서는 여기만 고친다.</summary>
        internal static class Metrics
        {
            public const float BoxW = 720f, BoxH = 196f, Bottom = 48f;
            public const float TextX = 32f, TextW = 656f;
            public const float NameY = 20f, NameH = 40f, NameFont = 26f;
            public const float MessageY = 64f, MessageH = 32f, MessageFont = 20f;
            public const float FirstChangeY = 102f, ChangeStep = 30f, ChangeH = 28f, ChangeFont = 18f;
            public const float ShowSeconds = 4f;
            public static readonly UiColor Ink = new UiColor(48, 71, 83);
            public static readonly UiColor ChangeInk = UnityLayout.Color(0.18f, 0.45f, 0.30f);
        }

        public const string RootId = "discovery-notice";
        public const string BoxId = "discovery-notice.box";
        public const string NameId = "discovery-notice.name";
        public const string MessageId = "discovery-notice.message";
        public static string ChangeId(int i) => "discovery-notice.change." + i;

        private readonly UiLabel[] _changes = new UiLabel[MaxChanges];
        private UiLabel _name, _message;
        private float _remaining;

        public DiscoveryNoticeScreen(ScreenServices services) : base(services) { }

        public DiscoveryNoticeModel Model { get; private set; } = new DiscoveryNoticeModel();

        /// <summary>닫히기까지 남은 초.</summary>
        public float RemainingSeconds => _remaining;

        protected override UiNode Build()
        {
            var root = new UiNode { Id = RootId };
            root.Stretch(0f, 0f, 0f, 0f);
            var box = root.Add(new UiPanel { Id = BoxId, Skin = UiSkin.Panel, Interactive = false });
            box.Place(0.5f, 1f, -Metrics.BoxW / 2f, -Metrics.Bottom - Metrics.BoxH, Metrics.BoxW, Metrics.BoxH);
            _name = box.Add(new UiLabel { Id = NameId, FontSize = Metrics.NameFont, Color = Metrics.Ink });
            _name.Place(0f, 0f, Metrics.TextX, Metrics.NameY, Metrics.TextW, Metrics.NameH);
            _message = box.Add(new UiLabel { Id = MessageId, FontSize = Metrics.MessageFont, Color = Metrics.Ink });
            _message.Place(0f, 0f, Metrics.TextX, Metrics.MessageY, Metrics.TextW, Metrics.MessageH);
            for (int i = 0; i < MaxChanges; i++)
            {
                _changes[i] = box.Add(new UiLabel { Id = ChangeId(i), FontSize = Metrics.ChangeFont, Color = Metrics.ChangeInk, Visible = false });
                _changes[i].Place(0f, 0f, Metrics.TextX, Metrics.FirstChangeY + i * Metrics.ChangeStep, Metrics.TextW, Metrics.ChangeH);
            }
            return root;
        }

        protected internal override void OnOpened(ScreenOpenContext context)
        {
            if (context.Interaction.HasValue && Services.Session != null)
                Model = DiscoveryNoticeModel.From(Services.Session.Discover(context.Interaction.Value.TargetId), Services.Definitions);
            _remaining = Metrics.ShowSeconds;
            Refresh();
        }

        protected internal override void OnChanged(string topic)
        {
            if (topic == ChangeTopics.Language || topic == ChangeTopics.All) Refresh();
        }

        protected internal override bool OnTick(float seconds)
        {
            _remaining -= seconds;
            if (_remaining <= 0f) Close();
            return false;
        }

        private void Refresh()
        {
            _name.Text = Model.DiscoveryId.Length == 0 ? string.Empty : NameOf(Model.NameKey, Model.DiscoveryId);
            _message.Text = T(Model.MessageKey);
            IReadOnlyList<DiscoveryChange> changes = Model.Changes;
            for (int i = 0; i < _changes.Length; i++)
            {
                bool shown = i < changes.Count;
                _changes[i].Visible = shown;
                _changes[i].Text = shown
                    ? F("ui.discovery.change", NameOf(changes[i].TargetId, changes[i].TargetId), changes[i].Delta.ToString("+0;-0", CultureInfo.InvariantCulture))
                    : string.Empty;
            }
        }
    }
}
