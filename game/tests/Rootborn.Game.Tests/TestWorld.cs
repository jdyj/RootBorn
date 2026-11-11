using System;
using System.IO;
using Rootborn.Core.Save;
using Rootborn.Data;
using Rootborn.Data.Builders;
using Rootborn.Data.Save;

namespace Rootborn.Game.Tests
{
    /// <summary>실제 data/를 한 번 읽어 테스트끼리 같이 쓴다. 정의는 바뀌지 않는 객체다.</summary>
    internal static class TestWorld
    {
        private static readonly Lazy<string> _root = new Lazy<string>(FindRoot);
        private static readonly Lazy<GameDefinitions> _definitions = new Lazy<GameDefinitions>(LoadDefinitions);
        private static readonly Lazy<GameStartSettings> _settings = new Lazy<GameStartSettings>(() => GameStartSettings.FromRegistry(DataDirectory));

        public static string DataDirectory => Path.Combine(_root.Value, "data");
        public static GameDefinitions Definitions => _definitions.Value;
        public static GameStartSettings Settings => _settings.Value;
        public static string FixtureSaves => Path.Combine(AppContext.BaseDirectory, "Fixtures", "saves");

        public const string Slot = "slot-test";
        public const string Player = SaveFileNames.DefaultPlayerId;

        public static GameSession Start(ISaveStore store, string slot = Slot, string player = Player)
            => GameSession.Start(Definitions, Settings, store, slot, player);

        private static GameDefinitions LoadDefinitions()
        {
            var result = GameDataLoader.Load(DataDirectory, RootbornBuilders.Register);
            result.ThrowIfFailed();
            return result.Definitions;
        }

        private static string FindRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "data", "registry.json"))) return dir.FullName;
                dir = dir.Parent;
            }
            throw new DirectoryNotFoundException("data/registry.json을 찾지 못했다");
        }
    }

    /// <summary>테스트마다 쓰고 지우는 임시 저장 폴더와 그 위의 FileSaveStore.</summary>
    internal sealed class TempSaves : IDisposable
    {
        public TempSaves()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rootborn-game-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
            Store = new FileSaveStore(Path);
        }

        public string Path { get; }
        public FileSaveStore Store { get; }

        /// <summary>고정 세이브 폴더(읽기만)를 이 임시 폴더의 슬롯으로 복사한다.</summary>
        public void CopyFixture(string fixtureSlotFolder, string slot)
        {
            var source = System.IO.Path.Combine(TestWorld.FixtureSaves, fixtureSlotFolder);
            var target = System.IO.Path.Combine(Path, slot);
            Directory.CreateDirectory(target);
            foreach (var file in Directory.GetFiles(source)) File.Copy(file, System.IO.Path.Combine(target, System.IO.Path.GetFileName(file)));
        }

        public void Dispose()
        {
            try { Directory.Delete(Path, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
}
