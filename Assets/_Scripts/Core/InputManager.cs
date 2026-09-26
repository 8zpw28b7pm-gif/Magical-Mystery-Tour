using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RF.Core
{
    public class InputManager : MonoBehaviour
    {
        private PlayerControls inputActions;

        public Vector2 inputVector { get; private set; }

        public InputAction aimAction;
        public InputAction lookAction;

        public event Action onJump;
        public event Action onAimStart;
        public event Action onAimStop;

        private void Awake()
        {
            inputActions = new PlayerControls();
        }

        private void OnEnable()
        {
            inputActions.Enable();

            aimAction = inputActions.Player.Aim;
            lookAction = inputActions.Player.Look;

            inputActions.Player.Jump.performed += OnJump;
            inputActions.Player.Aim.started += Aim;
            inputActions.Player.Aim.canceled += Aim;
        }

        private void OnDisable()
        {
            inputActions.Player.Jump.performed -= OnJump;
            inputActions.Player.Aim.started -= Aim;
            inputActions.Player.Aim.canceled -= Aim;

            inputActions.Disable();
        }

        private void OnJump(InputAction.CallbackContext context)
        {
            onJump?.Invoke();
        }

        private void Aim(InputAction.CallbackContext context)
        {
            if (context.started)
            {
                print("Started");
                onAimStart?.Invoke();
            }
            else if (context.canceled)
            {
                print("Canceled");
                onAimStop?.Invoke();
            }
        }

        public Vector2 GetInputVector()
        {
            return inputActions.Player.Move.ReadValue<Vector2>();
        }
    }
}