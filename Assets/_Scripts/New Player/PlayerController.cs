using RF.Animation;
using RF.Core;
using RF.Movement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RF.Control
{
    public class PlayerController : MonoBehaviour
    {

        private InputHandler _input;
        private PlayerInput _playerInput;
        private PlayerMotor _motor;
        private PlayerShooter _shooter;
        private PlayerCameraController _cameraController;
        private PlayerAnimationManager _animator;
        private GroundSensor _groundSensor;

        private CharacterController _controller;

        public bool IsGrounded => _groundSensor.IsGrounded;


        private bool IsCurrentDeviceMouse
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return _playerInput.currentControlScheme == "KeyboardMouse";
#else
                    return false;
#endif
            }
        }


        private void Awake()
        {
            _input = GetComponent<InputHandler>();
            _playerInput = GetComponent<PlayerInput>();
            _motor = GetComponent<PlayerMotor>();
            _shooter = GetComponent<PlayerShooter>();
            _cameraController = GetComponent<PlayerCameraController>();
            _animator = GetComponent<PlayerAnimationManager>();
            _groundSensor = GetComponentInChildren<GroundSensor>();

            _controller = GetComponent<CharacterController>();
        }

        private void Update()
        {
            _groundSensor.GroundedCheck();
            _motor.JumpAndGravity(_input);
            _motor.Move(_input, _controller);

            _shooter.HandleAim(_input);
            _shooter.HandleShoot(_input);

            _cameraController.HandleAimCamera(_input);
        }

        private void LateUpdate()
        {
            _cameraController.CameraRotation(_input, IsCurrentDeviceMouse);
        }
    }
}
