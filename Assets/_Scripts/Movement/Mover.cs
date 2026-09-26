using RF.Control;
using UnityEditor.Callbacks;
using UnityEngine;

namespace RF.Movement
{
    public class Mover : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CharacterCore core;

        [Header("Movement")]
        [SerializeField] private float groundSpeed = 5f;
        [SerializeField] private float acceleration;
        [SerializeField, Range(0, 1)] private float groundDecay = 0.9f;
        [SerializeField] private float rotationSpeed = 12f;

        [Header("Jumping and Gravity")]
        [SerializeField] private float jumpSpeed = 5f;
        [SerializeField] private float gravity = -9.81f;
        [SerializeField] private float gravityMultiplier = 3f;

        [SerializeField] private float verticalVelocity;

        public void Move()
        {
            Vector3 velocity = core.moveDirection * groundSpeed;
            velocity.y = verticalVelocity;

            core.CharacterController.Move(velocity * Time.deltaTime);
        }

        public void Jump()
        {
            if (core.GroundSensor.IsGrounded() && verticalVelocity < 0)
            {
                verticalVelocity = jumpSpeed;
            }
        }

        public void ApplyGravity()
        {
            if (core.GroundSensor.IsGrounded() && verticalVelocity < 0f)
            {
                verticalVelocity = -2f;
            }

            verticalVelocity += gravity * gravityMultiplier * Time.deltaTime;
        }

        public void FaceDirection()
        {
            Vector3 targetDirection = core.moveDirection;
            targetDirection.y = 0f;

            if (targetDirection == Vector3.zero) return;

            Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        public void FaceAimingDirection()
        {
            Vector3 direction = Camera.main.transform.forward;
            direction.y = 0f;

            if (direction.sqrMagnitude < 0.001f) return;

            Quaternion targetRotation = Quaternion.LookRotation(direction);

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime);
        }
    }
}