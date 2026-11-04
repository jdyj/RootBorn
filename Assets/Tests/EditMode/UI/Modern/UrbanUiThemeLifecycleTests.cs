using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Rootborn.Game.Managers;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rootborn.Tests.EditMode
{
    public sealed class UrbanUiThemeLifecycleTests
    {
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;
        private Type _type;
        private GameObject _root;
        private Component _runtime;
        private ScriptableObject _theme;
        private object _oldOwner, _oldCurrent, _oldChanged;
        private ResourceManager _resource;
        private const string Address = "data/ui/urban-molding";
        [SetUp]
        public void SetUp()
        {
            _type = typeof(Rootborn.UI.Modern.ModernUiTileImage).Assembly.GetType("Rootborn.UI.Modern.UrbanUiThemeRuntime");
            Assert.That(_type, Is.Not.Null);
            _oldOwner = _type.GetField("s_owner",Static).GetValue(null);
            _oldCurrent = _type.GetField("<Current>k__BackingField",Static).GetValue(null);
            _oldChanged = _type.GetField("Changed",Static).GetValue(null);
            _root = new GameObject("InactiveLifecycleTest");
            _root.SetActive(false); // Do not invoke Awake/bootstrap or access Addressables.
            _runtime = _root.AddComponent(_type);
            var themeType = _type.Assembly.GetType("Rootborn.UI.Modern.UrbanUiThemeDefinition");
            _theme = ScriptableObject.CreateInstance(themeType);
            _type.GetField("s_owner",Static).SetValue(null,_runtime);
            _type.GetField("<Current>k__BackingField",Static).SetValue(null,_theme);
            _resource = new ResourceManager();
            Cache()[Address] = _theme;
            _type.GetField("_resource",Instance).SetValue(_runtime,_resource);
            _type.GetField("_ownsAddress",Instance).SetValue(_runtime,true);
        }
        [TearDown]
        public void TearDown()
        {
            if(_type!=null)
            {
                _type.GetField("Changed",Static).SetValue(null,null);
                _type.GetField("s_owner",Static).SetValue(null,null);
            }
            UnityEngine.Object.DestroyImmediate(_root);
            UnityEngine.Object.DestroyImmediate(_theme);
            if(_type!=null)
            {
                _type.GetField("s_owner",Static).SetValue(null,_oldOwner);
                _type.GetField("<Current>k__BackingField",Static).SetValue(null,_oldCurrent);
                _type.GetField("Changed",Static).SetValue(null,_oldChanged);
            }
        }
        private Dictionary<string,UnityEngine.Object> Cache() =>
            (Dictionary<string,UnityEngine.Object>)typeof(ResourceManager).GetField("_cache",Instance).GetValue(_resource);
        private void Invoke(string name)
        {
            var method=_type.GetMethod(name,Instance);
            Assert.That(method,Is.Not.Null,name);
            method.Invoke(_runtime,null);
        }
        private void ThrowingSubscribers(Action after)
        {
            Action changed=()=>throw new InvalidOperationException("expected subscriber failure");
            changed+=after;
            _type.GetField("Changed",Static).SetValue(null,changed);
            LogAssert.Expect(LogType.Exception,new Regex("InvalidOperationException: expected subscriber failure"));
        }
        [Test]
        public void NotificationFailure_DoesNotSkipOtherSubscribersOrReleaseCurrentTheme()
        {
            int calls=0;
            ThrowingSubscribers(()=>calls++);
            Invoke("NotifyChanged");
            Assert.That(calls,Is.EqualTo(1));
            Assert.That(_type.GetProperty("Current").GetValue(null),Is.SameAs(_theme));
            Assert.That(Cache().ContainsKey(Address),Is.True);
            Assert.That(_type.GetField("_ownsAddress",Instance).GetValue(_runtime),Is.True);
        }
        [Test]
        public void Destruction_WithThrowingSubscriber_StillReleasesOwnedAddressAndNotifiesOthers()
        {
            int calls=0;
            ThrowingSubscribers(()=>calls++);
            Invoke("OnDestroy");
            Assert.That(calls,Is.EqualTo(1));
            Assert.That(Cache().ContainsKey(Address),Is.False);
            Assert.That(_type.GetField("_ownsAddress",Instance).GetValue(_runtime),Is.False);
            Assert.That(_type.GetProperty("Current").GetValue(null),Is.Null);
            Invoke("OnDestroy");
            Assert.That(calls,Is.EqualTo(1),"Repeated destruction must not publish or release again.");
        }
    }
}
