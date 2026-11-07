using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Rootborn.Game.Housing;
using Rootborn.Game.Quests;
using Rootborn.Game.Interiors;
using Rootborn.Game.Placement;
using Rootborn.Game.Player;
using Rootborn.Game.Save;
using Rootborn.Game.World;
using Rootborn.UI.Careers;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
namespace Rootborn.UI.Interiors
{
    public sealed class AuthoredHousePlacementBinding : MonoBehaviour
    {
        private AdultCareerRuntimeComponent _owner;
        private AuthoredHouseFurnitureSession _session;
        private int _stage;
        private string _slot;
        private bool _loaded;
        public static bool BindOwner(Scene scene,AdultCareerRuntimeComponent owner)
        {
            if(AuthoredWorldScene.Find(scene)==null)return true;
            var applier=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<InteriorTilemapApplier>(true)).FirstOrDefault();
            if(applier==null)return true;
            if(owner==null || !owner.CanMutate || owner.gameObject.scene!=scene)return false;
            var binding=applier.GetComponent<AuthoredHousePlacementBinding>();
            if(binding==null)binding=applier.gameObject.AddComponent<AuthoredHousePlacementBinding>();
            return binding.Bind(owner);
        }
        private bool Bind(AdultCareerRuntimeComponent owner)
        {
            if(_session!=null)
            { if(_slot!=owner.Progress.SaveSlot)return false;_owner=owner;Configure();return _loaded || Reload(); }
            _owner=owner;_slot=owner.Progress.SaveSlot;_stage=HouseStatePersistence.Load(_slot).CurrentStageIndex;
            var maps=gameObject.scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Tilemap>(true)).ToArray();
            var ground=maps.SingleOrDefault(m=>m.name=="HouseGroundTilemap");
            var collision=maps.SingleOrDefault(m=>m.name=="HouseCollisionTilemap");
            var decorations=maps.SingleOrDefault(m=>m.name=="HouseDecorationTilemap");
            if(ground==null || collision==null)return false;
            var fixedCells=new System.Collections.Generic.HashSet<Vector3Int>();
            if(decorations!=null)foreach(var cell in decorations.cellBounds.allPositionsWithin)if(decorations.HasTile(cell))fixedCells.Add(cell);
            foreach(var root in gameObject.scene.GetRootGameObjects())foreach(var box in root.GetComponentsInChildren<BoxCollider2D>(true))
            {
                if(!box.enabled || box.isTrigger || box.GetComponentInParent<PlayerIdentity>()!=null)continue;
                var bounds=box.bounds;var min=ground.WorldToCell(bounds.min+Vector3.one*.01f);var max=ground.WorldToCell(bounds.max-Vector3.one*.01f);
                for(int x=min.x;x<=max.x;x++)for(int y=min.y;y<=max.y;y++)fixedCells.Add(new Vector3Int(x,y,0));
            }
            var spawn=ground.WorldToCell(owner.transform.position);
            var approaches=gameObject.scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<WorldSpawnPoint>(true)).Select(s=>ground.WorldToCell(s.transform.position)).ToArray();
            var model=AuthoredHousePlacementModel.Capture(ground,collision,fixedCells.ToArray(),spawn,approaches);
            var objects=CreateMap("AuthoredUserFurniture",ground,true);var occupancy=CreateMap("AuthoredUserOccupancy",ground,false);
            var surface=objects.gameObject.AddComponent<TilePlacementSurface>();
            surface.ConfigureForTests("house",ground,objects,occupancy,null,model.Bounds,
                TilePlacementRuleFlags.RequireGround|TilePlacementRuleFlags.RejectOccupiedCells|TilePlacementRuleFlags.StayInsideBounds);
            _session=gameObject.AddComponent<AuthoredHouseFurnitureSession>();_session.Bind(model,surface);Configure();
            bool loaded=Reload();
            if(loaded)AnnouncePlacedFurniture();
            return loaded;
        }
        private Tilemap CreateMap(string label,Tilemap ground,bool render)
        {
            var go=new GameObject(label,typeof(Tilemap),typeof(TilemapRenderer));SceneManager.MoveGameObjectToScene(go,gameObject.scene);
            go.transform.SetParent(ground.transform.parent,false);var renderer=go.GetComponent<TilemapRenderer>();renderer.enabled=render;
            renderer.mode=TilemapRenderer.Mode.Individual;renderer.sortingOrder=10000;
            if(render)go.AddComponent<PlacedFurnitureDepthPresentation>();
            return go.GetComponent<Tilemap>();
        }
        private bool Owns() => _owner!=null && _owner.CanMutate && _owner.gameObject.scene==gameObject.scene && _owner.Progress.SaveSlot==_slot && ActiveSaveContext.SlotId==_slot;
        private void Configure()
        {
            _session.ConfigureCommit(Owns,Save,Reload);
            _session.PlacementCommitted-=RecordPlacement;
            _session.PlacementCommitted+=RecordPlacement;
        }
        private void RecordPlacement(FurnitureDefinition definition) => RecordPlacement(definition,false);
        // A replay announces furniture already in the room; quest chains catch up on it, the quest log does not count it again.
        private void RecordPlacement(FurnitureDefinition definition,bool replay)
        {
            if(!Owns())return;
            QuestGameplayEventBridge.Record(new QuestEvent(QuestEventKind.FurniturePlaced,
                QuestGameplayEventBridge.CreateRequestKey("furniture-placed"),furniture:definition,
                surfaceId:_session.Surface.SurfaceId,isReplay:replay),_owner.StudentComponent.Progress);
        }
        public static bool StepWantsFurniture(QuestStepDefinition step)
        {
            var objectives = step != null ? step.Objectives : null;
            if (objectives == null) return false;
            for (int i = 0; i < objectives.Length; i++) if (objectives[i] is Rootborn.Game.Quests.Objectives.FurniturePlacedQuestObjective) return true;
            return false;
        }
        // Every announcement saves and leaves a permanent processed-event key, so skip it unless a chain is waiting for furniture.
        private bool AnyCurrentStepWantsFurniture()
        {
            if(!Rootborn.Game.Common.GameDataRegistryProvider.TryGet(out var registry)||registry==null||_owner==null)return false;
            var log=_owner.QuestChains;
            var chains=registry.QuestChains;
            if(log==null||chains==null)return false;
            for(int i=0;i<chains.Length;i++)if(chains[i]!=null&&StepWantsFurniture(log.GetCurrentStep(chains[i])))return true;
            return false;
        }
        // A piece placed before its quest step began still counts once the room is entered or a new day starts.
        public void AnnouncePlacedFurniture()
        {
            if(_session==null||!AnyCurrentStepWantsFurniture())return;
            foreach(var definition in _session.PlacedFurniture.Distinct().ToArray())RecordPlacement(definition,true);
        }
        private void OnDestroy()
        { if(_session!=null)_session.PlacementCommitted-=RecordPlacement; }
        private bool Save(FurniturePlacementSaveData[] records)
        {
            if(!Owns())return false;
            string path=Path.Combine(new SaveService(_slot).DirectoryPath,FurniturePlacementLayoutPersistence.GetHouseLayoutFileNameForStage(_stage));
            string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try {File.WriteAllText(temp,JsonUtility.ToJson(new FurniturePlacementLayoutSaveData{Items=records},true));if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);return true;}
            catch(Exception ex) when(ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException){return false;}
            finally{try{if(File.Exists(temp))File.Delete(temp);}catch(IOException){}catch(UnauthorizedAccessException){}}
        }
        private bool Reload()
        {
            if(!Owns())return false;
            try
            {
                var storage=new SaveService(_slot);var json=storage.ReadJson(FurniturePlacementLayoutPersistence.GetHouseLayoutFileNameForStage(_stage));
                if(string.IsNullOrEmpty(json) && _stage==0)json=storage.ReadJson(FurniturePlacementLayoutPersistence.HouseLayoutFileName);
                FurniturePlacementSaveData[] records=Array.Empty<FurniturePlacementSaveData>();
                if(!string.IsNullOrEmpty(json))
                {
                    using(var document=JsonDocument.Parse(json))
                    {if(!document.RootElement.TryGetProperty("Items",out var items)||items.ValueKind!=JsonValueKind.Array)return false;}
                    records=JsonUtility.FromJson<FurniturePlacementLayoutSaveData>(json)?.Items;if(records==null)return false;
                }
                _session.Restore(records,_owner.Registry.FurnitureDefinitions);_loaded=true;return true;
            }
            catch(Exception ex) when(ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is JsonException){return false;}
        }
    }
}
