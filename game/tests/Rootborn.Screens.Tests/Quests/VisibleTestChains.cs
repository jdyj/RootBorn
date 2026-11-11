using System;
using System.IO;
using System.Text.Json.Nodes;
using Rootborn.Data;
using Rootborn.Data.Builders;

namespace Rootborn.Screens.Tests.Quests
{
    /// <summary>
    /// 실제 data/를 임시 폴더에 복사하고 개발용 퀘스트 체인(developerOnly)의 표시를 끈 정의. 실제 데이터에는 플레이어에게 보이는 체인이
    /// 하나뿐이라 카드 고르기·받기 단추를 시험하려면 체인이 더 있어야 한다. 한 번만 만들고 프로세스가 끝날 때 지운다. 실제 data/는 고치지 않는다.
    /// </summary>
    internal static class VisibleTestChains
    {
        private static readonly Lazy<GameDefinitions> _definitions = new Lazy<GameDefinitions>(Load);

        public static GameDefinitions Definitions => _definitions.Value;

        private static GameDefinitions Load()
        {
            // 저장소 모양 그대로(root/data, root/game/time.json): 흐르는 시간의 수치 파일은 data 폴더 옆의 game/에서 읽는다(TimeSettings).
            string root = Path.Combine(Path.GetTempPath(), "rootborn-visible-chains-" + Guid.NewGuid().ToString("N"));
            string copy = Path.Combine(root, "data");
            CopyDirectory(TestData.DataDirectory, copy);
            Directory.CreateDirectory(Path.Combine(root, "game"));
            File.Copy(Path.Combine(TestData.RepoRoot, "game", "time.json"), Path.Combine(root, "game", "time.json"));
            AppDomain.CurrentDomain.ProcessExit += (_, _) => { try { Directory.Delete(root, true); } catch (IOException) { } };
            string file = Path.Combine(copy, "studentlife", "QuestChainDefinition.json");
            JsonArray chains = JsonNode.Parse(File.ReadAllText(file))!.AsArray();
            foreach (JsonNode chain in chains)
                if (chain?["developerOnly"] != null) chain["developerOnly"] = false;
            File.WriteAllText(file, chains.ToJsonString());
            LoadResult loaded = GameDataLoader.Load(copy, RootbornBuilders.Register);
            loaded.ThrowIfFailed();
            return loaded.Definitions;
        }

        private static void CopyDirectory(string from, string to)
        {
            Directory.CreateDirectory(to);
            foreach (string dir in Directory.GetDirectories(from, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(Path.Combine(to, Path.GetRelativePath(from, dir)));
            foreach (string f in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
                File.Copy(f, Path.Combine(to, Path.GetRelativePath(from, f)));
        }
    }
}
