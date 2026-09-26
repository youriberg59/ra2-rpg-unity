using UnityEngine;

namespace RA2RPG.Player
{
    public sealed class HeroClickMover : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 4.5f;
        [SerializeField] private float stopDistance = 0.01f;

        private Vector3 target;
        private bool hasTarget;
        private GameObject targetMarker;
        private GameObject heroPositionMarker;

        public Vector3 Target => target;
        public bool HasTarget => hasTarget;

        private void Awake()
        {
            target = transform.position;
            CreateHeroPositionMarker();
        }

        private void Update()
        {
            ReadPointer();

            if (!hasTarget)
                return;

            Vector3 next = Vector3.MoveTowards(
                transform.position,
                target,
                moveSpeed * Time.deltaTime
            );

            transform.position = next;

            if ((transform.position - target).sqrMagnitude <= stopDistance * stopDistance)
            {
                transform.position = target;
                hasTarget = false;

                Debug.Log(
                    $"Hero arrived. Hero={transform.position:F3} Target={target:F3} " +
                    $"Delta={(transform.position - target):F3}"
                );
            }
        }

        private void ReadPointer()
        {
            if (Camera.main == null)
                return;

            bool moveClick =
                Input.GetMouseButtonDown(0) ||
                Input.GetMouseButtonDown(1);

            if (!moveClick || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                return;

            Vector3 mouse = Input.mousePosition;
            mouse.z = -Camera.main.transform.position.z;

            Vector3 world = Camera.main.ScreenToWorldPoint(mouse);
            world.z = transform.position.z;

            SetDestination(world);
        }

        public void SetDestination(Vector3 worldPosition)
        {
            worldPosition.z = transform.position.z;
            target = worldPosition;
            hasTarget = true;
            UpdateTargetMarker();
        }

        private void CreateHeroPositionMarker()
        {
            heroPositionMarker = GameObject.CreatePrimitive(PrimitiveType.Quad);
            heroPositionMarker.name = "HeroPositionMarker";
            heroPositionMarker.transform.SetParent(transform, false);
            heroPositionMarker.transform.localPosition = new Vector3(0f, 0f, -0.2f);
            heroPositionMarker.transform.localScale = new Vector3(0.12f, 0.12f, 1f);

            var renderer = heroPositionMarker.GetComponent<MeshRenderer>();
            renderer.material = new Material(Shader.Find("Sprites/Default"));
            renderer.material.color = new Color(0.1f, 0.45f, 1f, 0.9f);
        }

        private void UpdateTargetMarker()
        {
            if (targetMarker == null)
            {
                targetMarker = GameObject.CreatePrimitive(PrimitiveType.Quad);
                targetMarker.name = "ClickTargetMarker";
                targetMarker.transform.localScale = new Vector3(0.18f, 0.18f, 1f);

                var renderer = targetMarker.GetComponent<MeshRenderer>();
                renderer.material = new Material(Shader.Find("Sprites/Default"));
                renderer.material.color = new Color(1f, 0.15f, 0.15f, 0.85f);


            }

            targetMarker.transform.position = new Vector3(
                target.x,
                target.y,
                transform.position.z - 0.1f
            );
        }
    }
}
