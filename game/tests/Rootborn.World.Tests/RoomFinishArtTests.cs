using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using NUnit.Framework;
using Rootborn.Core.Building;
using Rootborn.Core.World;

namespace Rootborn.World.Tests
{
    /// <summary>
    /// 자동 테스트(창 없음): 꾸밈 조각 시트의 좌표표(data/atlases/art/world/finishes)를 정의의 그림 키로 푼다.
    /// 조각 이름은 Core가 내는 출처표의 글(시험 표 room-vectors.json의 모든 사례를 RoomTilePlan으로 만든 것)과 글자 그대로 같아야 한다 —
    /// 모든 벽 꾸밈·바닥 꾸밈에 대해 출처표가 부르는 조각이 시트에 있는지 본다. 그림의 픽셀은 보지 않는다.
    /// </summary>
    public sealed class RoomFinishArtTests
    {
        private static readonly string[] Tops = { "wall.plaster.top", "wall.top.brown", "wall.top.cream", "wall.top.light", "wall.top.slate" };

        private static RoomFinishArt Art() => new RoomFinishArt(Path.Combine(Repo.Root, "data"));

        private static RoomFinishDefinition[] Finishes(string slot) =>
            Repo.Definitions.All<RoomFinishDefinition>().Where(f => f.Slot == slot).ToArray();

        /// <summary>꽉 찬 정면 24가지와 좁은 정면 12가지(띠 16)의 이름 — 문법으로 늘어놓은 것(Python 쪽 목록과 따로 쓴 두 번째 손).</summary>
        private static IEnumerable<string> WallPieceNames()
        {
            foreach (int k in new[] { 1, 2 })
            {
                foreach (string end in new[] { "", "l", "r", "lr" })
                    foreach (string top in new[] { "", "c", "t" })
                        yield return "face:" + k + (end + top).Length switch { 0 => "", _ => ":" + end + top };
                foreach (string range in new[] { "0-16", "8-24", "16-32" })
                    foreach (string c in new[] { "", "c" })
                        yield return "face:" + k + ":lr" + c + "@" + range;
            }
        }

        private static IEnumerable<string> SillNames() =>
            from side in new[] { "n", "e", "w" } from range in new[] { "0-16", "8-24", "16-32" } select "sill:" + side + ":" + range;

        [Test]
        public void EveryWallFinishHas36Pieces_AndEveryFloorFinishHas2()
        {
            var art = Art();
            var names = WallPieceNames().ToArray();
            Assert.That(names.Length, Is.EqualTo(36));
            Assert.That(names, Is.Unique);
            foreach (var wall in Finishes(RoomFinishSlots.Wall))
                foreach (string name in names)
                    Assert.That(art.TryPiece(wall, name, out var piece), Is.True, wall.Id + " " + name);
            foreach (var floor in Finishes(RoomFinishSlots.Floor))
                foreach (string name in new[] { "floor", "floor:shade" })
                    Assert.That(art.TryPiece(floor, name, out _), Is.True, floor.Id + " " + name);
        }

        [Test]
        public void PiecesAreOnTheGrid_InsideTheSheet_AndDoNotShareASpot()
        {
            var art = Art();
            var seen = new HashSet<(string, int, int)>();
            foreach (var finish in Repo.Definitions.All<RoomFinishDefinition>())
            {
                var names = finish.Slot == RoomFinishSlots.Wall ? WallPieceNames() : new[] { "floor", "floor:shade" };
                foreach (string name in names)
                {
                    Assert.That(art.TryPiece(finish, name, out var piece), Is.True, finish.Id + " " + name);
                    Assert.That(piece.Texture, Is.EqualTo(finish.Sheet));
                    Assert.That(piece.X % RoomFinishArt.PieceSize + piece.Y % RoomFinishArt.PieceSize, Is.Zero, finish.Id + " " + name);
                    Assert.That(seen.Add((piece.Texture, piece.X, piece.Y)), Is.True, "같은 자리를 두 조각이 쓴다: " + finish.Id + " " + name);
                }
            }
        }

