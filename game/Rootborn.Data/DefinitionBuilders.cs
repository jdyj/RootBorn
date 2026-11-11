using System;
using System.Collections.Generic;

namespace Rootborn.Data
{
    /// <summary>
    /// JSON type 문자열마다 만들기 함수를 등록한다. data/에 있는 모든 type은 셋 중 하나여야 한다:
    /// Add(만들기 함수), NotARule(규칙이 아님, 이유), NotPortedYet(규칙인데 Core에 아직 없음, 기다리는 영역),
    /// NotUsedByGame(규칙이지만 지금 게임이 쓰지 않아 옮기지 않음, 이유와 근거 파일).
    /// 분기는 이 등록으로만 한다. 엔티티 id로 분기하지 않는다.
    /// </summary>
    public sealed class DefinitionBuilders
    {
        private readonly Dictionary<string, Func<NodeReader, BuildContext, object>> _builders =
            new Dictionary<string, Func<NodeReader, BuildContext, object>>(StringComparer.Ordinal);

        private readonly Dictionary<string, string> _notRules = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _notPortedYet = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _notUsedByGame = new Dictionary<string, string>(StringComparer.Ordinal);

        public IReadOnlyCollection<string> BuiltTypes => _builders.Keys;

        /// <summary>type → 규칙이 아닌 이유.</summary>
        public IReadOnlyDictionary<string, string> NotRules => _notRules;

        /// <summary>type → 기다리는 영역.</summary>
        public IReadOnlyDictionary<string, string> NotPorted => _notPortedYet;

        /// <summary>type → 게임이 쓰지 않는다는 근거.</summary>
        public IReadOnlyDictionary<string, string> NotUsed => _notUsedByGame;

        public void Add<T>(string jsonType, Func<NodeReader, BuildContext, T> build) where T : class
        {
            if (build == null) throw new ArgumentNullException(nameof(build));
            EnsureNew(jsonType);
            _builders.Add(jsonType, (node, context) => build(node, context));
        }

        public void NotARule(string jsonType, string reason)
        {
            EnsureNew(jsonType);
            _notRules.Add(jsonType, reason ?? "");
        }

        public void NotPortedYet(string jsonType, string waitingFor)
        {
            EnsureNew(jsonType);
            _notPortedYet.Add(jsonType, waitingFor ?? "");
        }

        /// <summary>규칙이지만 지금 게임(Town·학생 생활)이 부르지 않아 옮기지 않는 종류. 게임이 쓰게 되면 옮기고 여기서 뺀다.</summary>
        public void NotUsedByGame(string jsonType, string evidence)
        {
            EnsureNew(jsonType);
            _notUsedByGame.Add(jsonType, evidence ?? "");
        }

        public bool IsClassified(string jsonType)
        {
            return _builders.ContainsKey(jsonType) || _notRules.ContainsKey(jsonType) || _notPortedYet.ContainsKey(jsonType)
                || _notUsedByGame.ContainsKey(jsonType);
        }

        internal bool TryGetBuilder(string jsonType, out Func<NodeReader, BuildContext, object> build)
        {
            return _builders.TryGetValue(jsonType, out build);
        }

        internal string DescribeUnbuilt(string jsonType)
        {
            if (_notRules.TryGetValue(jsonType, out var reason)) return $"'{jsonType}'는 규칙이 아니다({reason})";
            if (_notPortedYet.TryGetValue(jsonType, out var waiting)) return $"'{jsonType}'는 아직 Core에 없다({waiting})";
            if (_notUsedByGame.TryGetValue(jsonType, out var evidence)) return $"'{jsonType}'는 게임이 쓰지 않아 옮기지 않았다({evidence})";
            return $"'{jsonType}'의 만들기 함수가 등록되지 않았다";
        }

        private void EnsureNew(string jsonType)
        {
            if (string.IsNullOrEmpty(jsonType)) throw new ArgumentException("type이 비어 있다", nameof(jsonType));
            if (IsClassified(jsonType)) throw new ArgumentException($"'{jsonType}'가 이미 등록돼 있다", nameof(jsonType));
        }
    }
}
