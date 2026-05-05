using Rootborn.Game.Crops;

namespace Rootborn.Game.Farming
{
    /// <summary>
    /// FarmGrid 의 셀 단위 토양 상태. struct 라 Dictionary 값으로 복사되며,
    /// FarmGrid 는 변경 후 dictionary 에 다시 대입한다.
    /// 별도 .cs 파일로 분리: Unity 의 한 파일 1 클래스 규약 + script-update-or-create 호환.
    /// </summary>
    public struct TileSoilState
    {
        public bool IsTilled;
        public int LastWateredDay;        // -1 = 한 번도 안 줌
        public float WaterLevel01;
        public int LastFertilizedDay;
        public float FertilizerMultiplier; // 비료 미적용 시 1f
        public int FertilizerExpiresOnDay; // <=0 이면 미적용
        public CropPlot Plot;

        public static TileSoilState CreateUntilled()
        {
            return new TileSoilState
            {
                IsTilled = false,
                LastWateredDay = -1,
                WaterLevel01 = 0f,
                LastFertilizedDay = -1,
                FertilizerMultiplier = 1f,
                FertilizerExpiresOnDay = 0,
                Plot = null,
            };
        }
    }
}
