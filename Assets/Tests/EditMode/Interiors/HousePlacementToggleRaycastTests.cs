using System.Reflection;
using NUnit.Framework;
using Rootborn.UI.Interiors;
using UnityEngine;
using UnityEngine.UI;
namespace Rootborn.Tests.EditMode.Interiors {
public sealed class HousePlacementToggleRaycastTests {
[Test] public void FallbackCanvas_ReceivesGraphicRaycasterForPlacementButton(){
var canvasGo=new GameObject("HouseInteriorPlacementCanvas",typeof(Canvas));
var panelGo=new GameObject("TestPlacementPanel",typeof(RectTransform),typeof(InteriorFurniturePlacementPanel));
var host=new GameObject("ToggleFixture");
try {panelGo.SetActive(false);var installer=host.AddComponent<HousePlacementModeToggleInstaller>();
var installed=(bool)typeof(HousePlacementModeToggleInstaller).GetMethod("TryInstall",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(installer,null);
Assert.IsTrue(installed);Assert.IsNotNull(canvasGo.GetComponent<GraphicRaycaster>(),"Placement buttons require a raycaster even when reusing a noninteractive HUD canvas.");
} finally {Object.DestroyImmediate(host);Object.DestroyImmediate(panelGo);Object.DestroyImmediate(canvasGo);}
}
}}
