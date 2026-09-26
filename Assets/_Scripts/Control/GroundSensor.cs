using UnityEngine;

namespace RF.Control
{
    public class GroundSensor : MonoBehaviour
    {
        [SerializeField] private CharacterCore core;

        private void Awake()
        {
            if (core == null)
            {
                core = GetComponentInParent<CharacterCore>();
            }
        }


        public bool IsGrounded()
        {
            return core.CharacterController.isGrounded;
        }
    }
}