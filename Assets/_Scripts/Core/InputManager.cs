using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RF.Core
{
    public class InputManager : MonoBehaviour
    {
        private PlayerControls inputActions;

        public Vector2 inputVector { get; private set; }

        public event Action onJump;

        private void Awake()
        {
            inputActions = new PlayerControls();
        }

        private void OnEnable()
        {
            inputActions.Enable();

            inputActions.Player.Jump.performed += OnJump;
        }

        private void OnDisable()
        {
            inputActions.Player.Jump.performed -= OnJump;

            inputActions.Disable();
        }

        private void OnJump(InputAction.CallbackContext context)
        {
            onJump?.Invoke();
        }

        public Vector2 GetInputVector()
        {
            return inputActions.Player.Move.ReadValue<Vector2>();
        }
    }
}