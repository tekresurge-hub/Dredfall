#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Dregfall.Editor
{
    [InitializeOnLoad]
    public static class DregfallCivilizationAssetSetup
    {
        const string ResourcesFolder = "Assets/DREGFALL/Resources";
        const string CatalogPath = ResourcesFolder + "/DREGFALL_CivilizationCatalog.asset";

        static DregfallCivilizationAssetSetup() => EditorApplication.delayCall += EnsureCatalog;

        [MenuItem("DREGFALL/Rebuild Civilization Catalog")]
        public static void EnsureCatalog()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!AssetDatabase.IsValidFolder(ResourcesFolder))
            {
                Directory.CreateDirectory(ResourcesFolder);
                AssetDatabase.Refresh();
            }

            DregfallCivilizationCatalog catalog = AssetDatabase.LoadAssetAtPath<DregfallCivilizationCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<DregfallCivilizationCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.isolatedBuildings = LoadPrefabs(new[]
            {
                "Assets/Abandoned buildings/Prefab/abandoned_buildings/house_enter.prefab"
            });

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"[DREGFALL] Civilization catalog ready: {catalog.isolatedBuildings.Length} isolated building prefab(s).");
        }

        static GameObject[] LoadPrefabs(string[] paths)
        {
            var result = new System.Collections.Generic.List<GameObject>();
            foreach (string path in paths)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null) result.Add(prefab);
                else Debug.LogWarning("[DREGFALL] Civilization prefab missing: " + path);
            }
            return result.ToArray();
        }
    }
}
#endif
