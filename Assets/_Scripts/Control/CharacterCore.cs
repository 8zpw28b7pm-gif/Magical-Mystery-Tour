using RF.Movement;
using UnityEngine;

namespace RF.Control
{
    public abstract class CharacterCore : MonoBehaviour
    {
        [SerializeField] private protected Mover mover;
        [SerializeField] private CharacterController characterController;
        [SerializeField] private protected Rigidbody body;
        [SerializeField] private protected GroundSensor groundSensor;
        [SerializeField] private Transform visualTransform;

        public Vector3 moveDirection;
        
        public CharacterController CharacterController => characterController;
        public GroundSensor GroundSensor => groundSensor;
        public Transform VisualTransform => visualTransform;
    }
}