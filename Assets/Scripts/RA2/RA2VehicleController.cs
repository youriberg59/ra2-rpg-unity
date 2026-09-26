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
        [SerializeField] private Transform visual;
        [SerializeField] private Transform body;
        [SerializeField] private Transform turret;
        [SerializeField] private Transform barrel;

        private Vector3 target;
        private bool hasTarget;

        private Quaternion bodyBaseRotation;
        private Quaternion turretBaseRotation;
        private Quaternion barrelBaseRotation;

        public Vector3 Target => target;
        public bool HasTarget => hasTarget;

        private void Awake()
        {
            target = transform.position;

            ResolveImportedHierarchy();

            if (body != null)
                bodyBaseRotation = body.localRotation;

            if (turret != null)
                turretBaseRotation = turret.localRotation;

            if (barrel != null)
                barrelBaseRotation = barrel.localRotation;
        }

        private void Update()
        {
            ReadMoveClick();
            MoveVehicle();
            AimTurretAtMouse();
        }

        private void ResolveImportedHierarchy()
        {
            if (visual == null)
                visual = transform.Find("Visual");

            if (visual != null && body == null)
                body = visual.Find("Body");

            if (visual != null && turret == null)
                turret = visual.Find("Turret");

            if (visual != null && barrel == null)
                barrel = visual.Find("Barrel");
        }

        private void ReadMoveClick()
        {
            Camera camera = Camera.main;
            if (camera == null)
                return;

            bool click =
                Input.GetMouseButtonDown(0) ||
                Input.GetMouseButtonDown(1);

            if (!click ||
                Input.GetKey(KeyCode.LeftShift) ||
                Input.GetKey(KeyCode.RightShift))
                return;

            Vector3 mouse = Input.mousePosition;
            mouse.z = -camera.transform.position.z;

            Vector3 world = camera.ScreenToWorldPoint(mouse);
            world.z = transform.position.z;

            target = world;
            hasTarget = true;
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

            // The VXL model is a real 3D volume. Its vertical axis is Unity Y,
            // so heading must be a yaw around local Y. Rotating the whole
            // presentation around screen Z causes the model to roll/flip.
            if (body != null)
            {
                float heading = Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg;
                Quaternion desired =
                    bodyBaseRotation *
                    Quaternion.AngleAxis(heading, Vector3.up);

                body.localRotation = Quaternion.RotateTowards(
                    body.localRotation,
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

            float angle = Mathf.Atan2(delta.x, delta.y) * Mathf.Rad2Deg;

            // Turret/barrel yaw around their own vertical Y axis.
            Quaternion desiredTurret =
                turretBaseRotation *
                Quaternion.AngleAxis(angle, Vector3.up);

            turret.localRotation = Quaternion.RotateTowards(
                turret.localRotation,
                desiredTurret,
                turretTurnSpeedDegrees * Time.deltaTime
            );

            if (barrel != null)
            {
                Quaternion desiredBarrel =
                    barrelBaseRotation *
                    Quaternion.AngleAxis(angle, Vector3.up);

                barrel.localRotation = Quaternion.RotateTowards(
                    barrel.localRotation,
                    desiredBarrel,
                    turretTurnSpeedDegrees * Time.deltaTime
                );
            }
        }

        public void SetDestination(Vector3 worldPosition)
        {
            worldPosition.z = transform.position.z;
            target = worldPosition;
            hasTarget = true;
        }
    }
}
