using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Dregfall
{
    // Dense streamed grass rendered in GPU-instanced variant batches.
    public sealed class DregfallGrassFieldRenderer : MonoBehaviour
    {
        const int BatchSize = 1023;

        sealed class GrassVariant
        {
            public Mesh mesh;
            public Material material;
        }

        sealed class GrassChunk
        {
            public readonly List<Matrix4x4>[] matrices;
            public Matrix4x4[][][] batches;
            public Vector3 center;

            public GrassChunk(int variants)
            {
                matrices = new List<Matrix4x4>[variants];
                for (int i = 0; i < variants; i++) matrices[i] = new List<Matrix4x4>();
            }

            public void BakeBatches()
            {
                batches = new Matrix4x4[matrices.Length][][];
                for (int v = 0; v < matrices.Length; v++)
                {
                    List<Matrix4x4> source = matrices[v];
                    int batchCount = Mathf.CeilToInt(source.Count / (float)BatchSize);
                    batches[v] = new Matrix4x4[batchCount][];
                    for (int b = 0; b < batchCount; b++)
                    {
                        int start = b * BatchSize;
                        int count = Mathf.Min(BatchSize, source.Count - start);
                        var batch = new Matrix4x4[count];
                        source.CopyTo(start, batch, 0, count);
                        batches[v][b] = batch;
                    }
                    source.Clear();
                }
            }
        }

        readonly Dictionary<Vector2Int, GrassChunk> chunks = new();
        readonly List<GrassVariant> variants = new();
        DregfallInfiniteWorld world;
        Transform player;
        int grassPerChunk;

        public void Initialize(DregfallInfiniteWorld owner, Transform target, GameObject[] grassPrefabs, int density)
        {
            world = owner;
            player = target;
            grassPerChunk = Mathf.Clamp(density, 500, 2200);
            variants.Clear();

            if (grassPrefabs == null) return;
            foreach (GameObject prefab in grassPrefabs)
            {
                if (prefab == null) continue;
                MeshFilter mf = prefab.GetComponentInChildren<MeshFilter>(true);
                Renderer mr = prefab.GetComponentInChildren<Renderer>(true);
                if (mf == null || mf.sharedMesh == null || mr == null || mr.sharedMaterial == null) continue;

                Material mat = new Material(mr.sharedMaterial) { name = "DREGFALL_Grass_" + prefab.name };
                mat.enableInstancing = true;

                // Keep grass blades crisp at the elevated gameplay camera angle.
                // Anisotropic filtering improves oblique texture detail without adding geometry.
                Texture mainTexture = mat.mainTexture;
                if (mainTexture != null)
                {
                    mainTexture.filterMode = FilterMode.Trilinear;
                    mainTexture.anisoLevel = 8;
                }

                variants.Add(new GrassVariant { mesh = mf.sharedMesh, material = mat });
            }
        }

        public void BuildChunk(Vector2Int coord)
        {
            if (variants.Count == 0 || chunks.ContainsKey(coord)) return;

            int seed = world.WorldSeed ^ (coord.x * 73856093) ^ (coord.y * 19349663) ^ 0x45D9F3B;
            var rng = new System.Random(seed);
            GrassChunk data = new GrassChunk(variants.Count);

            int carpetCount = Mathf.RoundToInt(grassPerChunk * 0.72f);
            for (int i = 0; i < carpetCount; i++)
            {
                float x = coord.x * world.ChunkSize + (float)rng.NextDouble() * world.ChunkSize;
                float z = coord.y * world.ChunkSize + (float)rng.NextDouble() * world.ChunkSize;
                float ecology = world.GetWildernessDensity(new Vector3(x, 0f, z));
                // Dense GPU grass also respects streamed waterways; banks remain readable.
                float waterMask = world.GetWaterMask(x, z);
                if (waterMask > 0.18f) continue;
                float patch = Mathf.PerlinNoise(x * 0.026f + 31.7f, z * 0.026f + 73.1f);
                float meadow = Mathf.PerlinNoise(x * 0.0045f + 119.3f, z * 0.0045f + 211.9f);
                float trail = Mathf.PerlinNoise(x * 0.012f + 317.2f, z * 0.012f + 89.4f);

                // Thick living carpet. Soil is exposed only in coherent clearing/trail pockets.
                float keep = Mathf.Clamp01(0.54f + ecology * 0.18f + patch * 0.20f + meadow * 0.14f);
                if (trail > 0.86f && meadow < 0.48f) keep *= 0.28f;
                if (patch < 0.22f) keep *= 0.35f;
                if ((float)rng.NextDouble() > keep) continue;

                float y = world.SampleGroundHeight(x, z);
                float hx = world.SampleGroundHeight(x + 0.75f, z);
                float hz = world.SampleGroundHeight(x, z + 0.75f);
                float slope = Mathf.Max(Mathf.Abs(hx - y), Mathf.Abs(hz - y)) / 0.75f;
                if (slope > 0.82f) continue;

                int variant = rng.Next(variants.Count);
                float heightNoise = Mathf.PerlinNoise(x * 0.035f + 9.2f, z * 0.035f + 17.8f);
                float height = Mathf.Lerp(0.62f, 1.18f,
                    Mathf.Clamp01(heightNoise * 0.72f + (float)rng.NextDouble() * 0.28f));
                float width = Mathf.Lerp(0.62f, 1.05f, (float)rng.NextDouble());
                Quaternion rotation = Quaternion.Euler(
                    Mathf.Lerp(-2.5f, 2.5f, (float)rng.NextDouble()),
                    (float)rng.NextDouble() * 360f,
                    Mathf.Lerp(-2.5f, 2.5f, (float)rng.NextDouble()));

                data.matrices[variant].Add(Matrix4x4.TRS(
                    new Vector3(x, y - 0.025f, z), rotation, new Vector3(width, height, width)));
            }

            data.center = new Vector3((coord.x + 0.5f) * world.ChunkSize, 0f, (coord.y + 0.5f) * world.ChunkSize);
            data.BakeBatches();
            chunks.Add(coord, data);
        }

        public void RemoveChunk(Vector2Int coord) => chunks.Remove(coord);

        void LateUpdate()
        {
            if (variants.Count == 0 || player == null) return;

            float renderDistance = world.ChunkSize * 1.05f;
            float renderDistanceSqr = renderDistance * renderDistance;
            Vector3 playerPos = player.position;

            foreach (GrassChunk chunk in chunks.Values)
            {
                float dx = chunk.center.x - playerPos.x;
                float dz = chunk.center.z - playerPos.z;
                if (dx * dx + dz * dz > renderDistanceSqr) continue;

                for (int v = 0; v < variants.Count; v++)
                {
                    GrassVariant variant = variants[v];
                    Matrix4x4[][] batches = chunk.batches[v];
                    for (int b = 0; b < batches.Length; b++)
                    {
                        Matrix4x4[] batch = batches[b];
#pragma warning disable 0618
                        Graphics.DrawMeshInstanced(variant.mesh, 0, variant.material, batch, batch.Length, null,
                            ShadowCastingMode.Off, false, 0, null, LightProbeUsage.Off, null);
#pragma warning restore 0618
                    }
                }
            }
        }

        void OnDestroy()
        {
            foreach (GrassVariant variant in variants)
                if (variant.material != null) Destroy(variant.material);
            variants.Clear();
        }
    }
}
