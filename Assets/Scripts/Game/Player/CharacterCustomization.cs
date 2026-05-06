using System;
using UnityEngine;

namespace Rootborn.Game.Player
{
    [Serializable]
    public sealed class CharacterCustomization
    {
        public enum Facing { Down, Left, Right, Up }

        [SerializeField] private int _bodyVariant;
        [SerializeField] private int _hairVariant;
        [SerializeField] private int _outfitVariant;
        [SerializeField] private Facing _defaultFacing = Facing.Down;

        public int BodyVariant { get => _bodyVariant; set => _bodyVariant = Mathf.Max(0, value); }
        public int HairVariant { get => _hairVariant; set => _hairVariant = Mathf.Max(0, value); }
        public int OutfitVariant { get => _outfitVariant; set => _outfitVariant = Mathf.Max(0, value); }
        public Facing DefaultFacing { get => _defaultFacing; set => _defaultFacing = value; }
    }
}