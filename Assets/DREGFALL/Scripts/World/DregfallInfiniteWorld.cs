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
            chunkRoot = new GameObject("DREGFALL_StreamedWorld").transform;
            transform.SetParent(chunkRoot);
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
            renderer.sharedMaterial = GetPlainMaterial();
            var collider = go.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;

            loaded.Add(coord, go);
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

        static Material plainMaterial;
        static Material GetPlainMaterial()
        {
            if (plainMaterial != null) return plainMaterial;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            plainMaterial = new Material(shader) { name = "DREGFALL_Phase2_PlainTerrain" };
            plainMaterial.color = new Color(0.20f, 0.23f, 0.19f);
            return plainMaterial;
        }
    }
}
