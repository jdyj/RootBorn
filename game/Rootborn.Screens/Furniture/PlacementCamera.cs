using System;

namespace Rootborn.Screens.Furniture
{
    /// <summary>
    /// 가구 배치 중의 카메라(Unity HousePlacementCameraController). 캐릭터를 따라가지 않고, 방향 키·WASD로 옮기고 +/-·휠로 확대하며
    /// F로 방 전체를 본다. 배치를 켜면 바로 전체 보기다. 값은 칸 단위(Unity 직교 카메라의 반 높이 = 칸 수)이고 위치는 맵 픽셀(y는 아래로).
    /// 맵 크기는 Begin에 넘긴 맵에서 온다. 화면 비율은 프레임마다 Host가 넘긴다(창 크기가 바뀔 수 있다). 엔진 참조 없음.
    /// </summary>
    public sealed class PlacementCamera
    {
        /// <summary>Unity 판 수치. 새 디자인에서 카메라 감을 바꾸려면 여기만 고친다.</summary>
        public static class Settings
        {
            public const float MinHalfHeightCells = 4f;
            public const float MaxHalfHeightCells = 14f;
            /// <summary>휠 한 칸에 반 높이를 이만큼(칸) 바꾼다.</summary>
            public const float ZoomStepCells = 1f;
            /// <summary>+/-를 누르고 있으면 초당 이만큼(칸) 바꾼다.</summary>
            public const float KeyboardZoomCellsPerSecond = 8f;
            public const float PanCellsPerSecond = 10f;
            public const float FullViewPadding = 1.08f;
        }

        private float _mapWidth, _mapHeight;
        private int _cellSize = 1;
        private float _aspect = 16f / 9f;

        public bool Active { get; private set; }
        /// <summary>보는 곳의 가운데(맵 픽셀).</summary>
        public float CenterX { get; private set; }
        public float CenterY { get; private set; }
        /// <summary>화면 높이의 반이 몇 칸인가(Unity orthographicSize).</summary>
        public float HalfHeightCells { get; private set; } = Settings.MinHalfHeightCells;

        /// <summary>배치를 켠다: 맵 크기(픽셀)와 칸 크기를 받고 전체 보기로 시작한다(Unity SetPlacementMode → FullView).</summary>
        public void Begin(float mapWidthPixels, float mapHeightPixels, int cellSize, float aspect)
        {
            if (cellSize <= 0) throw new ArgumentOutOfRangeException(nameof(cellSize));
            _mapWidth = Math.Max(1f, mapWidthPixels);
            _mapHeight = Math.Max(1f, mapHeightPixels);
            _cellSize = cellSize;
            _aspect = Math.Max(0.01f, aspect);
            Active = true;
            FullView();
        }

        /// <summary>배치를 끈다. Host는 다시 캐릭터를 따라가는 카메라로 돌아간다.</summary>
        public void End() => Active = false;

        /// <summary>창 비율이 바뀌었다. 위치를 맵 안으로 다시 맞춘다.</summary>
        public void SetAspect(float aspect)
        {
            float next = Math.Max(0.01f, aspect);
            if (Math.Abs(next - _aspect) < 0.0001f) return;
            _aspect = next;
            Clamp();
        }

        /// <summary>방 전체가 들어오게(여백 1.08배), 최소·최대 확대 안에서.</summary>
        public void FullView()
        {
            if (!Active) return;
            float halfHeight = _mapHeight / _cellSize * 0.5f;
            float halfWidthAsHeight = _mapWidth / _cellSize / _aspect * 0.5f;
            HalfHeightCells = ClampHalfHeight(Math.Max(halfHeight, halfWidthAsHeight) * Settings.FullViewPadding);
            CenterX = _mapWidth / 2f;
            CenterY = _mapHeight / 2f;
            Clamp();
        }

        /// <summary>방향(-1·0·1, y는 아래로)으로 dt초 옮긴다. 대각선은 같은 속도.</summary>
        public void Pan(int dx, int dy, float dt)
        {
            if (!Active || (dx == 0 && dy == 0) || dt <= 0f) return;
            float length = MathF.Sqrt(dx * dx + dy * dy);
            float step = Settings.PanCellsPerSecond * _cellSize * dt / length;
            CenterX += dx * step;
            CenterY += dy * step;
            Clamp();
        }

        /// <summary>+(direction -1, 가까이)·-(direction 1, 멀리)를 누르고 있는 동안.</summary>
        public void KeyboardZoom(int direction, float dt)
        {
            if (direction == 0 || dt <= 0f) return;
            ZoomBy(direction * Settings.ZoomStepCells * Settings.KeyboardZoomCellsPerSecond * dt);
        }

        /// <summary>휠: 위로 굴리면(양수) 가까이.</summary>
        public void Wheel(float notches)
        {
            if (Math.Abs(notches) <= 0.01f) return;
            ZoomBy(notches > 0f ? -Settings.ZoomStepCells : Settings.ZoomStepCells);
        }

        private void ZoomBy(float deltaCells)
        {
            if (!Active) return;
            HalfHeightCells = ClampHalfHeight(HalfHeightCells + deltaCells);
            Clamp();
        }

        /// <summary>화면 픽셀 높이가 이만할 때 맵 1픽셀이 화면 몇 픽셀인가.</summary>
        public float Scale(float screenHeightPixels) => screenHeightPixels / (2f * HalfHeightCells * _cellSize);

        /// <summary>화면 왼쪽 위가 맵의 어느 픽셀인가.</summary>
        public (float X, float Y) TopLeft(float screenWidthPixels, float screenHeightPixels)
        {
            float scale = Scale(screenHeightPixels);
            return (CenterX - screenWidthPixels / scale / 2f, CenterY - screenHeightPixels / scale / 2f);
        }

        private static float ClampHalfHeight(float value) => Math.Clamp(value, Settings.MinHalfHeightCells, Settings.MaxHalfHeightCells);

        /// <summary>보이는 영역이 맵 안에 들어오게. 맵이 보이는 영역보다 작은 축은 가운데(Unity ClampPosition).</summary>
        private void Clamp()
        {
            float halfH = HalfHeightCells * _cellSize, halfW = halfH * _aspect;
            CenterX = halfW * 2f <= _mapWidth ? Math.Clamp(CenterX, halfW, _mapWidth - halfW) : _mapWidth / 2f;
            CenterY = halfH * 2f <= _mapHeight ? Math.Clamp(CenterY, halfH, _mapHeight - halfH) : _mapHeight / 2f;
        }
    }
}
