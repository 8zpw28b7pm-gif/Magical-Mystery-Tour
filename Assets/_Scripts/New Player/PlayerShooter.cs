using RF.Core;
using Unity.Cinemachine;
using UnityEngine;

namespace RF.Control
{
    public class PlayerShooter : MonoBehaviour
    {
        [SerializeField] private CinemachineImpulseSource impulseSource;
        [SerializeField] private float shakeStrength = 0.2f;

        [SerializeField] private LayerMask aimMask;
        [SerializeField] private float aimFollowSpeed = 20f;

        [SerializeField] private Transform debugAimTarget;

        public void HandleShoot(InputHandler input)
        {
            if (input.fire)
            {
                Debug.Log("Shoot");

                if (Physics.Raycast(GetAimRay(), out RaycastHit hit, 999f, aimMask))
                {
                    if (hit.collider.TryGetComponent<Rigidbody>(out Rigidbody hitRB))
                    {
                        Vector3 forceDir = (hitRB.position - hit.point).normalized;
                        hitRB.AddForce(forceDir * 20, ForceMode.Impulse);
                    }
                }

                ApplyCameraShake();

                input.fire = false;
            }
        }

        public void HandleAim(InputHandler input)
        {
            Vector3 aimPoint = Physics.Raycast(GetAimRay(), out RaycastHit hit, 999f, aimMask) ? hit.point : GetAimRay().GetPoint(999f);

            if (hit.collider != null)
            {
                debugAimTarget.gameObject.SetActive(true);
                debugAimTarget.position = hit.point;
            }
            else
            {
                debugAimTarget.gameObject.SetActive(false);
            }

            if (input.aim)
            {
                Vector3 aimDirection = aimPoint - transform.position;
                aimDirection.y = 0f;

                if (aimDirection.sqrMagnitude < 0.0001f) return;

                Quaternion targetRotation = Quaternion.LookRotation(aimDirection);

                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * aimFollowSpeed);
            }
        }

        private Ray GetAimRay()
        {
            Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);

            Ray ray = Camera.main.ScreenPointToRay(screenCenter);

            return ray;
        }

        public void ApplyCameraShake()
        {
            impulseSource.GenerateImpulseWithForce(shakeStrength);
        }
    }
}
