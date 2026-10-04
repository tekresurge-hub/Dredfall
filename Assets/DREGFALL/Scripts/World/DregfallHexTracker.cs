using UnityEngine;

namespace Dregfall
{
    public sealed class DregfallHexTracker : MonoBehaviour
    {
        [SerializeField] float hexSize = 12f;
        Vector2Int current;
        bool initialized;

        public Vector2Int CurrentHex => current;

        void Update()
        {
            Vector2Int next = WorldToHex(transform.position);
            if (!initialized || next != current)
            {
                current = next;
                initialized = true;
                Debug.Log($"[DREGFALL] Entered Hex ({current.x}, {current.y})");
            }
        }

        Vector2Int WorldToHex(Vector3 p)
        {
            float q = (Mathf.Sqrt(3f) / 3f * p.x - 1f / 3f * p.z) / hexSize;
            float r = (2f / 3f * p.z) / hexSize;
            return CubeRound(q, r);
        }

        static Vector2Int CubeRound(float q, float r)
        {
            float x = q, z = r, y = -x - z;
            int rx = Mathf.RoundToInt(x), ry = Mathf.RoundToInt(y), rz = Mathf.RoundToInt(z);
            float dx = Mathf.Abs(rx - x), dy = Mathf.Abs(ry - y), dz = Mathf.Abs(rz - z);
            if (dx > dy && dx > dz) rx = -ry - rz;
            else if (dy > dz) ry = -rx - rz;
            else rz = -rx - ry;
            return new Vector2Int(rx, rz);
        }

        void OnGUI()
        {
            GUI.Box(new Rect(12, 12, 190, 34), $"Current Hex: ({current.x}, {current.y})");
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.7f);
            Gizmos.DrawWireSphere(transform.position, hexSize);
        }
    }
}