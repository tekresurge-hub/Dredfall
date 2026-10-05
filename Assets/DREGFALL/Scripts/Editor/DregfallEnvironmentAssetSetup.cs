#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Dregfall.Editor
{
    [InitializeOnLoad]
    public static class DregfallEnvironmentAssetSetup
    {
        const string ResourcesFolder = "Assets/DREGFALL/Resources";
        const string CatalogPath = ResourcesFolder + "/DREGFALL_EnvironmentCatalog.asset";
        const string VersionKey = "DREGFALL_ENV_CATALOG_V1";

        static DregfallEnvironmentAssetSetup()
        {
            EditorApplication.delayCall += EnsureCatalog;
        }

        [MenuItem("DREGFALL/Rebuild Environment Catalog")]
        public static void EnsureCatalog()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            if (!AssetDatabase.IsValidFolder(ResourcesFolder))
            {
                Directory.CreateDirectory(ResourcesFolder);
                AssetDatabase.Refresh();
            }

            DregfallEnvironmentCatalog catalog = AssetDatabase.LoadAssetAtPath<DregfallEnvironmentCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<DregfallEnvironmentCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.trees = LoadPrefabs(new[]
            {
                "Assets/Realistic Tree/Prefabs/Standard/Ash/Ash 2.prefab",
                "Assets/Realistic Tree/Prefabs/Standard/Ash/Ash 5.prefab",
                "Assets/Realistic Tree/Prefabs/Standard/Birch/Birch 2.prefab",
                "Assets/Realistic Tree/Prefabs/Standard/Birch/Birch 6.prefab",
                "Assets/Realistic Tree/Prefabs/Standard/Chestnut/Chestnut 3.prefab",
                "Assets/Realistic Tree/Prefabs/Standard/Spruce/Spruce 3.prefab",
                "Assets/Realistic Tree/Prefabs/Standard/Spruce/Spruce 7.prefab",
                "Assets/Flora-Form/Vegetation/Pine_002_M.prefab",
                "Assets/Flora-Form/Vegetation/Pine_002_M2.prefab"
            });

            catalog.undergrowth = LoadPrefabs(new[]
            {
                "Assets/Flora-Form/Vegetation/Bush_A.prefab",
                "Assets/Flora-Form/Vegetation/Bush_B.prefab",
                "Assets/Flora-Form/Vegetation/BushDry_A.prefab",
                "Assets/Flora-Form/Vegetation/Fern_A.prefab",
                "Assets/Flora-Form/Vegetation/Fern_B.prefab",
                "Assets/Flora-Form/Vegetation/Heather_A.prefab",
                "Assets/Flora-Form/Vegetation/Juniper_Bush_01.prefab",
                "Assets/Flora-Form/Vegetation/Shrub.prefab"
            });

            catalog.rocks = LoadPrefabs(new[]
            {
                "Assets/Hill Rock Mountain Terrain/Prefab/rock_set_01.prefab",
                "Assets/Hill Rock Mountain Terrain/Prefab/rock_set_02.prefab",
                "Assets/Hill Rock Mountain Terrain/Prefab/rock_set_03.prefab",
                "Assets/Hill Rock Mountain Terrain/Prefab/rock_set_04.prefab"
            });

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            SessionState.SetBool(VersionKey, true);
            Debug.Log($"[DREGFALL] Environment catalog ready: {catalog.trees.Length} trees, {catalog.undergrowth.Length} undergrowth, {catalog.rocks.Length} rocks.");
        }

        static GameObject[] LoadPrefabs(string[] paths)
        {
            var list = new System.Collections.Generic.List<GameObject>();
            foreach (string path in paths)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    Debug.LogWarning("[DREGFALL] Environment prefab missing: " + path);
                    continue;
                }

                RepairPrefabMaterials(prefab, path);
                if (HasBrokenMaterials(prefab))
                {
                    Debug.LogWarning("[DREGFALL] Skipping prefab with unresolved material/shader errors: " + path);
                    continue;
                }

                list.Add(prefab);
            }
            return list.ToArray();
        }

        static void RepairPrefabMaterials(GameObject prefab, string prefabPath)
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) return;

            Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
            {
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material == null || material.shader == null) continue;
                    string shaderName = material.shader.name;
                    bool broken = shaderName == "Hidden/InternalErrorShader" ||
                                  shaderName.StartsWith("Legacy Shaders/") ||
                                  shaderName == "Standard" ||
                                  shaderName.Contains("Nature/SpeedTree");
                    if (!broken) continue;

                    string materialPath = AssetDatabase.GetAssetPath(material);
                    if (string.IsNullOrEmpty(materialPath) || !materialPath.StartsWith("Assets/")) continue;

                    Texture main = null;
                    Color tint = Color.white;
                    if (material.HasProperty("_BaseMap")) main = material.GetTexture("_BaseMap");
                    else if (material.HasProperty("_MainTex")) main = material.GetTexture("_MainTex");
                    if (material.HasProperty("_BaseColor")) tint = material.GetColor("_BaseColor");
                    else if (material.HasProperty("_Color")) tint = material.GetColor("_Color");

                    material.shader = urpLit;
                    if (main != null) material.SetTexture("_BaseMap", main);
                    material.SetColor("_BaseColor", tint);

                    // Vegetation cards need alpha clipping after conversion.
                    string n = material.name.ToLowerInvariant();
                    if (n.Contains("leaf") || n.Contains("leaves") || n.Contains("fern") ||
                        n.Contains("bush") || n.Contains("grass") || n.Contains("pine"))
                    {
                        material.SetFloat("_AlphaClip", 1f);
                        material.SetFloat("_Cutoff", 0.35f);
                        material.EnableKeyword("_ALPHATEST_ON");
                        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                    }

                    EditorUtility.SetDirty(material);
                    Debug.Log($"[DREGFALL] Repaired environment material '{material.name}' for URP ({prefabPath}).");
                }
            }
        }

        static bool HasBrokenMaterials(GameObject prefab)
        {
            Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
            {
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material == null || material.shader == null ||
                        material.shader.name == "Hidden/InternalErrorShader")
                        return true;
                }
            }
            return false;
        }
    }
}
#endif
