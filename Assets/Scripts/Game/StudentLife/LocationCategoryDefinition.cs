using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(fileName = "LocationCategory_New", menuName = "Rootborn/Student Life/Locations/Category")]
    public sealed class LocationCategoryDefinition : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private string _displayName;
        [SerializeField] private int _sortOrder;

        public string Id => string.IsNullOrEmpty(_id) ? name : _id;
        public string DisplayName => string.IsNullOrEmpty(_displayName) ? Id : _displayName;
        public int SortOrder => _sortOrder;

        public void ConfigureForTests(string id, string displayName, int sortOrder)
        {
            _id = id;
            _displayName = displayName;
            _sortOrder = sortOrder;
        }
    }
}
