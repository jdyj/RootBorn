using System;using System.Collections.Generic;using UnityEngine;
namespace Rootborn.UI.Modern {
 [CreateAssetMenu(fileName="UrbanMoldingTheme",menuName="ROOTBORN/UI/Urban Molding Theme")]
 public sealed class UrbanUiThemeDefinition:ScriptableObject {
  public const string Address="data/ui/urban-molding";
  public const string AtlasAddress="sprites/ui/urban-molding/atlas";
  public const string IconAtlasAddress="sprites/ui/urban-life/icons";
  [SerializeField] private Sprite _panelSprite;
  [SerializeField] private Sprite _buttonSprite;
  [SerializeField] private Sprite _slotSprite;
  [SerializeField] private Color _buttonTextColor=new Color32(244,237,218,255);
  [SerializeField] private IconEntry[] _icons=Array.Empty<IconEntry>();
  [SerializeField] private PresentationEntry[] _presentations=Array.Empty<PresentationEntry>();
  [Serializable] private sealed class IconEntry { [SerializeField] internal string _key;[SerializeField] internal Sprite _sprite; }
  [Serializable] private sealed class PresentationEntry { [SerializeField] internal UnityEngine.Object _definition;[SerializeField] internal string _iconKey; }
  private Dictionary<string,Sprite> _iconIndex;
  private Dictionary<UnityEngine.Object,string> _presentationIndex;
  public Sprite PanelSprite=>_panelSprite;public Sprite ButtonSprite=>_buttonSprite;public Sprite SlotSprite=>_slotSprite;public Color ButtonTextColor=>_buttonTextColor;
  private void OnEnable(){_iconIndex=null;_presentationIndex=null;}
  private void OnValidate(){_iconIndex=null;_presentationIndex=null;}
  public Sprite GetIcon(string key){EnsureIndex();return !string.IsNullOrEmpty(key)&&_iconIndex.TryGetValue(key,out var value)?value:null;}
  public Sprite ResolveIcon(UnityEngine.Object definition,string fallbackKey){EnsureIndex();if(definition!=null&&_presentationIndex.TryGetValue(definition,out var key)){var found=GetIcon(key);if(found!=null)return found;}return GetIcon(fallbackKey);}
  private void EnsureIndex(){
   if(_iconIndex!=null)return;
   _iconIndex=new Dictionary<string,Sprite>(StringComparer.Ordinal);_presentationIndex=new Dictionary<UnityEngine.Object,string>();
   if(_icons!=null)foreach(var entry in _icons)if(entry!=null&&!string.IsNullOrEmpty(entry._key)&&!_iconIndex.ContainsKey(entry._key))_iconIndex.Add(entry._key,entry._sprite);
   if(_presentations!=null)foreach(var entry in _presentations)if(entry!=null&&entry._definition!=null&&!string.IsNullOrEmpty(entry._iconKey)&&!_presentationIndex.ContainsKey(entry._definition))_presentationIndex.Add(entry._definition,entry._iconKey);
  }
 }
}
