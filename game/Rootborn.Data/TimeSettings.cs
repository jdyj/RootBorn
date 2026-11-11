using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using Rootborn.Core.Time;

namespace Rootborn.Data
{
    /// <summary>
    /// 흐르는 시간의 수치(game/time.json): 하루 시작, 취침 시각, 밤 체력 감소, 늦잠 회복 감소. 하루 길이는 data/time의 TimeDefinition에서 온다.
    /// data/는 변환기 결과라 고치지 않는다. 위치는 data 폴더 옆의 game/time.json이다(저장소 안의 상대 위치, EconomySettings와 같다).
    /// 하루 길이가 있는 데이터(실제 게임)에 파일이 없거나, 파일을 읽지 못하거나, 값이 빠지거나 틀리면 읽기 오류다.
    /// 하루 길이가 없는 데이터(시험용 작은 폴더)는 파일이 없어도 되고 규칙이 없다.
    /// </summary>
    internal static class TimeSettings
    {
        public const string FileName = "time.json";
        private const string Source = "time";

        public static string PathFor(string dataDirectory)
        {
            var parent = Path.GetDirectoryName(Path.GetFullPath(dataDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            return Path.Combine(parent ?? string.Empty, "game", FileName);
        }

        public static TimeRules Read(string dataDirectory, GameDefinitions definitions, List<LoadError> errors)
        {
            var path = PathFor(dataDirectory);
            var day = definitions.All<DayClock>().FirstOrDefault();
            if (!File.Exists(path))
            {
                if (day != null) errors.Add(new LoadError("missing-file", Source, "", path, "흐르는 시간의 수치 파일이 없다(하루 시작·취침 시각·밤 체력 감소·늦잠 회복 감소)"));
                return null;
            }

            if (day == null)
            {
                errors.Add(new LoadError("missing-ref", Source, "", path, "하루 길이(data/time의 TimeDefinition)가 없다"));
                return null;
            }

            try
            {
                using var document = JsonDocument.Parse(File.ReadAllBytes(path));
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    errors.Add(new LoadError("parse", Source, "", path, "맨 위가 객체가 아니다"));
                    return null;
                }

                int before = errors.Count;
                int dayStart = ClockField(root, "dayStart", path, errors);
                int bedtime = ClockField(root, "bedtime", path, errors);
                int nightLoss = CountField(root, "nightEnergyLossPerHour", path, errors);
                int lateLoss = CountField(root, "lateSleepRecoveryLossPerHour", path, errors);
                if (errors.Count == before && dayStart == bedtime)
                    errors.Add(new LoadError("invalid-field", Source, "", path, "bedtime이 dayStart와 같다(깨어 있는 시간이 없다)"));
                if (errors.Count != before) return null;
                return new TimeRules(day.RealSecondsPerGameDay, dayStart, bedtime, nightLoss, lateLoss);
            }
            catch (JsonException e)
            {
                errors.Add(new LoadError("parse", Source, "", path, e.Message));
                return null;
            }
        }

        // "HH:MM"(00:00~23:59) → 분.
        private static int ClockField(JsonElement root, string name, string path, List<LoadError> errors)
        {
            if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
            {
                errors.Add(new LoadError("missing-field", Source, "", path, name + "(\"HH:MM\")이 없다"));
                return 0;
            }

            var text = value.GetString() ?? string.Empty;
            var parts = text.Split(':');
            if (parts.Length == 2 && parts[0].Length == 2 && parts[1].Length == 2
                && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out int hours)
                && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int minutes)
                && hours < 24 && minutes < 60)
                return hours * 60 + minutes;
            errors.Add(new LoadError("invalid-field", Source, "", path, name + " '" + text + "'이 \"HH:MM\"(00:00~23:59)이 아니다"));
            return 0;
        }

        private static int CountField(JsonElement root, string name, string path, List<LoadError> errors)
        {
            if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Number)
            {
                errors.Add(new LoadError("missing-field", Source, "", path, name + "(0 이상의 정수)이 없다"));
                return 0;
            }

            if (value.TryGetInt32(out int count) && count >= 0) return count;
            errors.Add(new LoadError("invalid-field", Source, "", path, name + "은 0 이상의 정수여야 한다"));
            return 0;
        }
    }
}
