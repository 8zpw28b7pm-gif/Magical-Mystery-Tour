using RF.Core;
using Unity.Cinemachine;
using UnityEngine;

namespace RF.Control
{
    public class PlayerCameraController : MonoBehaviour
    {
        [SerializeField] private GameObject cinemachineCameraTarget;

        [SerializeField] private CinemachineCamera followCamera;
        [SerializeField] private CinemachineCamera aimCamera;

        [SerializeField] private float sensitivityX = 1f;
        [SerializeField] private float sensitivityY = 1f;
        [SerializeField] private bool lockCameraPosition = false;
        [SerializeField] private float cameraAngleOverride = 0.0f;
        [SerializeField] private float topClamp = 70.0f;
        [SerializeField] private float bottomClamp = -30.0f;
        
        private float _cinemachineTargetYaw;
        private float _cinemachineTargetPitch;


        private const float _threshold = 0.00f;

        public void CameraRotation(InputHandler input, bool isCurrentDeviceMouse)
        {
            // if there is an input and camera position is not fixed
            if (input.look.sqrMagnitude >= _threshold && !lockCameraPosition)
            {
                //Don't multiply mouse input by Time.deltaTime;
                float deltaTimeMultiplier = isCurrentDeviceMouse ? 1.0f : Time.deltaTime;

                _cinemachineTargetYaw += input.look.x * deltaTimeMultiplier * sensitivityX;
                _cinemachineTargetPitch += input.look.y * deltaTimeMultiplier * sensitivityY;
            }

            // clamp our rotations so our values are limited 360 degrees
            _cinemachineTargetYaw = ClampAngle(_cinemachineTargetYaw, float.MinValue, float.MaxValue);
            _cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, bottomClamp, topClamp);

            // Cinemachine will follow this target
            cinemachineCameraTarget.transform.rotation = Quaternion.Euler(_cinemachineTargetPitch + cameraAngleOverride, _cinemachineTargetYaw, 0.0f);
        }

        public void HandleAimCamera(InputHandler input)
        {
            aimCamera.gameObject.SetActive(input.aim);
        }

        private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
        {
            if (lfAngle < -360f) lfAngle += 360f;
            if (lfAngle > 360f) lfAngle -= 360f;
            return Mathf.Clamp(lfAngle, lfMin, lfMax);
        }
    }
}
