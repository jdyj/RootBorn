using System;

namespace Rootborn.Core.Minigames
{
    public enum MinigameMechanic { Sequence, Choice, Timing, Placement, TargetDodge }

    /// <summary>성장 보너스가 도전 하나에 더하는 값. 도전을 만들 때마다 새로 계산한다.</summary>
    public struct MinigameTuning
    {
        public float TimingWindow, ExtraSeconds, Harmony, Steadiness;
        public int ExtraMistakes, HintCount;
    }

    /// <summary>배치 판 위의 칸. 엔진의 정수 벡터 대신 쓴다.</summary>
    public readonly struct GridPoint : IEquatable<GridPoint>
    {
        public GridPoint(int x, int y) { X = x; Y = y; }
        public int X { get; }
        public int Y { get; }
        public bool Equals(GridPoint other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is GridPoint other && Equals(other);
        public override int GetHashCode() => (X * 397) ^ Y;
    }

    /// <summary>칸 사각형. 끝(XMax, YMax)은 포함하지 않는다.</summary>
    public readonly struct GridRect
    {
        public GridRect(GridPoint position, GridPoint size) { X = position.X; Y = position.Y; Width = size.X; Height = size.Y; }
        public int X { get; }
        public int Y { get; }
        public int Width { get; }
        public int Height { get; }
        public int XMin => Math.Min(X, X + Width);
        public int YMin => Math.Min(Y, Y + Height);
        public int XMax => Math.Max(X, X + Width);
        public int YMax => Math.Max(Y, Y + Height);
        public bool Contains(GridPoint point) => point.X >= XMin && point.Y >= YMin && point.X < XMax && point.Y < YMax;
        public bool Overlaps(GridRect other) => other.XMin < XMax && other.XMax > XMin && other.YMin < YMax && other.YMax > YMin;
    }

    /// <summary>조준 화면 위의 점(0~1 범위로 쓴다).</summary>
    public readonly struct PlanePoint
    {
        public PlanePoint(float x, float y) { X = x; Y = y; }
        public float X { get; }
        public float Y { get; }
        public static float Distance(PlanePoint a, PlanePoint b)
        {
            float dx = a.X - b.X, dy = a.Y - b.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }
    }

    internal static class MinigameMath
    {
        public static float Clamp(float value, float min, float max) => value < min ? min : value > max ? max : value;
        public static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;

        // 엔진의 반올림과 같다(가운데 값은 짝수 쪽).
        public static int RoundToInt(float value) => (int)Math.Round(value);
        public static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
