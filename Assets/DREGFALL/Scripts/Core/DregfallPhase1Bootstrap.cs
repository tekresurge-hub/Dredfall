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
            cam.farClipPlane = 350f;

            GameObject player = new GameObject("DREGFALL_Survivor");
            player.transform.position = new Vector3(0f, 8f, 0f);

            CharacterController cc = player.AddComponent<CharacterController>();
            cc.height = 2f;
            cc.radius = 0.42f;
            cc.center = new Vector3(0f, 1f, 0f);

            player.AddComponent<DregfallPlayerController>();
            player.AddComponent<DregfallInteractionSystem>();

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

            GameObject worldSystem = new GameObject("DREGFALL_InfiniteWorldSystem");
            DregfallInfiniteWorld world = worldSystem.AddComponent<DregfallInfiniteWorld>();
            world.Initialize(player.transform);

            Debug.Log("[DREGFALL] Phase 2A foundation ready. Continuous streamed terrain active; old hex/test world retired.");
        }
    }
}
