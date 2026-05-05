using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Rootborn.Editor.Tools;
using Rootborn.Game.Common;
using UnityEngine;

namespace Rootborn.Tests.EditMode
{
    /// <summary>
    /// UISpriteAddresses 상수 ↔ AddressablesSetup.UiSpriteEntries 일관성 검증.
    /// 한 쪽만 수정되면 런타임에 sprite 캐시 미스 → 단색 fallback UI 가 빌드됨 (회귀 사고).
    /// </summary>
    public sealed class UISpriteAddressesTests
    {
        // 1) UISpriteAddresses 의 AllSingleSprites 에 선언된 모든 주소가 AddressablesSetup 에 등록 entry 로 존재해야 함.
        [Test]
        public void AllSingleSprites_AreAllRegisteredInAddressablesSetup()
        {
            var entries = AddressablesSetup.GetUiSpriteAddresses();
            var registered = new HashSet<string>(entries);

            var missing = new List<string>();
            foreach (var addr in UISpriteAddresses.AllSingleSprites)
            {
                if (!registered.Contains(addr)) missing.Add(addr);
            }

            Assert.IsEmpty(missing,
                "UISpriteAddresses.AllSingleSprites 중 AddressablesSetup.UiSpriteEntries 에 등록 안 된 주소: " +
                string.Join(", ", missing));
        }

        // 2) AllSheets 에 선언된 sheet 주소도 AddressablesSetup 에 등록되어야 함.
        [Test]
        public void AllSheets_AreAllRegisteredInAddressablesSetup()
        {
            var entries = AddressablesSetup.GetUiSpriteAddresses();
            var registered = new HashSet<string>(entries);

            var missing = new List<string>();
            foreach (var (sheetAddr, _) in UISpriteAddresses.AllSheets)
            {
                if (!registered.Contains(sheetAddr)) missing.Add(sheetAddr);
            }

            Assert.IsEmpty(missing,
                "UISpriteAddresses.AllSheets 중 AddressablesSetup.UiSpriteEntries 에 등록 안 된 sheet: " +
                string.Join(", ", missing));
        }

        // 3) AddressablesSetup 의 모든 entry 가 가리키는 asset 파일이 디스크에 실제 존재해야 함.
        //    (Pixelwood Valley 폴더가 .gitignore 에 있더라도 로컬 디스크엔 있어야 함.)
        [Test]
        public void AllUiSpriteEntries_HaveAssetFileOnDisk()
        {
            var entries = AddressablesSetup.GetUiSpriteEntries();
            var missing = new List<string>();
            foreach (var (path, _) in entries)
            {
                if (!File.Exists(path)) missing.Add(path);
            }
            Assert.IsEmpty(missing,
                "AddressablesSetup.UiSpriteEntries 가 가리키는데 디스크에 없는 asset: " +
                string.Join(", ", missing));
        }

        // 4) 모든 BookFlipFrames 가 AllSingleSprites 에 포함되어 있어야 함 (애니메이션 누락 방지).
        [Test]
        public void BookFlipFrames_AreAllInAllSingleSprites()
        {
            var all = new HashSet<string>(UISpriteAddresses.AllSingleSprites);
            foreach (var f in UISpriteAddresses.BookFlipFrames)
            {
                Assert.IsTrue(all.Contains(f), $"BookFlipFrames 의 '{f}' 가 AllSingleSprites 에 없음.");
            }
            Assert.AreEqual(9, UISpriteAddresses.BookFlipFrames.Length, "Page 1~9 9프레임 애니메이션 보장.");
        }

        // 5) Bookmark sub-sprite 이름은 PixelwoodSliceSetup.SliceBookmarkSheet 가 생성하는 'Bookmark_{0..4}' 와 정확히 일치.
        [Test]
        public void BookmarkSubSpriteNames_FollowExpectedConvention()
        {
            var subs = new[] {
                UISpriteAddresses.SubBookmark0, UISpriteAddresses.SubBookmark1,
                UISpriteAddresses.SubBookmark2, UISpriteAddresses.SubBookmark3,
                UISpriteAddresses.SubBookmark4,
            };
            for (int i = 0; i < subs.Length; i++)
            {
                Assert.AreEqual($"Bookmark_{i}", subs[i],
                    $"SubBookmark{i} = '{subs[i]}' 가 PixelwoodSliceSetup.SliceBookmarkSheet 의 명명 규약 'Bookmark_{i}' 와 어긋남.");
            }
        }

        // 6) AllSheets 가 선언한 sub-sprite 이름이 SubBookmark0..4 와 일치 (preload 누락 방지).
        [Test]
        public void AllSheets_BookmarkSheet_DeclaresFiveSubSprites()
        {
            (string sheet, string[] subs) found = default;
            bool any = false;
            foreach (var entry in UISpriteAddresses.AllSheets)
            {
                if (entry.sheetAddress == UISpriteAddresses.BookmarkSheet)
                {
                    found = entry;
                    any = true;
                    break;
                }
            }
            Assert.IsTrue(any, "BookmarkSheet 가 AllSheets 에 없음 — preload 안 됨.");
            Assert.AreEqual(5, found.subs.Length, "Bookmark 5색 sub-sprite 누락.");
        }
    }
}
