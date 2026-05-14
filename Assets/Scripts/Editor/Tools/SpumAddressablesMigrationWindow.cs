using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Rootborn.Editor.Tools
{
    public sealed class SpumAddressablesMigrationWindow : EditorWindow
    {
        private GameObject _sourcePrefab;
        private AnimationClip _animationClip;
        private Sprite _previewSprite;
        private string _outputRootPath = "Assets/Data/Characters/Spum";
        private string _catalogId = "spum.catalog.student";
        private string _appearanceId = "appearance.spum.student";
        private string _categoryId = "body";
        private string _partId = "spum.body.default";
        private string _displayNameKey = "character.spum.body.default";
        private bool _isDefaultPart = true;

        [MenuItem("Rootborn/SPUM/Addressables Migration")]
        public static void Open()
        {
            GetWindow<SpumAddressablesMigrationWindow>("SPUM Migration");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Source Assets", EditorStyles.boldLabel);
            _sourcePrefab = (GameObject)EditorGUILayout.ObjectField("Prefab", _sourcePrefab, typeof(GameObject), false);
            _animationClip = (AnimationClip)EditorGUILayout.ObjectField("Animation Clip", _animationClip, typeof(AnimationClip), false);
            _previewSprite = (Sprite)EditorGUILayout.ObjectField("Preview Sprite", _previewSprite, typeof(Sprite), false);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("ROOTBORN Output", EditorStyles.boldLabel);
            _outputRootPath = EditorGUILayout.TextField("Output Root", _outputRootPath);
            _catalogId = EditorGUILayout.TextField("Catalog Id", _catalogId);
            _appearanceId = EditorGUILayout.TextField("Appearance Id", _appearanceId);
            _categoryId = EditorGUILayout.TextField("Category Id", _categoryId);
            _partId = EditorGUILayout.TextField("Part Id", _partId);
            _displayNameKey = EditorGUILayout.TextField("Display Name Key", _displayNameKey);
            _isDefaultPart = EditorGUILayout.Toggle("Default Part", _isDefaultPart);

            using (new EditorGUI.DisabledScope(_sourcePrefab == null))
            {
                if (GUILayout.Button("Migrate"))
                {
                    RunMigration();
                }
            }
        }

        private void RunMigration()
        {
            string prefabPath = AssetDatabase.GetAssetPath(_sourcePrefab);
            var clipPaths = new List<string>();
            if (_animationClip != null)
            {
                clipPaths.Add(AssetDatabase.GetAssetPath(_animationClip));
            }

            var spritePaths = new List<string>();
            if (_previewSprite != null)
            {
                spritePaths.Add(AssetDatabase.GetAssetPath(_previewSprite));
            }

            var request = new SpumAddressablesMigrationRequest(
                prefabPath,
                clipPaths,
                spritePaths,
                _outputRootPath,
                _catalogId,
                _appearanceId,
                _categoryId,
                _partId,
                _displayNameKey,
                _isDefaultPart);

            SpumAddressablesMigrationResult result = SpumAddressablesMigrationService.Migrate(request);
            Debug.Log("[ROOTBORN/SPUM] Migrated SPUM assets to " + result.AddressablesGroupName + ": " + result.AppearanceAssetPath);
        }
    }
}
