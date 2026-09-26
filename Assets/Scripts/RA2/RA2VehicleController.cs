using UnityEngine;

namespace RA2RPG.RA2
{
    /// <summary>
    /// Lightweight top-down/isometric vehicle controller for the current RPG demo.
    /// Gameplay coordinates stay on the Unity XY plane; the imported VXL visual
    /// remains a child and keeps its own isometric presentation transform.
    /// </summary>
    public sealed class RA2VehicleController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 3.2f;
        [SerializeField] private float turnSpeedDegrees = 240f;
        [SerializeField] private float stopDistance = 0.015f;

        [Header("Turret")]
        [SerializeField] private float turretTurnSpeedDegrees = 360f;

        [Header("Imported hierarchy")]
        [SerializeField] private Transform heading;
        [SerializeField] private Transform visual;
        [SerializeField] private Transform turret;
        [SerializeField] private Transform barrel;

        private Vector3 target;
        private bool hasTarget;
        private bool isSelected;

        private Quaternion headingBaseRotation;
        private Quaternion turretBaseRotation;
        private Quaternion barrelBaseRotation;

        public Vector3 Target => target;
        public bool HasTarget => hasTarget;
        public bool IsSelected => isSelected;

        private void Awake()
        {
            target = transform.position;

            ResolveImportedHierarchy();

            if (heading != null)
                headingBaseRotation = heading.localRotation;

            if (turret != null)
                turretBaseRotation = turret.localRotation;

            if (barrel != null)
                barrelBaseRotation = barrel.localRotation;
        }

        private void Start()
        {
            RA2VehicleSelectionManager.EnsureExists();
        }

        private void Update()
        {
            MoveVehicle();

            if (isSelected)
                AimTurretAtMouse();
        }

        private void ResolveImportedHierarchy()
        {
            if (visual == null)
                visual = transform.Find("Visual");

            if (visual != null && heading == null)
                heading = visual.Find("Heading");

            if (heading != null && turret == null)
                turret = heading.Find("Turret");

            if (heading != null && barrel == null)
                barrel = heading.Find("Barrel");
        }

        private void MoveVehicle()
        {
            if (!hasTarget)
                return;

            Vector3 delta = target - transform.position;
            delta.z = 0f;

            if (delta.sqrMagnitude <= stopDistance * stopDistance)
            {
                transform.position = target;
                hasTarget = false;
                return;
            }

            Vector3 direction = delta.normalized;

            // Heading lives BELOW the fixed isometric presentation.
            // Rotating around local Y now turns only the vehicle on its own
            // vertical axis without rotating the isometric plane itself.
            if (heading != null)
            {
                float angle = Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg;
                Quaternion desired =
                    headingBaseRotation *
                    Quaternion.AngleAxis(angle, Vector3.up);

                heading.localRotation = Quaternion.RotateTowards(
                    heading.localRotation,
                    desired,
                    turnSpeedDegrees * Time.deltaTime
                );
            }

            transform.position = Vector3.MoveTowards(
                transform.position,
                target,
                moveSpeed * Time.deltaTime
            );
        }

        private void AimTurretAtMouse()
        {
            if (turret == null)
                return;

            Camera camera = Camera.main;
            if (camera == null)
                return;

            Vector3 mouse = Input.mousePosition;
            mouse.z = -camera.transform.position.z;

            Vector3 world = camera.ScreenToWorldPoint(mouse);
            Vector3 delta = world - transform.position;
            delta.z = 0f;

            if (delta.sqrMagnitude < 0.000001f)
                return;

            float worldAngle = Mathf.Atan2(delta.x, delta.y) * Mathf.Rad2Deg;
            float headingAngle = heading != null
                ? heading.localEulerAngles.y
                : 0f;
            float localAim = Mathf.DeltaAngle(headingAngle, worldAngle);

            // RA2 voxel forward is +X, so localAim maps directly to yaw
            // around the voxel model's vertical axis.
            Quaternion desiredTurret =
                turretBaseRotation *
                Quaternion.AngleAxis(localAim, Vector3.up);

            turret.localRotation = Quaternion.RotateTowards(
                turret.localRotation,
                desiredTurret,
                turretTurnSpeedDegrees * Time.deltaTime
            );

            if (barrel != null)
            {
                Quaternion desiredBarrel =
                    barrelBaseRotation *
                    Quaternion.AngleAxis(localAim, Vector3.up);

                barrel.localRotation = Quaternion.RotateTowards(
                    barrel.localRotation,
                    desiredBarrel,
                    turretTurnSpeedDegrees * Time.deltaTime
                );
            }
        }

        public void SetSelected(bool selected)
        {
            isSelected = selected;
        }

        public void SetDestination(Vector3 worldPosition)
        {
            worldPosition.z = transform.position.z;
            target = worldPosition;
            hasTarget = true;
        }
    }
}
