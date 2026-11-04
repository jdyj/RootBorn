using System;using System.Linq;using System.Reflection;using System.Collections.Generic;using NUnit.Framework;using UnityEngine;using UnityEngine.AddressableAssets;using UnityEngine.AddressableAssets.ResourceLocators;using UnityEngine.ResourceManagement.ResourceLocations;using Rootborn.Game.Managers;
namespace Rootborn.Tests.EditMode{public sealed class DataManagerPackedRegistryTests{
[TestCase("UnityEngine.ResourceManagement.ResourceProviders.BundledAssetProvider",false)]
[TestCase("CustomRemoteRegistryProvider",false)]
[TestCase("UnityEngine.ResourceManagement.ResourceProviders.AssetDatabaseProvider",true)]
public void EditorFastPathHonorsRegistryLocationProvider(string provider,bool expected){var old=UnityEngine.AddressableAssets.Addressables.ResourceLocators.ToArray();try{UnityEngine.AddressableAssets.Addressables.ClearResourceLocators();var map=new ResourceLocationMap("PackedRegistryRegression");map.Add(DataManager.AddrRegistry,new ResourceLocationBase("registry","registry",provider,typeof(Rootborn.Game.Common.GameDataRegistry)));UnityEngine.AddressableAssets.Addressables.AddResourceLocator(map);var method=typeof(DataManager).GetMethod("LoadEditorRegistry",BindingFlags.Static|BindingFlags.NonPublic);var result=method.Invoke(null,null);Assert.AreEqual(expected,result!=null,"An authored bundle location must not be replaced by the editor registry");}finally{UnityEngine.AddressableAssets.Addressables.ClearResourceLocators();foreach(var locator in old)UnityEngine.AddressableAssets.Addressables.AddResourceLocator(locator);}}
}}

