using UnityEngine;

namespace Rootborn.Game.Crops
{
    public sealed class CropPlot : MonoBehaviour
    {
        [SerializeField] private CropDefinition _crop;
        [SerializeField] private SpriteRenderer _renderer;
        // FarmGrid 가 plot 을 어느 셀에 등록했는지 역참조 (수확 시 dictionary 정리에 사용).
        public Vector3Int Cell { get; set; }

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
            Tick(deltaSeconds, waterLevel01, isRaining, 1f);
        }

        public void Tick(float deltaSeconds, float waterLevel01, bool isRaining, float fertilizerMultiplier)
        {
            if (_crop == null) return;
            if (CurrentStage >= _crop.StageCount - 1) return;

            float stageDuration = _crop.GetStageDuration(CurrentStage);
            if (stageDuration <= 0f) return;

            float progress01 = StageElapsedSec / stageDuration;
            var ctx = new CropGrowthContext(_crop, CurrentStage, progress01, waterLevel01, isRaining, fertilizerMultiplier);
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
            if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
            if (_renderer == null || _crop == null) return;
            var sprites = _crop.GrowthStageSprites;
            if (CurrentStage >= 0 && CurrentStage < sprites.Length)
            {
                _renderer.sprite = sprites[CurrentStage];
            }
        }
    }
}
