using System;
using UnityEngine;

namespace Rootborn.Game.Family
{
    public sealed class CharacterPartAnimator : MonoBehaviour
    {
        [SerializeField] private float _framesPerSecond = 8f;
        [SerializeField] private int _walkFrameCount = 4;

        private CharacterPartComposer _composer;
        private CharacterAppearance _appearance;
        private CharacterPartDefinition[] _definitions = Array.Empty<CharacterPartDefinition>();
        private Func<CharacterPartDefinition, string, Sprite> _resolveSprite;
        private Vector2 _input;
        private Vector2 _facing = new Vector2(0f, -1f);
        private float _elapsed;
        private int _lastMotionKey;
        private CharacterPartMotionDefinition _motionDefinition;
        private bool _lastWalking;
        private CharacterPartAnimationClipDefinition _activeClip;
        private float _clipElapsed;

        public float FramesPerSecond
        {
            get => _framesPerSecond;
            set => _framesPerSecond = Mathf.Max(0.01f, value);
        }

        public int WalkFrameCount
        {
            get => _walkFrameCount;
            set => _walkFrameCount = Mathf.Max(1, value);
        }

        public CharacterPartAnimationClipDefinition ActiveClip => _activeClip;
        public CharacterPartMotionDefinition MotionDefinition => _motionDefinition;

        public void ConfigureMotion(CharacterPartMotionDefinition profile)
        {
            if (_motionDefinition == profile)
            {
                return;
            }

            _motionDefinition = profile;
            _elapsed = 0f;
            _lastMotionKey = GetMotionKey();
            _lastWalking = _input.sqrMagnitude > 0.01f;
        }

        public void Configure(
            CharacterPartComposer composer,
            CharacterAppearance appearance,
            CharacterPartDefinition[] definitions,
            Func<CharacterPartDefinition, string, Sprite> resolveSprite)
        {
            _composer = composer != null ? composer : GetComponent<CharacterPartComposer>();
            _appearance = appearance;
            _definitions = definitions ?? Array.Empty<CharacterPartDefinition>();
            _resolveSprite = resolveSprite;
            if (_composer != null)
            {
                _composer.EnsureLayers(_definitions);
            }
        }

        public void SetMotion(Vector2 input, Vector2 facing)
        {
            _input = input;
            if (facing.sqrMagnitude > 0.0001f)
            {
                _facing = facing.normalized;
            }

            int motionKey = GetMotionKey();
            bool walking = _input.sqrMagnitude > 0.01f;
            if (motionKey != _lastMotionKey || walking != _lastWalking)
            {
                _elapsed = 0f;
                _lastMotionKey = motionKey;
                _lastWalking = walking;
            }
        }

        public void PlayClip(CharacterPartAnimationClipDefinition clip)
        {
            if (clip == null)
            {
                return;
            }

            _activeClip = clip;
            _clipElapsed = 0f;
        }

        public void StopClip()
        {
            _activeClip = null;
            _clipElapsed = 0f;
        }

        public void Tick(float deltaTime)
        {
            if (_composer == null || _appearance == null || _definitions == null || _resolveSprite == null)
            {
                return;
            }

            float safeDelta = Mathf.Max(0f, deltaTime);
            if (_activeClip != null)
            {
                _clipElapsed += safeDelta;
                int clipColumn = Mathf.FloorToInt(_clipElapsed * _activeClip.FramesPerSecond);
                if (_activeClip.Loop)
                {
                    clipColumn %= _activeClip.FrameCount;
                }
                else if (clipColumn >= _activeClip.FrameCount)
                {
                    StopClip();
                    ApplyMotionFrame(0f);
                    return;
                }

                _composer.ApplyAnimationFrame(_appearance, _definitions, _activeClip.Row, clipColumn, _resolveSprite);
                return;
            }

            ApplyMotionFrame(safeDelta);
        }

        private void ApplyMotionFrame(float deltaTime)
        {
            bool walking = _input.sqrMagnitude > 0.01f;
            int row = _motionDefinition != null
                ? (walking ? _motionDefinition.WalkRow : _motionDefinition.IdleRow)
                : FacingToRow(_facing);
            int column = _motionDefinition != null ? _motionDefinition.GetIdleColumn(_facing) : 0;
            if (walking)
            {
                _elapsed += deltaTime;
                float fps = _motionDefinition != null ? _motionDefinition.FramesPerSecond : FramesPerSecond;
                int count = _motionDefinition != null ? _motionDefinition.FramesPerDirection : WalkFrameCount;
                int start = _motionDefinition != null ? _motionDefinition.GetWalkStartColumn(_facing) : 0;
                column = start + Mathf.FloorToInt(_elapsed * fps) % count;
            }

            _composer.ApplyAnimationFrame(_appearance, _definitions, row, column, _resolveSprite);
        }

        private int GetMotionKey()
        {
            return _motionDefinition != null
                ? CharacterPartMotionDefinition.GetDirectionIndex(_facing)
                : FacingToRow(_facing);
        }

        private static int FacingToRow(Vector2 facing)
        {
            if (Mathf.Abs(facing.x) > Mathf.Abs(facing.y))
            {
                return 1;
            }

            return facing.y > 0f ? 2 : 0;
        }
    }
}
