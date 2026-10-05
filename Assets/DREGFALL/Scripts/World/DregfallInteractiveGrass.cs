using System.Collections.Generic;
using UnityEngine;

namespace Dregfall
{
    // Lightweight shared grass interaction. Individual grass clumps do not run Update;
    // one interactor bends only nearby registered clumps and lets them spring back.
    public sealed class DregfallInteractiveGrass : MonoBehaviour
    {
        static readonly List<DregfallInteractiveGrass> Active = new();
        static Transform player;
        static DregfallGrassDriver driver;

        Quaternion restLocalRotation;
        float personalStrength;

        public static void SetPlayer(Transform target)
        {
            player = target;
            if (driver == null && target != null)
            {
                driver = target.GetComponent<DregfallGrassDriver>();
                if (driver == null) driver = target.gameObject.AddComponent<DregfallGrassDriver>();
            }
        }

        void Awake()
        {
            restLocalRotation = transform.localRotation;
            personalStrength = 0.82f + Mathf.Abs(Mathf.Sin(transform.position.x * 1.73f + transform.position.z * 2.31f)) * 0.28f;
        }

        void OnEnable()
        {
            if (!Active.Contains(this)) Active.Add(this);
        }

        void OnDisable()
        {
            Active.Remove(this);
        }

        internal static void Tick()
        {
            if (player == null) return;

            Vector3 p = player.position;
            const float bendRadius = 2.35f;
            const float bendRadiusSq = bendRadius * bendRadius;

            for (int i = Active.Count - 1; i >= 0; i--)
            {
                DregfallInteractiveGrass grass = Active[i];
                if (grass == null)
                {
                    Active.RemoveAt(i);
                    continue;
                }

                Transform t = grass.transform;
                Vector3 delta = t.position - p;
                delta.y = 0f;
                float sqr = delta.sqrMagnitude;

                Quaternion target = grass.restLocalRotation;
                float speed = 5.5f;

                if (sqr < bendRadiusSq)
                {
                    float distance = Mathf.Sqrt(Mathf.Max(0.0001f, sqr));
                    float influence = 1f - distance / bendRadius;
                    Vector3 away = delta / distance;
                    Vector3 bendAxisWorld = Vector3.Cross(Vector3.up, away).normalized;
                    Vector3 bendAxisLocal = t.parent != null ? t.parent.InverseTransformDirection(bendAxisWorld) : bendAxisWorld;
                    float angle = Mathf.Lerp(8f, 48f, influence) * grass.personalStrength;
                    target = grass.restLocalRotation * Quaternion.AngleAxis(angle, bendAxisLocal);
                    speed = 13f;
                }

                t.localRotation = Quaternion.Slerp(t.localRotation, target, 1f - Mathf.Exp(-speed * Time.deltaTime));
            }
        }
    }

    public sealed class DregfallGrassDriver : MonoBehaviour
    {
        void LateUpdate()
        {
            DregfallInteractiveGrass.Tick();
        }
    }
}
