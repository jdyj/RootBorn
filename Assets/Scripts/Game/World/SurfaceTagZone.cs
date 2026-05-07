using UnityEngine;

namespace Rootborn.Game.World
{
    public sealed class SurfaceTagZone : MonoBehaviour
    {
        [SerializeField] private string _surface = "Soil";

        public string Surface => string.IsNullOrEmpty(_surface) ? "Soil" : _surface;

        public void SetSurfaceForRuntime(string surface)
        {
            _surface = string.IsNullOrEmpty(surface) ? "Soil" : surface;
        }
    }
}