        [Test]
        public void CommonPieces_NineSillsAndTheDoor_ResolveOnTheSheet()
        {
            var art = Art();
            string sheet = Finishes(RoomFinishSlots.Wall).First().Sheet;
            var names = SillNames().ToArray();
            Assert.That(names.Length, Is.EqualTo(9));
            foreach (string name in names.Append(TileSources.Door))
                Assert.That(art.TryCommon(sheet, name, out var piece), Is.True, name);
        }

        [Test]
        public void TheFiveBandColorSetsResolve_WithTheirFourColors()
        {
            var art = Art();
            string sheet = Finishes(RoomFinishSlots.Wall).First().Sheet;
            foreach (string top in Tops)
            {
                Assert.That(art.TryBand(sheet, top, out var c), Is.True, top);
                Assert.That(new[] { c.Outline, c.Dark, c.Face, c.Light }.Distinct().Count(), Is.EqualTo(4), top + ": 네 색이 서로 다르다");
                Assert.That(c.Face >> 24, Is.EqualTo(255u), "투명하지 않다");
            }
        }

        [Test]
        public void BandColors_ArePackedLikeMonoGameColor_LowestByteIsRed()
        {
            string sheet = Finishes(RoomFinishSlots.Wall).First().Sheet;
            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(Repo.Root, "data", "atlases", sheet + ".atlas.json")));
            var cream = doc.RootElement.GetProperty("bands").GetProperty("wall.top.cream");
            Assert.That(Art().TryBand(sheet, "wall.top.cream", out var c), Is.True);
            foreach (var (letter, packed) in new[] { ("o", c.Outline), ("d", c.Dark), ("f", c.Face), ("l", c.Light) })
            {
                string hex = cream.GetProperty(letter).GetString();
                uint r = Convert.ToUInt32(hex.Substring(1, 2), 16), g = Convert.ToUInt32(hex.Substring(3, 2), 16), b = Convert.ToUInt32(hex.Substring(5, 2), 16);
                Assert.That(packed & 0xFF, Is.EqualTo(r), letter + " R");
                Assert.That((packed >> 8) & 0xFF, Is.EqualTo(g), letter + " G");
                Assert.That((packed >> 16) & 0xFF, Is.EqualTo(b), letter + " B");
                Assert.That(packed >> 24, Is.EqualTo(255u), letter + " A");
            }
        }

        [Test]
        public void BandOfTheSheet_Is16()
        {
            var art = Art();
            foreach (string sheet in Repo.Definitions.All<RoomFinishDefinition>().Select(f => f.Sheet).Distinct())
                Assert.That(art.BandOf(sheet), Is.EqualTo(16), sheet);
        }

        [Test]
        public void UnknownThings_ReturnFalse_NotThrow()
        {
            var art = Art();
            var wall = Finishes(RoomFinishSlots.Wall).First();
            Assert.That(art.TryPiece(wall, "face:9", out _), Is.False);
            Assert.That(art.TryPiece(wall, "", out _), Is.False);
            Assert.That(art.TryPiece(wall, null, out _), Is.False);
            Assert.That(art.TryPiece(null, "face:1", out _), Is.False);
            Assert.That(art.TryPiece(wall, "floor", out _), Is.False, "벽 꾸밈에는 바닥 조각이 없다");
            Assert.That(art.TryCommon(wall.Sheet, "sill:s:0-16", out _), Is.False);
            Assert.That(art.TryBand(wall.Sheet, "wall.top.nope", out _), Is.False);
            var other = new RoomFinishDefinition("finish.x", RoomFinishSlots.Wall, "k", "art/world/finishes/none.png", "paper", null, null);
            Assert.That(art.TryPiece(other, "face:1", out _), Is.False);
            Assert.That(art.TryCommon("art/world/finishes/none.png", TileSources.Door, out _), Is.False);
            Assert.That(art.TryBand("art/world/finishes/none.png", Tops[0], out _), Is.False);
            Assert.That(art.BandOf("art/world/finishes/none.png"), Is.Zero);
        }

        [Test]
        public void EveryPieceNameTheCorePlanEmits_IsOnTheSheet_ForEveryFinish()
        {
            var art = Art();
            var walls = Finishes(RoomFinishSlots.Wall);
            var floors = Finishes(RoomFinishSlots.Floor);
            string sheet = walls.First().Sheet;
            int checkedParts = 0;
            var asked = new HashSet<string>();

            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(Repo.Root, "Tools", "pixel-kit", "tests", "fixtures", "room-vectors.json")));
            foreach (var c in doc.RootElement.GetProperty("cases").EnumerateArray())
            {
                // 조각 시트는 정면 1·2칸, 띠 16만 굽는다. 그 밖의 사례(face-height-3, band-8 등)는 시트에 없는 조각을 부르고, 그런 맵은
                // 맵 검사(MapChecks "출처표가 쓰는 조각")가 알린다.
                if (c.GetProperty("faceHeight").GetInt32() != 2 || c.GetProperty("band").GetInt32() != art.BandOf(sheet))
                    continue;
                var rows = c.GetProperty("grid").EnumerateArray().Select(r => r.GetString()).ToArray();
                int columns = rows.Max(r => r.Length);
                var cells = new int[columns * rows.Length];
                for (int y = 0; y < rows.Length; y++)
                    for (int x = 0; x < rows[y].Length; x++)
                        cells[y * columns + x] = rows[y][x] == '#' ? RoomCells.Wall : rows[y][x] == '.' ? RoomCells.Floor : RoomCells.Out;
                var doors = c.GetProperty("doors").EnumerateArray().Select(d => new CellPos(d[0].GetInt32(), d[1].GetInt32())).ToList();
                var plan = RoomTilePlan.Build(new RoomGrid(columns, rows.Length, cells), c.GetProperty("faceHeight").GetInt32(), c.GetProperty("band").GetInt32(), doors);

                foreach (CellTiles tiles in plan.Cells)
                    foreach (TilePart part in tiles.Parts)
                    {
                        checkedParts++;
                        switch (part.Source)
                        {
                            case TileSources.Face:
                            case TileSources.Narrow:
                                asked.Add(part.Piece);
                                foreach (var wall in walls) Assert.That(art.TryPiece(wall, part.Piece, out _), Is.True, c.GetProperty("id") + " " + wall.Id + " " + part.Piece);
                                break;
                            case TileSources.Floor:
                                string key = part.Shade.StartsWith("floor:shade", StringComparison.Ordinal) ? "floor:shade" : "floor";
                                asked.Add(key);
                                foreach (var floor in floors) Assert.That(art.TryPiece(floor, key, out _), Is.True, floor.Id + " " + key);
                                break;
                            case TileSources.Sill:
                            case TileSources.Door:
                                asked.Add(part.Piece);
                                Assert.That(art.TryCommon(sheet, part.Piece, out _), Is.True, c.GetProperty("id") + " " + part.Piece);
                                break;
                            case TileSources.Band:
                                Assert.That(part.Shade, Is.AnyOf("o", "d", "f", "l"));
                                break;
                            default:
                                Assert.Fail("처리하지 않은 출처 종류 " + part.Source);
                                break;
                        }
                    }
            }

            Assert.That(checkedParts, Is.GreaterThan(1000));
            Assert.That(asked.Count, Is.GreaterThan(20), "시험 표가 여러 조각을 부른다");
        }

        [Test]
        public void TheFinishSheetsAndAtlasesExistInTheRepository()
        {
            foreach (string sheet in Repo.Definitions.All<RoomFinishDefinition>().Select(f => f.Sheet).Distinct())
            {
                Assert.That(File.Exists(Path.Combine(Repo.Root, sheet)), Is.True, sheet);
                Assert.That(File.Exists(Path.Combine(Repo.Root, "data", "atlases", sheet + ".atlas.json")), Is.True, sheet);
            }
        }
    }
}
