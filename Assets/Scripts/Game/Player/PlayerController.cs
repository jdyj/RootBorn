using Rootborn.Game.Common;
using Rootborn.Game.Family;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Rootborn.Game.Player
{
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] private float _moveSpeed = 4f;
        [SerializeField] private Animator _animator;
        [SerializeField] private SpriteRenderer _renderer;
        [SerializeField] private SpriteRenderer _toolRenderer;
        [SerializeField] private CharacterPartComposer _partComposer;
        [SerializeField] private CharacterPartAnimator _partAnimator;
        [SerializeField] private Rigidbody2D _rb;
        [SerializeField] private PlayerInventory _inventory;
        [SerializeField] private GatherInteractor _interactor;
        [SerializeField] private float _attackFrameDuration = 0.06f;
        [SerializeField] private Camera _camera;

        private InputAction _moveAction;
        private InputAction _attackAction;
        private InputAction _pointerAction;
        private Vector2 _input;
        private Vector2 _lastFacing = new Vector2(0f, -1f);
        private string _activeToolId;
        private string _activeToolSpritePrefix;
        private CharacterPartAnimationClipDefinition _activeCharacterPartAnimationClip;

        private bool _isAttacking;
        private float _attackTime;
        private Vector2 _attackFacing;
        private bool _attackTriggered;

        private static readonly int HashMoveX = Animator.StringToHash("MoveX");
        private static readonly int HashMoveY = Animator.StringToHash("MoveY");
        private static readonly int HashSpeed = Animator.StringToHash("Speed");

        public Vector2 LastFacing => _lastFacing;
        public Vector2 Input => _input;

        private void Awake()
        {
            if (_animator == null) _animator = GetComponent<Animator>();
            if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
            if (_partComposer == null) _partComposer = GetComponent<CharacterPartComposer>();
            if (_partAnimator == null) _partAnimator = GetComponent<CharacterPartAnimator>();
            if (_rb == null) _rb = GetComponent<Rigidbody2D>();
            if (_inventory == null) _inventory = GetComponent<PlayerInventory>();
            if (_interactor == null) _interactor = GetComponent<GatherInteractor>();
            if (_camera == null) _camera = Camera.main;
        }

        private void OnEnable()
        {
            _moveAction = new InputAction(type: InputActionType.Value, expectedControlType: "Vector2");
            _moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            _moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");
            _moveAction.Enable();

            _attackAction = new InputAction(type: InputActionType.Button);
            _attackAction.AddBinding("<Mouse>/leftButton");
            _attackAction.performed += OnAttackPerformed;
            _attackAction.Enable();

            _pointerAction = new InputAction(type: InputActionType.Value, expectedControlType: "Vector2");
            _pointerAction.AddBinding("<Mouse>/position");
            _pointerAction.Enable();

            TryBindInventory();
        }

        private void OnAttackPerformed(InputAction.CallbackContext ctx)
        {
            if (!HasToolSprites(_activeToolSpritePrefix)) return;
            if (_isAttacking) return;

            var screenPos = _pointerAction != null ? _pointerAction.ReadValue<Vector2>() : Vector2.zero;
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;
            var worldPos = _camera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 0f));
            var dir = (Vector2)(worldPos - transform.position);
            if (dir.sqrMagnitude < 0.0001f) dir = _lastFacing;
            _attackFacing = dir.normalized;
            _lastFacing = _attackFacing;
            _isAttacking = true;
            _attackTime = 0f;
            _attackTriggered = false;
            if (_partAnimator != null && _activeCharacterPartAnimationClip != null)
            {
                _partAnimator.PlayClip(_activeCharacterPartAnimationClip);
            }
            if (Mathf.Abs(_attackFacing.x) > Mathf.Abs(_attackFacing.y))
            {
                ApplyFlipX(_attackFacing.x > 0f);
            }
        }

        private void TryBindInventory()
        {
            if (_inventory != null) return;
            _inventory = GetComponent<PlayerInventory>();
            if (_inventory == null) return;
            _inventory.OnEquipmentChanged += OnEquipmentChanged;
            OnEquipmentChanged();
        }

        private void OnDisable()
        {
            if (_moveAction != null)
            {
                _moveAction.Disable();
                _moveAction.Dispose();
                _moveAction = null;
            }
            if (_attackAction != null)
            {
                _attackAction.performed -= OnAttackPerformed;
                _attackAction.Disable();
                _attackAction.Dispose();
                _attackAction = null;
            }
            if (_pointerAction != null)
            {
                _pointerAction.Disable();
                _pointerAction.Dispose();
                _pointerAction = null;
            }
            if (_inventory != null) _inventory.OnEquipmentChanged -= OnEquipmentChanged;
        }

        private void OnEquipmentChanged()
        {
            var tool = _inventory != null ? _inventory.EquippedToolItem : null;
            string newId = tool != null ? tool.Id : null;
            if (newId == _activeToolId) return;
            _activeToolId = newId;
            _activeToolSpritePrefix = tool != null ? tool.ToolSpritePrefix : null;
            _activeCharacterPartAnimationClip = null;
            var data = Rootborn.Game.Managers.Managers.Data;
            if (data != null && tool != null && data.ToolById.TryGetValue(tool.Id, out var toolDefinition))
            {
                _activeCharacterPartAnimationClip = toolDefinition.CharacterPartAnimationClip;
            }
            _isAttacking = false;
            _attackTime = 0f;

            if (_animator != null)
            {
                _animator.enabled = !HasToolSprites(_activeToolSpritePrefix);
            }
            if (!HasToolSprites(_activeToolSpritePrefix) && _toolRenderer != null)
            {
                _toolRenderer.sprite = null;
                _toolRenderer.enabled = false;
            }
        }

        private static bool HasToolSprites(string spritePrefix)
        {
            return !string.IsNullOrEmpty(spritePrefix);
        }

        private static string FacingToDirKey(Vector2 facing)
        {
            if (Mathf.Abs(facing.x) > Mathf.Abs(facing.y))
            {
                return "Side";
            }
            return facing.y > 0f ? "Up" : "Down";
        }

        private void Update()
        {
            TryBindInventory();
            if (_partComposer == null) _partComposer = GetComponent<CharacterPartComposer>();
            if (_partAnimator == null) _partAnimator = GetComponent<CharacterPartAnimator>();

            _input = _moveAction != null ? _moveAction.ReadValue<Vector2>() : Vector2.zero;

            if (_rb == null)
            {
                transform.position += (Vector3)(_input * (_moveSpeed * UnityEngine.Time.deltaTime));
            }

            if (!_isAttacking && _input.sqrMagnitude > 0.01f)
            {
                _lastFacing = _input.normalized;
            }

            if (_animator != null && _animator.enabled)
            {
                _animator.SetFloat(HashMoveX, _input.x);
                _animator.SetFloat(HashMoveY, _input.y);
                _animator.SetFloat(HashSpeed, _input.sqrMagnitude);
            }

            var facingForFlip = _isAttacking ? _attackFacing : _lastFacing;
            bool isSide = Mathf.Abs(facingForFlip.x) > Mathf.Abs(facingForFlip.y);
            ApplyFlipX(isSide && facingForFlip.x > 0f);

            if (_partAnimator != null)
            {
                _partAnimator.SetMotion(_isAttacking ? Vector2.zero : _input, facingForFlip);
                _partAnimator.Tick(UnityEngine.Time.deltaTime);
            }

            UpdateToolSprite();
        }

        private void ApplyFlipX(bool flipX)
        {
            if (_renderer != null)
            {
                _renderer.flipX = flipX;
            }
            if (_toolRenderer != null)
            {
                _toolRenderer.flipX = flipX;
            }
            if (_partComposer != null)
            {
                _partComposer.SetFlipX(flipX);
            }
        }

        private SpriteRenderer EnsureToolRenderer()
        {
            if (_toolRenderer != null)
            {
                return _toolRenderer;
            }

            var child = transform.Find("Part_tool");
            if (child == null)
            {
                var go = new GameObject("Part_tool");
                go.transform.SetParent(transform, false);
                child = go.transform;
            }

            _toolRenderer = child.GetComponent<SpriteRenderer>();
            if (_toolRenderer == null)
            {
                _toolRenderer = child.gameObject.AddComponent<SpriteRenderer>();
            }
            _toolRenderer.sortingOrder = 10;
            _toolRenderer.enabled = false;
            return _toolRenderer;
        }

        private void UpdateToolSprite()
        {
            string prefix = _activeToolSpritePrefix;
            if (!HasToolSprites(prefix))
            {
                if (_toolRenderer != null)
                {
                    _toolRenderer.sprite = null;
                    _toolRenderer.enabled = false;
                }
                return;
            }

            var rm = Rootborn.Game.Managers.Managers.Resource;
            if (rm == null) return;

            var facing = _isAttacking ? _attackFacing : _lastFacing;
            string dir = FacingToDirKey(facing);
            string sheetAddr = $"sprites/player/tool/{prefix.ToLowerInvariant()}-{dir.ToLowerInvariant()}";

            int frame;
            if (_isAttacking)
            {
                _attackTime += UnityEngine.Time.deltaTime;
                int rawFrame = Mathf.FloorToInt(_attackTime / Mathf.Max(0.01f, _attackFrameDuration));
                int totalFrames = GetSheetFrameCount(prefix, dir, rm, sheetAddr);
                frame = rawFrame;

                if (!_attackTriggered && frame >= totalFrames - 1)
                {
                    _attackTriggered = true;
                    if (_interactor != null) _interactor.TriggerInteract();
                }
                if (frame >= totalFrames)
                {
                    _isAttacking = false;
                    _attackTime = 0f;
                    frame = 0;
                }
            }
            else
            {
                frame = 0;
            }

            string subName = $"{prefix}_{dir}_{frame}";
            var s = rm.GetCachedSubSprite(sheetAddr, subName);
            if (s == null && frame > 0)
            {
                s = rm.GetCachedSubSprite(sheetAddr, $"{prefix}_{dir}_0");
            }
            if (s != null)
            {
                EnsureToolRenderer();
                _toolRenderer.sprite = s;
                _toolRenderer.enabled = true;
            }
        }

        private static int GetSheetFrameCount(string prefix, string dir, Rootborn.Game.Managers.ResourceManager rm, string sheetAddr)
        {
            for (int i = 0; i < 8; i++)
            {
                if (rm.GetCachedSubSprite(sheetAddr, $"{prefix}_{dir}_{i}") == null) return i;
            }
            return 8;
        }

        private void FixedUpdate()
        {
            if (_rb == null) return;
            _rb.linearVelocity = _input * _moveSpeed;
        }

        public void Bind(Animator animator, SpriteRenderer renderer)
        {
            _animator = animator;
            _renderer = renderer;
        }
    }
}
