using RF.Animation;
using RF.Control;
using RF.Core;
using UnityEngine;

namespace RF.Movement
{
    public class PlayerMotor : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float MoveSpeed = 2.0f;
        [SerializeField] float SprintSpeed = 5.335f;
        [SerializeField][Range(0.0f, 0.3f)] private float RotationSmoothTime = 0.12f;
        [SerializeField] float SpeedChangeRate = 10.0f;

        [SerializeField] private GroundSensor groundSensor;
        // [SerializeField] private float groundedOffset = 0f;
        // [SerializeField] private float groundedRadius = 0.2f;
        // [SerializeField] private LayerMask groundLayers;
        // [SerializeField] private bool isGrounded = true;

        [Header("Jumping and Gravity")]
        [SerializeField] private float fallTimeout = 0.15f;
        [SerializeField] private float gravity = -15.0f;
        [SerializeField] private float jumpHeight = 1.2f;
        [SerializeField] private float jumpTimeout = 0.50f;


        private float _speed;
        private float _targetRotation = 0.0f;
        private float _rotationVelocity;
        private float _verticalVelocity;
        private float _terminalVelocity = 53.0f;
        private float _jumpTimeoutDelta;
        private float _fallTimeoutDelta;

        private Camera _mainCamera;

        private void Awake()
        {
            _mainCamera = Camera.main;
        }

        public void Move(InputHandler input, CharacterController characterController)
        {
            float targetSpeed = input.sprint ? SprintSpeed : MoveSpeed;

            if (input.move == Vector2.zero) targetSpeed = 0.0f;

            float currentHorizontalSpeed = new Vector3(characterController.velocity.x, 0.0f, characterController.velocity.z).magnitude;

            float speedOffset = 0.1f;
            float inputMagnitude = 1f;

            // accelerate or decelerate to target speed
            if (currentHorizontalSpeed < targetSpeed - speedOffset || currentHorizontalSpeed > targetSpeed + speedOffset)
            {
                _speed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed * inputMagnitude, Time.deltaTime * SpeedChangeRate);

                _speed = Mathf.Round(_speed * 1000f) / 1000f;
            }
            else
            {
                _speed = targetSpeed;
            }

            // normalise input direction
            Vector3 inputDirection = new Vector3(input.move.x, 0.0f, input.move.y).normalized;

            // if there is a move input rotate player when the player is moving
            if (input.move != Vector2.zero)
            {
                _targetRotation = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg + _mainCamera.transform.eulerAngles.y;
                float rotation = Mathf.SmoothDampAngle(transform.eulerAngles.y, _targetRotation, ref _rotationVelocity, RotationSmoothTime);

                if (!input.aim)
                {
                    // rotate to face input direction relative to camera position
                    transform.rotation = Quaternion.Euler(0.0f, rotation, 0.0f);
                }
            }

            Vector3 targetDirection = Quaternion.Euler(0.0f, _targetRotation, 0.0f) * Vector3.forward;

            // move the player
            characterController.Move(targetDirection.normalized * (_speed * Time.deltaTime) + new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime);
        }

        public void JumpAndGravity(InputHandler input)
        {
            if (groundSensor.IsGrounded)
            {
                _fallTimeoutDelta = fallTimeout;

                // stop our velocity dropping infinitely when grounded
                if (_verticalVelocity < 0.0f)
                {
                    _verticalVelocity = -2f;
                }

                // Jump
                if (input.jump && _jumpTimeoutDelta <= 0.0f)
                {
                    _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                    GetComponent<PlayerAnimationManager>().TriggerJumpAnimation();
                }

                // jump timeout
                if (_jumpTimeoutDelta >= 0.0f)
                {
                    _jumpTimeoutDelta -= Time.deltaTime;
                }

            }
            else
            {
                if (GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).IsName("Jump"))
                {
                    GetComponent<PlayerAnimationManager>().ResetJumpTrigger();
                }
                // reset the jump timeout timer
                _jumpTimeoutDelta = jumpTimeout;

                // fall timeout
                if (_fallTimeoutDelta >= 0.0f)
                {
                    _fallTimeoutDelta -= Time.deltaTime;
                }

                // if we are not grounded, do not jump
                input.jump = false;
            }

            // apply gravity over time if under terminal (multiply by delta time twice to linearly speed up over time)
            if (_verticalVelocity < _terminalVelocity)
            {
                _verticalVelocity += gravity * Time.deltaTime;
            }
        }
    }
}