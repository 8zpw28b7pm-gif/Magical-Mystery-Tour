using Unity.AppUI.Core;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.AI;

namespace RF.AI
{
    public class BirdController : MonoBehaviour
    {
        [SerializeField] private float waypointTolerance = 1f;
        [SerializeField] private PatrolPath patrolPath;
        [SerializeField] private float speed;
        [SerializeField] private float rotateSpeed = 10f;
        private NavMeshAgent agent;

        [SerializeField] private int currentWaypointIndex;


        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
        }

        private void Start()
        {
            agent.updateRotation = false;
        }

        private void Update()
        {
            MoveToWaypoint();
            FaceDirection();
        }

        private void FaceDirection()
        {
            Vector3 currentWaypoint = patrolPath.GetWaypoint(currentWaypointIndex);
            Vector3 targetDirection = (currentWaypoint - transform.position).normalized;
            Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
            
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotateSpeed * Time.deltaTime);
        }



        private void MoveToWaypoint()
        {
            agent.SetDestination(patrolPath.GetWaypoint(currentWaypointIndex));

            if (HasReachedWaypoint())
            {
                CycleWaypoints();
            }
        }

        private void CycleWaypoints()
        {
            currentWaypointIndex = patrolPath.GetNextIndex(currentWaypointIndex);
        }

        private bool HasReachedWaypoint()
        {
            return Vector3.Distance(transform.position, patrolPath.GetWaypoint(currentWaypointIndex)) < waypointTolerance;
        }
    }
}
