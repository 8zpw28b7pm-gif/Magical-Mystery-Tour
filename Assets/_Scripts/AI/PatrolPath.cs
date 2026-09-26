using System.Collections.Generic;
using UnityEngine;

namespace RF.AI
{
    public class PatrolPath : MonoBehaviour
    {
        [SerializeField] private float waypointGizmoRadius = 0.1f;

        [SerializeField] private bool drawGizmos = true;

        private void OnDrawGizmos()
        {
            if (!drawGizmos) return;

            for (int i = 0; i < transform.childCount; i++)
            {
                int j = GetNextIndex(i);
                Gizmos.DrawSphere(transform.GetChild(i).position, waypointGizmoRadius);
                Gizmos.DrawLine(GetWaypoint(i), GetWaypoint(j));
            }
        }

        public int GetNextIndex(int i)
        {
            if (i + 1 == transform.childCount)
            {
                return 0;
            }

            return i + 1;
        }

        public Vector3 GetWaypoint(int i)
        {
            return transform.GetChild(i).position;
        }
    }
}
