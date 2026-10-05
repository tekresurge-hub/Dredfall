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
            public GrassChunk(int variants)
            {
                matrices = new List<Matrix4x4>[variants];
                for (int i = 0; i < variants; i++) matrices[i] = new List<Matrix4x4>();
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
            grassPerChunk = Mathf.Clamp(density, 900, 5200);
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
                variants.Add(new GrassVariant { mesh = mf.sharedMesh, material = mat });
            }
        }

        public void BuildChunk(Vector2Int coord)
        {
            if (variants.Count == 0 || chunks.ContainsKey(coord)) return;

            int seed = world.WorldSeed ^ (coord.x * 73856093) ^ (coord.y * 19349663) ^ 0x45D9F3B;
            var rng = new System.Random(seed);
            GrassChunk data = new GrassChunk(variants.Count);

            int carpetCount = Mathf.RoundToInt(grassPerChunk * 1.75f);\n            for (int i = 0; i < carpetCount; i++)
            {
                float x = coord.x * world.ChunkSize + (float)rng.NextDouble() * world.ChunkSize;
                float z = coord.y * world.ChunkSize + (float)rng.NextDouble() * world.ChunkSize;
                float ecology = world.GetWildernessDensity(new Vector3(x, 0f, z));
                float patch = Mathf.PerlinNoise(x * 0.021f + 31.7f, z * 0.021f + 73.1f);
                float barePatch = Mathf.PerlinNoise(x * 0.008f + 119.3f, z * 0.008f + 211.9f);

                float keep = Mathf.Clamp01(0.86f + ecology * 0.12f + patch * 0.08f);
                if (barePatch > 0.86f) keep *= 0.38f;
                if ((float)rng.NextDouble() > keep) continue;

                float y = world.SampleGroundHeight(x, z);
                float hx = world.SampleGroundHeight(x + 0.75f, z);
                float hz = world.SampleGroundHeight(x, z + 0.75f);
                float slope = Mathf.Max(Mathf.Abs(hx - y), Mathf.Abs(hz - y)) / 0.75f;
                if (slope > 0.82f) continue;

                int variant = rng.Next(variants.Count);
                float height = Mathf.Lerp(1.45f, 2.55f, (float)rng.NextDouble());
                float width = height * Mathf.Lerp(0.72f, 1.18f, (float)rng.NextDouble());
                Quaternion rotation = Quaternion.Euler(
                    Mathf.Lerp(-2.5f, 2.5f, (float)rng.NextDouble()),
                    (float)rng.NextDouble() * 360f,
                    Mathf.Lerp(-2.5f, 2.5f, (float)rng.NextDouble()));

                data.matrices[variant].Add(Matrix4x4.TRS(
                    new Vector3(x, y - 0.025f, z), rotation, new Vector3(width, height, width)));
            }

            chunks.Add(coord, data);
        }

        public void RemoveChunk(Vector2Int coord) => chunks.Remove(coord);

        void LateUpdate()
        {
            if (variants.Count == 0 || player == null) return;

            foreach (GrassChunk chunk in chunks.Values)
            {
                for (int v = 0; v < variants.Count; v++)
                {
                    List<Matrix4x4> matrices = chunk.matrices[v];
                    GrassVariant variant = variants[v];

                    for (int start = 0; start < matrices.Count; start += BatchSize)
                    {
                        int count = Mathf.Min(BatchSize, matrices.Count - start);
                        Matrix4x4[] batch = new Matrix4x4[count];
                        matrices.CopyTo(start, batch, 0, count);
#pragma warning disable 0618
                        Graphics.DrawMeshInstanced(variant.mesh, 0, variant.material, batch, count, null,
                            ShadowCastingMode.Off, true, 0, null, LightProbeUsage.Off, null);
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
