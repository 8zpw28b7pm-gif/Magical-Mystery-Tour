using RF.Control;
using UnityEditor.Callbacks;
using UnityEngine;

namespace RF.Movement
{
    public class Mover : MonoBehaviour
    {

        [Header("Movement")]
        [SerializeField] private float groundSpeed = 5f;
        [SerializeField] private float acceleration;
        [SerializeField, Range(0, 1)] private float groundDecay = 0.9f;
        [SerializeField] private float rotationSpeed = 12f;

        [Header("Jumping")]
        [SerializeField] private float jumpSpeed = 5f;

        [Header("Jumping")]
        [SerializeField] private GroundSensor groundSensor;

        [Header("Visual")]
        [SerializeField] private Transform visualTransform;


        public void MoveWithInput(Vector3 moveDirection, Rigidbody body)
        {
            moveDirection.y = 0f;
            moveDirection = Vector3.ClampMagnitude(moveDirection, 1f);

            if (moveDirection.sqrMagnitude > 0f)
            {
                Vector3 velocity = body.linearVelocity;
                Vector3 horizontalVelocity = new Vector3(velocity.x, 0f, velocity.z);

                horizontalVelocity += moveDirection * acceleration;
                horizontalVelocity = Vector3.ClampMagnitude(horizontalVelocity, groundSpeed);

                body.linearVelocity = new Vector3(
                    horizontalVelocity.x,
                    velocity.y,
                    horizontalVelocity.z
                );
            }
        }

        public void HandleJump(Rigidbody body)
        {
            if (groundSensor.IsGrounded())
            {
                Vector3 velocity = body.linearVelocity;
                body.linearVelocity = new Vector3(velocity.x, jumpSpeed, velocity.z);
            }
        }

        public void ApplyFriction(Vector3 moveDirection, Rigidbody body)
        {
            moveDirection.y = 0f;

            if (groundSensor.IsGrounded() &&
                moveDirection.sqrMagnitude == 0f &&
                body.linearVelocity.y <= 0f)
            {
                Vector3 velocity = body.linearVelocity;

                body.linearVelocity = new Vector3(
                    velocity.x * groundDecay,
                    velocity.y,
                    velocity.z * groundDecay
                );
            }
        }

        public void FaceDirection(Vector3 moveDirection)
        {
            if (moveDirection == Vector3.zero) return;

            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
        }
    }
}