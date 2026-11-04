// Design candidate: do not import until the capture API/design is accepted.
using System;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Placement;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
namespace Rootborn.Tests.EditMode.World
{
    public sealed class AuthoredHousePlacementCaptureTests
    {
        private Scene _scene; private Tilemap _ground, _collision; private Tile _tile;
        private readonly Vector3Int _free=new Vector3Int(-4,-3,0);
        private readonly Vector3Int _spawn=new Vector3Int(-3,-3,0);
        private readonly Vector3Int _door=new Vector3Int(-2,-3,0);
        [SetUp] public void SetUp()
        {
            _scene=EditorSceneManager.NewPreviewScene();
            var grid=new GameObject("AuthoredGrid",typeof(Grid)); SceneManager.MoveGameObjectToScene(grid,_scene);
            _ground=Map(grid,"Ground"); _collision=Map(grid,"Collision");
            _tile=ScriptableObject.CreateInstance<Tile>();
            for(int x=-4;x<=-2;x++) for(int y=-3;y<=-2;y++) _ground.SetTile(new Vector3Int(x,y,0),_tile);
        }
        private Tilemap Map(GameObject parent,string name)
        {var go=new GameObject(name,typeof(Tilemap)); go.transform.SetParent(parent.transform,false); return go.GetComponent<Tilemap>();}
        [TearDown] public void TearDown(){EditorSceneManager.ClosePreviewScene(_scene); UnityEngine.Object.DestroyImmediate(_tile);}
        private object Capture(params Vector3Int[] fixedCells)
        {
            var type=typeof(TilePlacementSurface).Assembly.GetType("Rootborn.Game.Placement.AuthoredHousePlacementModel");
            Assert.That(type,Is.Not.Null,"Readonly authored capture must precede any placement replay.");
            var method=type.GetMethod("Capture",BindingFlags.Public|BindingFlags.Static);
            Assert.That(method,Is.Not.Null);
            return method.Invoke(null,new object[]{_ground,_collision,fixedCells,_spawn,new[]{_door}});
        }
        private bool CanOccupy(object model,params Vector3Int[] cells)
        {var method=model.GetType().GetMethod("CanOccupy",new[]{typeof(Vector3Int[])}); Assert.That(method,Is.Not.Null); return (bool)method.Invoke(model,new object[]{cells});}
        [Test] public void Capture_PreservesNegativeGridCoordinates()
        {var model=Capture(); Assert.That(CanOccupy(model,_free),Is.True); Assert.That(CanOccupy(model,Vector3Int.zero),Is.False);}
        [Test] public void Capture_RejectsCollisionAndExplicitFixedPropFootprint()
        {var wall=new Vector3Int(-4,-2,0); _collision.SetTile(wall,_tile); var model=Capture(_free); Assert.That(CanOccupy(model,wall),Is.False); Assert.That(CanOccupy(model,_free),Is.False);}
        [Test] public void Capture_ReservesSpawnAndDoorApproach()
        {var model=Capture(); Assert.That(CanOccupy(model,_spawn),Is.False); Assert.That(CanOccupy(model,_door),Is.False);}
        [Test] public void Capture_DoesNotMutateAuthoredTileOrTransform()
        {
            _ground.SetTileFlags(_free,TileFlags.None); _ground.SetColor(_free,Color.cyan);
            var matrix=Matrix4x4.Scale(new Vector3(-1,1,1)); _ground.SetTransformMatrix(_free,matrix);
            var position=_ground.transform.position; Capture();
            Assert.That(_ground.GetTile(_free),Is.SameAs(_tile)); Assert.That(_ground.GetColor(_free),Is.EqualTo(Color.cyan));
            Assert.That(_ground.GetTransformMatrix(_free),Is.EqualTo(matrix)); Assert.That(_ground.transform.position,Is.EqualTo(position));
        }
        [Test] public void Capture_IsSnapshotRatherThanLiveMutableTilemapView()
        {var model=Capture(); _ground.SetTile(_free,null); Assert.That(CanOccupy(model,_free),Is.True);}
        [Test] public void BlockingPlacement_CannotSealTheOnlyRouteToDoor()
        {
            _ground.ClearAllTiles();for(int x=-4;x<=-2;x++)_ground.SetTile(new Vector3Int(x,-3,0),_tile);
            var type=typeof(TilePlacementSurface).Assembly.GetType("Rootborn.Game.Placement.AuthoredHousePlacementModel");Assert.That(type,Is.Not.Null);
            var model=type.GetMethod("Capture").Invoke(null,new object[]{_ground,_collision,Array.Empty<Vector3Int>(),_free,new[]{_door}});
            var m=type.GetMethod("PreservesAccess",new[]{typeof(Vector3Int[])});Assert.That(m,Is.Not.Null,"Blocking furniture must preserve door access.");
            Assert.That((bool)m.Invoke(model,new object[]{new[]{_spawn}}),Is.False);
            Assert.That((bool)m.Invoke(model,new object[]{Array.Empty<Vector3Int>()}),Is.True);
        }
        [Test] public void FootprintValidation_RejectsWholePlacementWhenOneCellIsReserved()
        {var model=Capture(); Assert.That(CanOccupy(model,_free,_spawn),Is.False);}
    }
}
