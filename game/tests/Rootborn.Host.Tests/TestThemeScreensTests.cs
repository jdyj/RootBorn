using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using NUnit.Framework;
using Rootborn.Host.Rendering;
using Rootborn.Host.Text;
using Rootborn.Screens;
using Rootborn.Ui;

namespace Rootborn.Host.Tests
{
    /// <summary>
    /// 그림 교체 준비: 테두리 두께·그림 크기가 도시형 몰딩과 다른 시험용 테마(TestTheme/theme.json, 단색 사각형 PNG는 여기서 만든다)로
    /// 실제 창과 게임 루프를 띄우고, 등록 표(ScreenCatalog)의 모든 화면을 하나씩 열어 그린다.
    /// 본다: 열고 그리는 동안 예외가 없다, 테마에 없는 아이콘을 그리려 하지 않는다, 패널 안 글자가 패널 밖으로 나가지 않는다.
    /// 화면은 ScreenStack.Open으로 연다(키·상호작용으로 여는 길은 각 화면 테스트가 본다). 자동 테스트이고 실제 플레이 근거가 아니다.
    /// 넘침은 화면을 고치지 않고 KnownOverflows에 이유와 함께 적어 둔다. 보고서·스크린샷: output/host-theme-evidence/.
    /// </summary>
    [TestFixture, SingleThreaded, NonParallelizable]
    public sealed class TestThemeScreensTests
    {
        /// <summary>
        /// 알려진 넘침: 화면 id → 이유. 테마와 상관없이 화면 배치에서 생기는 것들이다. 새로 생기면 테스트가 실패한다.
        /// 고치는 쪽은 화면 묶음 세션(Rootborn.Screens)이다.
        /// </summary>
        private static readonly Dictionary<string, string> KnownOverflows = new Dictionary<string, string>(StringComparer.Ordinal)
        {
        };

        private const float Tolerance = 0.5f;

        /// <summary>내용이 있어야 보이는 화면을 여는 상호작용(새 게임 데이터의 NPC·장소·알바).</summary>
        private static readonly Rootborn.Core.Interaction.InteractionEvent[] ContentInteractions =
        {
            new Rootborn.Core.Interaction.InteractionEvent("House", "npc", "npc.first-guide"),
            new Rootborn.Core.Interaction.InteractionEvent("Town", "location-activity", "location.library"),
            new Rootborn.Core.Interaction.InteractionEvent("Town", "part-time-work", "work.shift.corner-store"),
        };

