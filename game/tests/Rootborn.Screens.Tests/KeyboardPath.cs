using System.Collections.Generic;
using NUnit.Framework;
using Rootborn.Ui;

namespace Rootborn.Screens.Tests
{
    /// <summary>
    /// 키만으로 포커스를 그 요소까지 옮긴다(테스트 도우미). 길은 먼저 찾아 두고(요소마다 방향 키를 눌러 보는 계획 단계 — 상태를 바꾸지 않는
    /// 단추·칸에만 쓴다), 원래 포커스로 돌아가 찾은 방향 키를 차례로 ScreenStack.Key에 넣는다. 닿지 못하면 실패한다.
    /// </summary>
    internal static class KeyboardPath
    {
        private static readonly ScreenKey[] Directions = { ScreenKey.Up, ScreenKey.Down, ScreenKey.Left, ScreenKey.Right };

        public static void MoveFocusTo(ScreenStack stack, UiNode target, bool shift)
        {
            UiNode start = stack.Tree.Focused;
            if (start == target) return;
            if (start == null)
            {
                stack.Key(ScreenKey.Down, shift);
                start = stack.Tree.Focused;
                Assert.That(start, Is.Not.Null, "방향 키가 포커스를 만들지 못했다");
                if (start == target) return;
            }

            var previous = new Dictionary<UiNode, (UiNode from, ScreenKey key)>();
            var queue = new Queue<UiNode>();
            queue.Enqueue(start);
            previous[start] = (null, ScreenKey.Up);
            while (queue.Count > 0 && !previous.ContainsKey(target))
            {
                UiNode from = queue.Dequeue();
                foreach (ScreenKey key in Directions)
                {
                    stack.Tree.SetFocus(from);
                    stack.Key(key, shift);
                    UiNode to = stack.Tree.Focused;
                    if (to == null || previous.ContainsKey(to)) continue;
                    previous[to] = (from, key);
                    queue.Enqueue(to);
                }
            }
            stack.Tree.SetFocus(start);
            Assert.That(previous.ContainsKey(target), Is.True, "방향 키로 닿지 못한다: " + target.Id);

            var keys = new List<ScreenKey>();
            for (UiNode n = target; n != start; n = previous[n].from) keys.Insert(0, previous[n].key);
            foreach (ScreenKey key in keys) stack.Key(key, shift);
            Assert.That(stack.Tree.Focused, Is.SameAs(target), "찾은 길을 다시 눌러 닿는다: " + target.Id);
        }

        /// <summary>포커스를 옮기고 Enter.</summary>
        public static void Press(ScreenStack stack, UiNode target, bool shift)
        {
            MoveFocusTo(stack, target, shift);
            stack.Key(ScreenKey.Enter, shift);
        }
    }
}
