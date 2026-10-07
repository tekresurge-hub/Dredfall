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
                "Assets/Abandoned buildings/Prefab/abandoned_buildings/house_enter.prefab",
                "Assets/Abandoned buildings/Prefab/abandoned_buildings/house_aband.prefab",
                "Assets/Abandoned buildings/Prefab/abandoned_buildings/house_ruined.prefab"
            });

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"[DREGFALL] Civilization catalog ready: {catalog.isolatedBuildings.Length} isolated building prefab(s).");
        }

        static void RepairBuildingMaterials(GameObject prefab, string prefabPath)
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) return;

            foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
            {
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material == null) continue;

                    string n = material.name.ToLowerInvariant();
                    string albedoPath = n.Contains("roof")
                        ? "Assets/Abandoned buildings/Textures/abandoned_buildings/T_house_roof_D.tga"
                        : n.Contains("ruined")
                            ? "Assets/Abandoned buildings/Textures/abandoned_buildings/T_house_ruined_D.tga"
                            : "Assets/Abandoned buildings/Textures/abandoned_buildings/T_house_aband_D.tga";
                    string normalPath = n.Contains("roof")
                        ? "Assets/Abandoned buildings/Textures/abandoned_buildings/T_house_roof_N.tga"
                        : n.Contains("ruined")
                            ? "Assets/Abandoned buildings/Textures/abandoned_buildings/T_house_ruined_N.tga"
                            : "Assets/Abandoned buildings/Textures/abandoned_buildings/T_house_aband_N.tga";

                    Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath);
                    Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
                    material.shader = urpLit;
                    material.SetColor("_BaseColor", Color.white);
                    material.SetFloat("_Metallic", 0f);
                    material.SetFloat("_Smoothness", 0.12f);
                    if (albedo != null) material.SetTexture("_BaseMap", albedo);
                    if (normal != null)
                    {
                        material.SetTexture("_BumpMap", normal);
                        material.SetFloat("_BumpScale", 1f);
                        material.EnableKeyword("_NORMALMAP");
                    }
                    material.DisableKeyword("_EMISSION");
                    EditorUtility.SetDirty(material);
                    Debug.Log($"[DREGFALL] Rebuilt building material '{material.name}' with explicit URP textures ({prefabPath}).");
                }
            }
        }

        static GameObject[] LoadPrefabs(string[] paths)
        {
            var result = new System.Collections.Generic.List<GameObject>();
            foreach (string path in paths)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                {
                    RepairBuildingMaterials(prefab, path);
                    result.Add(prefab);
                }
                else Debug.LogWarning("[DREGFALL] Civilization prefab missing: " + path);
            }
            return result.ToArray();
        }
    }
}
#endif
