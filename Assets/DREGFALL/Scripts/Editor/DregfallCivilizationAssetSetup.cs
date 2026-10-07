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
                Material[] materials = renderer.sharedMaterials;
                foreach (Material material in materials)
                {
                    if (material == null || material.shader == null) continue;
                    string shaderName = material.shader.name;
                    bool needsConversion = shaderName == "Standard" ||
                                           shaderName == "Hidden/InternalErrorShader" ||
                                           shaderName.StartsWith("Legacy Shaders/") ||
                                           shaderName.StartsWith("HDRP/") ||
                                           shaderName.Contains("High Definition");
                    if (!needsConversion) continue;

                    Texture albedo = material.HasProperty("_BaseMap") ? material.GetTexture("_BaseMap") :
                                     material.HasProperty("_MainTex") ? material.GetTexture("_MainTex") : null;
                    Texture normal = material.HasProperty("_BumpMap") ? material.GetTexture("_BumpMap") : null;
                    Color tint = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") :
                                 material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;
                    float metallic = material.HasProperty("_Metallic") ? material.GetFloat("_Metallic") : 0f;
                    float smoothness = material.HasProperty("_Smoothness") ? material.GetFloat("_Smoothness") :
                                       material.HasProperty("_Glossiness") ? material.GetFloat("_Glossiness") : 0.18f;

                    material.shader = urpLit;
                    if (albedo != null) material.SetTexture("_BaseMap", albedo);
                    if (normal != null)
                    {
                        material.SetTexture("_BumpMap", normal);
                        material.EnableKeyword("_NORMALMAP");
                    }
                    material.SetColor("_BaseColor", tint);
                    material.SetFloat("_Metallic", metallic);
                    material.SetFloat("_Smoothness", Mathf.Clamp(smoothness, 0f, 0.45f));
                    EditorUtility.SetDirty(material);
                    Debug.Log($"[DREGFALL] Converted building material '{material.name}' to URP Lit ({prefabPath}).");
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
