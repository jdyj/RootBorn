using UnityEngine;
using UnityEngine.InputSystem;

namespace Rootborn.Game.Player
{
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] private float _moveSpeed = 4f;
        [SerializeField] private Animator _animator;
        [SerializeField] private SpriteRenderer _renderer;

        private InputAction _moveAction;
        private Vector2 _input;
        private Vector2 _lastFacing = new Vector2(0f, -1f); // 기본 Down

        private static readonly int HashMoveX = Animator.StringToHash("MoveX");
        private static readonly int HashMoveY = Animator.StringToHash("MoveY");
        private static readonly int HashSpeed = Animator.StringToHash("Speed");

        public Vector2 LastFacing => _lastFacing;
        public Vector2 Input => _input;

        private void Awake()
        {
            // Animator/Renderer 미설정 시 자동 검색 (FarmAutoFiller가 인스턴스화 후 와이어링 안 한 케이스)
            if (_animator == null) _animator = GetComponent<Animator>();
            if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
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
        }

        private void OnDisable()
        {
            if (_moveAction != null)
            {
                _moveAction.Disable();
                _moveAction.Dispose();
                _moveAction = null;
            }
        }

        private void Update()
        {
            _input = _moveAction != null ? _moveAction.ReadValue<Vector2>() : Vector2.zero;
            float dt = UnityEngine.Time.deltaTime;
            transform.position += (Vector3)(_input * (_moveSpeed * dt));

            if (_input.sqrMagnitude > 0.01f)
            {
                _lastFacing = _input.normalized;
            }

            if (_animator != null)
            {
                _animator.SetFloat(HashMoveX, _input.x);
                _animator.SetFloat(HashMoveY, _input.y);
                _animator.SetFloat(HashSpeed, _input.sqrMagnitude);
            }

            if (_renderer != null && Mathf.Abs(_input.x) > 0.01f)
            {
                // Pixelwood Side.png 원본은 왼쪽을 향함 → 오른쪽 입력일 때 flipX
                _renderer.flipX = _input.x > 0f;
            }
        }

        public void Bind(Animator animator, SpriteRenderer renderer)
        {
            _animator = animator;
            _renderer = renderer;
        }
    }
}
