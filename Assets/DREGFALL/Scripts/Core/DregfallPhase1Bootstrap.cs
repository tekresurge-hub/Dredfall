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

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "DREGFALL_TestGround";
            ground.transform.localScale = new Vector3(8f, 1f, 8f);

            GameObject player = new GameObject("DREGFALL_Survivor");
            player.transform.position = new Vector3(0f, 0.05f, 0f);

            CharacterController cc = player.AddComponent<CharacterController>();
            cc.height = 2f;
            cc.radius = 0.42f;
            cc.center = new Vector3(0f, 1f, 0f);

            player.AddComponent<DregfallPlayerController>();
            player.AddComponent<DregfallInteractionSystem>();
            player.AddComponent<DregfallHexTracker>();

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

            CreateTree(new Vector3(5, 1.5f, 4));
            CreateTree(new Vector3(-7, 1.5f, 8));
            CreateTree(new Vector3(10, 1.5f, -7));
            CreateRock(new Vector3(-5, .65f, -5));
            CreateRock(new Vector3(8, .65f, 10));

            Debug.Log("[DREGFALL] Phase 1 survivor ready. WASD move, Shift sprint, right-click a tree.");
        }

        static void CreateTree(Vector3 position)
        {
            GameObject tree = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tree.name = "Test Tree";
            tree.transform.position = position;
            tree.transform.localScale = new Vector3(.8f, 3f, .8f);
            var i = tree.AddComponent<DregfallInteractable>();
            i.Configure("Tree", "A mature tree. Useful timber if you have the right tool.", true);
        }

        static void CreateRock(Vector3 position)
        {
            GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rock.name = "Test Rock";
            rock.transform.position = position;
            rock.transform.localScale = new Vector3(1.6f, 1.1f, 1.4f);
            var i = rock.AddComponent<DregfallInteractable>();
            i.Configure("Rock", "A weathered rock outcrop.", false);
        }
    }
}
