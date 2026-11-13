using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Rootborn.Core.Building;
using Rootborn.Core.World;

namespace Rootborn.Core.Tests.Building
{
    /// <summary>픽셀 키트가 낸 시험 표(Tools/pixel-kit/tests/fixtures/room-vectors.json)를 읽는다. Core는 파일·JSON을 모르고 테스트가 읽는다.
    /// 꼴은 Tools/pixel-kit/README.md "방 만들기 — 시험 표".</summary>
    internal static class RoomVectors
    {
        internal sealed class Part
        {
            public string Src, Shade, Piece, Px;
            public bool HasOwner;
            public int OwnX, OwnY;

            public override string ToString() =>
                Src + " " + (Shade ?? Piece) + (HasOwner ? " own(" + OwnX + "," + OwnY + ")" : "") + " px=" + Px;
        }

        internal sealed class Cell
        {
            public int X, Y;
            public string Kind;
            public List<Part> Parts = new List<Part>();
        }

        internal sealed class Case
        {
            public string Id;
            public int FaceHeight, Band;
            public string[] Rows;
            public List<CellPos> Doors = new List<CellPos>();
            public List<Cell> Cells = new List<Cell>();

            public RoomGrid Grid()
            {
                int columns = 0;
                foreach (string row in Rows)
                    columns = Math.Max(columns, row.Length);
                var cells = new int[columns * Rows.Length];
                for (int y = 0; y < Rows.Length; y++)
                    for (int x = 0; x < Rows[y].Length; x++)
                        cells[y * columns + x] = Rows[y][x] == '#' ? RoomCells.Wall : Rows[y][x] == '.' ? RoomCells.Floor : RoomCells.Out;
                return new RoomGrid(columns, Rows.Length, cells);   // 짧은 줄의 나머지는 밖(꼴 문서)
            }

            public RoomTilePlan Build() => RoomTilePlan.Build(Grid(), FaceHeight, Band, Doors);

            public override string ToString() => Id;
        }

        internal sealed class Table
        {
            public int CellSize;
            public Dictionary<string, (string Field, bool Own)> Sources = new Dictionary<string, (string, bool)>();
            public List<Case> Cases = new List<Case>();
        }

        private static Table _table;

        internal static string FilePath => Path.Combine(AppContext.BaseDirectory, "Fixtures", "room-vectors.json");

        internal static Table Load()
        {
            if (_table != null)
                return _table;
            using var doc = JsonDocument.Parse(File.ReadAllText(FilePath));
            var root = doc.RootElement;
            var table = new Table { CellSize = root.GetProperty("cell").GetInt32() };
            foreach (var s in root.GetProperty("sources").EnumerateObject())
                table.Sources[s.Name] = (s.Value.GetProperty("field").GetString(), s.Value.GetProperty("own").GetBoolean());
            foreach (var c in root.GetProperty("cases").EnumerateArray())
            {
                var item = new Case
                {
                    Id = c.GetProperty("id").GetString(),
                    FaceHeight = c.GetProperty("faceHeight").GetInt32(),
                    Band = c.GetProperty("band").GetInt32(),
                };
                var rows = new List<string>();
                foreach (var r in c.GetProperty("grid").EnumerateArray())
                    rows.Add(r.GetString());
                item.Rows = rows.ToArray();
                foreach (var d in c.GetProperty("doors").EnumerateArray())
                    item.Doors.Add(new CellPos(d[0].GetInt32(), d[1].GetInt32()));
                foreach (var cell in c.GetProperty("cells").EnumerateArray())
                {
                    var v = new Cell { X = cell.GetProperty("x").GetInt32(), Y = cell.GetProperty("y").GetInt32(), Kind = cell.GetProperty("kind").GetString() };
                    foreach (var p in cell.GetProperty("parts").EnumerateArray())
                    {
                        var part = new Part { Src = p.GetProperty("src").GetString(), Px = p.GetProperty("px").GetString() };
                        if (p.TryGetProperty("shade", out var shade)) part.Shade = shade.GetString();
                        if (p.TryGetProperty("piece", out var piece)) part.Piece = piece.GetString();
                        if (p.TryGetProperty("own", out var own))
                        {
                            part.HasOwner = true;
                            part.OwnX = own[0].GetInt32();
                            part.OwnY = own[1].GetInt32();
                        }
                        v.Parts.Add(part);
                    }
                    item.Cells.Add(v);
                }
                table.Cases.Add(item);
            }
            _table = table;
            return table;
        }

        internal static IEnumerable<Case> Cases() => Load().Cases;

        internal static Case Find(string id) => Load().Cases.Find(c => c.Id == id) ?? throw new InvalidOperationException("시험 표에 사례 " + id + "가 없다");
    }
}
