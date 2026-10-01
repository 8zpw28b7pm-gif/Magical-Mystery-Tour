using RF.Control;
using StarterAssets;
using UnityEngine;
using UnityEngine.TextCore.Text;

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
            UpdateVerticalValues(characterController);

            if (_groundSensor.IsGrounded)
            {
                ResetJumpTrigger();
            }
            animator.SetBool("IsGrounded", _groundSensor.IsGrounded);
        }

        public void TriggerJumpAnimation()
        {
            animator.SetBool("HasJumped", true);
            animator.SetTrigger("Jump");
        }

        public void ResetJumpTrigger()
        {
            animator.ResetTrigger("Jump");
            animator.SetBool("HasJumped", false);
        }

        public void UpdateAnimatorMovementValues(CharacterController controller)
        {
            Vector3 velocity = controller.velocity;
            velocity.y = 0;
            Vector3 inverseVelocity = transform.InverseTransformDirection(velocity);
            float forwardsSpeed = Mathf.Abs(inverseVelocity.magnitude);
            animator.SetFloat("ForwardSpeed", forwardsSpeed, 0.1f, Time.deltaTime);
        }

        public void UpdateVerticalValues(CharacterController controller)
        {
            animator.SetFloat("VerticalSpeed", controller.velocity.y, 0.1f, Time.deltaTime);
        }
    }
}