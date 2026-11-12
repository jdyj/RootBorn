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
                    // 하루에 한 번, 슬롯 파일과 그 사본을 디스크로 내린다(평소 저장은 내리지 않는다 — FileSaveStore 머리말).
                    FlushToDisk(file);
                    var copy = Path.Combine(temp, fileName);
                    File.Copy(file, copy);
                    FlushToDisk(copy);
                }

                // 같은 날 사본이 있으면 옆으로 치워 두고 새 사본을 넣은 뒤 지운다. 도중에 꺼져도 그 날 사본이 하나는 남는다
                // (치워 둔 폴더는 '.'으로 시작해 다음 Prune이 지운다).
                var target = Path.Combine(copies, name);
                string aside = null;
                if (Directory.Exists(target))
                {
                    aside = Path.Combine(copies, "." + name + "." + Guid.NewGuid().ToString("N") + ".old");
                    Directory.Move(target, aside);
                    DayCopyStepForTests?.Invoke("old-aside");
                }

                Directory.Move(temp, target);
                if (aside != null) Directory.Delete(aside, true);
            }
            finally
            {
                if (Directory.Exists(temp)) Directory.Delete(temp, true);
            }

            Prune(copies, keepCount);
        }

        /// <summary>테스트용: 디스크로 내린 파일 경로마다 부른다.</summary>
        internal Action<string> FlushedForTests { get; set; }

        /// <summary>테스트용: 사본 바꿔 넣기의 단계 이름으로 부른다.</summary>
        internal Action<string> DayCopyStepForTests { get; set; }

        // 파일 내용을 운영체제 캐시에서 디스크로 내린다. 내용은 바꾸지 않는다.
        private void FlushToDisk(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.Read)) stream.Flush(true);
            FlushedForTests?.Invoke(path);
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
