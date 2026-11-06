using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Rootborn.Tests.EditMode.StudentLife
{
    public sealed class GameTextCatalogTests
    {
        [Test] public void Catalog_ResolvesBothLanguagesAndFormatsTimeBudget()
        {
            var catalog = Create();
            try
            {
                Invoke(catalog,"ConfigureJson","{\"entries\":[{\"key\":\"tp\",\"ko\":\"남은 TP {0}/{1}\",\"en\":\"TP {0}/{1} remaining\"}]}");
                Assert.That(Invoke(catalog,"Format","tp","ko",new object[]{1,2}), Is.EqualTo("남은 TP 1/2"));
                Assert.That(Invoke(catalog,"Format","tp","en",new object[]{1,2}), Is.EqualTo("TP 1/2 remaining"));
            }
            finally { UnityEngine.Object.DestroyImmediate(catalog); }
        }
        [Test] public void Catalog_UsesEnglishFallbackAndExposesMissingKey()
        {
            var catalog=Create();
            try
            {
                Invoke(catalog,"ConfigureJson","{\"entries\":[{\"key\":\"hello\",\"ko\":\"\",\"en\":\"Hello\"}]}");
                Assert.That(Invoke(catalog,"Get","hello","ko"),Is.EqualTo("Hello"));
                Assert.That(Invoke(catalog,"Get","missing","en"),Is.EqualTo("missing"));
            }
            finally { UnityEngine.Object.DestroyImmediate(catalog); }
        }
        [Test] public void Catalog_InvalidReloadDoesNotDestroyWorkingTranslations()
        {
            var catalog=Create();
            try
            {
                Invoke(catalog,"ConfigureJson","{\"entries\":[{\"key\":\"hello\",\"ko\":\"안녕\",\"en\":\"Hello\"}]}");
                Assert.Throws<TargetInvocationException>(()=>Invoke(catalog,"ConfigureJson","{\"entries\":[{\"key\":\"x\",\"en\":\"a\"},{\"key\":\"x\",\"en\":\"b\"}]}"));
                Assert.That(Invoke(catalog,"Get","hello","ko"),Is.EqualTo("안녕"));
            }
            finally { UnityEngine.Object.DestroyImmediate(catalog); }
        }
        private static ScriptableObject Create()
        {
            var type=typeof(Rootborn.Game.StudentLife.StudentLifeProgress).Assembly.GetType("Rootborn.Game.Localization.GameTextCatalog");
            Assert.That(type,Is.Not.Null,"GameTextCatalog missing");
            return ScriptableObject.CreateInstance(type);
        }
        private static object Invoke(object o,string name,params object[] args) => o.GetType().GetMethod(name).Invoke(o,args);

        // Quest, dialogue, item and daily-plan text keys reach players directly; a missing entry shows the raw key on screen.
        [TestCase("Assets/Data/Quests")]
        [TestCase("Assets/Data/Dialogue")]
        [TestCase("Assets/Data/Items")]
        [TestCase("Assets/Data/StudentLife/DailyPlans")]
        public void PlayerFacingDataTextKeys_AllHaveCatalogEntries(string folder)
        {
            string catalog=System.IO.File.ReadAllText("Assets/Localization/GameTextCatalog.asset");
            var keys=new System.Collections.Generic.HashSet<string>();
            foreach(System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(catalog,@"  - key: ""?([^""\r\n]+)""?")) keys.Add(m.Groups[1].Value);
            var missing=new System.Collections.Generic.List<string>();
            foreach(var path in System.IO.Directory.GetFiles(folder,"*.asset",System.IO.SearchOption.AllDirectories))
            {
                if(path.Replace('\\','/').Contains("/Test")||System.IO.Path.GetFileName(path).Contains("_Test")) continue;
                foreach(System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(System.IO.File.ReadAllText(path),@"\n  _(?:\w*[Kk]ey): ([^\s{][^\r\n]*)"))
                {
                    string key=m.Groups[1].Value.Trim().Trim('"');
                    if(key.Length>0 && !key.Contains(" ") && !key.Contains(".test.") && !keys.Contains(key)) missing.Add(System.IO.Path.GetFileName(path)+": "+key);
                }
            }
            Assert.That(missing,Is.Empty,"Text keys with no catalog entry: "+string.Join(", ",missing));
        }
    }
}
