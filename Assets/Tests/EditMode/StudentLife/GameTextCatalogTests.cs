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
    }
}
