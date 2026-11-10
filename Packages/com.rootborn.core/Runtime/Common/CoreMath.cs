using System;

namespace Rootborn.Core.Common
{
    /// <summary>엔진 수학 함수 없이 같은 결과를 내는 계산. 이름과 동작은 규칙 코드가 쓰던 것과 같다.</summary>
    public static class CoreMath
    {
        /// <summary>value를 0 이상 length 미만으로 접는다. 음수도 앞에서부터 접힌다(-1, 24 → 23).</summary>
        public static float Repeat(float value, float length)
        {
            float wrapped = value - MathF.Floor(value / length) * length;
            return wrapped < 0f ? 0f : wrapped > length ? length : wrapped;
        }

        public static int FloorToInt(float value) => (int)MathF.Floor(value);

        public static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;
    }
}
