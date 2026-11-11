using System;
using Rootborn.Core.World;

namespace Rootborn.World
{
    /// <summary>
    /// 캐릭터를 부드럽게 따라가는 카메라(Unity CameraFollow.LateUpdate): 원하는 자리(캐릭터 가운데를 경계 안으로 자른 것)로
    /// SmoothDamp해 가고, 그 결과를 다시 경계 안으로 자른다. 데드존은 없다. 처음과 Snap 뒤에는 바로 맞춘다(Unity SnapToTarget).
    /// 상태는 카메라 가운데와 속도(맵 픽셀)뿐이다. 프레임 간격이 달라도 같은 시각에는 거의 같은 자리다(SmoothDamp는 감쇠를 지수로 근사한다).
    /// </summary>
    public sealed class SmoothCamera
    {
        private readonly float _smoothSeconds;
        private float _x, _y, _vx, _vy;
        private bool _placed;

        public SmoothCamera(float smoothSeconds) { _smoothSeconds = Math.Max(0f, smoothSeconds); }

        /// <summary>마지막으로 정한 카메라 가운데(맵 픽셀, 반올림 전).</summary>
        public float CenterX => _x;
        public float CenterY => _y;

        /// <summary>다음 Follow에서 바로 맞춘다(맵에 들어섰을 때, 가구 배치 카메라에서 돌아올 때).</summary>
        public void Snap() => _placed = false;

        /// <summary>한 번 따라간다. dt는 지난 Follow 뒤로 흐른 시간. 돌려주는 사각형은 CameraFrame.View(정수 픽셀, 경계 안)이다.</summary>
        public AreaRect Follow(float focusX, float focusY, AreaRect bounds, float viewW, float viewH, float dt)
        {
            AreaRect desired = CameraFrame.View(focusX, focusY, bounds, viewW, viewH);
            float tx = desired.X + viewW / 2f, ty = desired.Y + viewH / 2f;
            if (!_placed || _smoothSeconds <= 0f)
            {
                _x = tx; _y = ty; _vx = 0f; _vy = 0f;
                _placed = true;
            }
            else if (dt > 0f)
            {
                _x = SmoothDamp(_x, tx, ref _vx, _smoothSeconds, dt);
                _y = SmoothDamp(_y, ty, ref _vy, _smoothSeconds, dt);
            }
            // 결과도 경계 안으로 자른다(Unity: transform.position = ClampToBounds(SmoothDamp(...))). 속도는 그대로 둔다(Unity와 같다).
            _x = ClampCenter(_x, bounds.X, bounds.W, viewW);
            _y = ClampCenter(_y, bounds.Y, bounds.H, viewH);
            AreaRect view = CameraFrame.View(_x, _y, bounds, viewW, viewH);
            return view;
        }

        /// <summary>경계 안에서 가운데가 갈 수 있는 곳(경계가 화면보다 작으면 경계 가운데 — CameraFrame과 같다).</summary>
        private static float ClampCenter(float center, float start, float size, float view) =>
            size <= view ? start + size / 2f : Math.Clamp(center, start + view / 2f, start + size - view / 2f);

        /// <summary>Unity Mathf.SmoothDamp(최고 속도 제한 없음)과 같은 식.</summary>
        public static float SmoothDamp(float current, float target, ref float velocity, float smoothTime, float dt)
        {
            smoothTime = Math.Max(0.0001f, smoothTime);
            float omega = 2f / smoothTime;
            float x = omega * dt;
            float exp = 1f / (1f + x + 0.48f * x * x + 0.235f * x * x * x);
            float change = current - target;
            float originalTo = target;
            target = current - change;
            float temp = (velocity + omega * change) * dt;
            velocity = (velocity - omega * temp) * exp;
            float output = target + (change + temp) * exp;
            if (originalTo - current > 0f == output > originalTo)
            {
                output = originalTo;
                velocity = (output - originalTo) / dt;
            }
            return output;
        }
    }
}
