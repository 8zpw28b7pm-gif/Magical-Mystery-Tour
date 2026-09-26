using UnityEngine;
using UnityPipeline.Microsoft.CodeAnalysis.Emit;

namespace RF.Control
{
    public class Projectile : MonoBehaviour
    {
        [SerializeField] private float speed;
        private Vector3 moveDir;

        Rigidbody rb;

        private void Awake()
        {
            if (rb == null)
            {
                rb = GetComponent<Rigidbody>();
            }
        }

        public void Init(Vector3 direction)
        {
            transform.forward = direction;
        }

        private void Update()
        {
            transform.Translate(transform.forward * speed * Time.deltaTime);
        }

        private void OnTriggerEnter(Collider other)
        {
            Debug.Log("Hit! " + other.gameObject.name);
        }
    }
}
