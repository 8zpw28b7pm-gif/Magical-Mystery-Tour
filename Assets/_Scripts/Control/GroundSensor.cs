using UnityEngine;

namespace RF.Control
{
    public class GroundSensor : MonoBehaviour
    {
        [SerializeField] private bool isGrounded;
        public bool IsGrounded() => isGrounded;

        private void OnTriggerStay(Collider other)
        {
            isGrounded = true;
        }

        private void OnTriggerExit(Collider other)
        {
            isGrounded = false;
        }
    }
}