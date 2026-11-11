using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rootborn.Host.World;
using Rootborn.World;

namespace Rootborn.Host.Tests
{
    /// <summary>
    /// 레이어 캐릭터가 방향 키에 따라 방향 줄과 걷기 칸을 바꾸는지, 실제 창과 게임 루프로 본다(장치 이벤트 주입, OS 입력 계층 미확인).
    /// 스크린샷은 Unity 판 플레이 화면(output/assetpacks/character-parts-v1-20261104/previews/playmode)과 나란히 보는 근거다.
    /// </summary>
    [TestFixture, SingleThreaded, NonParallelizable]
    public sealed class CharacterScreenTests
    {
        private PlayerDriver _player;

        [OneTimeSetUp]
        public void StartGame()
        {
            HostOptions options = HostOptions.Resolve(new[] { "--world", "House", "--size", "1280x720" });
            options.TestMode = true;
            _player = new PlayerDriver(options);
            _player.Frames(3);
        }

        [OneTimeTearDown]
        public void StopGame()
        {
            _player?.Dispose();
            _player = null;
        }

        private WorldScreen Screen => _player.Game.World;

        [Test, Order(1)]
        public void CHAR_PLAY_001_NewGameLookIsThePlaygroundBodyWithCasualOutfitAndShortBrownHair()
        {
            Assert.That(Screen.PlayerLook.Body.Id, Is.EqualTo("body_playground"));
            Assert.That(Screen.PlayerLook.Layers.Select(l => l.Id), Is.EqualTo(new[] { "outfit_playground_casual", "head_playground_short_brown" }));
            Assert.That((Screen.PlayerAnimator.Row, Screen.PlayerAnimator.Column), Is.EqualTo((CharacterAnimator.RowDown, CharacterAnimator.StandingColumn)));
            _player.Screenshot("char-01-down-stand");
        }

        [Order(2)]
        [TestCase(TestKey.Left, CharacterAnimator.RowLeft, "char-02-left")]
        [TestCase(TestKey.Right, CharacterAnimator.RowRight, "char-03-right")]
        [TestCase(TestKey.Up, CharacterAnimator.RowUp, "char-04-up")]
        [TestCase(TestKey.Down, CharacterAnimator.RowDown, "char-05-down")]
        public void CHAR_PLAY_002_HoldingADirectionKeyWalksThroughTheStepFramesOfThatRow(TestKey key, int row, string shot)
        {
            var columns = new List<int>();
            _player.KeyDown(key);
            for (int i = 0; i < 40; i++)
            {
                _player.Frames(1);
                columns.Add(Screen.PlayerAnimator.Column);
                if (i == 20) _player.Screenshot(shot + "-walk");
            }
            Assert.That(Screen.PlayerAnimator.Row, Is.EqualTo(row));
            Assert.That(columns.Distinct().OrderBy(c => c), Is.EqualTo(new[] { 0, 1, 2 }), "걷는 동안 세 칸을 다 쓴다");
            _player.KeyUp(key);
            Assert.That(Screen.PlayerAnimator.Row, Is.EqualTo(row), "멈춰도 보던 방향");
            Assert.That(Screen.PlayerAnimator.Column, Is.EqualTo(CharacterAnimator.StandingColumn));
            _player.Screenshot(shot + "-stand");
        }
    }
}
