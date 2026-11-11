using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Rootborn.Core.Inventory;

namespace Rootborn.Data
{
    /// <summary>
    /// 돈 설정(game/economy.json). data/는 변환기 결과라 고치지 않고, 아이템 정의에는 통화임을 알리는 필드가 없어서
    /// 어느 아이템이 지갑으로 가는지는 MonoGame 판이 직접 갖는 이 파일이 정한다. 코드는 아이템 id를 모른다.
    /// 위치는 data 폴더 옆의 game/economy.json이다(저장소 안의 상대 위치). 파일이 없으면 통화가 없다(돈도 가방의 아이템).
    /// 파일이 있는데 읽지 못하거나 가리키는 아이템이 없으면 읽기 오류다.
    /// </summary>
    internal static class EconomySettings
    {
        public const string FileName = "economy.json";

        public static string PathFor(string dataDirectory)
        {
            var parent = Path.GetDirectoryName(Path.GetFullPath(dataDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            return Path.Combine(parent ?? string.Empty, "game", FileName);
        }

        public static ItemDefinition ReadCurrency(string dataDirectory, GameDefinitions definitions, List<LoadError> errors)
        {
            var path = PathFor(dataDirectory);
            if (!File.Exists(path)) return null;

            string id;
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllBytes(path));
                id = document.RootElement.ValueKind == JsonValueKind.Object
                     && document.RootElement.TryGetProperty("currencyItem", out var value) && value.ValueKind == JsonValueKind.String
                    ? value.GetString()
                    : null;
            }
            catch (JsonException e)
            {
                errors.Add(new LoadError("parse", "economy", "", path, e.Message));
                return null;
            }

            if (string.IsNullOrEmpty(id))
            {
                errors.Add(new LoadError("missing-field", "economy", "", path, "currencyItem(통화 아이템 id)이 없다"));
                return null;
            }

            if (!definitions.TryGet<ItemDefinition>(id, out var item))
            {
                errors.Add(new LoadError("missing-ref", "economy", id, path, $"currencyItem '{id}'인 ItemDefinition이 없다"));
                return null;
            }

            return item;
        }
    }
}
