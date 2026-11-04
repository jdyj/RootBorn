using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rootborn.Game.Careers;
using Rootborn.Game.Common;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using Object=UnityEngine.Object;
namespace Rootborn.Tests.EditMode.Careers
{
    public sealed class FacilityVisualTests
    {
        private readonly List<Object> _objects=new List<Object>();
        [TearDown]public void TearDown(){foreach(var o in _objects)if(o!=null)Object.DestroyImmediate(o);_objects.Clear();}
        [Test]public void Footprint_RejectsCollisionAcrossTheEntrance()
        {
            var visual=Visual();Cells(visual,"_collisionCells",new Vector2Int(1,0));Assert.That(Get(visual,"IsValid"),Is.False);
        }
        [Test]public void Footprint_RejectsOutOfBoundsCollisionCells()
        {
            var visual=Visual();Cells(visual,"_collisionCells",new Vector2Int(8,1));Assert.That(Get(visual,"IsValid"),Is.False);
        }
        [Test]public void DoorAnchor_UsesMeasuredOffsetInsteadOfBuildingCenter()
        {
            var visual=Visual();Set(visual,"_spriteOffset",new Vector2(1.5f,-.5f));Set(visual,"_entranceOffset",new Vector2(-.4f,.1f));Assert.That(Get(visual,"EntranceLocalPosition"),Is.EqualTo(new Vector2(1.1f,-.4f)));Assert.That(Get(visual,"IsValid"),Is.True);
        }
        [Test]public void Registry_HasTwelveDistinctFacilityStagesWithClearEntrances()
        {
            var registry=AssetDatabase.LoadAssetAtPath<GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset");var p=new SerializedObject(registry).FindProperty("_facilityVisuals");Assert.That(p,Is.Not.Null);Assert.That(p.arraySize,Is.EqualTo(12));var keys=new HashSet<string>();for(int i=0;i<p.arraySize;i++){var v=p.GetArrayElementAtIndex(i).objectReferenceValue;Assert.That(v,Is.Not.Null);Assert.That(Get(v,"IsValid"),Is.True,v.name);var f=(FacilityDefinition)Get(v,"Facility");Assert.That(keys.Add(f.Id+":"+Get(v,"Level")),Is.True);Assert.That(RegistryContains(registry,v),Is.True);}
        }
        [Test]public void BuildingTextures_PreserveNativePixelsAndBinaryTransparency()
        {
            foreach(var id in Ids())
            {
                var path="Assets/Art/CareerFacilities/Sprites/"+id+".png";var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);Assert.That(sprite,Is.Not.Null,path);var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                Assert.That(importer.filterMode,Is.EqualTo(FilterMode.Point));Assert.That(importer.spritePixelsPerUnit,Is.EqualTo(48));Assert.That(importer.textureCompression,Is.EqualTo(TextureImporterCompression.Uncompressed));Assert.That(importer.mipmapEnabled,Is.False);
                var raw=new Texture2D(2,2,TextureFormat.RGBA32,false);_objects.Add(raw);Assert.That(raw.LoadImage(System.IO.File.ReadAllBytes(path)),Is.True);Assert.That(sprite.rect.size,Is.EqualTo(new Vector2(raw.width,raw.height)));
                foreach(var pixel in raw.GetPixels32())Assert.That(pixel.a==0||pixel.a==255,Is.True,id+" has softened alpha");
            }
        }
        [Test]public void BuildingSprites_AreLocalAddressablesWithUniqueStableAddresses()
        {
            foreach(var id in Ids()){var path="Assets/Art/CareerFacilities/Sprites/"+id+".png";var entry=AddressableAssetSettingsDefaultObject.Settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(path));Assert.That(entry,Is.Not.Null,path);Assert.That(entry.parentGroup.Name,Is.EqualTo("Sprites"));Assert.That(entry.address,Is.EqualTo("sprites/career-facilities/"+id));}
        }
        private bool RegistryContains(GameDataRegistry registry,Object visual){var f=(FacilityDefinition)Get(visual,"Facility");return registry.FacilityDefinitions.Contains(f);}
        private static IEnumerable<string> Ids(){foreach(var name in new[]{"chef","emergencycare","interior","soldier"})for(int level=1;level<=3;level++)yield return name+"_lv"+level;}
        private ScriptableObject Visual()
        {
            var type=typeof(FacilityDefinition).Assembly.GetType("Rootborn.Game.Careers.FacilityVisualDefinition");Assert.That(type,Is.Not.Null,"Missing data-defined facility geometry");var v=ScriptableObject.CreateInstance(type);_objects.Add(v);var f=ScriptableObject.CreateInstance<FacilityDefinition>();_objects.Add(f);Set(v,"_facility",f);Set(v,"_level",1);Set(v,"_spriteAddress","sprite.fixture");Set(v,"_siteSize",new Vector2Int(4,2));Cells(v,"_entranceCells",new Vector2Int(1,0),new Vector2Int(2,0));Cells(v,"_approachCells",new Vector2Int(1,-1),new Vector2Int(2,-1));Cells(v,"_collisionCells",new Vector2Int(0,0),new Vector2Int(3,0));return v;
        }
        private object Get(object o,string field){var p=o.GetType().GetProperty(field);Assert.That(p,Is.Not.Null,field);return p.GetValue(o);}
        private void Set(Object o,string field,object value){var so=new SerializedObject(o);var p=so.FindProperty(field);Assert.That(p,Is.Not.Null,field);if(value is string s)p.stringValue=s;else if(value is int i)p.intValue=i;else if(value is Vector2Int v)p.vector2IntValue=v;else if(value is Vector2 f)p.vector2Value=f;else p.objectReferenceValue=(Object)value;so.ApplyModifiedPropertiesWithoutUndo();}
        private void Cells(Object o,string field,params Vector2Int[] cells){var so=new SerializedObject(o);var p=so.FindProperty(field);Assert.That(p,Is.Not.Null,field);p.arraySize=cells.Length;for(int i=0;i<cells.Length;i++)p.GetArrayElementAtIndex(i).vector2IntValue=cells[i];so.ApplyModifiedPropertiesWithoutUndo();}
    }
}
