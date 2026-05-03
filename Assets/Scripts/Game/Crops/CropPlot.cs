using UnityEngine;

namespace Rootborn.Game.Crops
{
    public sealed class CropPlot : MonoBehaviour
    {
        [SerializeField] private CropDefinition _crop;
        [SerializeField] private SpriteRenderer _renderer;

        public CropDefinition Crop => _crop;
        public int CurrentStage { get; private set; }
        public float StageElapsedSec { get; private set; }
        public bool IsHarvestable => _crop != null && _crop.IsHarvestable(CurrentStage);

        public void Plant(CropDefinition crop)
        {
            _crop = crop;
            CurrentStage = 0;
            StageElapsedSec = 0f;
            UpdateSprite();
        }

        public void Tick(float deltaSeconds, float waterLevel01, bool isRaining)
        {
            if (_crop == null) return;
            if (CurrentStage >= _crop.StageCount - 1) return;

            float stageDuration = _crop.GetStageDuration(CurrentStage);
            if (stageDuration <= 0f) return;

            float progress01 = StageElapsedSec / stageDuration;
            var ctx = new CropGrowthContext(_crop, CurrentStage, progress01, waterLevel01, isRaining);
            float mul = _crop.ComputeGrowthRateMultiplier(in ctx);

            StageElapsedSec += deltaSeconds * mul;

            while (StageElapsedSec >= stageDuration && CurrentStage < _crop.StageCount - 1)
            {
                StageElapsedSec -= stageDuration;
                CurrentStage++;
                stageDuration = _crop.GetStageDuration(CurrentStage);
                if (stageDuration <= 0f) break;
            }

            UpdateSprite();
        }

        private void UpdateSprite()
        {
            if (_renderer == null || _crop == null) return;
            var sprites = _crop.GrowthStageSprites;
            if (CurrentStage >= 0 && CurrentStage < sprites.Length)
            {
                _renderer.sprite = sprites[CurrentStage];
            }
        }
    }
}
