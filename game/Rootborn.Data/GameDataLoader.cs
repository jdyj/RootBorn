using System;
using System.Collections.Generic;
using System.Linq;

namespace Rootborn.Data
{
    /// <summary>data/ 를 읽어 등록된 종류의 엔티티를 빠짐없이 만든다. 오류는 모아서 LoadResult에 담는다.</summary>
    public static class GameDataLoader
    {
        public static LoadResult Load(string dataDirectory, Action<DefinitionBuilders> register)
        {
            if (register == null) throw new ArgumentNullException(nameof(register));

            var builders = new DefinitionBuilders();
            register(builders);

            var errors = new List<LoadError>();
            var table = EntityTable.ReadDirectory(dataDirectory, errors);
            var context = new BuildContext(table, builders, errors);
            var unclassified = new SortedSet<string>(StringComparer.Ordinal);
            var entries = new List<GameDefinitions.Entry>();

            // 참조로 닿지 않는 것도 만든다.
            foreach (var node in table.All)
            {
                if (!builders.TryGetBuilder(node.Type, out _))
                {
                    if (!builders.IsClassified(node.Type)) unclassified.Add(node.Type);
                    continue;
                }

                context.Build(node);
            }

            foreach (var node in table.All)
            {
                if (context.TryGetBuilt(node, out var built)) entries.Add(new GameDefinitions.Entry(node.Id, node.Type, built));
            }

            var definitions = new GameDefinitions(entries, table);
            definitions.Currency = EconomySettings.ReadCurrency(dataDirectory, definitions, errors);
            definitions.TimeRules = TimeSettings.Read(dataDirectory, definitions, errors);
            return new LoadResult(definitions, errors, unclassified.ToArray(), builders);
        }
    }

    public sealed class LoadResult
    {
        internal LoadResult(GameDefinitions definitions, IReadOnlyList<LoadError> errors, IReadOnlyList<string> unclassifiedTypes, DefinitionBuilders builders)
        {
            Definitions = definitions;
            Errors = errors;
            UnclassifiedTypes = unclassifiedTypes;
            Builders = builders;
        }

        public GameDefinitions Definitions { get; }
        public IReadOnlyList<LoadError> Errors { get; }

        /// <summary>표에 있지만 Add/NotARule/NotPortedYet/NotUsedByGame 어디에도 없는 종류.</summary>
        public IReadOnlyList<string> UnclassifiedTypes { get; }

        /// <summary>등록 내용(분류 집계·보고용).</summary>
        public DefinitionBuilders Builders { get; }

        public bool Ok => Errors.Count == 0 && UnclassifiedTypes.Count == 0;

        /// <summary>오류나 분류 안 된 종류가 있으면 던진다. 일부만 읽힌 채로 게임을 시작하지 않기 위해서다.</summary>
        public void ThrowIfFailed()
        {
            if (Ok) return;

            var lines = new List<string>();
            if (UnclassifiedTypes.Count > 0) lines.Add("분류 안 된 종류: " + string.Join(", ", UnclassifiedTypes));
            lines.AddRange(Errors.Take(20).Select(error => error.ToString()));
            throw new GameDataException($"게임 데이터 오류 {Errors.Count}건, 분류 안 된 종류 {UnclassifiedTypes.Count}개\n" + string.Join("\n", lines));
        }
    }

    public sealed class GameDataException : Exception
    {
        public GameDataException(string message) : base(message)
        {
        }
    }
}
