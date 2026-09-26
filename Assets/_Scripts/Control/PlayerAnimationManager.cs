using StarterAssets;
using UnityEngine;

namespace RF.Animation
{
    public class PlayerAnimationManager : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        ThirdPersonController thirdPersonController;

        CharacterController characterController;

        private void Awake()
        {
            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }

            thirdPersonController = GetComponent<ThirdPersonController>();
            characterController = GetComponent<CharacterController>();
        }

        private void Update()
        {
            UpdateAnimatorMovementValues(characterController);
        }

        public void UpdateAnimatorMovementValues(CharacterController controller)
        {
            Vector3 velocity = controller.velocity;
            Vector3 inverseVelocity = transform.InverseTransformDirection(velocity);
            float forwardsSpeed = Mathf.Abs(inverseVelocity.magnitude);
            animator.SetFloat("ForwardSpeed", forwardsSpeed, 0.1f, Time.deltaTime);
        }
    }
}