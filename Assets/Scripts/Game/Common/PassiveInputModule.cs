using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Rootborn.Game.Common
{
    public static class UiInputModuleInstaller
    {
        public static bool PreferPassiveInputModule { get; set; }

        public static BaseInputModule AddPreferredInputModule(GameObject gameObject)
        {
            if (PreferPassiveInputModule)
            {
                return gameObject.GetComponent<PassiveInputModule>() ?? gameObject.AddComponent<PassiveInputModule>();
            }

            return gameObject.GetComponent<InputSystemUIInputModule>() ?? gameObject.AddComponent<InputSystemUIInputModule>();
        }
    }

    public sealed class PassiveInputModule : BaseInputModule
    {
        public override void Process()
        {
        }
    }
}
