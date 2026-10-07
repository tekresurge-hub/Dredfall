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

            GameObject worldSystem = new GameObject("DREGFALL_FixedWorldSystem");
            DregfallWorldGenerator world = worldSystem.AddComponent<DregfallWorldGenerator>();
            world.GenerateWorld();

            Vector3 spawn = world.GetRecommendedSpawnPoint();

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

            Debug.Log("[DREGFALL] Fixed 8x8 km world active. Unlimited generation retired.");
        }
    }
}
