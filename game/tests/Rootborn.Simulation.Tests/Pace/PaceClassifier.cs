using System;
using System.Collections.Generic;
using Rootborn.Screens;

namespace Rootborn.Simulation.Pace
{
    /// <summary>
    /// 걸음을 분류에 넣는다. 화면 id·엔티티 id를 보지 않고 화면 등록의 성질과 행동 종류만 본다:
    /// - 활동 중(같이 하기에서 활동이 끝나기 전) → 활동 중
    /// - 걷기·문 행동 → 걷기 (이동을 막지 않는 패널이 떠 있어도)
    /// - 맨 위 화면이 없을 때: 서 있기·기다리기 → 할 것 없음, 키 → 살펴보기(그 키가 여는 화면)
    /// - 맨 위 화면이 패널·모달인데 시계를 멈추지 않음(KeepsClockRunning) → 미니게임
    /// - 잠자리 상호작용 종류가 여는 화면 → 잠
    /// - 상호작용으로 열지 않는, 월드 위의 패널(키로 여는 가방·수첩·기록) → 살펴보기
    /// - 그 밖(대화·이벤트·장소·결과 같은 모달, 상호작용이 여는 패널, 시작 화면) → 읽기·고르기
    /// </summary>
    public sealed class PaceClassifier
    {
        private readonly ScreenRegistry _registry;
        private readonly string _sleepScreen;
        private readonly HashSet<string> _keyScreens = new HashSet<string>(StringComparer.Ordinal);

        /// <param name="sleepInteractionKind">잠자리 상호작용 종류(그 종류가 여는 화면이 잠 분류).</param>
        public PaceClassifier(ScreenRegistry registry, string sleepInteractionKind)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            if (_registry.TryGetByInteraction(sleepInteractionKind, out string sleep)) _sleepScreen = sleep;
            foreach (var pair in _registry.KeyBindings) _keyScreens.Add(pair.Value);
        }

        public string Classify(string topScreen, PlayAction action, bool busy)
        {
            if (busy) return PaceCategories.Busy;
            if (action != null && (action.Kind == PlayActionKind.WalkInteract || action.Kind == PlayActionKind.Door)) return PaceCategories.Walk;
            if (topScreen == null)
            {
                if (action != null && action.Kind == PlayActionKind.Key) return PaceCategories.Browse;
                return PaceCategories.NothingToDo; // 서 있기·기다리기(밤샘, 같이 하는 방에서 남 기다리기, 할 수 있는 것이 없는 관찰)
            }
            if (!_registry.TryGet(topScreen, out ScreenEntry entry)) return PaceCategories.ReadChoose;
            if ((entry.Layer == ScreenLayer.Panel || entry.Layer == ScreenLayer.Modal) && !entry.PausesClock) return PaceCategories.Minigame;
            if (topScreen == _sleepScreen) return PaceCategories.Sleep;
            if (entry.Layer == ScreenLayer.Panel && !entry.OpenedByInteraction && !entry.WithoutWorld) return PaceCategories.Browse;
            return PaceCategories.ReadChoose;
        }

        /// <summary>사람이 고르는 횟수: 떠 있는 화면에서 누르기·키·글자 치기 한 번이 한 번. 미니게임(제한 시간 몫이 맡는다)·걷기·기다리기는 0.</summary>
        public int Choices(string topScreen, PlayAction action, string category)
        {
            if (topScreen == null || action == null || category == PaceCategories.Minigame || category == PaceCategories.Walk) return 0;
            return action.Kind == PlayActionKind.Click || action.Kind == PlayActionKind.Key || action.Kind == PlayActionKind.Type ? 1 : 0;
        }

        /// <summary>그 화면의 글을 읽는 시간으로 세는가: HUD(흘끗 본다)와 시계가 흐르는 화면(미니게임 — 제한 시간 몫에 든다)은 빼고.</summary>
        public bool CountsText(string screen)
        {
            if (!_registry.TryGet(screen, out ScreenEntry entry)) return true;
            if (entry.Layer == ScreenLayer.Hud) return false;
            return entry.Layer == ScreenLayer.Notice || entry.PausesClock;
        }

        /// <summary>그 화면이 키로 여는 화면인가(보고용).</summary>
        public bool OpenedByKey(string screen) => _keyScreens.Contains(screen);
    }

    /// <summary>새로 나온 글자 수: 앞 관찰에 없던 글줄의 글자(공백 빼고). 글줄이 사라졌다 다시 나오면 다시 센다(다시 연 화면을 다시 읽는다고 본다 — 가정).</summary>
    public sealed class NewTextCounter
    {
        private HashSet<string> _before = new HashSet<string>(StringComparer.Ordinal);
        private HashSet<string> _now = new HashSet<string>(StringComparer.Ordinal);

        public int Count(IEnumerable<string> visible)
        {
            _now.Clear();
            int count = 0;
            foreach (string line in visible)
            {
                if (string.IsNullOrEmpty(line) || !_now.Add(line) || _before.Contains(line)) continue;
                foreach (char ch in line) if (!char.IsWhiteSpace(ch)) count++;
            }
            (_before, _now) = (_now, _before);
            return count;
        }
    }
}
