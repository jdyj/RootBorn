using UnityEngine;

namespace Rootborn.Game.World
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class LdtkTownMapRuntimeBinding : MonoBehaviour
    {
        [SerializeField] private LdtkTownMapDefinition _definition;
        [SerializeField] private SpriteRenderer _renderer;

        private LdtkTownMapAnalysis _analysis;
        private bool _analysisReady;

        public LdtkTownMapDefinition Definition => _definition;
        public LdtkTownMapAnalysis Analysis
        {
            get
            {
                EnsureAnalyzed();
                return _analysis;
            }
        }

        public bool IsValid
        {
            get
            {
                EnsureAnalyzed();
                return _definition != null && _renderer != null && _renderer.sprite != null && _analysis.CompositeMatchesPixelSize;
            }
        }

        private void Awake()
        {
            ApplyDefinition();
        }

        private void OnValidate()
        {
            _renderer = _renderer != null ? _renderer : GetComponent<SpriteRenderer>();
            ApplyDefinition();
        }

        public void Bind(LdtkTownMapDefinition definition)
        {
            _definition = definition;
            _analysisReady = false;
            ApplyDefinition();
        }

        public void ApplyDefinition()
        {
            _renderer = _renderer != null ? _renderer : GetComponent<SpriteRenderer>();
            if (_definition == null || _renderer == null)
            {
                _analysisReady = false;
                return;
            }

            if (_definition.CompositeSprite != null)
            {
                _renderer.sprite = _definition.CompositeSprite;
            }

            _analysisReady = false;
            EnsureAnalyzed();
            transform.position = new Vector3(_definition.WorldCenter.x, _definition.WorldCenter.y, transform.position.z);
        }

        private void EnsureAnalyzed()
        {
            if (_analysisReady)
            {
                return;
            }

            _analysis = _definition != null ? _definition.Analyze() : LdtkTownMapAnalysis.Empty(null, 32);
            _analysisReady = true;
        }
    }
}