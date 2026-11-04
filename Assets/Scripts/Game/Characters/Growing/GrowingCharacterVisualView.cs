using Rootborn.Game.Tools;
using UnityEngine;

namespace Rootborn.Game.Characters.Growing
{
    /// <summary>
    /// Draws body, outfit and head as stacked frame layers. Sprites change only when the frame changes,
    /// so a standing character costs nothing per frame. Movement and input stay with PlayerController.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GrowingCharacterVisualView : MonoBehaviour, ICharacterVisualView
    {
        public const float WalkFramesPerSecond = 8f;
        public const float ActionSeconds = 0.3f;
        public const string BodyLayerName = "Growing_Body";
        public const string OutfitLayerName = "Growing_Outfit";
        public const string HeadLayerName = "Growing_Head";

        private const int RowDown = 0;
        private const int RowLeft = 1;
        private const int RowRight = 2;
        private const int RowUp = 3;
        private const int StandingColumn = 1;
        private const int FirstActionColumn = 3;
        private const int ActionColumnCount = 2;
        private const float MoveThresholdSqr = 0.0001f;
        private const float RemoteMoveThresholdSqr = 0.000001f;
        private static readonly int[] WalkCycle = { 1, 2, 1, 0 };

        [SerializeField] private GrowingCharacterAppearanceDefinition _appearance;
        [SerializeField] private SpriteRenderer _bodyRenderer;
        [SerializeField] private SpriteRenderer _outfitRenderer;
        [SerializeField] private SpriteRenderer _headRenderer;

        private int _row = RowDown;
        private int _column = StandingColumn;
        private int _appliedRow = -1;
        private int _appliedColumn = -1;
        private int _walkIndex = -1;
        private float _walkTimer;
        private float _actionRemaining;
        private Vector2 _lastPosition;
        private bool _isRemote;

        public event System.Action<GrowingCharacterAppearanceDefinition> AppearanceChanged;

        /// <summary>True for another player's character: its look comes from the network and its motion from movement.</summary>
        public bool IsRemote
        {
            get => _isRemote;
            set
            {
                // Where it stands now is where it spawned, not a step.
                if (value && !_isRemote) _lastPosition = transform.position;
                _isRemote = value;
            }
        }


        public int CurrentRow => _row;
        public int CurrentColumn => _column;
        public int FrameChangeCount { get; private set; }
        public GrowingCharacterAppearanceDefinition Appearance => _appearance;
        public SpriteRenderer BodyRenderer => _bodyRenderer;
        public SpriteRenderer OutfitRenderer => _outfitRenderer;
        public SpriteRenderer HeadRenderer => _headRenderer;

        public void Apply(GrowingCharacterAppearanceDefinition appearance)
        {
            _appearance = appearance;
            EnsureRenderers();
            _appliedRow = -1;
            _appliedColumn = -1;
            ApplyFrame();
            AppearanceChanged?.Invoke(appearance);
        }

#if UNITY_EDITOR
        public void WireForPrefab(SpriteRenderer body, SpriteRenderer outfit, SpriteRenderer head)
        {
            _bodyRenderer = body;
            _outfitRenderer = outfit;
            _headRenderer = head;
        }
#endif

        /// <summary>Copies layer and order from the character's own renderer so y-sorting keeps working.</summary>
        public void MatchSorting(SpriteRenderer source)
        {
            if (source == null) return;
            EnsureRenderers();
            SetSorting(_bodyRenderer, source, 0);
            SetSorting(_outfitRenderer, source, 1);
            SetSorting(_headRenderer, source, 2);
        }

        public void SetMotion(Vector2 input, Vector2 facing)
        {
            Advance(input, facing, UnityEngine.Time.deltaTime);
        }

        public void Advance(Vector2 input, Vector2 facing, float deltaTime)
        {
            _row = RowFor(facing, _row);
            if (_actionRemaining > 0f)
            {
                _actionRemaining -= deltaTime;
                float progress = 1f - Mathf.Clamp01(_actionRemaining / ActionSeconds);
                _column = FirstActionColumn + Mathf.Min(ActionColumnCount - 1, (int)(progress * ActionColumnCount));
                if (_actionRemaining <= 0f) _column = StandingColumn;
            }
            else if (input.sqrMagnitude > MoveThresholdSqr)
            {
                _walkTimer += deltaTime;
                float step = 1f / WalkFramesPerSecond;
                while (_walkTimer >= step - 0.0001f)
                {
                    _walkTimer -= step;
                    _walkIndex = (_walkIndex + 1) % WalkCycle.Length;
                }
                _column = WalkCycle[Mathf.Max(_walkIndex, 0)];
            }
            else
            {
                _walkIndex = -1;
                _walkTimer = 0f;
                _column = StandingColumn;
            }
            ApplyFrame();
        }

        private void Update()
        {
            if (!IsRemote) return;
            Vector2 position = transform.position;
            Vector2 delta = position - _lastPosition;
            _lastPosition = position;
            bool moved = delta.sqrMagnitude > RemoteMoveThresholdSqr;
            // Synced steps can be far shorter than a stick input, so only their direction counts.
            Vector2 motion = moved ? delta.normalized : Vector2.zero;
            Advance(motion, motion, UnityEngine.Time.deltaTime);
        }

        // Right has its own row in the sheet, so mirroring would flip it back to the wrong side.
        public void SetFlipX(bool flipX)
        {
        }

        public void PlayAction(CharacterVisualAction action)
        {
            if (action == CharacterVisualAction.None) return;
            _actionRemaining = ActionSeconds;
        }

        public void ApplyEquippedToolVisual(ToolVisualMappingDefinition mapping)
        {
        }

        private static int RowFor(Vector2 facing, int fallback)
        {
            if (facing.sqrMagnitude <= MoveThresholdSqr) return fallback;
            if (Mathf.Abs(facing.x) > Mathf.Abs(facing.y)) return facing.x < 0f ? RowLeft : RowRight;
            return facing.y < 0f ? RowDown : RowUp;
        }

        private void ApplyFrame()
        {
            if (_appearance == null || (_row == _appliedRow && _column == _appliedColumn)) return;
            EnsureRenderers();
            _appliedRow = _row;
            _appliedColumn = _column;
            FrameChangeCount++;
            _bodyRenderer.sprite = FrameOf(_appearance.Body);
            _outfitRenderer.sprite = FrameOf(_appearance.Outfit);
            _headRenderer.sprite = FrameOf(_appearance.Head);
        }

        private Sprite FrameOf(GrowingCharacterSheetDefinition sheet)
        {
            return sheet != null ? sheet.GetFrame(_row, _column) : null;
        }

        private void EnsureRenderers()
        {
            if (_bodyRenderer == null) _bodyRenderer = CreateLayer(BodyLayerName, 0);
            if (_outfitRenderer == null) _outfitRenderer = CreateLayer(OutfitLayerName, 1);
            if (_headRenderer == null) _headRenderer = CreateLayer(HeadLayerName, 2);
        }

        private SpriteRenderer CreateLayer(string layerName, int order)
        {
            var layer = new GameObject(layerName);
            layer.transform.SetParent(transform, false);
            var layerRenderer = layer.AddComponent<SpriteRenderer>();
            layerRenderer.sortingOrder = order;
            return layerRenderer;
        }

        private static void SetSorting(SpriteRenderer target, SpriteRenderer source, int offset)
        {
            target.sortingLayerID = source.sortingLayerID;
            target.sortingOrder = source.sortingOrder + offset;
        }
    }
}
