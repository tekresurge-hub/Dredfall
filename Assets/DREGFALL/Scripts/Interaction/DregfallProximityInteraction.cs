using UnityEngine;
using UnityEngine.InputSystem;

namespace Dregfall
{
    public sealed class DregfallProximityInteraction : MonoBehaviour
    {
        [SerializeField, Range(1f, 5f)] float interactionRadius = 2.35f;
        [SerializeField] LayerMask layers = ~0;

        DregfallHingedInteractable current;

        void Update()
        {
            FindNearest();
            if (current != null && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                current.Toggle();
        }

        void FindNearest()
        {
            current = null;
            float best = interactionRadius * interactionRadius;
            Collider[] hits = Physics.OverlapSphere(transform.position, interactionRadius, layers, QueryTriggerInteraction.Collide);
            foreach (Collider hit in hits)
            {
                DregfallHingedInteractable candidate = hit.GetComponentInParent<DregfallHingedInteractable>();
                if (candidate == null) continue;
                float d = (candidate.transform.position - transform.position).sqrMagnitude;
                if (d < best) { best = d; current = candidate; }
            }
        }

        void OnGUI()
        {
            if (current == null) return;
            string action = current.IsOpen ? "Close" : "Open";
            GUIStyle style = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 16
            };
            Rect rect = new Rect(Screen.width * 0.5f - 115f, Screen.height * 0.72f, 230f, 34f);
            GUI.Box(rect, $"[E] {action} {current.DisplayName}", style);
        }
    }
}
