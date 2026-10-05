using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Dregfall
{
    public sealed class DregfallInfiniteWorld : MonoBehaviour
    {
        [Header("World")]
        [SerializeField] int worldSeed = 839174;
        [SerializeField] int chunkSize = 64;
        [SerializeField, Range(1, 5)] int viewRadius = 2;
        [SerializeField, Range(8, 64)] int verticesPerSide = 25;
        [SerializeField] float terrainHeight = 5f;
        [SerializeField] float noiseScale = 0.0065f;

        [Header("Geography")]
        [SerializeField] float continentalScale = 0.00065f;
        [SerializeField] float regionalScale = 0.0018f;
        [SerializeField] float localScale = 0.0075f;
        [SerializeField] float roughnessScale = 0.0032f;
        [SerializeField] float maxRegionalRelief = 18f;

        [Header("Streaming")]
        [SerializeField, Range(1, 8)] int chunksBuiltPerFrame = 2;

        [Header("Phase 2C Wilderness")]
        [SerializeField, Range(0, 160)] int maxTreesPerChunk = 85;
        [SerializeField, Range(400, 2600)] int denseGrassPerChunk = 1800;
        [SerializeField, Range(0, 160)] int interactiveGrassPerChunk = 70;
        [SerializeField, Range(0, 500)] int maxUndergrowthPerChunk = 280;
        [SerializeField, Range(0, 40)] int maxRocksPerChunk = 18;
        [SerializeField] float maxVegetationSlope = 0.72f;
        [SerializeField] float spawnClearingRadius = 11f;
        [SerializeField] float forestPatchScale = 0.0045f;
        [SerializeField] float clearingScale = 0.009f;

        DregfallEnvironmentCatalog environmentCatalog;
        DregfallGrassFieldRenderer grassField;
        Transform player;
        Transform chunkRoot;
        readonly Dictionary<Vector2Int, GameObject> loaded = new();
        readonly Queue<Vector2Int> buildQueue = new();
        readonly HashSet<Vector2Int> queued = new();
        Vector2Int lastPlayerChunk = new(int.MinValue, int.MinValue);

        public int WorldSeed => worldSeed;
        public int ChunkSize => chunkSize;

        public string GetChunkId(Vector2Int coord) => $"world-{worldSeed}:chunk-{coord.x}:{coord.y}";

        public Vector2Int GetChunkCoordinate(Vector3 worldPosition) => WorldToChunk(worldPosition);

        public string GetStableWorldId(Vector3 worldPosition)
        {
            Vector2Int coord = WorldToChunk(worldPosition);
            int localX = Mathf.FloorToInt(worldPosition.x - coord.x * chunkSize);
            int localZ = Mathf.FloorToInt(worldPosition.z - coord.y * chunkSize);
            return $"{GetChunkId(coord)}:cell-{localX}:{localZ}";
        }

        public void Initialize(Transform target)
        {
            player = target;
            DregfallInteractiveGrass.SetPlayer(target);
            chunkRoot = new GameObject("DREGFALL_StreamedWorld").transform;
            transform.SetParent(chunkRoot);
            environmentCatalog = Resources.Load<DregfallEnvironmentCatalog>("DREGFALL_EnvironmentCatalog");
            if (environmentCatalog == null)
                Debug.LogWarning("[DREGFALL] Environment catalog not ready yet. Unity will generate it automatically in the Editor.");
            else
            {
                grassField = gameObject.AddComponent<DregfallGrassFieldRenderer>();
                grassField.Initialize(this, target, environmentCatalog.grass, denseGrassPerChunk);
            }
            Debug.Log($"[DREGFALL] Unlimited world initialized. Seed: {worldSeed}");
            Refresh(true);
            StartCoroutine(BuildQueuedChunks());
        }

        void Update()
        {
            if (player == null) return;
            Vector2Int now = WorldToChunk(player.position);
            if (now != lastPlayerChunk) Refresh(false);
        }

        void Refresh(bool immediateCenter)
        {
            Vector2Int center = WorldToChunk(player.position);
            lastPlayerChunk = center;

            var wanted = new HashSet<Vector2Int>();
            for (int z = -viewRadius; z <= viewRadius; z++)
            for (int x = -viewRadius; x <= viewRadius; x++)
            {
                Vector2Int coord = center + new Vector2Int(x, z);
                wanted.Add(coord);
                if (!loaded.ContainsKey(coord) && queued.Add(coord))
                {
                    if (immediateCenter && coord == center) BuildChunk(coord);
                    else buildQueue.Enqueue(coord);
                }
            }

            var remove = new List<Vector2Int>();
            foreach (var pair in loaded)
                if (!wanted.Contains(pair.Key)) remove.Add(pair.Key);

            foreach (Vector2Int coord in remove)
            {
                GameObject oldChunk = loaded[coord];
                loaded.Remove(coord);
                if (grassField != null) grassField.RemoveChunk(coord);
                Destroy(oldChunk);
            }
        }

        IEnumerator BuildQueuedChunks()
        {
            while (true)
            {
                int budget = chunksBuiltPerFrame;
                while (budget-- > 0 && buildQueue.Count > 0)
                {
                    Vector2Int coord = buildQueue.Dequeue();
                    queued.Remove(coord);
                    Vector2Int center = WorldToChunk(player.position);
                    if (Mathf.Abs(coord.x - center.x) <= viewRadius &&
                        Mathf.Abs(coord.y - center.y) <= viewRadius &&
                        !loaded.ContainsKey(coord))
                        BuildChunk(coord);
                }
                yield return null;
            }
        }

        void BuildChunk(Vector2Int coord)
        {
            if (loaded.ContainsKey(coord)) return;

            GameObject go = new GameObject($"Chunk_{coord.x}_{coord.y}__{GetChunkId(coord)}");
            go.transform.SetParent(chunkRoot, false);
            go.transform.position = new Vector3(coord.x * chunkSize, 0f, coord.y * chunkSize);

            Mesh mesh = GenerateMesh(coord);
            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = GetGroundMaterial();
            var collider = go.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;

            loaded.Add(coord, go);
            if (grassField != null) grassField.BuildChunk(coord);
            PopulateWilderness(coord, go.transform);
        }

        void PopulateWilderness(Vector2Int coord, Transform chunk)
        {
            if (environmentCatalog == null)
            {
                Debug.LogError("[DREGFALL] Phase 2C cannot populate wilderness: environment catalog is missing.");
                return;
            }

            int seed = HashSeed(worldSeed, coord.x * 73856093 ^ coord.y * 19349663);
            var rng = new System.Random(seed);

            // Do not decide the whole chunk from one sample. Each candidate reads the continuous
            // world ecology maps so forests and clearings flow naturally across chunk boundaries.
            SpawnEcologicalCategory(environmentCatalog.trees, maxTreesPerChunk, coord, chunk, rng, 0, 0.78f, 1.24f);
            SpawnEcologicalCategory(environmentCatalog.grass, interactiveGrassPerChunk, coord, chunk, rng, 3, 1.35f, 2.15f);
            SpawnEcologicalCategory(environmentCatalog.undergrowth, maxUndergrowthPerChunk, coord, chunk, rng, 1, 0.55f, 1.35f);
            SpawnEcologicalCategory(environmentCatalog.rocks, maxRocksPerChunk, coord, chunk, rng, 2, 0.55f, 1.55f);
        }

        void SpawnEcologicalCategory(GameObject[] prefabs, int targetCount, Vector2Int coord, Transform parent,
            System.Random rng, int category, float minScale, float maxScale)
        {
            if (prefabs == null || prefabs.Length == 0 || targetCount <= 0) return;

            float sx = HashSeed(worldSeed, 17) * 0.001f;
            float sz = HashSeed(worldSeed, 53) * 0.001f;
            int spawned = 0;
            int attempts = Mathf.Max(targetCount * 7, 64);

            for (int attempt = 0; attempt < attempts && spawned < targetCount; attempt++)
            {
                float worldX = coord.x * chunkSize + 1f + (float)rng.NextDouble() * (chunkSize - 2f);
                float worldZ = coord.y * chunkSize + 1f + (float)rng.NextDouble() * (chunkSize - 2f);
                if (category != 3 && new Vector2(worldX, worldZ).sqrMagnitude < spawnClearingRadius * spawnClearingRadius) continue;

                float broad = GetWildernessDensity(new Vector3(worldX, 0f, worldZ));
                float forest = Mathf.PerlinNoise(worldX * forestPatchScale + sx * 1.71f,
                                                 worldZ * forestPatchScale + sz * 1.71f);
                float local = Mathf.PerlinNoise(worldX * clearingScale + sx * 3.17f,
                                                worldZ * clearingScale + sz * 3.17f);
                float glade = Mathf.PerlinNoise(worldX * 0.0038f + sx * 5.3f,
                                                worldZ * 0.0038f + sz * 5.3f);

                float chance;

                if (category == 0)
                {
                    // Big and small forests: dense cores, feathered edges, occasional open glades.
                    chance = Mathf.Clamp01(0.16f + broad * 0.38f + forest * 0.58f);
                    if (glade > 0.72f) chance *= 0.20f;
                }
                else if (category == 1)
                {
                    // Forest floor should stay visually busy even around clearings and tree lines.
                    chance = Mathf.Clamp01(0.48f + broad * 0.20f + forest * 0.22f + local * 0.18f);
                    if (glade > 0.78f) chance *= 0.65f;
                }
                else if (category == 3)
                {
                    // Grass is the living carpet. Keep it thick through forests and especially clearings,
                    // but preserve occasional soil pockets so the terrain still has natural variation.
                    chance = Mathf.Clamp01(0.72f + broad * 0.12f + local * 0.14f);
                    if (forest > 0.82f) chance *= 0.88f;
                    if (glade > 0.72f) chance = Mathf.Min(1f, chance + 0.08f);
                    float soilPocket = Mathf.PerlinNoise(worldX * 0.018f + sx * 7.1f,
                                                         worldZ * 0.018f + sz * 7.1f);
                    if (soilPocket > 0.84f) chance *= 0.18f;
                }
                else
                {
                    // Rocks occur throughout the landscape, with modest clustering.
                    chance = Mathf.Clamp01(0.30f + (1f - broad) * 0.20f + local * 0.22f);
                }

                if ((float)rng.NextDouble() > chance) continue;

                float y = SampleHeight(worldX, worldZ, sx, sz);
                float hx = SampleHeight(worldX + 1.25f, worldZ, sx, sz);
                float hz = SampleHeight(worldX, worldZ + 1.25f, sx, sz);
                float slope = Mathf.Max(Mathf.Abs(hx - y), Mathf.Abs(hz - y)) / 1.25f;
                float allowedSlope = category == 0 ? maxVegetationSlope : category == 3 ? maxVegetationSlope * 1.05f : maxVegetationSlope * 1.25f;
                if (slope > allowedSlope) continue;

                GameObject prefab = PickRuntimeSafePrefab(prefabs, rng);
                if (prefab == null) continue;

                GameObject instance = Instantiate(prefab, parent);
                string prefix = category == 0 ? "Tree" : category == 1 ? "GroundPlant" : category == 3 ? "Grass" : "Rock";
                instance.name = $"Wild_{prefix}_{prefab.name}_{spawned}";
                instance.transform.position = new Vector3(worldX, y, worldZ);
                instance.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                float scale = Mathf.Lerp(minScale, maxScale, (float)rng.NextDouble());
                instance.transform.localScale *= scale;

                if (category == 3)
                {
                    // Only the small nearby physical grass set receives interaction components.
                    if (instance.GetComponent<DregfallInteractiveGrass>() == null)
                        instance.AddComponent<DregfallInteractiveGrass>();
                }

                if (category == 0)
                {
                    DregfallInteractable interactable = instance.GetComponent<DregfallInteractable>();
                    if (interactable == null) interactable = instance.AddComponent<DregfallInteractable>();
                    interactable.Configure("Tree", "A mature tree. It can be harvested with the right tool.", true);
                }

                spawned++;
            }
        }

        GameObject PickRuntimeSafePrefab(GameObject[] prefabs, System.Random rng)
        {
            if (prefabs == null || prefabs.Length == 0) return null;

            int start = rng.Next(prefabs.Length);
            for (int offset = 0; offset < prefabs.Length; offset++)
            {
                GameObject candidate = prefabs[(start + offset) % prefabs.Length];
                if (candidate != null && IsRuntimeRenderable(candidate))
                    return candidate;
            }
            return null;
        }

        static bool IsRuntimeRenderable(GameObject prefab)
        {
            Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0) return false;

            bool hasRenderableMaterial = false;
            foreach (Renderer renderer in renderers)
            {
                Material[] materials = renderer.sharedMaterials;
                if (materials == null || materials.Length == 0) continue;

                foreach (Material material in materials)
                {
                    if (material == null || material.shader == null || !material.shader.isSupported)
                        return false;

                    string shaderName = material.shader.name;
                    if (shaderName == "Hidden/InternalErrorShader" ||
                        shaderName.StartsWith("HDRP/", System.StringComparison.OrdinalIgnoreCase) ||
                        shaderName.Contains("High Definition", System.StringComparison.OrdinalIgnoreCase))
                        return false;

                    hasRenderableMaterial = true;
                }
            }
            return hasRenderableMaterial;
        }

        Mesh GenerateMesh(Vector2Int coord)
        {
            int resolution = Mathf.Max(2, verticesPerSide);
            int count = resolution * resolution;
            var vertices = new Vector3[count];
            var uvs = new Vector2[count];
            var triangles = new int[(resolution - 1) * (resolution - 1) * 6];
            float step = (float)chunkSize / (resolution - 1);

            float seedX = HashSeed(worldSeed, 17) * 0.001f;
            float seedZ = HashSeed(worldSeed, 53) * 0.001f;

            for (int z = 0; z < resolution; z++)
            for (int x = 0; x < resolution; x++)
            {
                float localX = x * step;
                float localZ = z * step;
                float worldX = coord.x * chunkSize + localX;
                float worldZ = coord.y * chunkSize + localZ;
                float h = SampleHeight(worldX, worldZ, seedX, seedZ);
                int i = z * resolution + x;
                vertices[i] = new Vector3(localX, h, localZ);
                uvs[i] = new Vector2(worldX / chunkSize, worldZ / chunkSize);
            }

            int t = 0;
            for (int z = 0; z < resolution - 1; z++)
            for (int x = 0; x < resolution - 1; x++)
            {
                int i = z * resolution + x;
                triangles[t++] = i;
                triangles[t++] = i + resolution;
                triangles[t++] = i + 1;
                triangles[t++] = i + 1;
                triangles[t++] = i + resolution;
                triangles[t++] = i + resolution + 1;
            }

            var mesh = new Mesh { name = $"DREGFALL_Terrain_{coord.x}_{coord.y}" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = uvs;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        public float SampleGroundHeight(float x, float z)
        {
            float sx = HashSeed(worldSeed, 17) * 0.001f;
            float sz = HashSeed(worldSeed, 53) * 0.001f;
            return SampleHeight(x, z, sx, sz);
        }

        float SampleHeight(float x, float z, float sx, float sz)
        {
            // Very large landforms keep the world from looking like repeated noise.
            float continental = Mathf.PerlinNoise(x * continentalScale + sx * 0.31f, z * continentalScale + sz * 0.31f);
            float regional = Mathf.PerlinNoise(x * regionalScale + sx * 0.73f, z * regionalScale + sz * 0.73f);
            float roughness = Mathf.PerlinNoise(x * roughnessScale + sx * 1.19f, z * roughnessScale + sz * 1.19f);

            // Some broad regions stay naturally flatter for future roads, farms and settlements,
            // while wilderness regions can become rougher and more elevated.
            float settlementSuitability = Mathf.SmoothStep(0.28f, 0.72f,
                Mathf.PerlinNoise(x * 0.00115f + sx * 2.07f, z * 0.00115f + sz * 2.07f));
            float flatness = 1f - Mathf.Abs(settlementSuitability * 2f - 1f);
            flatness = Mathf.SmoothStep(0.35f, 0.85f, flatness);

            float regionalShape = ((continental - 0.5f) * 0.65f + (regional - 0.5f) * 0.35f) * maxRegionalRelief;
            regionalShape *= Mathf.Lerp(1f, 0.42f, flatness);

            float local = Mathf.PerlinNoise(x * localScale + sx, z * localScale + sz) - 0.5f;
            float detail = Mathf.PerlinNoise(x * noiseScale * 2.35f + sx * 1.7f, z * noiseScale * 2.35f + sz * 1.7f) - 0.5f;
            float localAmplitude = Mathf.Lerp(terrainHeight * 1.45f, terrainHeight * 0.32f, flatness);
            localAmplitude *= Mathf.Lerp(0.75f, 1.25f, roughness);

            return regionalShape + local * localAmplitude + detail * terrainHeight * 0.18f;
        }

        public float GetSettlementSuitability(Vector3 worldPosition)
        {
            float sx = HashSeed(worldSeed, 17) * 0.001f;
            float sz = HashSeed(worldSeed, 53) * 0.001f;
            float n = Mathf.PerlinNoise(worldPosition.x * 0.00115f + sx * 2.07f,
                                        worldPosition.z * 0.00115f + sz * 2.07f);
            return 1f - Mathf.Abs(Mathf.SmoothStep(0.28f, 0.72f, n) * 2f - 1f);
        }

        public float GetWildernessDensity(Vector3 worldPosition)
        {
            float sx = HashSeed(worldSeed, 89) * 0.001f;
            float sz = HashSeed(worldSeed, 131) * 0.001f;
            return Mathf.PerlinNoise(worldPosition.x * 0.0009f + sx,
                                     worldPosition.z * 0.0009f + sz);
        }

        static int HashSeed(int seed, int salt)
        {
            unchecked
            {
                int h = seed ^ salt;
                h = (h * 397) ^ (h >> 16);
                return h & 0x7fffffff;
            }
        }

        Vector2Int WorldToChunk(Vector3 position)
        {
            return new Vector2Int(
                Mathf.FloorToInt(position.x / chunkSize),
                Mathf.FloorToInt(position.z / chunkSize));
        }

        static Material groundMaterial;
        static Material GetGroundMaterial()
        {
            if (groundMaterial != null) return groundMaterial;

            Material source = Resources.Load<Material>("DREGFALL_GroundMaterial");
            if (source != null)
            {
                groundMaterial = source;
                return groundMaterial;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            groundMaterial = new Material(shader) { name = "DREGFALL_GroundFallback" };
            groundMaterial.color = new Color(0.18f, 0.22f, 0.16f);
            groundMaterial.SetFloat("_Smoothness", 0.08f);
            return groundMaterial;
        }
    }
}
