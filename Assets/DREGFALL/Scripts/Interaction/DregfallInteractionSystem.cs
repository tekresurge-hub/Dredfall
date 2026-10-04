using UnityEngine;
using UnityEngine.InputSystem;

namespace Dregfall
{
    public sealed class DregfallInteractionSystem : MonoBehaviour
    {
        [SerializeField] float maxDistance = 18f;
        Camera worldCamera;
        DregfallInteractable selected;
        bool menuOpen;
        Vector2 menuPosition;
        string message;
        float messageUntil;

        void Awake() => worldCamera = Camera.main;

        void Update()
        {
            if (Mouse.current == null) return;
            if (worldCamera == null) worldCamera = Camera.main;

            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                menuOpen = false;
                selected = null;
                Ray ray = worldCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
                if (Physics.Raycast(ray, out RaycastHit hit, 200f))
                {
                    selected = hit.collider.GetComponentInParent<DregfallInteractable>();
                    if (selected != null && Vector3.Distance(transform.position, selected.transform.position) <= maxDistance)
                    {
                        menuOpen = true;
                        Vector2 p = Mouse.current.position.ReadValue();
                        menuPosition = new Vector2(p.x, Screen.height - p.y);
                    }
                }
            }

            if (Mouse.current.leftButton.wasPressedThisFrame && menuOpen)
            {
                Vector2 p = Mouse.current.position.ReadValue();
                p.y = Screen.height - p.y;
                Rect menu = GetMenuRect();
                if (!menu.Contains(p)) menuOpen = false;
            }
        }

        Rect GetMenuRect() => new Rect(Mathf.Clamp(menuPosition.x, 8, Screen.width - 228), Mathf.Clamp(menuPosition.y, 8, Screen.height - 150), 220, selected != null && selected.CanChop ? 130 : 100);

        void OnGUI()
        {
            if (menuOpen && selected != null)
            {
                Rect r = GetMenuRect();
                GUI.Box(r, selected.DisplayName);
                if (GUI.Button(new Rect(r.x + 10, r.y + 30, r.width - 20, 30), "Inspect"))
                {
                    message = selected.Description;
                    messageUntil = Time.time + 4f;
                    menuOpen = false;
                }
                if (selected.CanChop && GUI.Button(new Rect(r.x + 10, r.y + 68, r.width - 20, 30), "Chop Down"))
                {
                    message = "Requires an Axe";
                    messageUntil = Time.time + 3f;
                    menuOpen = false;
                }
            }

            if (Time.time < messageUntil && !string.IsNullOrEmpty(message))
            {
                float width = 360f;
                GUI.Box(new Rect((Screen.width - width) * .5f, Screen.height - 90f, width, 42f), message);
            }
        }
    }
}