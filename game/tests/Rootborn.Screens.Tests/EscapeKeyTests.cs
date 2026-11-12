using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Rootborn.Screens.Tests
{
    /// <summary>
    /// Esc 규칙(2026-11-13 임시로 정함): 맨 위의 닫을 수 있는 화면(패널·모달)을 닫고, 닫을 화면이 없으면 설정을 연다.
    /// 고르기를 끝내야 하는 화면(등록 때 KeepsOpenOnEscape로 이유를 적은 것)은 Esc로 닫지 않는다(아무 일 없음).
    /// 등록된 모든 화면을 하나씩 열어 Esc 한 번을 누른다(창 없이 — 자동 테스트).
    /// </summary>
    public sealed class EscapeKeyTests
    {
        private enum Outcome { Closes, OpensSettings, Nothing }

        private static Outcome Expected(ScreenEntry entry)
        {
            if (entry.Layer == ScreenLayer.Hud || entry.Layer == ScreenLayer.Notice) return Outcome.OpensSettings;
            return entry.EscapeKeepsOpenReason != null ? Outcome.Nothing : Outcome.Closes;
        }

        private static Outcome Observe(ScreenEntry entry)
        {
            using KeyboardReachTests.Harness h = KeyboardReachTests.OpenScreen(entry);
            ScreenStack stack = h.Stack;
            Assert.That(stack.IsOpen(entry.Id), Is.True, entry.Id + " 준비: 열렸다");
            bool settingsBefore = stack.IsOpen(ScreenCatalog.Settings);
            bool shift = !stack.Find(entry.Id).BlocksMovement;
            stack.Key(ScreenKey.Escape, shift);
            if (!stack.IsOpen(entry.Id)) return Outcome.Closes;
            if (!settingsBefore && stack.IsOpen(ScreenCatalog.Settings)) return Outcome.OpensSettings;
            return Outcome.Nothing;
        }

        [Test]
        public void Escape_ClosesTheTopScreen_OrOpensSettings_OrDoesNothingOnlyForListedScreens()
        {
            var wrong = new List<string>();
            var table = new List<string>();
            foreach (ScreenEntry entry in ScreenCatalog.Create().Screens.OrderBy(e => e.Id, System.StringComparer.Ordinal))
            {
                Outcome expected = Expected(entry), seen = Observe(entry);
                table.Add(entry.Id + " → " + seen + (entry.EscapeKeepsOpenReason != null ? " (" + entry.EscapeKeepsOpenReason + ")" : ""));
                if (seen != expected) wrong.Add(entry.Id + ": " + seen + " (기대 " + expected + ")");
            }
            TestContext.Out.WriteLine(string.Join("\n", table));
            Assert.That(wrong, Is.Empty);
        }

        [Test]
        public void ScreensThatKeepOpenOnEscape_SayWhy()
        {
            foreach (ScreenEntry entry in ScreenCatalog.Create().Screens.Where(e => e.EscapeKeepsOpenReason != null))
            {
                Assert.That(entry.Layer == ScreenLayer.Panel || entry.Layer == ScreenLayer.Modal, Is.True, entry.Id + ": HUD·알림은 Esc로 닫는 화면이 아니다");
                Assert.That(entry.EscapeKeepsOpenReason.Trim().Length, Is.GreaterThan(5), entry.Id + " 이유");
            }
        }

        [Test]
        public void Escape_WithAPanelUnderAModal_ClosesOnlyTheModal()
        {
            using var t = new TestSession();
            t.Stack.OpenHud();
            t.Stack.Open(ScreenCatalog.Inventory);
            t.Stack.Open(ScreenCatalog.CareerCandidates);
            t.Stack.Key(ScreenKey.Escape, false);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.CareerCandidates), Is.False, "맨 위(모달)를 닫는다");
            Assert.That(t.Stack.IsOpen(ScreenCatalog.Inventory), Is.True, "아래 패널은 그대로");
            t.Stack.Key(ScreenKey.Escape, true);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.Inventory), Is.False, "다음 Esc가 패널을 닫는다");
            Assert.That(t.Stack.IsOpen(ScreenCatalog.Settings), Is.False, "닫을 화면이 있었으니 설정은 열지 않는다");
            t.Stack.Key(ScreenKey.Escape, false);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.Settings), Is.True, "닫을 화면이 없으면 설정");
        }
    }
}
