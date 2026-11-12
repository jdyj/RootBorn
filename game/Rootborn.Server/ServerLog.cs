using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace Rootborn.Server
{
    /// <summary>
    /// 서버의 운영 로그. 줄마다 시각("yyyy-MM-dd HH:mm:ss")을 붙여 표준 출력에 쓰고, 파일을 받았으면(--log) 파일에도 쓴다.
    /// 파일은 한도(<see cref="MaxBytes"/>)를 넘기 전에 돌려 쓴다: 지금 파일을 "&lt;파일&gt;.1"로, 그 전 것은 ".2"로 … 밀고 <see cref="KeepFiles"/>개를 넘는 것은 지운다.
    /// 여러 스레드(루프, 콘솔 입력)가 써도 줄이 섞이지 않는다. 운영자가 읽는 것이라 현지화하지 않는다.
    /// 파일에 쓰지 못하면(디스크가 참 등) 표준 출력에 한 번 알리고 파일 쓰기를 멈춘다 — 서버는 계속 돈다.
    /// </summary>
    public sealed class ServerLog : IDisposable
    {
        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);
        private readonly object _gate = new object();
        private readonly TextWriter _console;
        private readonly Func<DateTime> _now;
        private FileStream _file;
        private bool _fileBroken;

        /// <param name="console">표준 출력(테스트는 StringWriter).</param>
        /// <param name="filePath">로그 파일. 없으면 표준 출력만.</param>
        /// <param name="maxBytes">파일 하나의 한도(바이트). 넘기 전에 돌려 쓴다.</param>
        /// <param name="keepFiles">남기는 지난 파일 수(.1 ~ .N). 0이면 돌릴 때 지난 것을 지운다.</param>
        /// <param name="now">시각. 없으면 지금 시각(테스트가 바꿔 넣는다).</param>
        /// <exception cref="IOException">로그 파일을 열 수 없다.</exception>
        /// <exception cref="UnauthorizedAccessException">로그 파일을 열 수 없다.</exception>
        public ServerLog(TextWriter console, string filePath = null, long maxBytes = ServerOptions.DefaultLogMaxBytes, int keepFiles = ServerOptions.DefaultLogFiles,
            Func<DateTime> now = null)
        {
            _console = console ?? TextWriter.Null;
            _now = now ?? (() => DateTime.Now);
            if (maxBytes < 1024) throw new ArgumentException("로그 파일 한도는 1KB 이상이다: " + maxBytes, nameof(maxBytes));
            if (keepFiles < 0) throw new ArgumentException("남기는 로그 파일 수는 0 이상이다: " + keepFiles, nameof(keepFiles));
            MaxBytes = maxBytes;
            KeepFiles = keepFiles;
            if (string.IsNullOrWhiteSpace(filePath)) return;
            FilePath = Path.GetFullPath(filePath);
            string folder = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
            _file = Open(FilePath);
        }

        public string FilePath { get; }
        public long MaxBytes { get; }
        public int KeepFiles { get; }

        /// <summary>시각을 붙여 한 줄 쓴다.</summary>
        public void Write(string line)
        {
            lock (_gate)
            {
                string stamped = _now().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + " " + (line ?? string.Empty);
                _console.WriteLine(stamped);
                _console.Flush();
                if (_file == null) return;
                try
                {
                    byte[] bytes = Utf8NoBom.GetBytes(stamped + Environment.NewLine);
                    if (_file.Length > 0 && _file.Length + bytes.Length > MaxBytes) Rotate();
                    _file.Write(bytes, 0, bytes.Length);
                    _file.Flush();
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                {
                    BreakFile(e);
                }
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                _file?.Dispose();
                _file = null;
            }
        }

        // 지금 파일을 닫고 .1로, 지난 것을 하나씩 밀고 다시 연다.
        private void Rotate()
        {
            _file.Dispose();
            _file = null;
            if (KeepFiles == 0)
            {
                File.Delete(FilePath);
            }
            else
            {
                File.Delete(Numbered(KeepFiles));
                for (int i = KeepFiles - 1; i >= 1; i--)
                    if (File.Exists(Numbered(i))) File.Move(Numbered(i), Numbered(i + 1));
                File.Move(FilePath, Numbered(1));
            }

            _file = Open(FilePath);
        }

        private string Numbered(int i) => FilePath + "." + i.ToString(CultureInfo.InvariantCulture);

        private static FileStream Open(string path)
        {
            var file = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
            return file;
        }

        private void BreakFile(Exception e)
        {
            if (_fileBroken) return;
            _fileBroken = true;
            _file?.Dispose();
            _file = null;
            _console.WriteLine(_now().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + " 로그 파일에 쓰지 못해 파일 쓰기를 멈춘다(표준 출력은 계속): " + e.Message);
            _console.Flush();
        }
    }
}
