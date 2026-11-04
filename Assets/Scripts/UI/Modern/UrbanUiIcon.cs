using UnityEngine;using UnityEngine.UI;
namespace Rootborn.UI.Modern {
 /// <summary>Decorative icon binding; theme readiness is event-driven and the shared owner loads textures.</summary>
 [ExecuteAlways,DisallowMultipleComponent,RequireComponent(typeof(Image))]
 public sealed class UrbanUiIcon:MonoBehaviour {
  [SerializeField] private string _key;
  [SerializeField] private UnityEngine.Object _definition;
  private Image _image;
  private Sprite _fallbackSprite;
  public void Configure(string key){ConfigureWithFallback(key,null);}
  public void ConfigureWithFallback(string key,Sprite fallbackSprite){_definition=null;_key=key;_fallbackSprite=fallbackSprite;Subscribe();Refresh();}
  public void ConfigureFor(UnityEngine.Object definition,string fallbackKey){_fallbackSprite=null;_definition=definition;_key=fallbackKey;Subscribe();Refresh();}
  private void OnEnable(){Subscribe();Refresh();}
  private void OnDisable(){UrbanUiThemeRuntime.Changed-=Refresh;if(_image!=null)_image.enabled=false;}
  private void OnDestroy(){UrbanUiThemeRuntime.Changed-=Refresh;}
  private void OnValidate(){Refresh();}
  private void Subscribe(){UrbanUiThemeRuntime.Changed-=Refresh;if(isActiveAndEnabled)UrbanUiThemeRuntime.Changed+=Refresh;}
  private void Refresh(){
   if(_image==null)_image=GetComponent<Image>();if(_image==null)return;
   var theme=UrbanUiThemeRuntime.Current;_image.sprite=(theme!=null?theme.ResolveIcon(_definition,_key):null)??_fallbackSprite;
   _image.type=Image.Type.Simple;_image.preserveAspect=true;_image.raycastTarget=false;_image.color=Color.white;
   _image.enabled=isActiveAndEnabled&&_image.sprite!=null;
  }
 }
}
