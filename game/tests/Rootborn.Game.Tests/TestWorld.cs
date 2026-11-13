using System;
using System.Collections.Generic;
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
        private static readonly object _gate = new object();
        private static string _overlayOverride;
        private static Loaded _loaded;

        /// <summary>데이터 덮어쓰기 폴더를 주는 환경 변수(시뮬레이션 테스트 전용, 게임 실행에는 쓰지 않는다). 없으면 data/ 그대로다.</summary>
        public const string OverlayVariable = "ROOTBORN_DATA_OVERLAY";

        private sealed class Loaded
        {
            public GameDefinitions Definitions;
            public GameStartSettings Settings;
            public IReadOnlyList<OverlayChange> Changes;
        }

        public static string RepoRoot => _root.Value;
        public static string DataDirectory => Path.Combine(_root.Value, "data");
        public static GameDefinitions Definitions => Current.Definitions;
        public static GameStartSettings Settings => Current.Settings;

        /// <summary>지금 쓰는 덮어쓰기 폴더. <see cref="UseOverlay"/>로 정한 값, 없으면 환경 변수, 둘 다 없으면 null.</summary>
        public static string OverlayDirectory
        {
            get
            {
                lock (_gate)
                {
                    if (_overlayOverride != null) return _overlayOverride.Length == 0 ? null : FromRepoRoot(_overlayOverride);
                }
                var value = Environment.GetEnvironmentVariable(OverlayVariable);
                return string.IsNullOrWhiteSpace(value) ? null : FromRepoRoot(value);
            }
        }

        /// <summary>상대 경로는 저장소 뿌리 기준이다(테스트의 현재 폴더는 출력 폴더라 기댈 수 없고, 원격 실행은 커밋된 폴더를 상대 경로로만 가리킬 수 있다).</summary>
        private static string FromRepoRoot(string path) => Path.GetFullPath(path, RepoRoot);

        /// <summary>덮어쓰기로 바뀐 필드 목록(없으면 빈 목록). 보고서가 "덮어쓴 데이터로 돈 숫자"라고 적는 근거다.</summary>
        public static IReadOnlyList<OverlayChange> OverlayChanges => Current.Changes;

        /// <summary>테스트 전용: 이 폴더로 덮어쓴 정의로 바꾼다(빈 문자열이면 덮어쓰기 없음). Dispose하면 이전 상태로 돌아간다. 병렬로 도는 테스트가 없을 때만 쓴다.</summary>
        public static IDisposable UseOverlay(string overlayDirectory)
        {
            lock (_gate)
            {
                var previous = _overlayOverride;
                _overlayOverride = overlayDirectory ?? "";
                _loaded = null;
                return new Restore(previous);
            }
        }

        private sealed class Restore : IDisposable
        {
            private readonly string _previous;
            public Restore(string previous) { _previous = previous; }
            public void Dispose()
            {
                lock (_gate) { _overrideReset(_previous); }
            }
        }

        private static void _overrideReset(string previous)
        {
            _overlayOverride = previous;
            _loaded = null;
        }

        private static Loaded Current
        {
            get
            {
                lock (_gate)
                {
                    if (_loaded != null) return _loaded;
                }
                var overlay = OverlayDirectory;
                var result = GameDataLoader.Load(DataDirectory, RootbornBuilders.Register, overlay);
                result.ThrowIfFailed();
                var loaded = new Loaded
                {
                    Definitions = result.Definitions,
                    Settings = GameStartSettings.FromDefinitions(result.Definitions),
                    Changes = result.OverlayChanges,
                };
                lock (_gate) { return _loaded ??= loaded; }
            }
        }
        public static string FixtureSaves => Path.Combine(AppContext.BaseDirectory, "Fixtures", "saves");

        public const string Slot = "slot-test";
        public const string Player = SaveFileNames.DefaultPlayerId;

        public static GameSession Start(ISaveStore store, string slot = Slot, string player = Player)
            => GameSession.Start(Definitions, Settings, store, slot, player);

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
