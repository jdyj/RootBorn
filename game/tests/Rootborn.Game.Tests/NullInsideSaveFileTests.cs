using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using NUnit.Framework;

namespace Rootborn.Game.Tests
{
    /// <summary>
    /// 문법은 맞지만 안이 빈 세이브 파일(목록 자리에 null, 객체 자리에 null). 손으로 고쳤거나 다른 도구가 쓴 파일에서 나온다.
    /// 시작은 그 파일을 그대로 읽어 이어 가거나(빈 목록으로 본다), 손상으로 알리고 거절한다(SaveSlotDamagedException).
    /// 다른 예외로 죽지 않는다 — 실행 프로그램은 손상 알림만 화면에 보일 수 있다. 어느 쪽이든 시작은 슬롯을 바꾸지 않는다. 자동 테스트다.
    /// </summary>
    public sealed class NullInsideSaveFileTests
    {
        private const string Checkpoint = "day-3";

        public static IEnumerable<TestCaseData> Files()
        {
            foreach (var pair in SlotCheckpoints.Files(Checkpoint).Where(f => f.Key.EndsWith(".json", StringComparison.Ordinal)))
            {
                // 바꿀 자리가 있는 파일만 낸다(건너뛰는 경우를 만들지 않는다).
                string text = System.Text.Encoding.UTF8.GetString(pair.Value);
                if (Nullify(JsonNode.Parse(text), true) > 0) yield return new TestCaseData(pair.Key, "arrays").SetName("NullArraysIn_" + pair.Key);
                if (Nullify(JsonNode.Parse(text), false) > 0) yield return new TestCaseData(pair.Key, "objects").SetName("NullObjectsIn_" + pair.Key);
            }
        }

        [TestCaseSource(nameof(Files))]
        public void Start_ContinuesOrReportsDamage_ButNeverDiesOnAnotherError(string file, string what)
        {
            using var saves = SlotCheckpoints.Restore(Checkpoint);
            string path = Path.Combine(saves.Path, SlotCheckpoints.Slot, file);
            JsonNode root = JsonNode.Parse(File.ReadAllText(path));
            Assert.That(Nullify(root, what == "arrays"), Is.GreaterThan(0));
            File.WriteAllText(path, root.ToJsonString());
            var before = SlotCheckpoints.Read(saves);

            try
            {
                TestWorld.Start(saves.Store, SlotCheckpoints.Slot);
            }
            catch (SaveSlotDamagedException damaged)
            {
                Assert.That(damaged.Problems, Is.Not.Empty);
            }

            Assert.That(SlotCheckpoints.Same(before, SlotCheckpoints.Read(saves)), Is.True, "시작은 슬롯을 바꾸지 않는다");
        }

        // 맨 위 객체의 자식부터: 배열(또는 객체) 값을 모두 null로 바꾼다. 바꾼 수를 돌려준다.
        private static int Nullify(JsonNode node, bool arrays)
        {
            int changed = 0;
            if (node is JsonObject obj)
            {
                foreach (string key in obj.Select(p => p.Key).ToList())
                {
                    JsonNode child = obj[key];
                    if (arrays ? child is JsonArray : child is JsonObject)
                    {
                        obj[key] = null;
                        changed++;
                    }
                    else if (child != null) changed += Nullify(child, arrays);
                }
            }
            else if (node is JsonArray list)
            {
                foreach (JsonNode child in list)
                    if (child != null) changed += Nullify(child, arrays);
            }

            return changed;
        }
    }
}
