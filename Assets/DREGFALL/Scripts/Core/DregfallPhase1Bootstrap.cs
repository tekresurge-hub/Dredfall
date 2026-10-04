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

            GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "DREGFALL_Survivor";
            player.transform.position = new Vector3(0, 1.05f, 0);
            Object.Destroy(player.GetComponent<CapsuleCollider>());
            CharacterController cc = player.AddComponent<CharacterController>();
            cc.height = 2f; cc.radius = .45f; cc.center = Vector3.zero;
            player.AddComponent<DregfallPlayerController>();
            player.AddComponent<DregfallInteractionSystem>();
            player.AddComponent<DregfallHexTracker>();

            DregfallCameraFollow follow = cam.GetComponent<DregfallCameraFollow>();
            if (follow == null) follow = cam.gameObject.AddComponent<DregfallCameraFollow>();
            follow.SetTarget(player.transform);

            CreateTree(new Vector3(5, 1.5f, 4));
            CreateTree(new Vector3(-7, 1.5f, 8));
            CreateTree(new Vector3(10, 1.5f, -7));
            CreateRock(new Vector3(-5, .65f, -5));
            CreateRock(new Vector3(8, .65f, 10));

            Debug.Log("[DREGFALL] Phase 1 bootstrap ready. WASD move, Shift sprint, right-click a tree.");
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