using RF.Control;
using StarterAssets;
using UnityEngine;

namespace RF.Animation
{
    public class PlayerAnimationManager : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        private GroundSensor _groundSensor;
        ThirdPersonController thirdPersonController;

        CharacterController characterController;

        private void Awake()
        {
            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }

            _groundSensor = GetComponent<GroundSensor>();
            thirdPersonController = GetComponent<ThirdPersonController>();
            characterController = GetComponent<CharacterController>();
        }

        private void Update()
        {
            UpdateAnimatorMovementValues(characterController);

            animator.SetBool("IsGrounded", _groundSensor.IsGrounded);
        }

        public void TriggerJumpAnimation()
        {
            animator.SetTrigger("Jump");
        }

        public void ResetJumpTrigger()
        {
            animator.ResetTrigger("Jump");
        }

        public void UpdateAnimatorMovementValues(CharacterController controller)
        {
            Vector3 velocity = controller.velocity;
            velocity.y = 0;
            Vector3 inverseVelocity = transform.InverseTransformDirection(velocity);
            float forwardsSpeed = Mathf.Abs(inverseVelocity.magnitude);
            animator.SetFloat("ForwardSpeed", forwardsSpeed, 0.1f, Time.deltaTime);
        }
    }
}