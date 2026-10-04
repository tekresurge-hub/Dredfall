using UnityEngine;

namespace Dregfall
{
    public sealed class DregfallCameraFollow : MonoBehaviour
    {
        [SerializeField] Vector3 offset = new Vector3(0f, 13f, -10f);
        [SerializeField] float followSharpness = 8f;
        [SerializeField] Vector3 lookOffset = new Vector3(0f, 1f, 0f);
        Transform target;

        public void SetTarget(Transform value, bool snap = true)
        {
            target = value;
            if (snap && target != null) transform.position = target.position + offset;
        }

        void LateUpdate()
        {
            if (target == null) return;
            Vector3 desired = target.position + offset;
            transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-followSharpness * Time.deltaTime));
            transform.rotation = Quaternion.LookRotation((target.position + lookOffset) - transform.position, Vector3.up);
        }
    }
}