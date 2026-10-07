using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dregfall
{
    /// <summary>
    /// Fixed 8 km x 8 km (64 km²) DREGFALL world generator.
    /// No infinite generation. One deterministic survival map.
    /// Reuses the existing DREGFALL environment/civilization catalogs.
    /// </summary>
    public sealed class DregfallWorldGenerator : MonoBehaviour
    {
        [Header("World")]
        [SerializeField] int worldSeed = 240919;
        [SerializeField] int worldSizeMeters = 8000;
        [SerializeField] int terrainHeightMeters = 650;
        [SerializeField] int heightmapResolution = 1025;

        [Header("Terrain")]
        [SerializeField] float baseHeight = 0.055f;
        [SerializeField] float rollingHillsStrength = 0.13f;
        [SerializeField] float rollingHillsScale = 0.00075f;
        [SerializeField] float largeLandformStrength = 0.10f;
        [SerializeField] float largeLandformScale = 0.00022f;
        [SerializeField] float detailStrength = 0.035f;
        [SerializeField] float detailScale = 0.0035f;

        [Header("Settlements")]
        [SerializeField] int townCount = 4;
        [SerializeField] int villageCount = 8;
        [SerializeField] int industrialZoneCount = 3;
        [SerializeField] int farmClusterCount = 6;
        [SerializeField] int buildingsPerTown = 55;
        [SerializeField] int buildingsPerVillage = 18;
        [SerializeField] Vector2 townRadiusRange = new Vector2(230f, 420f);
        [SerializeField] Vector2 villageRadiusRange = new Vector2(100f, 220f);

        [Header("Nature")]
        [SerializeField] int treeCount = 18000;
        [SerializeField] int undergrowthCount = 5000;
        [SerializeField] int rockCount = 1800;
        [SerializeField] float natureRoadClearance = 14f;
        [SerializeField] float natureBuildingClearance = 16f;

        [Header("Wilderness")]
        [SerializeField] int isolatedCabins = 30;

        [Header("Spawns")]
        [SerializeField] int spawnPointCount = 100;
        [SerializeField] float minimumSpawnDistanceFromTownCenter = 250f;

        readonly List<Vector3> settlementCenters = new();
        readonly List<float> settlementRadii = new();
        readonly List<Vector3> roadSamples = new();
        readonly List<Vector3> buildingPositions = new();
        readonly List<Vector3> spawnPoints = new();

        Transform generatedRoot;
        Terrain terrain;
        System.Random rng;
        DregfallEnvironmentCatalog environmentCatalog;
        DregfallCivilizationCatalog civilizationCatalog;

        const string RootName = "_DREGFALL_FIXED_8X8_WORLD";

        public int WorldSeed => worldSeed;
        public int WorldSizeMeters => worldSizeMeters;

        public string GetStableWorldId(Vector3 worldPosition)
        {
            int x = Mathf.FloorToInt(worldPosition.x);
            int z = Mathf.FloorToInt(worldPosition.z);
            return $"world-{worldSeed}:cell-{x}:{z}";
        }

        public Vector3 GetRecommendedSpawnPoint()
        {
            if (spawnPoints.Count == 0) BuildSpawnPoints();
            if (spawnPoints.Count == 0) return GroundPoint(worldSizeMeters * 0.5f, worldSizeMeters * 0.5f);
            int index = Mathf.Abs(Environment.TickCount) % spawnPoints.Count;
            return spawnPoints[index];
        }

        [ContextMenu("Generate World")]
        public void GenerateWorld()
        {
            ClearWorld();

            rng = new System.Random(worldSeed);
            environmentCatalog = Resources.Load<DregfallEnvironmentCatalog>("DREGFALL_EnvironmentCatalog");
            civilizationCatalog = Resources.Load<DregfallCivilizationCatalog>("DREGFALL_CivilizationCatalog");

            generatedRoot = new GameObject(RootName).transform;
            generatedRoot.SetParent(transform, false);

            BuildTerrain();
            PlanSettlementLocations();
            BuildRoadNetwork();
            BuildSettlements();
            BuildWildernessLocations();
            BuildNature();
            BuildSpawnPoints();

            Debug.Log($"[DREGFALL] Fixed world ready. Seed={worldSeed}, size={worldSizeMeters}x{worldSizeMeters}m (64 km²), settlements={settlementCenters.Count}, spawns={spawnPoints.Count}.");
        }

        [ContextMenu("Clear World")]
        public void ClearWorld()
        {
            Transform old = transform.Find(RootName);
            if (old != null)
            {
                if (Application.isPlaying) Destroy(old.gameObject);
                else DestroyImmediate(old.gameObject);
            }

            settlementCenters.Clear();
            settlementRadii.Clear();
            roadSamples.Clear();
            buildingPositions.Clear();
            spawnPoints.Clear();
            terrain = null;
        }

        void BuildTerrain()
        {
            TerrainData td = new TerrainData
            {
                heightmapResolution = ClosestHeightmapResolution(heightmapResolution),
                size = new Vector3(worldSizeMeters, terrainHeightMeters, worldSizeMeters)
            };

            int res = td.heightmapResolution;
            float[,] heights = new float[res, res];

            float ox1 = NextFloat(1000f, 90000f);
            float oz1 = NextFloat(1000f, 90000f);
            float ox2 = NextFloat(1000f, 90000f);
            float oz2 = NextFloat(1000f, 90000f);
            float ox3 = NextFloat(1000f, 90000f);
            float oz3 = NextFloat(1000f, 90000f);

            for (int z = 0; z < res; z++)
            {
                float wz = (float)z / (res - 1) * worldSizeMeters;
                for (int x = 0; x < res; x++)
                {
                    float wx = (float)x / (res - 1) * worldSizeMeters;

                    float large = Mathf.Pow(Mathf.PerlinNoise(ox1 + wx * largeLandformScale, oz1 + wz * largeLandformScale), 1.35f);
                    float rolling = Mathf.PerlinNoise(ox2 + wx * rollingHillsScale, oz2 + wz * rollingHillsScale);
                    float detail = Mathf.PerlinNoise(ox3 + wx * detailScale, oz3 + wz * detailScale);

                    float h = baseHeight;
                    h += large * largeLandformStrength;
                    h += (rolling - 0.45f) * rollingHillsStrength;
                    h += (detail - 0.5f) * detailStrength;

                    float nx = Mathf.Abs(wx / worldSizeMeters * 2f - 1f);
                    float nz = Mathf.Abs(wz / worldSizeMeters * 2f - 1f);
                    float edge = Mathf.Max(nx, nz);
                    h -= Mathf.SmoothStep(0f, 0.045f, Mathf.InverseLerp(0.78f, 1f, edge));

                    heights[z, x] = Mathf.Clamp01(h);
                }
            }

            td.SetHeights(0, 0, heights);

            GameObject go = Terrain.CreateTerrainGameObject(td);
            go.name = "DREGFALL_Terrain_8x8km";
            go.transform.SetParent(generatedRoot, false);
            terrain = go.GetComponent<Terrain>();
            terrain.drawInstanced = true;
            terrain.heightmapPixelError = 12f;
            terrain.basemapDistance = 1500f;
        }

        void PlanSettlementLocations()
        {
            AddSeparatedCenters(townCount, townRadiusRange, 1000f);
            AddSeparatedCenters(villageCount, villageRadiusRange, 600f);
            AddSeparatedCenters(industrialZoneCount, new Vector2(180f, 340f), 750f);
            AddSeparatedCenters(farmClusterCount, new Vector2(130f, 260f), 450f);
        }

        void AddSeparatedCenters(int count, Vector2 radiusRange, float preferredSpacing)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 candidate = Vector3.zero;
                bool accepted = false;

                for (int attempt = 0; attempt < 100; attempt++)
                {
                    float margin = 450f;
                    candidate = GroundPoint(
                        NextFloat(margin, worldSizeMeters - margin),
                        NextFloat(margin, worldSizeMeters - margin));

                    accepted = true;
                    foreach (Vector3 center in settlementCenters)
                    {
                        if (Vector3.Distance(candidate, center) < preferredSpacing)
                        {
                            accepted = false;
                            break;
                        }
                    }

                    if (accepted) break;
                }

                if (accepted)
                {
                    settlementCenters.Add(candidate);
                    settlementRadii.Add(NextFloat(radiusRange.x, radiusRange.y));
                }
            }
        }

        void BuildRoadNetwork()
        {
            if (settlementCenters.Count < 2) return;

            for (int i = 1; i < settlementCenters.Count; i++)
            {
                int nearest = 0;
                float best = float.MaxValue;

                for (int j = 0; j < i; j++)
                {
                    float d = (settlementCenters[i] - settlementCenters[j]).sqrMagnitude;
                    if (d < best)
                    {
                        best = d;
                        nearest = j;
                    }
                }

                SampleRoadCurve(settlementCenters[i], settlementCenters[nearest]);
            }

            for (int i = 0; i < 5; i++)
            {
                bool horizontal = i % 2 == 0;
                Vector3 a;
                Vector3 b;

                if (horizontal)
                {
                    float z = NextFloat(800f, worldSizeMeters - 800f);
                    a = GroundPoint(80f, z);
                    b = GroundPoint(worldSizeMeters - 80f, Mathf.Clamp(z + NextFloat(-900f, 900f), 100f, worldSizeMeters - 100f));
                }
                else
                {
                    float x = NextFloat(800f, worldSizeMeters - 800f);
                    a = GroundPoint(x, 80f);
                    b = GroundPoint(Mathf.Clamp(x + NextFloat(-900f, 900f), 100f, worldSizeMeters - 100f), worldSizeMeters - 80f);
                }

                SampleRoadCurve(a, b);
            }
        }

        void SampleRoadCurve(Vector3 a, Vector3 b)
        {
            float distance = Vector3.Distance(a, b);
            int segments = Mathf.Max(2, Mathf.CeilToInt(distance / 20f));

            Vector3 mid = Vector3.Lerp(a, b, 0.5f);
            Vector3 perpendicular = Vector3.Cross((b - a).normalized, Vector3.up);
            Vector3 control = mid + perpendicular * NextFloat(-160f, 160f);

            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                Vector3 p = QuadraticBezier(a, control, b, t);
                p.y = GroundY(p.x, p.z);
                roadSamples.Add(p);
            }
        }

        void BuildSettlements()
        {
            int cursor = 0;

            for (int i = 0; i < townCount && cursor < settlementCenters.Count; i++, cursor++)
                BuildResidentialSettlement(settlementCenters[cursor], settlementRadii[cursor], buildingsPerTown, $"Town_{i + 1:00}");

            for (int i = 0; i < villageCount && cursor < settlementCenters.Count; i++, cursor++)
                BuildResidentialSettlement(settlementCenters[cursor], settlementRadii[cursor], buildingsPerVillage, $"Village_{i + 1:00}");

            for (int i = 0; i < industrialZoneCount && cursor < settlementCenters.Count; i++, cursor++)
                BuildResidentialSettlement(settlementCenters[cursor], settlementRadii[cursor], Mathf.Max(8, buildingsPerVillage / 2), $"Industrial_{i + 1:00}");

            for (int i = 0; i < farmClusterCount && cursor < settlementCenters.Count; i++, cursor++)
                BuildResidentialSettlement(settlementCenters[cursor], settlementRadii[cursor], Mathf.Max(5, buildingsPerVillage / 3), $"Farm_{i + 1:00}");
        }

        void BuildResidentialSettlement(Vector3 center, float radius, int count, string name)
        {
            Transform parent = NewContainer(name, generatedRoot);
            GameObject[] buildings = civilizationCatalog != null ? civilizationCatalog.isolatedBuildings : null;
            if (buildings == null || buildings.Length == 0)
            {
                Debug.LogWarning("[DREGFALL] Civilization catalog has no usable buildings. World layout still generated.");
                return;
            }

            for (int i = 0; i < count; i++)
            {
                Vector3 p = RandomPointInCircle(center, radius);
                p.y = GroundY(p.x, p.z);
                if (NearBuilding(p, 18f)) continue;

                GameObject prefab = Pick(buildings);
                if (prefab == null) continue;

                GameObject building = Instantiate(prefab, p, Quaternion.Euler(0f, NextFloat(0f, 360f), 0f), parent);
                building.name = $"{name}_Building_{i:000}_{prefab.name}";
                DregfallBuildingRuntimeAdapter.Prepare(building);
                buildingPositions.Add(p);
            }
        }

        void BuildWildernessLocations()
        {
            GameObject[] buildings = civilizationCatalog != null ? civilizationCatalog.isolatedBuildings : null;
            if (buildings == null || buildings.Length == 0) return;

            Transform root = NewContainer("Wilderness_Buildings", generatedRoot);

            for (int i = 0; i < isolatedCabins; i++)
            {
                Vector3 p = FindWildernessPoint();
                GameObject prefab = Pick(buildings);
                if (prefab == null) continue;

                GameObject building = Instantiate(prefab, p, Quaternion.Euler(0f, NextFloat(0f, 360f), 0f), root);
                building.name = $"Wilderness_Building_{i:000}_{prefab.name}";
                DregfallBuildingRuntimeAdapter.Prepare(building);
                buildingPositions.Add(p);
            }
        }

        void BuildNature()
        {
            if (environmentCatalog == null) return;

            Transform nature = NewContainer("Nature", generatedRoot);
            Transform trees = NewContainer("Trees", nature);
            Transform undergrowth = NewContainer("Undergrowth", nature);
            Transform rocks = NewContainer("Rocks", nature);

            SpawnNatureCategory(environmentCatalog.trees, treeCount, trees, 0.82f, 1.28f);
            SpawnNatureCategory(environmentCatalog.undergrowth, undergrowthCount, undergrowth, 0.75f, 1.35f);
            SpawnNatureCategory(environmentCatalog.rocks, rockCount, rocks, 0.65f, 1.75f);
        }

        void SpawnNatureCategory(GameObject[] prefabs, int count, Transform parent, float minScale, float maxScale)
        {
            if (prefabs == null || prefabs.Length == 0) return;

            int made = 0;
            int attempts = 0;

            while (made < count && attempts < count * 8)
            {
                attempts++;
                Vector3 p = RandomWorldPoint(100f);

                if (InsideSettlement(p, 1.05f) || NearRoad(p, natureRoadClearance) || NearBuilding(p, natureBuildingClearance))
                    continue;

                GameObject prefab = Pick(prefabs);
                if (prefab == null) continue;

                GameObject go = Instantiate(prefab, p, Quaternion.Euler(0f, NextFloat(0f, 360f), 0f), parent);
                go.name = $"Nature_{prefab.name}_{made:00000}";
                go.transform.localScale *= NextFloat(minScale, maxScale);
                made++;
            }
        }

        void BuildSpawnPoints()
        {
            spawnPoints.Clear();

            int attempts = 0;
            while (spawnPoints.Count < spawnPointCount && attempts < spawnPointCount * 100)
            {
                attempts++;
                Vector3 p = RandomWorldPoint(160f);

                bool tooNearTown = false;
                foreach (Vector3 center in settlementCenters)
                {
                    if (Vector3.Distance(p, center) < minimumSpawnDistanceFromTownCenter)
                    {
                        tooNearTown = true;
                        break;
                    }
                }

                if (tooNearTown || NearBuilding(p, 35f)) continue;
                spawnPoints.Add(p);
            }
        }

        public float SampleGroundHeight(float x, float z) => GroundY(x, z);

        public float GetWildernessDensity(Vector3 worldPosition)
        {
            float a = Mathf.PerlinNoise(worldPosition.x * 0.0009f + worldSeed * 0.001f,
                                        worldPosition.z * 0.0009f + worldSeed * 0.002f);
            float b = Mathf.PerlinNoise(worldPosition.x * 0.0042f + 51.2f,
                                        worldPosition.z * 0.0042f + 17.8f);
            return Mathf.Clamp01(a * 0.72f + b * 0.28f);
        }

        public float GetForestRegionDensity(Vector3 worldPosition)
        {
            return Mathf.PerlinNoise(worldPosition.x * 0.00055f + worldSeed * 0.0007f,
                                     worldPosition.z * 0.00055f + worldSeed * 0.0011f);
        }

        bool InsideSettlement(Vector3 p, float multiplier)
        {
            for (int i = 0; i < settlementCenters.Count; i++)
                if (Vector3.Distance(p, settlementCenters[i]) < settlementRadii[i] * multiplier)
                    return true;
            return false;
        }

        bool NearRoad(Vector3 p, float radius)
        {
            float r2 = radius * radius;
            for (int i = 0; i < roadSamples.Count; i += 3)
                if ((roadSamples[i] - p).sqrMagnitude < r2) return true;
            return false;
        }

        bool NearBuilding(Vector3 p, float radius)
        {
            float r2 = radius * radius;
            for (int i = 0; i < buildingPositions.Count; i++)
                if ((buildingPositions[i] - p).sqrMagnitude < r2) return true;
            return false;
        }

        Vector3 FindWildernessPoint()
        {
            Vector3 p = RandomWorldPoint(100f);

            for (int i = 0; i < 100; i++)
            {
                p = RandomWorldPoint(100f);
                if (!InsideSettlement(p, 1.3f) && !NearRoad(p, 55f) && !NearBuilding(p, 80f))
                    break;
            }

            return p;
        }

        Vector3 RandomWorldPoint(float margin)
        {
            float x = NextFloat(margin, worldSizeMeters - margin);
            float z = NextFloat(margin, worldSizeMeters - margin);
            return GroundPoint(x, z);
        }

        Vector3 RandomPointInCircle(Vector3 center, float radius)
        {
            float angle = NextFloat(0f, Mathf.PI * 2f);
            float distance = Mathf.Sqrt(NextFloat01()) * radius;
            return new Vector3(
                center.x + Mathf.Cos(angle) * distance,
                center.y,
                center.z + Mathf.Sin(angle) * distance);
        }

        Vector3 GroundPoint(float x, float z) => new Vector3(x, GroundY(x, z), z);

        float GroundY(float x, float z)
        {
            if (terrain == null) return 0f;
            return terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y;
        }

        GameObject Pick(GameObject[] prefabs)
        {
            if (prefabs == null || prefabs.Length == 0) return null;

            for (int i = 0; i < prefabs.Length * 2; i++)
            {
                GameObject candidate = prefabs[NextInt(0, prefabs.Length)];
                if (candidate != null) return candidate;
            }

            return null;
        }

        Transform NewContainer(string name, Transform parent)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        int ClosestHeightmapResolution(int requested)
        {
            int[] valid = { 513, 1025, 2049, 4097 };
            int best = valid[0];
            int delta = Mathf.Abs(requested - best);

            foreach (int v in valid)
            {
                int d = Mathf.Abs(requested - v);
                if (d < delta)
                {
                    best = v;
                    delta = d;
                }
            }

            return best;
        }

        Vector3 QuadraticBezier(Vector3 a, Vector3 c, Vector3 b, float t)
        {
            float u = 1f - t;
            return u * u * a + 2f * u * t * c + t * t * b;
        }

        float NextFloat01() => (float)rng.NextDouble();
        float NextFloat(float min, float max) => Mathf.Lerp(min, max, NextFloat01());
        int NextInt(int minInclusive, int maxExclusive) => rng.Next(minInclusive, maxExclusive);
    }
}
