using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Rootborn.Core.Save;

namespace Rootborn.Data.Save
{
    /// <summary>
    /// 날마다 남기는 슬롯 사본: &lt;root&gt;/&lt;슬롯&gt;/backups/&lt;날 번호&gt;/에 슬롯 폴더의 파일(맨 위 칸만, '.'으로 시작하는 임시 파일 빼고)을 복사한다.
    /// 사본은 임시 폴더에 다 복사한 뒤 이름을 바꿔 넣으므로, 도중에 꺼지면 그 날 사본이 없을 뿐 반쯤 찬 사본은 남지 않는다.
    /// 슬롯을 지우면 사본도 같이 지워진다(슬롯 폴더 안에 있다).
    /// </summary>
    public sealed partial class FileSaveStore : ISaveSlotDayCopies
    {
        /// <summary>슬롯 폴더 안의 날마다 사본 폴더 이름.</summary>
        public const string DayCopiesFolder = "backups";

        public void KeepDayCopy(string saveSlot, int dayNumber, int keepCount)
        {
            if (keepCount <= 0) return;
            if (dayNumber < 0) throw new ArgumentOutOfRangeException(nameof(dayNumber));
            var folder = SlotFolder(saveSlot);
            RecoverBatch(saveSlot);
            if (!Directory.Exists(folder)) return;

            var copies = Path.Combine(folder, DayCopiesFolder);
            Directory.CreateDirectory(copies);
            var name = dayNumber.ToString(CultureInfo.InvariantCulture);
            var temp = Path.Combine(copies, "." + name + "." + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                Directory.CreateDirectory(temp);
                foreach (var file in Directory.GetFiles(folder))
                {
                    var fileName = Path.GetFileName(file);
                    if (fileName.StartsWith(".", StringComparison.Ordinal)) continue; // 쓰다 남은 임시 파일
                    File.Copy(file, Path.Combine(temp, fileName));
                }

                var target = Path.Combine(copies, name);
                if (Directory.Exists(target)) Directory.Delete(target, true);
                Directory.Move(temp, target);
            }
            finally
            {
                if (Directory.Exists(temp)) Directory.Delete(temp, true);
            }

            Prune(copies, keepCount);
        }

        // 날 번호 이름의 사본은 큰 것부터 keepCount개만 둔다. 앞서 꺼져 남은 임시 폴더('.'으로 시작)는 지운다. 그 밖의 폴더는 건드리지 않는다.
        private static void Prune(string copies, int keepCount)
        {
            var days = new List<int>();
            foreach (var dir in Directory.GetDirectories(copies))
            {
                var dirName = Path.GetFileName(dir);
                if (dirName.StartsWith(".", StringComparison.Ordinal)) Directory.Delete(dir, true);
                else if (int.TryParse(dirName, NumberStyles.None, CultureInfo.InvariantCulture, out var day)
                         && day.ToString(CultureInfo.InvariantCulture) == dirName) days.Add(day);
            }

            days.Sort();
            for (int i = 0; i < days.Count - keepCount; i++)
                Directory.Delete(Path.Combine(copies, days[i].ToString(CultureInfo.InvariantCulture)), true);
        }
    }
}
