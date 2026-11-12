using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using NUnit.Framework;
using Rootborn.Core.Careers;
using Rootborn.Core.Placement;
using Rootborn.Core.Workspaces;

namespace Rootborn.Game.Tests
{
    /// <summary>세이브 견고성 시험용: 진행 지점마다 슬롯을 한 번 만들어 파일 바이트로 기억하고, 시험마다 새 임시 폴더에 되살린다.</summary>
    internal static class SlotCheckpoints
    {
        public const string Slot = "slot-robust";
        public const string FreeDesk = "growth31.assembly.desk";
        public const string ChefWorkspace = "workspace.chef";

        /// <summary>요리사 일터 면 id(데이터에서 읽는다).</summary>
        public static string ChefSurface => TestWorld.Definitions.Get<Workspace>(ChefWorkspace).SurfaceId;

        /// <summary>요리사 일터 면에 들어간다(처음이면 기본 템플릿이 깔리고 일터 배치 파일이 생긴다).</summary>
        public static void EnterChefWorkspace(GameSession game) => game.EnterPlacementSurface(WorkspaceRoom.GridFor(game.Definitions, ChefWorkspace), ChefSurface);

        public static readonly string[] Names = { "first-command", "day-3", "furniture-placed", "adult-chosen", "workspace-entered" };

        private static readonly Dictionary<string, Lazy<SortedDictionary<string, byte[]>>> Built = Names.ToDictionary(
            n => n, n => new Lazy<SortedDictionary<string, byte[]>>(() => Build(n)));

        /// <summary>그 지점의 슬롯 파일(이름 → 바이트).</summary>
        public static SortedDictionary<string, byte[]> Files(string checkpoint) => Built[checkpoint].Value;

        public static IEnumerable<string> FileNames(string checkpoint) => Files(checkpoint).Keys;

        /// <summary>임시 폴더에 그 지점의 슬롯을 되살린다.</summary>
        public static TempSaves Restore(string checkpoint, string slot = Slot)
        {
            var saves = new TempSaves();
            WriteAll(saves, Files(checkpoint), slot);
            return saves;
        }

        public static void WriteAll(TempSaves saves, IDictionary<string, byte[]> files, string slot = Slot)
        {
            var folder = Path.Combine(saves.Path, slot);
            Directory.CreateDirectory(folder);
            foreach (var pair in files) File.WriteAllBytes(Path.Combine(folder, pair.Key), pair.Value);
        }

        /// <summary>슬롯 폴더의 모든 파일(임시 파일 포함)을 읽는다.</summary>
        public static SortedDictionary<string, byte[]> Read(TempSaves saves, string slot = Slot)
        {
            var result = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
            var folder = Path.Combine(saves.Path, slot);
            if (Directory.Exists(folder))
                foreach (var file in Directory.GetFiles(folder)) result[Path.GetFileName(file)] = File.ReadAllBytes(file);
            return result;
        }

        public static bool Same(IDictionary<string, byte[]> a, IDictionary<string, byte[]> b)
            => a.Count == b.Count && a.All(p => b.TryGetValue(p.Key, out var other) && p.Value.AsSpan().SequenceEqual(other));

        /// <summary>맨 위 Version 값. 객체가 아니거나 Version이 없으면 null.</summary>
        public static int? TopVersion(byte[] bytes)
        {
            try
            {
                using var doc = JsonDocument.Parse(bytes);
                if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("Version", out var v) && v.ValueKind == JsonValueKind.Number)
                    return v.GetInt32();
            }
            catch (JsonException) { }
            return null;
        }

        private static SortedDictionary<string, byte[]> Build(string checkpoint)
        {
            using var saves = new TempSaves();
            switch (checkpoint)
            {
                case "first-command":
                    TestWorld.Start(saves.Store, Slot).TalkTo("npc.first-guide");
                    break;
                case "day-3":
                {
                    GameSession game = TestWorld.Start(saves.Store, Slot);
                    MainChainTests.PlayToMyDream(game, () => game, new List<string>());
                    break;
                }
                case "furniture-placed":
                {
                    GameSession game = TestWorld.Start(saves.Store, Slot);
                    game = MainChainTests.PlayToMyDream(game, () => game, new List<string>());
                    game.PerformLocationActivity("opening.clue.archive-door", "location-activity:archive-door:day3");
                    game.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);
                    Assert.That(game.PlaceFurniture(FreeDesk, HouseRoom.FirstFreeAnchor(game, FreeDesk), FurniturePlacementDirection.North), Is.True);
                    break;
                }
                case "adult-chosen":
                {
                    TestWorld.Start(saves.Store, Slot).TalkTo("npc.first-guide");
                    SaveEdits.EditSnapshot(saves.Store, SaveEdits.MakeChefCandidateReady, Slot);
                    var game = TestWorld.Start(saves.Store, Slot);
                    Assert.That(game.ChooseFacility("facility.chef", "facility:chef").Kind, Is.EqualTo(AdultCareerResultKind.Applied));
                    break;
                }
                case "workspace-entered":
                {
                    TestWorld.Start(saves.Store, Slot).TalkTo("npc.first-guide");
                    SaveEdits.EditSnapshot(saves.Store, SaveEdits.MakeChefCandidateReady, Slot);
                    var game = TestWorld.Start(saves.Store, Slot);
                    Assert.That(game.ChooseFacility("facility.chef", "facility:chef").Kind, Is.EqualTo(AdultCareerResultKind.Applied));
                    EnterChefWorkspace(game);
                    break;
                }
                default: throw new ArgumentException(checkpoint);
            }

            return Read(saves);
        }
    }
}
