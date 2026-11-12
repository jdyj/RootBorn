using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace Rootborn.Net.Room
{
    /// <summary>
    /// 방 슬롯 하나를 프로세스 하나만 열게 하는 잠금(데디케이티드 서버와 내 PC에서 연 방이 같이 쓴다).
    /// &lt;세이브 폴더&gt;/&lt;슬롯&gt;/.room-lock 을 남과 나누지 않고(FileShare.None) 열어 둔다 — 운영체제가 잠금을 프로세스에 묶어 두므로
    /// 프로세스가 죽으면 잠금도 풀린다. 그래서 죽은 서버가 남긴 파일은 다음 시작이 그대로 다시 잡는다(남은 파일을 지울 필요가 없다).
    /// 누가 잡았는지는 옆 파일(.room-lock-owner)에 적는다. 정상으로 닫으면 그 파일을 지우므로, 잡을 때 그 파일이 남아 있으면 지난번 주인이
    /// 끝내지 못하고 꺼진 것이다(<see cref="PreviousOwnerCrashed"/>).
    /// 이름이 '.'으로 시작해 세이브 파일·사본·손님 세이브 검사가 모두 건너뛴다.
    /// </summary>
    public sealed class RoomSlotLock : IDisposable
    {
        public const string LockFileName = ".room-lock";
        public const string OwnerFileName = ".room-lock-owner";

        private readonly FileStream _handle;
        private readonly string _ownerPath;
        private bool _released;

        private RoomSlotLock(FileStream handle, string ownerPath, string previousOwner)
        {
            _handle = handle;
            _ownerPath = ownerPath;
            PreviousOwner = previousOwner;
        }

        /// <summary>지난번 주인이 정상으로 닫지 않고 꺼졌으면 그 주인 설명(<see cref="OwnerFileName"/>의 내용), 아니면 null.</summary>
        public string PreviousOwner { get; }

        public bool PreviousOwnerCrashed => PreviousOwner != null;

        /// <summary>슬롯 폴더(&lt;세이브 폴더&gt;/&lt;슬롯&gt;)를 잠근다. 폴더가 없으면 만든다.</summary>
        /// <param name="owner">누가 잡았는지(로그·오류 문구용). 예: "데디케이티드 서버", "내 PC의 방".</param>
        /// <exception cref="RoomSlotLockedException">다른 프로세스(또는 이 프로세스의 다른 방)가 잡고 있다.</exception>
        /// <exception cref="UnauthorizedAccessException">세이브 폴더에 쓸 수 없다.</exception>
        /// <exception cref="IOException">세이브 폴더에 쓸 수 없다(디스크 등).</exception>
        public static RoomSlotLock Acquire(string saveRoot, string saveSlot, string owner)
        {
            if (string.IsNullOrWhiteSpace(saveRoot)) throw new ArgumentException("세이브 폴더가 비어 있다", nameof(saveRoot));
            if (string.IsNullOrWhiteSpace(saveSlot)) throw new ArgumentException("세이브 슬롯이 비어 있다", nameof(saveSlot));
            string folder = Path.Combine(saveRoot, saveSlot);
            Directory.CreateDirectory(folder);
            string lockPath = Path.Combine(folder, LockFileName);
            string ownerPath = Path.Combine(folder, OwnerFileName);

            FileStream handle;
            try
            {
                handle = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException e) when (!(e is FileNotFoundException) && !(e is DirectoryNotFoundException) && !(e is PathTooLongException) && IsHeld(lockPath))
            {
                throw new RoomSlotLockedException(folder, ReadOwner(ownerPath), e);
            }

            try
            {
                string previous = File.Exists(ownerPath) ? ReadOwner(ownerPath) ?? "(알 수 없음)" : null;
                File.WriteAllText(ownerPath, Describe(owner));
                return new RoomSlotLock(handle, ownerPath, previous);
            }
            catch
            {
                handle.Dispose();
                throw;
            }
        }

        /// <summary>잠금을 푼다: 주인 파일을 지우고(잡은 채로 — 다음 주인이 쓴 것을 지우지 않게) 잠금 파일을 닫는다. 두 번 불러도 된다.</summary>
        public void Dispose()
        {
            if (_released) return;
            _released = true;
            try
            {
                File.Delete(_ownerPath);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                // 지우지 못하면 다음 시작이 "끝내지 못하고 꺼짐"으로 볼 뿐이다.
            }

            _handle.Dispose();
        }

        // 다른 쪽이 잡고 있는지: 한 번 더 열어 본다(잠깐의 다른 IO 오류와 나누려고).
        private static bool IsHeld(string lockPath)
        {
            try
            {
                using (new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None)) return false;
            }
            catch (IOException)
            {
                return true;
            }
        }

        private static string ReadOwner(string ownerPath)
        {
            try
            {
                return File.Exists(ownerPath) ? File.ReadAllText(ownerPath).Trim() : null;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                return null;
            }
        }

        private static string Describe(string owner)
        {
            using var process = Process.GetCurrentProcess();
            return (string.IsNullOrWhiteSpace(owner) ? "방" : owner) + ", 프로세스 " + process.Id.ToString(CultureInfo.InvariantCulture)
                + ", 기계 " + Environment.MachineName + ", 시작 " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// 그 슬롯을 다른 프로세스가 이미 열었다(같은 세이브 폴더·슬롯으로 서버를 둘 띄움, 또는 내 PC에서 연 방과 서버가 같은 슬롯).
    /// InvalidOperationException이라 "열 수 없다"를 받는 쪽(Host의 방 열기)이 그대로 실패로 처리한다.
    /// </summary>
    public sealed class RoomSlotLockedException : InvalidOperationException
    {
        public RoomSlotLockedException(string slotFolder, string owner, Exception inner)
            : base("이 슬롯은 이미 다른 곳에서 열려 있다: " + slotFolder + (string.IsNullOrEmpty(owner) ? "" : " (" + owner + ")"), inner)
        {
            SlotFolder = slotFolder;
            Owner = owner;
        }

        public string SlotFolder { get; }

        /// <summary>잡은 쪽 설명(주인 파일 내용). 모르면 null.</summary>
        public string Owner { get; }
    }
}
