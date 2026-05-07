using System;
using UnityEngine;

namespace Rootborn.Game.Family
{
    [Serializable]
    public sealed class CharacterPartLayer
    {
        [SerializeField] private string _categoryId;
        [SerializeField] private SpriteRenderer _renderer;

        public CharacterPartLayer(string categoryId, SpriteRenderer renderer)
        {
            _categoryId = categoryId;
            _renderer = renderer;
        }

        public string CategoryId => _categoryId;
        public SpriteRenderer Renderer => _renderer;
    }
}
