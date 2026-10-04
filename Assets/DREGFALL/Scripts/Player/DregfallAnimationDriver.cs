using UnityEngine;

namespace Dregfall
{
    [RequireComponent(typeof(DregfallPlayerController))]
    public sealed class DregfallAnimationDriver : MonoBehaviour
    {
        static readonly int MoveSpeedHash = Animator.StringToHash("MoveSpeed");

        [SerializeField] Animator animator;
        DregfallPlayerController player;

        public void SetAnimator(Animator target)
        {
            animator = target;
            if (animator != null) animator.applyRootMotion = false;
        }

        void Awake()
        {
            player = GetComponent<DregfallPlayerController>();
        }

        void Update()
        {
            if (animator == null || player == null) return;
            animator.SetFloat(MoveSpeedHash, player.CurrentPlanarSpeed, 0.12f, Time.deltaTime);
        }
    }
}
