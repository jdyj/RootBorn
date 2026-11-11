using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NUnit.Framework;

namespace Rootborn.Host.Tests
{
    /// <summary>
    /// 저장한 스크린샷 PNG는 모든 픽셀이 불투명(알파 255)이다. 장면 버퍼는 반투명 그림을 알파를 곱하지 않고(NonPremultiplied) 섞으며
    /// 알파가 깎이는데, 창에는 불투명(Opaque)으로 복사해 보이지 않지만 PNG를 그대로 쓰면 바탕이 비친다(저녁·밤 건물 아래의 밝은 줄).
    /// 실제 창과 게임 루프, 화면 버퍼에서 저장한 파일을 다시 읽어 본다.
    /// </summary>
    [TestFixture, SingleThreaded, NonParallelizable]
    public sealed class ScreenshotAlphaTests
    {
        [TestCase(19f, "evening")]
        [TestCase(22f, "night")]
        public void ASavedScreenshot_IsOpaqueEverywhere(float hour, string name)
        {
            HostOptions options = HostOptions.Resolve(new[]
            {
                "--world", "Town", "--hour", hour.ToString(System.Globalization.CultureInfo.InvariantCulture), "--time-scale", "0", "--size", "1280x720",
            });
            options.TestMode = true;
            options.EvidenceDir = Path.Combine(options.RepoRoot, "output", "host-ambience-evidence");
            using var player = new PlayerDriver(options);
            player.Frames(3);
            string path = player.Screenshot("screenshot-alpha-" + name);

            using FileStream stream = File.OpenRead(path);
            using Texture2D saved = Texture2D.FromStream(player.Game.GraphicsDevice, stream);
            var pixels = new Color[saved.Width * saved.Height];
            saved.GetData(pixels);
            int translucent = 0;
            foreach (Color c in pixels) if (c.A != 255) translucent++;
            Assert.That(translucent, Is.EqualTo(0), "알파가 255가 아닌 픽셀 수 / " + pixels.Length);
        }
    }
}
