using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Rootborn.Game
{
    /// <summary>슬롯을 이어 갈 수 없게 만드는 문제의 종류.</summary>
    public enum SaveSlotProblemKind
    {
        /// <summary>있어야 할 파일이 없다(예: 다른 영역 파일은 있는데 스냅샷이 없다).</summary>
        Missing,

        /// <summary>파일이 있는데 읽을 수 없다(비었거나 깨짐, 더 새 형식 버전). <see cref="SaveSlotProblem.Error"/>에 저장소의 오류가 있다.</summary>
        Unreadable,
    }

    /// <summary>슬롯의 파일 하나에 대한 문제.</summary>
    public sealed class SaveSlotProblem
    {
        public SaveSlotProblem(string fileName, SaveSlotProblemKind kind, Exception error = null)
        {
            FileName = fileName ?? string.Empty;
            Kind = kind;
            Error = error;
        }

        public string FileName { get; }
        public SaveSlotProblemKind Kind { get; }

        /// <summary>읽을 수 없을 때 저장소가 낸 오류. 빠진 파일이면 null.</summary>
        public Exception Error { get; }
    }

    /// <summary>
    /// 슬롯이 손상돼 이어 갈 수 없다. GameSession.Start가 시작할 때 읽은 모든 파일의 문제를 모아 한 번에 던진다.
    /// 시작을 거절한 것이고 슬롯의 파일은 하나도 바꾸지 않았다. 새 게임으로 덮어쓸지, 사본에서 되살릴지는 부르는 쪽(화면)이 정한다.
    /// 메시지는 로그용이다. 화면에 보일 문구는 <see cref="Problems"/>로 현지화 키를 골라 만든다.
    /// </summary>
    public sealed class SaveSlotDamagedException : IOException
    {
        public SaveSlotDamagedException(string saveSlot, IReadOnlyList<SaveSlotProblem> problems, IReadOnlyList<string> foundFiles)
            : base(Describe(saveSlot, problems), FirstError(problems))
        {
            SaveSlot = saveSlot ?? string.Empty;
            Problems = problems ?? Array.Empty<SaveSlotProblem>();
            FoundFiles = foundFiles ?? Array.Empty<string>();
        }

        public string SaveSlot { get; }

        /// <summary>문제가 있는 파일들(읽은 순서).</summary>
        public IReadOnlyList<SaveSlotProblem> Problems { get; }

        /// <summary>시작할 때 읽을 수 있었던 파일들. 빠진 파일 말고 무엇이 남아 있는지 알린다.</summary>
        public IReadOnlyList<string> FoundFiles { get; }

        private static Exception FirstError(IReadOnlyList<SaveSlotProblem> problems)
        {
            if (problems == null) return null;
            for (int i = 0; i < problems.Count; i++) if (problems[i]?.Error != null) return problems[i].Error;
            return null;
        }

        private static string Describe(string saveSlot, IReadOnlyList<SaveSlotProblem> problems)
        {
            var text = new StringBuilder("세이브 슬롯이 손상돼 이어 갈 수 없다: ").Append(saveSlot);
            if (problems != null)
                for (int i = 0; i < problems.Count; i++)
                {
                    var problem = problems[i];
                    if (problem == null) continue;
                    text.Append("\n- ").Append(problem.FileName).Append(problem.Kind == SaveSlotProblemKind.Missing ? " (없음)" : " (읽을 수 없음)");
                    if (problem.Error != null) text.Append(": ").Append(problem.Error.Message);
                }

            return text.ToString();
        }
    }
}
