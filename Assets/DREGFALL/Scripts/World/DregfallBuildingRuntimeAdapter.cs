using UnityEngine;

namespace Dregfall
{
    public sealed class DregfallBuildingRuntimeAdapter : MonoBehaviour
    {
        public static void Prepare(GameObject building)
        {
            if (building == null) return;
            if (building.name.ToLowerInvariant().Contains("house_enter"))
                PrepareEnterableHouse(building);
        }

        static void PrepareEnterableHouse(GameObject building)
        {
            foreach (MeshCollider meshCollider in building.GetComponentsInChildren<MeshCollider>(true))
                meshCollider.enabled = false;

            Renderer[] renderers = building.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

            Vector3 c = building.transform.InverseTransformPoint(bounds.center);
            Vector3 s = bounds.size;
            float wall = 0.28f;
            float doorWidth = 1.45f;
            float doorHeight = 2.15f;
            float halfX = s.x * 0.5f;
            float halfZ = s.z * 0.5f;
            float bottom = c.y - s.y * 0.5f;

            Transform shell = new GameObject("DREGFALL_CollisionShell").transform;
            shell.SetParent(building.transform, false);

            AddBox(shell, "LeftWall", new Vector3(c.x-halfX+wall*.5f,c.y,c.z), new Vector3(wall,s.y,s.z));
            AddBox(shell, "RightWall", new Vector3(c.x+halfX-wall*.5f,c.y,c.z), new Vector3(wall,s.y,s.z));
            AddBox(shell, "BackWall", new Vector3(c.x,c.y,c.z+halfZ-wall*.5f), new Vector3(s.x-wall*2f,s.y,wall));

            float side = (s.x-doorWidth)*.5f;
            AddBox(shell, "FrontLeft", new Vector3(c.x-halfX+side*.5f,c.y,c.z-halfZ+wall*.5f), new Vector3(side,s.y,wall));
            AddBox(shell, "FrontRight", new Vector3(c.x+halfX-side*.5f,c.y,c.z-halfZ+wall*.5f), new Vector3(side,s.y,wall));
            AddBox(shell, "DoorHeader", new Vector3(c.x,bottom+doorHeight+(s.y-doorHeight)*.5f,c.z-halfZ+wall*.5f), new Vector3(doorWidth,s.y-doorHeight,wall));

            CreateDoor(building.transform, new Vector3(c.x-doorWidth*.5f,bottom,c.z-halfZ+wall*.55f), doorWidth, doorHeight);
        }

        static void AddBox(Transform parent, string name, Vector3 center, Vector3 size)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.AddComponent<BoxCollider>().size = size;
        }

        static void CreateDoor(Transform parent, Vector3 hingePosition, float width, float height)
        {
            GameObject hinge = new GameObject("DREGFALL_Door_Hinge");
            hinge.transform.SetParent(parent, false);
            hinge.transform.localPosition = hingePosition;

            GameObject leaf = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leaf.name = "DREGFALL_Door_Leaf";
            leaf.transform.SetParent(hinge.transform, false);
            leaf.transform.localPosition = new Vector3(width*.5f,height*.5f,0f);
            leaf.transform.localScale = new Vector3(width,height,.12f);

            Renderer renderer = leaf.GetComponent<Renderer>();
            if (renderer != null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader != null)
                {
                    Material material = new Material(shader);
                    material.color = new Color(.19f,.145f,.095f,1f);
                    material.SetFloat("_Smoothness",.04f);
                    renderer.material = material;
                }
            }

            DregfallHingedInteractable hingeInteraction = hinge.AddComponent<DregfallHingedInteractable>();
            hingeInteraction.Configure(DregfallHingeType.Door,"Door",hinge.transform,new Vector3(0f,96f,0f),.78f,null,null);
        }
    }
}
