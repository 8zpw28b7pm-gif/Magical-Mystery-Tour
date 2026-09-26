using UnityEngine;

public class TimedDestroy : MonoBehaviour
{
    [SerializeField] private float destroyTime = 1f;

    private void OnEnable()
    {
        Destroy(gameObject, destroyTime);
    }
}
