using UnityEngine;
using UnityEngine.InputSystem;

namespace Dregfall
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class DregfallPlayerController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] float walkSpeed = 4.2f;
        [SerializeField] float sprintSpeed = 7.0f;
        [SerializeField] float acceleration = 16f;
        [SerializeField] float rotationSharpness = 14f;
        [SerializeField] float gravity = -25f;

        CharacterController controller;
        Transform cameraTransform;
        Vector3 planarVelocity;
        float verticalVelocity;

        public float Speed01 { get; private set; }
        public float CurrentPlanarSpeed => planarVelocity.magnitude;
        public bool IsSprinting { get; private set; }

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            cameraTransform = Camera.main != null ? Camera.main.transform : null;
        }

        void Update()
        {
            if (Keyboard.current == null) return;
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;

            Vector2 input = Vector2.zero;
            if (Keyboard.current.wKey.isPressed) input.y += 1;
            if (Keyboard.current.sKey.isPressed) input.y -= 1;
            if (Keyboard.current.dKey.isPressed) input.x += 1;
            if (Keyboard.current.aKey.isPressed) input.x -= 1;
            input = Vector2.ClampMagnitude(input, 1f);

            Vector3 forward = cameraTransform != null ? cameraTransform.forward : Vector3.forward;
            Vector3 right = cameraTransform != null ? cameraTransform.right : Vector3.right;
            forward.y = 0; right.y = 0;
            forward.Normalize(); right.Normalize();

            Vector3 desiredDirection = (forward * input.y + right * input.x).normalized;
            IsSprinting = input.sqrMagnitude > 0.01f && (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);
            float targetSpeed = IsSprinting ? sprintSpeed : walkSpeed;
            Vector3 desiredVelocity = desiredDirection * targetSpeed;
            planarVelocity = Vector3.MoveTowards(planarVelocity, desiredVelocity, acceleration * Time.deltaTime);

            if (desiredDirection.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(desiredDirection, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 1f - Mathf.Exp(-rotationSharpness * Time.deltaTime));
            }

            if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
            else verticalVelocity += gravity * Time.deltaTime;

            controller.Move((planarVelocity + Vector3.up * verticalVelocity) * Time.deltaTime);
            Speed01 = Mathf.Clamp01(planarVelocity.magnitude / sprintSpeed);
        }
    }
}