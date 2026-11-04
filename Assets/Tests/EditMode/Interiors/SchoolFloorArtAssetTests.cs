using System;using System.IO;using System.Linq;using System.Text;using System.Security.Cryptography;using NUnit.Framework;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine;using UnityEngine.SceneManagement;using UnityEngine.Tilemaps;using Rootborn.Game.World;
namespace Rootborn.Tests.EditMode.Interiors{public sealed class SchoolFloorArtAssetTests{
[Serializable]public sealed class Baseline{public int groundCount;public string groundCells;public string nonGround;}
public static Baseline Capture(Scene scene){var roots=scene.GetRootGameObjects();var maps=roots.SelectMany(g=>g.GetComponentsInChildren<Tilemap>(true)).OrderBy(m=>m.name).ToArray();var cells=new StringBuilder();var other=new StringBuilder();int count=0;foreach(var map in maps){if(map.name!="SchoolGroundTilemap")other.AppendLine(map.name+" "+map.transform.localToWorldMatrix);foreach(var cell in map.cellBounds.allPositionsWithin){var tile=map.GetTile(cell);if(tile==null)continue;if(map.name=="SchoolGroundTilemap"){count++;cells.AppendLine(cell.ToString());}else other.AppendLine(cell+" "+CanonicalTilePath(map.name,AssetDatabase.GetAssetPath(tile))+" "+map.GetTransformMatrix(cell)+" "+map.GetColor(cell));}}foreach(var c in roots.SelectMany(g=>g.GetComponentsInChildren<Collider2D>(true)).OrderBy(c=>PathOf(c.transform))){other.AppendLine("collider "+PathOf(c.transform)+" "+c.GetType().Name+" "+c.enabled+" "+c.isTrigger+" "+c.offset+" "+c.transform.localToWorldMatrix);if(c is BoxCollider2D b)other.AppendLine("size "+b.size);}foreach(var portal in roots.SelectMany(g=>g.GetComponentsInChildren<WorldPortal>(true)).OrderBy(p=>PathOf(p.transform)))other.AppendLine("portal "+PathOf(portal.transform)+" "+portal.DestinationScene+" "+portal.DestinationSpawnId+" "+portal.transform.localToWorldMatrix);return new Baseline{groundCount=count,groundCells=Hash(cells.ToString()),nonGround=Hash(other.ToString())};}
// Exactly ten reviewed art-role aliases on their owning map only. Geometry, cells,
// color, transforms, collider and portal hashing remain unchanged; old baseline is not regenerated.
static string CanonicalTilePath(string mapName,string tilePath){
if(mapName=="SchoolWallTilemap"){
switch(tilePath){
case "Assets/Data/Interiors/TileSets/SchoolComfy31/School_WallTop.asset":return "Assets/LDtk/Generated/SchoolTiles/Room_Builder_Walls_32x32_x32_y576_f0.asset";
case "Assets/Data/Interiors/TileSets/SchoolComfy31/School_WallBottom.asset":return "Assets/LDtk/Generated/SchoolTiles/Room_Builder_Walls_32x32_x32_y608_f0.asset";
case "Assets/Data/Interiors/TileSets/SchoolComfy31/School_WallCornerTop.asset":return "Assets/LDtk/Generated/SchoolTiles/Room_Builder_Walls_32x32_x96_y576_f0.asset";
case "Assets/Data/Interiors/TileSets/SchoolComfy31/School_WallCornerBottom.asset":return "Assets/LDtk/Generated/SchoolTiles/Room_Builder_Walls_32x32_x96_y608_f0.asset";
}
}
if(mapName=="SchoolBorderTilemap"){
switch(tilePath){
case "Assets/Data/Interiors/TileSets/SchoolComfy31/School_BorderLeftCap.asset":return "Assets/LDtk/Generated/InteriorTiles/Room_Builder_borders_32x32_x192_y192_f0.asset";
case "Assets/Data/Interiors/TileSets/SchoolComfy31/School_BorderMiddleCap.asset":return "Assets/LDtk/Generated/SchoolTiles/Room_Builder_borders_32x32_x320_y160_f0.asset";
case "Assets/Data/Interiors/TileSets/SchoolComfy31/School_BorderRightCap.asset":return "Assets/LDtk/Generated/InteriorTiles/Room_Builder_borders_32x32_x256_y192_f0.asset";
case "Assets/Data/Interiors/TileSets/SchoolComfy31/School_BorderLeftStrip.asset":return "Assets/LDtk/Generated/InteriorTiles/Room_Builder_borders_32x32_x192_y224_f0.asset";
case "Assets/Data/Interiors/TileSets/SchoolComfy31/School_BorderMiddleStrip.asset":return "Assets/LDtk/Generated/InteriorTiles/Room_Builder_borders_32x32_x384_y224_f0.asset";
case "Assets/Data/Interiors/TileSets/SchoolComfy31/School_BorderRightStrip.asset":return "Assets/LDtk/Generated/InteriorTiles/Room_Builder_borders_32x32_x256_y224_f0.asset";
}
}
return tilePath;
}
[Test]public void ArtAliasesDoNotMaskWrongMapsGroundOrUnknownTilePaths(){
{string p="Assets/Data/Interiors/TileSets/SchoolComfy31/School_WallTop.asset";foreach(string wrong in new[]{"SchoolBorderTilemap","SchoolGroundTilemap","HouseWallTilemap","UnrecognizedMap"})Assert.That(CanonicalTilePath(wrong,p),Is.EqualTo(p));}
{string p="Assets/Data/Interiors/TileSets/SchoolComfy31/School_WallBottom.asset";foreach(string wrong in new[]{"SchoolBorderTilemap","SchoolGroundTilemap","HouseWallTilemap","UnrecognizedMap"})Assert.That(CanonicalTilePath(wrong,p),Is.EqualTo(p));}
{string p="Assets/Data/Interiors/TileSets/SchoolComfy31/School_WallCornerTop.asset";foreach(string wrong in new[]{"SchoolBorderTilemap","SchoolGroundTilemap","HouseWallTilemap","UnrecognizedMap"})Assert.That(CanonicalTilePath(wrong,p),Is.EqualTo(p));}
{string p="Assets/Data/Interiors/TileSets/SchoolComfy31/School_WallCornerBottom.asset";foreach(string wrong in new[]{"SchoolBorderTilemap","SchoolGroundTilemap","HouseWallTilemap","UnrecognizedMap"})Assert.That(CanonicalTilePath(wrong,p),Is.EqualTo(p));}
{string p="Assets/Data/Interiors/TileSets/SchoolComfy31/School_BorderLeftCap.asset";foreach(string wrong in new[]{"SchoolWallTilemap","SchoolGroundTilemap","HouseWallTilemap","UnrecognizedMap"})Assert.That(CanonicalTilePath(wrong,p),Is.EqualTo(p));}
{string p="Assets/Data/Interiors/TileSets/SchoolComfy31/School_BorderMiddleCap.asset";foreach(string wrong in new[]{"SchoolWallTilemap","SchoolGroundTilemap","HouseWallTilemap","UnrecognizedMap"})Assert.That(CanonicalTilePath(wrong,p),Is.EqualTo(p));}
{string p="Assets/Data/Interiors/TileSets/SchoolComfy31/School_BorderRightCap.asset";foreach(string wrong in new[]{"SchoolWallTilemap","SchoolGroundTilemap","HouseWallTilemap","UnrecognizedMap"})Assert.That(CanonicalTilePath(wrong,p),Is.EqualTo(p));}
{string p="Assets/Data/Interiors/TileSets/SchoolComfy31/School_BorderLeftStrip.asset";foreach(string wrong in new[]{"SchoolWallTilemap","SchoolGroundTilemap","HouseWallTilemap","UnrecognizedMap"})Assert.That(CanonicalTilePath(wrong,p),Is.EqualTo(p));}
{string p="Assets/Data/Interiors/TileSets/SchoolComfy31/School_BorderMiddleStrip.asset";foreach(string wrong in new[]{"SchoolWallTilemap","SchoolGroundTilemap","HouseWallTilemap","UnrecognizedMap"})Assert.That(CanonicalTilePath(wrong,p),Is.EqualTo(p));}
{string p="Assets/Data/Interiors/TileSets/SchoolComfy31/School_BorderRightStrip.asset";foreach(string wrong in new[]{"SchoolWallTilemap","SchoolGroundTilemap","HouseWallTilemap","UnrecognizedMap"})Assert.That(CanonicalTilePath(wrong,p),Is.EqualTo(p));}
foreach(string map in new[]{"SchoolWallTilemap","SchoolBorderTilemap"})foreach(string p in new[]{
"Assets/Data/Interiors/TileSets/SchoolComfy31/School_WallUnknown.asset",
"Assets/Data/Interiors/TileSets/SchoolComfy31/School_BorderUnknown.asset",
"Assets/Data/Interiors/TileSets/Other/School_WallTop.asset",
"Assets/Data/Interiors/TileSets/SchoolComfy31/School_OakFloor.asset",
"Assets/LDtk/Generated/SchoolTiles/Room_Builder_Walls_32x32_x32_y576_f0.asset"})
Assert.That(CanonicalTilePath(map,p),Is.EqualTo(p));
}
[Test]public void CornerAliasesRetainTheirDistinctOriginalRolePaths(){
string top=CanonicalTilePath("SchoolWallTilemap","Assets/Data/Interiors/TileSets/SchoolComfy31/School_WallCornerTop.asset");
string bottom=CanonicalTilePath("SchoolWallTilemap","Assets/Data/Interiors/TileSets/SchoolComfy31/School_WallCornerBottom.asset");
Assert.That(top,Is.EqualTo("Assets/LDtk/Generated/SchoolTiles/Room_Builder_Walls_32x32_x96_y576_f0.asset"));
Assert.That(bottom,Is.EqualTo("Assets/LDtk/Generated/SchoolTiles/Room_Builder_Walls_32x32_x96_y608_f0.asset"));
Assert.That(top,Is.Not.EqualTo(bottom),"Shared TOP pixels must not collapse distinct original corner role identities");
}
static string PathOf(Transform t)=>t.parent==null?t.name:PathOf(t.parent)+"/"+t.name;
static string Hash(string value)=>BitConverter.ToString(SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-","");
[Test]public void FloorSpriteMatchesOneWorldCellWithoutMipmaps(){string path="Assets/Art/SchoolPixels/Comfy31/School_oak_floor_48.png";var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);Assert.That(sprite,Is.Not.Null);Assert.That(sprite.rect.size,Is.EqualTo(new Vector2(48,48)));Assert.That(sprite.pixelsPerUnit,Is.EqualTo(48));Assert.That(sprite.pivot,Is.EqualTo(new Vector2(24,24)));var i=(TextureImporter)AssetImporter.GetAtPath(path);Assert.That(i.filterMode,Is.EqualTo(FilterMode.Point));Assert.That(i.mipmapEnabled,Is.False);Assert.That(i.textureCompression,Is.EqualTo(TextureImporterCompression.Uncompressed));}
[Test]public void SavedSchoolUsesNewFloorAndPreservesOccupiedCellsAndNonGroundStructure(){var scene=EditorSceneManager.OpenScene("Assets/Scenes/School.unity",OpenSceneMode.Additive);try{var expected=JsonUtility.FromJson<Baseline>(File.ReadAllText("output/design-comfy-20261031/evidence/school-floor-saved-baseline.json"));var actual=Capture(scene);Assert.That(actual.groundCount,Is.EqualTo(expected.groundCount));Assert.That(actual.groundCells,Is.EqualTo(expected.groundCells));var board=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Rootborn.UI.StudentLife.DailyPlanBoardInteractor>(true)).Single();Assert.That(board.transform.position,Is.EqualTo(new Vector3(9.5f,-2f,0)),"Reviewed wall-mounted board position must persist");var mounted=board.transform.position;try{board.transform.position=new Vector3(9.5f,-6.5f,0);var normalized=Capture(scene);Assert.That(normalized.nonGround,Is.EqualTo(expected.nonGround),"Only the reviewed board wall mount and ten exact map-owned art-role path aliases differ from the floor-stage baseline; cells, matrices, colors, collider fields and portals remain exact");}finally{board.transform.position=mounted;}var floor=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Tilemap>(true)).Single(m=>m.name=="SchoolGroundTilemap");var tile=AssetDatabase.LoadAssetAtPath<Tile>("Assets/Data/Interiors/TileSets/SchoolComfy31/School_OakFloor.asset");Assert.That(tile,Is.Not.Null);foreach(var cell in floor.cellBounds.allPositionsWithin)if(floor.HasTile(cell))Assert.That(floor.GetTile(cell),Is.SameAs(tile));Assert.That(floor.GetComponent<Collider2D>(),Is.Null);Assert.That(tile.colliderType,Is.EqualTo(Tile.ColliderType.None));}finally{EditorSceneManager.CloseScene(scene,true);}}
}}
