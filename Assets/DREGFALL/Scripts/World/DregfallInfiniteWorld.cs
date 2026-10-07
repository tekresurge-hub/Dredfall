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
        [SerializeField, Range(8, 64)] int verticesPerSide = 33;
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
        [SerializeField, Range(0, 160)] int maxTreesPerChunk = 44;
        [SerializeField, Range(350, 2200)] int denseGrassPerChunk = 1080;
        [SerializeField, Range(0, 300)] int interactiveGrassPerChunk = 18;
        [SerializeField, Range(0, 650)] int maxUndergrowthPerChunk = 118;
        [SerializeField, Range(0, 40)] int maxRocksPerChunk = 2;
        [SerializeField] float maxVegetationSlope = 0.72f;
        [SerializeField] float spawnClearingRadius = 11f;
        [SerializeField] float forestPatchScale = 0.0045f;
        [SerializeField] float clearingScale = 0.009f;

        [Header("Phase 2D Waterways")]
        [SerializeField, Range(0.2f, 2f)] float creekDepth = 0.45f;
        [SerializeField, Range(0.2f, 2f)] float creekHalfWidth = 0.65f;
        [SerializeField, Range(0f, 0.15f)] float pondFrequency = 0.035f;

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
            // Break the obvious repeated "brown carpet" look without adding extra GameObjects.
            // Each deterministic chunk gets a subtle material tint while the texture remains shared.
            var groundBlock = new MaterialPropertyBlock();
            float groundVariation = Mathf.PerlinNoise(coord.x * 0.173f + 41.7f, coord.y * 0.173f + 93.1f);
            float dampVariation = Mathf.PerlinNoise(coord.x * 0.071f + 121.3f, coord.y * 0.071f + 17.9f);
            Color drySoil = new Color(0.235f, 0.215f, 0.165f, 1f);
            Color mossSoil = new Color(0.145f, 0.175f, 0.105f, 1f);
            Color groundTint = Color.Lerp(drySoil, mossSoil, Mathf.Clamp01(groundVariation * 0.72f + dampVariation * 0.28f));
            groundBlock.SetColor("_BaseColor", groundTint);
            groundBlock.SetColor("_Color", groundTint);
            renderer.SetPropertyBlock(groundBlock);
            var collider = go.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;

            loaded.Add(coord, go);
            if (grassField != null) grassField.BuildChunk(coord);
            // Phase 2D water rendering temporarily disabled: previous surface mesh could cover streamed terrain.
            // Water mask/bed data stays deterministic so localized streams can be rebuilt safely.
            BuildWaterSurface(coord, go.transform);
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
            // Forests are regional: dense woodland pockets separated by genuine clearings.
            // This looks natural without paying the cost of uniformly filling every chunk.
            Vector3 chunkCenter = new Vector3((coord.x + 0.5f) * chunkSize, 0f, (coord.y + 0.5f) * chunkSize);
            float forestRegion = GetForestRegionDensity(chunkCenter);
            int treeBudget = Mathf.RoundToInt(maxTreesPerChunk * Mathf.Lerp(0.18f, 1f, forestRegion));
            int plantBudget = Mathf.RoundToInt(maxUndergrowthPerChunk * Mathf.Lerp(0.28f, 1f, forestRegion));
            SpawnEcologicalCategory(environmentCatalog.trees, treeBudget, coord, chunk, rng, 0, 0.92f, 1.18f);
            SpawnEcologicalCategory(environmentCatalog.grass, interactiveGrassPerChunk, coord, chunk, rng, 3, 0.88f, 1.08f);
            SpawnEcologicalCategory(environmentCatalog.undergrowth, plantBudget, coord, chunk, rng, 1, 0.68f, 1.16f);
            SpawnEcologicalCategory(environmentCatalog.rocks, maxRocksPerChunk, coord, chunk, rng, 2, 0.48f, 0.78f);
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
                // Phase 2D water rendering is temporarily disabled. Do not let its provisional
                // water mask erase the certified wilderness population while waterways are rebuilt.

                float broad = GetWildernessDensity(new Vector3(worldX, 0f, worldZ));
                float forest = Mathf.PerlinNoise(worldX * forestPatchScale + sx * 1.71f,
                                                 worldZ * forestPatchScale + sz * 1.71f);
                float local = Mathf.PerlinNoise(worldX * clearingScale + sx * 3.17f,
                                                worldZ * clearingScale + sz * 3.17f);
                float glade = Mathf.PerlinNoise(worldX * 0.0038f + sx * 5.3f,
                                                worldZ * 0.0038f + sz * 5.3f);

                float chance;

                // Coherent biome mask. Forest cores become recognisably dense, edges feather out,
                // and broad glades remain readable instead of every chunk looking equally scattered.
                float forestCore = Mathf.SmoothStep(0.48f, 0.76f, forest);
                float deepForest = Mathf.SmoothStep(0.63f, 0.88f, forest) * Mathf.Lerp(0.55f, 1f, broad);
                float clearing = Mathf.SmoothStep(0.68f, 0.86f, glade);
                float edge = 1f - Mathf.Abs(forestCore * 2f - 1f);

                if (category == 0)
                {
                    chance = Mathf.Lerp(0.08f, 0.94f, forestCore);
                    chance *= Mathf.Lerp(0.38f, 1f, broad);
                    chance *= Mathf.Lerp(1f, 0.10f, clearing);
                }
                else if (category == 1)
                {
                    // Bushes/ground plants favour forest edges and pockets beneath trees.
                    chance = Mathf.Clamp01(0.34f + forestCore * 0.28f + edge * 0.28f + local * 0.16f);
                    chance *= Mathf.Lerp(1f, 0.58f, clearing);
                }
                else if (category == 3)
                {
                    // Interactive grass is only the small physical subset; dense visual grass is GPU rendered.
                    chance = Mathf.Clamp01(0.70f + local * 0.16f + clearing * 0.10f - deepForest * 0.10f);
                    float soilPocket = Mathf.PerlinNoise(worldX * 0.018f + sx * 7.1f,
                                                         worldZ * 0.018f + sz * 7.1f);
                    if (soilPocket > 0.86f) chance *= 0.24f;
                }
                else
                {
                    // Rocks form occasional coherent groups, especially outside the deepest forest.
                    float rockCluster = Mathf.PerlinNoise(worldX * 0.0068f + sx * 9.7f,
                                                          worldZ * 0.0068f + sz * 9.7f);
                    chance = Mathf.Clamp01(0.12f + rockCluster * 0.42f + (1f - forestCore) * 0.14f);
                    if (rockCluster < 0.43f) chance *= 0.22f;
                }

                if ((float)rng.NextDouble() > chance) continue;

                float y = SampleHeight(worldX, worldZ, sx, sz);
                float hx = SampleHeight(worldX + 1.25f, worldZ, sx, sz);
                float hz = SampleHeight(worldX, worldZ + 1.25f, sx, sz);
                float slope = Mathf.Max(Mathf.Abs(hx - y), Mathf.Abs(hz - y)) / 1.25f;
                float allowedSlope = category == 0 ? maxVegetationSlope : category == 3 ? maxVegetationSlope * 1.05f : maxVegetationSlope * 1.25f;
                if (slope > allowedSlope) continue;

                GameObject prefab = PickRuntimeSafePrefab(prefabs, rng, category);
                if (prefab == null) continue;

                GameObject instance = Instantiate(prefab, parent);
                string prefix = category == 0 ? "Tree" : category == 1 ? "GroundPlant" : category == 3 ? "Grass" : "Rock";
                instance.name = $"Wild_{prefix}_{prefab.name}_{spawned}";
                float yaw = (float)rng.NextDouble() * 360f;
                float pitch = category == 2 ? Mathf.Lerp(-7f, 7f, (float)rng.NextDouble()) : 0f;
                float roll = category == 2 ? Mathf.Lerp(-7f, 7f, (float)rng.NextDouble()) : 0f;
                float groundSink = category == 2 ? Mathf.Lerp(0.12f, 0.34f, (float)rng.NextDouble()) : 0f;
                instance.transform.position = new Vector3(worldX, y - groundSink, worldZ);
                instance.transform.rotation = Quaternion.Euler(pitch, yaw, roll);
                float scaleNoise = Mathf.PerlinNoise(worldX * 0.021f + category * 13.7f, worldZ * 0.021f + category * 29.1f);
                float scale = Mathf.Lerp(minScale, maxScale, Mathf.Clamp01(scaleNoise * 0.62f + (float)rng.NextDouble() * 0.38f));
                instance.transform.localScale *= scale;

                // Runtime vegetation budget: preserve detailed assets near the survivor,
                // but do not pay full shadow/detail cost for every prefab in all 25 loaded chunks.
                Renderer[] instanceRenderers = instance.GetComponentsInChildren<Renderer>(true);
                foreach (Renderer r in instanceRenderers)
                {
                    if (r == null) continue;
                    r.allowOcclusionWhenDynamic = true;

                    if (category == 0)
                    {
                        // Trees keep silhouettes/shadows, but cull before the outer streamed world edge.
                        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                        r.receiveShadows = true;
                    }
                    else
                    {
                        // Tiny foliage/rocks don't need expensive individual real-time shadows.
                        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        r.receiveShadows = true;
                    }
                }

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

        GameObject PickRuntimeSafePrefab(GameObject[] prefabs, System.Random rng, int category)
        {
            if (prefabs == null || prefabs.Length == 0) return null;

            int start = rng.Next(prefabs.Length);
            for (int offset = 0; offset < prefabs.Length; offset++)
            {
                GameObject candidate = prefabs[(start + offset) % prefabs.Length];
                if (candidate != null && IsRuntimeRenderable(candidate, category))
                    return candidate;
            }
            return null;
        }

        static bool IsRuntimeRenderable(GameObject prefab, int category)
        {
            Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0) return false;

            // The catalog can contain demo/placeholder objects that technically render but look
            // terrible in the survival world. Trees especially must have a real mesh hierarchy,
            // useful vertical silhouette and sane materials before they are allowed to spawn.
            if (category == 0)
            {
                string lowerName = prefab.name.ToLowerInvariant();
                if (lowerName.Contains("demo") || lowerName.Contains("preview") ||
                    lowerName.Contains("sample") || lowerName.Contains("lod0") ||
                    lowerName.Contains("stump") || lowerName.Contains("bush") ||
                    lowerName.Contains("shrub") || lowerName.Contains("hedge"))
                    return false;

                MeshFilter[] treeMeshes = prefab.GetComponentsInChildren<MeshFilter>(true);
                if (treeMeshes == null || treeMeshes.Length == 0) return false;

                Bounds combined = new Bounds();
                bool boundsReady = false;
                foreach (MeshFilter mf in treeMeshes)
                {
                    if (mf == null || mf.sharedMesh == null) continue;
                    Bounds b = mf.sharedMesh.bounds;
                    Vector3 size = Vector3.Scale(b.size, mf.transform.lossyScale);
                    Bounds scaled = new Bounds(mf.transform.position, new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z)));
                    if (!boundsReady) { combined = scaled; boundsReady = true; }
                    else combined.Encapsulate(scaled);
                }
                if (!boundsReady) return false;
                float horizontal = Mathf.Max(0.01f, Mathf.Max(combined.size.x, combined.size.z));
                if (combined.size.y < horizontal * 0.72f) return false;
            }

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

                    if (category == 0)
                    {
                        Color tint = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : material.color;
                        float peak = Mathf.Max(tint.r, Mathf.Max(tint.g, tint.b));
                        if (peak < 0.12f) return false;
                    }

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

        public float GetWaterMask(float x, float z)
        {
            float sx = HashSeed(worldSeed, 211) * 0.001f;
            float sz = HashSeed(worldSeed, 307) * 0.001f;

            // Sparse creek bands: compare Z to a smoothly warped centerline repeated only every ~420m.
            // This produces narrow continuous streams instead of flooding whole chunks.
            float bandPeriod = 420f;
            float meander = (Mathf.PerlinNoise(x * 0.0022f + sx, z * 0.00045f + sz) - 0.5f) * 105f;
            float band = Mathf.Repeat(z + meander + worldSeed * 0.071f, bandPeriod);
            float distanceToCenter = Mathf.Min(band, bandPeriod - band);
            float width = Mathf.Lerp(1.4f, 3.0f,
                Mathf.PerlinNoise(x * 0.0014f + sx * 2.1f, z * 0.0014f + sz * 2.1f));
            float creek = 1f - Mathf.SmoothStep(width, width + 2.2f, distanceToCenter);

            // Gate some creek stretches out so waterways are uncommon rather than everywhere.
            float gate = Mathf.PerlinNoise(x * 0.00042f + sx * 1.7f, z * 0.00042f + sz * 1.7f);
            creek *= Mathf.SmoothStep(0.46f, 0.57f, gate);

            // Rare compact ponds.
            float pondNoise = Mathf.PerlinNoise(x * 0.0032f + sx * 3.1f, z * 0.0032f + sz * 3.1f);
            float pond = Mathf.SmoothStep(0.885f, 0.925f, pondNoise);
            return Mathf.Clamp01(Mathf.Max(creek, pond));
        }

        public bool IsWater(float x, float z) => GetWaterMask(x, z) > 0.52f;

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

            float baseHeight = regionalShape + local * localAmplitude + detail * terrainHeight * 0.18f;
            // Phase 2D waterways are temporarily isolated from terrain generation.
            // Preserve the working Phase 2B/2C terrain until the localized stream system is rebuilt.
            return baseHeight;
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

        public float GetForestRegionDensity(Vector3 worldPosition)
        {
            float sx = HashSeed(worldSeed, 433) * 0.001f;
            float sz = HashSeed(worldSeed, 577) * 0.001f;
            float broad = Mathf.PerlinNoise(worldPosition.x * 0.00145f + sx,
                                            worldPosition.z * 0.00145f + sz);
            float pockets = Mathf.PerlinNoise(worldPosition.x * 0.0042f + sx * 1.9f,
                                              worldPosition.z * 0.0042f + sz * 1.9f);
            float density = broad * 0.78f + pockets * 0.22f;
            return Mathf.SmoothStep(0.34f, 0.72f, density);
        }


        void BuildWaterSurface(Vector2Int coord, Transform parent)
        {
            // Scarce survival water: most chunks intentionally contain no surface water.
            // A deterministic regional gate means players can travel a long way while staying dry.
            int waterSeed = HashSeed(worldSeed, coord.x * 92821 ^ coord.y * 68917 ^ 0x2D71);
            var rng = new System.Random(waterSeed);
            float regionX = (coord.x * chunkSize + chunkSize * 0.5f) * 0.00085f + 91.3f;
            float regionZ = (coord.y * chunkSize + chunkSize * 0.5f) * 0.00085f + 47.9f;
            float wetRegion = Mathf.PerlinNoise(regionX, regionZ);

            // Only unusually wet regions can create water, and even there only a few chunks do.
            if (wetRegion < 0.60f || rng.NextDouble() > pondFrequency) return;

            float worldX = coord.x * chunkSize + Mathf.Lerp(10f, chunkSize - 10f, (float)rng.NextDouble());
            float worldZ = coord.y * chunkSize + Mathf.Lerp(10f, chunkSize - 10f, (float)rng.NextDouble());

            // Tiny muddy puddle / seep. This is deliberately not an easy, river-sized water supply.
            float radiusX = Mathf.Lerp(0.85f, 1.8f, (float)rng.NextDouble());
            float radiusZ = Mathf.Lerp(0.65f, 1.35f, (float)rng.NextDouble());
            const int ringSegments = 18;
            var vertices = new List<Vector3>(ringSegments + 1);
            var uvs = new List<Vector2>(ringSegments + 1);
            var triangles = new List<int>(ringSegments * 3);

            float centerY = SampleGroundHeight(worldX, worldZ) + 0.035f;
            vertices.Add(new Vector3(worldX - coord.x * chunkSize, centerY, worldZ - coord.y * chunkSize));
            uvs.Add(new Vector2(0.5f, 0.5f));

            for (int i = 0; i < ringSegments; i++)
            {
                float angle = i / (float)ringSegments * Mathf.PI * 2f;
                float irregular = Mathf.Lerp(0.82f, 1.12f,
                    Mathf.PerlinNoise(worldX * 0.11f + i * 0.37f, worldZ * 0.11f + i * 0.19f));
                float px = worldX + Mathf.Cos(angle) * radiusX * irregular;
                float pz = worldZ + Mathf.Sin(angle) * radiusZ * irregular;
                float py = SampleGroundHeight(px, pz) + 0.045f;
                vertices.Add(new Vector3(px - coord.x * chunkSize, py, pz - coord.y * chunkSize));
                uvs.Add(new Vector2(0.5f + Mathf.Cos(angle) * 0.5f, 0.5f + Mathf.Sin(angle) * 0.5f));
            }

            for (int i = 0; i < ringSegments; i++)
            {
                int a = i + 1;
                int b = ((i + 1) % ringSegments) + 1;
                // Upward-facing winding for the elevated camera.
                triangles.Add(0); triangles.Add(a); triangles.Add(b);
            }

            GameObject water = new GameObject("DREGFALL_RareWater_Puddle");
            water.transform.SetParent(parent, false);
            Mesh mesh = new Mesh { name = $"DREGFALL_RarePuddle_{coord.x}_{coord.y}" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            water.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = water.AddComponent<MeshRenderer>();
            mr.sharedMaterial = GetWaterMaterial();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = true;
        }

        static Material waterMaterial;
        static Material GetWaterMaterial()
        {
            if (waterMaterial != null) return waterMaterial;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            waterMaterial = new Material(shader) { name = "DREGFALL_Water" };
            waterMaterial.color = new Color(0.055f, 0.105f, 0.09f, 1f);
            waterMaterial.SetFloat("_Smoothness", 0.94f);
            waterMaterial.SetFloat("_Metallic", 0f);
            return waterMaterial;
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
                groundMaterial = new Material(source) { name = "DREGFALL_GroundRuntime" };
                groundMaterial.enableInstancing = true;
                groundMaterial.SetFloat("_Smoothness", 0.015f);
                // Smaller texture scale removes the stretched/muddy appearance from the elevated camera.
                groundMaterial.mainTextureScale = new Vector2(11f, 11f);
                if (groundMaterial.mainTexture != null)
                {
                    groundMaterial.mainTexture.filterMode = FilterMode.Trilinear;
                    groundMaterial.mainTexture.anisoLevel = 16;
                }
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