        private string _work;
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _work = Path.Combine(Path.GetTempPath(), "rootborn-test-theme-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_work);
            _root = HostOptions.Resolve(new[] { "--sample" }).RepoRoot;
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_work, true); } catch (IOException) { }
        }

        /// <summary>TestTheme/theme.json을 임시 폴더로 옮기고 testImages대로 PNG를 만든다.</summary>
        private string MakeTestTheme()
        {
            string dir = Path.Combine(_work, "theme");
            Directory.CreateDirectory(dir);
            string source = Path.Combine(_root, "game", "tests", "Rootborn.Host.Tests", "TestTheme", ThemeFolder.FileName);
            File.Copy(source, Path.Combine(dir, ThemeFolder.FileName));
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(source));
            foreach (JsonProperty image in doc.RootElement.GetProperty("testImages").EnumerateObject())
            {
                JsonElement e = image.Value;
                JsonElement size = e.GetProperty("size");
                TestPng.WriteFramedSquare(Path.Combine(dir, image.Name), size[0].GetInt32(), size[1].GetInt32(), e.GetProperty("edge").GetInt32(),
                    Hex(e.GetProperty("line").GetString()), Hex(e.GetProperty("fill").GetString()));
            }
            return dir;
        }

        private static uint Hex(string s) => (uint.Parse(s.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture) << 8) | 0xFF;

        [Test]
        public void TestTheme_DiffersFromUrban_InImageSizeAndBorder()
        {
            ThemeFolder test = ThemeFolder.Load(MakeTestTheme(), _root);
            ThemeFolder urban = ThemeFolder.Load(Path.Combine(_root, "game", "themes", "urban"), _root);
            foreach (string skin in ThemeRequirements.Skins)
            {
                SkinSource a = test.Skins.Get(skin), b = urban.Skins.Get(skin);
                Assert.That(a.Padding, Is.Not.EqualTo(b.Padding), skin + " 테두리 두께(논리)가 같다");
                Assert.That((a.Slices.ColX(3) - a.Slices.ColX(0), a.Slices.RowY(3) - a.Slices.RowY(0)),
                    Is.Not.EqualTo((b.Slices.ColX(3) - b.Slices.ColX(0), b.Slices.RowY(3) - b.Slices.RowY(0))), skin + " 그림 크기가 같다");
            }
        }

        [Test]
        public void EveryRegisteredScreen_DrawsWithTheTestTheme_WithoutErrors_AndTextStaysInsideItsPanel()
        {
            string theme = MakeTestTheme();
            string evidence = Path.Combine(_root, "output", "host-theme-evidence");
            HostOptions options = HostOptions.Resolve(new[]
            {
                "--save-root", Path.Combine(_work, "saves"), "--slot", "slot-1", "--size", "1920x1080",
                "--evidence", evidence, "--ui-theme", theme,
            });
            options.TestMode = true;
            Directory.CreateDirectory(Path.Combine(_work, "saves"));

            var errors = new List<string>();
            var overflows = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
            var borderTouches = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
            var notDrawn = new List<string>();
            var report = new StringBuilder();
            List<ScreenEntry> entries = ScreenCatalog.Create().Screens.OrderBy(e => e.Id, StringComparer.Ordinal).ToList();
            Assert.That(entries.Count, Is.GreaterThan(10));

            using (var player = new PlayerDriver(options))
            {
                player.Frames(3);
                Assert.That(player.Game.Renderer.Skins.Get(UiSkin.Panel).Texture, Does.StartWith(theme), "시험용 테마로 그린다");
                using var textRenderer = new TextRenderer(player.Game.GraphicsDevice, options.FontPath);
                var measurer = new HostTextMeasurer(textRenderer) { Scale = player.Game.Fit.Scale };

                foreach (ScreenEntry entry in entries)
                {
                    bool openedHere = false;
                    try
                    {
                        if (!player.Screens.IsOpen(entry.Id)) { player.Screens.Open(entry.Id); openedHere = true; }
                        player.Frames(2); // 실제 그리기(테마 그림으로)
                    }
                    catch (Exception e)
                    {
                        errors.Add(entry.Id + ": " + e.GetType().Name + ": " + e.Message);
                        TryClose(player, entry.Id, openedHere);
                        continue;
                    }

                    if (!Inspect(entry.Id)) notDrawn.Add(entry.Id);
                    TryClose(player, entry.Id, openedHere);
                }

                // 내용이 있어야 보이는 화면(대화, 장소, 미니게임)은 그 상호작용으로 한 번 더 연다.
                foreach (Rootborn.Core.Interaction.InteractionEvent interaction in ContentInteractions.Append(StorageChestHostTests.PrepareChest(player)))
                {
                    try
                    {
                        player.Screens.Interaction(interaction);
                        player.Frames(3);
                    }
                    catch (Exception e)
                    {
                        errors.Add(interaction.Kind + "/" + interaction.TargetId + ": " + e.GetType().Name + ": " + e.Message);
                        continue;
                    }
                    foreach (GameScreen open in player.Screens.OpenScreens.Where(o => o.Layer != ScreenLayer.Hud).ToList())
                    {
                        if (Inspect(open.Id)) notDrawn.Remove(open.Id);
                        TryClose(player, open.Id, true);
                    }
                }

                bool Inspect(string id)
                {
                    GameScreen screen = player.Screens.Find(id);
                    var list = new DrawList();
                    if (screen != null && screen.IsOpen) EmitVisible(screen.Root, list, measurer);
                    if (list.Commands.Count == 0) return false;
                    player.ScreenshotScreen(id, "theme-" + id);
                    Check(id, list, measurer, player.Game.Renderer.Skins, overflows, borderTouches);
                    return true;
                }

                List<string> missing = player.Game.Renderer.MissingImages.Where(id => id.StartsWith("icon:", StringComparison.Ordinal)).OrderBy(x => x).ToList();
                report.AppendLine("# 시험용 테마로 모든 화면 그리기");
                report.AppendLine("화면 " + entries.Count + "개: " + string.Join(", ", entries.Select(e => e.Id)));
                report.AppendLine("열거나 그리다 난 예외: " + (errors.Count == 0 ? "없음" : string.Join(" / ", errors)));
                report.AppendLine("테마에 없는 아이콘: " + (missing.Count == 0 ? "없음" : string.Join(", ", missing)));
                report.AppendLine("그릴 것이 없던 화면(숨은 HUD 등): " + (notDrawn.Count == 0 ? "없음" : string.Join(", ", notDrawn)));
                report.AppendLine("## 패널 밖으로 나간 글자");
                foreach (var pair in overflows) report.AppendLine("- " + pair.Key + ": " + string.Join(" | ", pair.Value.Distinct()));
                report.AppendLine("## 패널 테두리(안쪽 여백)에 닿은 글자 — 참고");
                foreach (var pair in borderTouches) report.AppendLine("- " + pair.Key + ": " + string.Join(" | ", pair.Value.Distinct().Take(6)));
                Directory.CreateDirectory(evidence);
                File.WriteAllText(Path.Combine(evidence, "theme-report.md"), report.ToString());
                TestContext.Out.WriteLine(report.ToString());

                Assert.That(errors, Is.Empty);
                Assert.That(missing, Is.Empty, "ThemeRequirements.Icons에 빠진 아이콘 키");
            }

            Assert.That(overflows.Keys.Except(KnownOverflows.Keys), Is.Empty, "새로 넘친 화면(보고서: output/host-theme-evidence/theme-report.md)");
            Assert.That(KnownOverflows.Keys.Except(overflows.Keys), Is.Empty, "더 넘치지 않는 화면은 KnownOverflows에서 지운다");
        }

        private static void TryClose(PlayerDriver player, string id, bool openedHere)
        {
            if (!openedHere) return;
            try { player.Screens.Close(id); player.Frames(1); } catch (Exception) { }
        }

        /// <summary>UiTree.Emit과 같은 순서로 한 화면의 보이는 요소만 그리기 명령으로 낸다.</summary>
        internal static void EmitVisible(UiNode node, DrawList list, ITextMeasurer text)
        {
            if (!node.Visible) return;
            node.Emit(list, text);
            for (int i = 0; i < node.Children.Count; i++) EmitVisible(node.Children[i], list, text);
        }

        /// <summary>
        /// 글자마다 그 아래 패널(앞서 그린 패널 중 글자 가운데를 품는 가장 작은 것)을 찾아, 보이는 글자 상자(잘라 내기 안쪽)가 패널 밖으로 나가는지 본다.
        /// 패널의 안쪽 여백(테마의 테두리 두께)에 닿는 것은 따로 모은다.
        /// </summary>
        internal static void Check(string screenId, DrawList list, ITextMeasurer measurer, UiSkins skins,
            SortedDictionary<string, List<string>> overflows, SortedDictionary<string, List<string>> borderTouches)
        {
            var panels = new List<(UiRect rect, string skin)>();
            var clips = new Stack<UiRect>();
            foreach (DrawCommand c in list.Commands)
            {
                switch (c.Kind)
                {
                    case DrawKind.Panel: panels.Add((c.Rect, c.Skin)); break;
                    case DrawKind.PushClip: clips.Push(clips.Count == 0 ? c.Rect : Intersect(clips.Peek(), c.Rect)); break;
                    case DrawKind.PopClip: if (clips.Count > 0) clips.Pop(); break;
                    case DrawKind.Text:
                    {
                        var box = new UiRect(c.Rect.X, c.Rect.Y, measurer.Width(c.Text, c.FontSize), measurer.LineHeight(c.FontSize));
                        if (clips.Count > 0)
                        {
                            box = Intersect(box, clips.Peek());
                            if (box.W <= 0 || box.H <= 0) break;
                        }
                        float cx = box.X + box.W / 2f, cy = box.Y + box.H / 2f;
                        (UiRect rect, string skin)? under = null;
                        foreach (var p in panels)
                            if (p.rect.Contains(cx, cy) && (under == null || p.rect.W * p.rect.H < under.Value.rect.W * under.Value.rect.H)) under = p;
                        if (under == null) break;
                        UiRect r = under.Value.rect;
                        string label = "\"" + Short(c.Text) + "\"(" + under.Value.skin + ")";
                        if (box.X < r.X - Tolerance || box.Y < r.Y - Tolerance || box.Right > r.Right + Tolerance || box.Bottom > r.Bottom + Tolerance)
                            Add(overflows, screenId, label);
                        else if (skins.TryGet(under.Value.skin, out SkinSource skin))
                        {
                            SkinPadding pad = skin.Padding;
                            if (box.X < r.X + pad.Left - Tolerance || box.Y < r.Y + pad.Top - Tolerance || box.Right > r.Right - pad.Right + Tolerance || box.Bottom > r.Bottom - pad.Bottom + Tolerance)
                                Add(borderTouches, screenId, label);
                        }
                        break;
                    }
                }
            }
        }

        private static UiRect Intersect(UiRect a, UiRect b)
        {
            float x = Math.Max(a.X, b.X), y = Math.Max(a.Y, b.Y);
            return new UiRect(x, y, Math.Max(0f, Math.Min(a.Right, b.Right) - x), Math.Max(0f, Math.Min(a.Bottom, b.Bottom) - y));
        }

        private static string Short(string s) => s.Length <= 16 ? s : s.Substring(0, 16) + "…";

        private static void Add(SortedDictionary<string, List<string>> into, string key, string value)
        {
            if (!into.TryGetValue(key, out List<string> list)) into[key] = list = new List<string>();
            list.Add(value);
        }
    }

    /// <summary>테스트가 쓰는 작은 PNG 만들기(8비트 RGBA, 거르기 없음).</summary>
    public static class TestPng
    {
        /// <summary>바깥 edge 픽셀 띠는 line 색, 안쪽은 fill 색인 사각형. 색은 0xRRGGBBAA.</summary>
        public static void WriteFramedSquare(string path, int w, int h, int edge, uint line, uint fill)
        {
            var raw = new byte[h * (w * 4 + 1)];
            int i = 0;
            for (int y = 0; y < h; y++)
            {
                raw[i++] = 0;
                for (int x = 0; x < w; x++)
                {
                    bool border = x < edge || y < edge || x >= w - edge || y >= h - edge;
                    uint c = border ? line : fill;
                    raw[i++] = (byte)(c >> 24); raw[i++] = (byte)(c >> 16); raw[i++] = (byte)(c >> 8); raw[i++] = (byte)c;
                }
            }
            byte[] compressed;
            using (var buffer = new MemoryStream())
            {
                using (var z = new ZLibStream(buffer, CompressionLevel.Optimal, leaveOpen: true)) z.Write(raw, 0, raw.Length);
                compressed = buffer.ToArray();
            }
            using FileStream s = File.Create(path);
            s.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
            var ihdr = new byte[13];
            Big(ihdr, 0, w); Big(ihdr, 4, h);
            ihdr[8] = 8; ihdr[9] = 6; // 8비트, RGBA
            Chunk(s, "IHDR", ihdr);
            Chunk(s, "IDAT", compressed);
            Chunk(s, "IEND", Array.Empty<byte>());
        }

        private static void Big(byte[] b, int at, int v) { b[at] = (byte)(v >> 24); b[at + 1] = (byte)(v >> 16); b[at + 2] = (byte)(v >> 8); b[at + 3] = (byte)v; }

        private static void Chunk(Stream s, string type, byte[] data)
        {
            var len = new byte[4]; Big(len, 0, data.Length);
            s.Write(len);
            byte[] t = Encoding.ASCII.GetBytes(type);
            s.Write(t); s.Write(data);
            uint crc = Crc(Crc(0xFFFFFFFFu, t), data) ^ 0xFFFFFFFFu;
            var c = new byte[4]; Big(c, 0, (int)crc);
            s.Write(c);
        }

        private static uint[] _table;

        private static uint Crc(uint crc, byte[] data)
        {
            if (_table == null)
            {
                _table = new uint[256];
                for (uint n = 0; n < 256; n++)
                {
                    uint c = n;
                    for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                    _table[n] = c;
                }
            }
            foreach (byte b in data) crc = _table[(crc ^ b) & 0xFF] ^ (crc >> 8);
            return crc;
        }
    }
}
