using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Dregfall
{
    // Dense grass is rendered as instanced meshes rather than thousands of GameObjects.
    // This keeps the streamed world visually full while remaining practical to render.
    public sealed class DregfallGrassFieldRenderer : MonoBehaviour
    {
        const int BatchSize = 1023;

        sealed class GrassChunk
        {
            public readonly List<Matrix4x4[]> batches = new();
        }

        readonly Dictionary<Vector2Int, GrassChunk> chunks = new();
        DregfallInfiniteWorld world;
        Transform player;
        Mesh mesh;
        Material material;
        int grassPerChunk;

        public void Initialize(DregfallInfiniteWorld owner, Transform target, GameObject[] grassPrefabs, int density)
        {
            world = owner;
            player = target;
            grassPerChunk = Mathf.Clamp(density, 400, 2600);

            if (grassPrefabs == null) return;
            foreach (GameObject prefab in grassPrefabs)
            {
                if (prefab == null) continue;
                MeshFilter mf = prefab.GetComponentInChildren<MeshFilter>(true);
                Renderer mr = prefab.GetComponentInChildren<Renderer>(true);
                if (mf == null || mf.sharedMesh == null || mr == null || mr.sharedMaterial == null) continue;
                mesh = mf.sharedMesh;
                material = new Material(mr.sharedMaterial) { name = "DREGFALL_InstancedGrass" };
                material.enableInstancing = true;
                break;
            }
        }

        public void BuildChunk(Vector2Int coord)
        {
            if (mesh == null || material == null || chunks.ContainsKey(coord)) return;

            int seed = world.WorldSeed ^ (coord.x * 73856093) ^ (coord.y * 19349663) ^ 0x45D9F3B;
            var rng = new System.Random(seed);
            var matrices = new List<Matrix4x4>(grassPerChunk);

            for (int i = 0; i < grassPerChunk; i++)
            {
                float x = coord.x * world.ChunkSize + (float)rng.NextDouble() * world.ChunkSize;
                float z = coord.y * world.ChunkSize + (float)rng.NextDouble() * world.ChunkSize;

                float ecology = world.GetWildernessDensity(new Vector3(x, 0f, z));
                float patch = Mathf.PerlinNoise(x * 0.021f + 31.7f, z * 0.021f + 73.1f);
                float barePatch = Mathf.PerlinNoise(x * 0.008f + 119.3f, z * 0.008f + 211.9f);

                // Dense living floor with coherent thin/bare patches rather than uniform confetti.
                float keep = Mathf.Clamp01(0.80f + ecology * 0.16f + patch * 0.10f);
                if (barePatch > 0.82f) keep *= 0.30f;
                if ((float)rng.NextDouble() > keep) continue;

                float y = world.SampleGroundHeight(x, z);
                float hx = world.SampleGroundHeight(x + 0.75f, z);
                float hz = world.SampleGroundHeight(x, z + 0.75f);
                float slope = Mathf.Max(Mathf.Abs(hx - y), Mathf.Abs(hz - y)) / 0.75f;
                if (slope > 0.82f) continue;

                float scale = Mathf.Lerp(1.35f, 2.25f, (float)rng.NextDouble());
                float widthVariation = Mathf.Lerp(0.82f, 1.18f, (float)rng.NextDouble());
                Quaternion rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                matrices.Add(Matrix4x4.TRS(new Vector3(x, y, z), rotation,
                    new Vector3(scale * widthVariation, scale, scale * widthVariation)));
            }

            GrassChunk data = new GrassChunk();
            for (int start = 0; start < matrices.Count; start += BatchSize)
            {
                int count = Mathf.Min(BatchSize, matrices.Count - start);
                Matrix4x4[] batch = new Matrix4x4[count];
                matrices.CopyTo(start, batch, 0, count);
                data.batches.Add(batch);
            }
            chunks.Add(coord, data);
        }

        public void RemoveChunk(Vector2Int coord)
        {
            chunks.Remove(coord);
        }

        void LateUpdate()
        {
            if (mesh == null || material == null || player == null) return;

            foreach (GrassChunk chunk in chunks.Values)
            {
                foreach (Matrix4x4[] batch in chunk.batches)
                {
#pragma warning disable 0618
                    Graphics.DrawMeshInstanced(mesh, 0, material, batch, batch.Length, null,
                        ShadowCastingMode.Off, true, 0, null, LightProbeUsage.Off, null);
#pragma warning restore 0618
                }
            }
        }

        void OnDestroy()
        {
            if (material != null) Destroy(material);
        }
    }
}
