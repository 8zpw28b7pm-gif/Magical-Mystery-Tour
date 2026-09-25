using System;
using RF.Core;
using RF.Movement;
using Unity.VisualScripting;
using UnityEngine;

namespace RF.Control
{
    public class PlayerController : MonoBehaviour
    {
        private Camera mainCamera;
        [SerializeField] private InputManager inputManager;
        [SerializeField] private Mover mover;

        [SerializeField] private GroundSensor groundSensor;

        [SerializeField] private Rigidbody body;

        private Vector3 moveDirection;
        private float horizontalInput;
        private float verticalInput;


        private void Awake()
        {
            if (mover == null)
            {
                mover = GetComponent<Mover>();
            }

            if (body == null)
            {
                body = GetComponent<Rigidbody>();
            }

            mainCamera = Camera.main;
        }

        private void OnEnable()
        {
            inputManager.onJump += HandleJump;
        }

        private void OnDisable()
        {
            inputManager.onJump -= HandleJump;
        }

        private void Update()
        {
            Vector2 input = inputManager.GetInputVector();

            Vector3 forward = mainCamera.transform.forward;
            Vector3 right = mainCamera.transform.right;

            forward.y = 0f;
            right.y = 0f;

            moveDirection = Vector3.ClampMagnitude(
                forward.normalized * input.y +
                right.normalized * input.x,
                1f
            );
        }

        private void FixedUpdate()
        {
            mover.MoveWithInput(moveDirection, body);
            mover.FaceDirection(moveDirection);
        }

        private void HandleJump()
        {
            if (!groundSensor.IsGrounded()) return;

            mover.HandleJump(body);
        }
    }
}
