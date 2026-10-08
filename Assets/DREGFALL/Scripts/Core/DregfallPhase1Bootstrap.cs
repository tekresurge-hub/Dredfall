using UnityEngine;

namespace Dregfall
{
    public static class DregfallPhase1Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Build()
        {
            if (Object.FindFirstObjectByType<DregfallPlayerController>() != null) return;

            Camera cam = Camera.main;
            if (cam == null)
            {
                GameObject c = new GameObject("Main Camera");
                cam = c.AddComponent<Camera>();
                c.tag = "MainCamera";
            }
            cam.fieldOfView = 52f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 1200f;
            DregfallVisualQuality.Apply(cam);

            // Respect a terrain authored in the current scene (including the HDRP demo).
            // Only generate the legacy fixed world when no terrain is present.
            Terrain[] sceneTerrains = Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
            Vector3 spawn;
            if (sceneTerrains.Length > 0)
            {
                Terrain selected = sceneTerrains[0];
                for (int i = 1; i < sceneTerrains.Length; i++)
                {
                    if (sceneTerrains[i].terrainData != null &&
                        (selected.terrainData == null ||
                         sceneTerrains[i].terrainData.size.x * sceneTerrains[i].terrainData.size.z >
                         selected.terrainData.size.x * selected.terrainData.size.z))
                        selected = sceneTerrains[i];
                }

                Vector3 origin = selected.transform.position;
                Vector3 size = selected.terrainData.size;
                float x = origin.x + size.x * 0.5f;
                float z = origin.z + size.z * 0.5f;
                float y = selected.SampleHeight(new Vector3(x, 0f, z)) + origin.y;
                spawn = new Vector3(x, y, z);
                Debug.Log("[DREGFALL] Using existing scene terrain; procedural terrain generation skipped.");
            }
            else
            {
                GameObject worldSystem = new GameObject("DREGFALL_FixedWorldSystem");
                DregfallWorldGenerator world = worldSystem.AddComponent<DregfallWorldGenerator>();
                world.GenerateWorld();
                spawn = world.GetRecommendedSpawnPoint();
            }

            GameObject player = new GameObject("DREGFALL_Survivor");
            player.transform.position = spawn + Vector3.up * 3f;

            CharacterController cc = player.AddComponent<CharacterController>();
            cc.height = 2f;
            cc.radius = 0.42f;
            cc.center = new Vector3(0f, 1f, 0f);

            player.AddComponent<DregfallPlayerController>();
            player.AddComponent<DregfallInteractionSystem>();
            player.AddComponent<DregfallProximityInteraction>();
            DregfallInteractiveGrass.SetPlayer(player.transform);

            GameObject survivorPrefab = Resources.Load<GameObject>("DREGFALL_SurvivorVisual");
            if (survivorPrefab != null)
            {
                GameObject visual = Object.Instantiate(survivorPrefab, player.transform);
                visual.name = "Survivor_Visual";
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;

                Animator animator = visual.GetComponentInChildren<Animator>();
                if (animator != null)
                {
                    animator.applyRootMotion = false;
                    DregfallAnimationDriver driver = player.AddComponent<DregfallAnimationDriver>();
                    driver.SetAnimator(animator);
                }
            }
            else
            {
                Debug.LogWarning("[DREGFALL] Survivor visual has not been generated yet. Open the project once in Unity and let the DREGFALL asset setup finish.");
                GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                fallback.name = "Temporary_Survivor_Visual";
                fallback.transform.SetParent(player.transform, false);
                fallback.transform.localPosition = new Vector3(0f, 1f, 0f);
                Object.Destroy(fallback.GetComponent<CapsuleCollider>());
            }

            DregfallCameraFollow follow = cam.GetComponent<DregfallCameraFollow>();
            if (follow == null) follow = cam.gameObject.AddComponent<DregfallCameraFollow>();
            follow.SetTarget(player.transform);

            Debug.Log("[DREGFALL] Survivor initialized. Existing scene terrain is preferred when available.");
        }
    }
}
