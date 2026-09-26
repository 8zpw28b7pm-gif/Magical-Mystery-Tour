using RF.Core;
using RF.Movement;
using UnityEngine;
using UnityEngine.Video;

namespace RF.Control
{
    public class PlayerController : CharacterCore
    {
        [SerializeField] private InputManager inputManager;
        [SerializeField] private PlayerAimController aimController;
        
        [SerializeField] private GameObject crosshair;


        public float horizontalInput;
        public float verticalInput;

        public bool isGrounded;

        private Camera mainCamera;

        Vector3 aimDirection;
        public bool isAiming;

        private void Awake()
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;

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
            isAiming = inputManager.aimAction.IsPressed();
            crosshair.SetActive(isAiming);

            SetMoveDirection();

            mover.ApplyGravity();
            mover.Move();

         
        }

        private void SetMoveDirection()
        {
            Vector2 input = inputManager.GetInputVector();
            verticalInput = input.y;
            horizontalInput = input.x;

            Vector3 forward = mainCamera.transform.forward;
            Vector3 right = mainCamera.transform.right;

            forward.y = 0;
            right.y = 0;

            forward.Normalize();
            right.Normalize();

            moveDirection = (forward * verticalInput) + (right * horizontalInput);
            moveDirection = moveDirection.normalized;
        }

        private void SetLookDirection()
        {
            Vector2 lookVector = inputManager.lookAction.ReadValue<Vector2>();

            aimDirection = new Vector3(0, lookVector.x, 0);
        }
        private void HandleJump()
        {
            if (!groundSensor.IsGrounded()) return;

            mover.Jump();
        }

        private void HandleAim()
        {

        }
    }
}
