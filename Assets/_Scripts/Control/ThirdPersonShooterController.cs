using Unity.Cinemachine;
using UnityEngine;
using StarterAssets;
using RF.Control;


public class ThirdPersonShooterController : MonoBehaviour
{

    [SerializeField] private CinemachineCamera aimVirtualCamera;
    [SerializeField] private float normalSensitivity;
    [SerializeField] private float aimSensitivity;
    [SerializeField] private float faceAimDirSpeed;
    [SerializeField] private LayerMask aimColliderMask;

    [SerializeField] private Projectile projectilePrefab;
    [SerializeField] private Transform projectileSpawnPoint;

    Camera mainCamera;

    private ThirdPersonController thirdPersonController;
    private StarterAssetsInputs starterAssetsInputs;

    private void Awake()
    {
        thirdPersonController = GetComponent<ThirdPersonController>();
        starterAssetsInputs = GetComponent<StarterAssetsInputs>();

        mainCamera = Camera.main;
    }

    private void Update()
    {
        if (mainCamera == null)
            return;

        Aim();
        Shoot();
    }

    private void Aim()
    {
        bool isAiming = starterAssetsInputs.aim;

        if (aimVirtualCamera.gameObject.activeSelf != isAiming)
            aimVirtualCamera.gameObject.SetActive(isAiming);

        thirdPersonController.SetSensitivity(
            isAiming ? aimSensitivity : normalSensitivity);

        thirdPersonController.SetRotateOnMove(!isAiming);

        Ray ray = mainCamera.ViewportPointToRay(
            new Vector3(0.5f, 0.5f, 0f));

        bool hasHit = Physics.Raycast(
            ray,
            out RaycastHit hit,
            999f,
            aimColliderMask,
            QueryTriggerInteraction.Ignore);

        Vector3 aimPoint = hasHit
            ? hit.point
            : ray.GetPoint(999f);


        if (!isAiming)
            return;

        // With no hit, face along the camera ray.
        // With a hit, face toward that point from the character.
        Vector3 aimDirection = hasHit
            ? aimPoint - transform.position
            : ray.direction;

        aimDirection.y = 0f;

        // A hit directly above/below the character has no useful yaw.
        if (aimDirection.sqrMagnitude < 0.0001f)
        {
            aimDirection = ray.direction;
            aimDirection.y = 0f;
        }

        // Looking exactly vertically has no horizontal direction.
        if (aimDirection.sqrMagnitude < 0.0001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(
            aimDirection.normalized,
            Vector3.up);

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            faceAimDirSpeed * Time.deltaTime);

    }

    private void Shoot()
    {
        if (starterAssetsInputs.shoot)
        {
            starterAssetsInputs.ShootInput(false);

            Projectile spawnedProjectil = Instantiate(projectilePrefab, projectileSpawnPoint.position, Quaternion.identity);
            // spawnedProjectil.Init()
        }
    }
}

