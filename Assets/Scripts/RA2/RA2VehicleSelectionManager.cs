using UnityEngine;

namespace RA2RPG.RA2
{
    /// <summary>
    /// Physics-free vehicle selection/command manager for the current XY demo.
    /// Left click selects a vehicle, right click moves the selected vehicle.
    /// </summary>
    public sealed class RA2VehicleSelectionManager : MonoBehaviour
    {
        private static RA2VehicleSelectionManager instance;

        [SerializeField] private float fallbackSelectionRadiusPixels = 55f;

        private RA2VehicleController selected;
        private GameObject indicator;
        private LineRenderer indicatorLine;

        public static RA2VehicleSelectionManager Instance => instance;
        public RA2VehicleController Selected => selected;

        public static void EnsureExists()
        {
            if (instance != null)
                return;

            var existing = FindFirstObjectByType<RA2VehicleSelectionManager>();
            if (existing != null)
            {
                instance = existing;
                return;
            }

            var go = new GameObject("RA2 Vehicle Selection Manager");
            instance = go.AddComponent<RA2VehicleSelectionManager>();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            CreateIndicator();
        }

        private void Update()
        {
            Camera camera = Camera.main;
            if (camera == null)
                return;

            if (Input.GetKeyDown(KeyCode.Escape))
                Select(null);

            if (Input.GetMouseButtonDown(0))
                SelectVehicleAtScreenPoint(camera, Input.mousePosition);

            if (Input.GetMouseButtonDown(1) && selected != null)
            {
                Vector3 mouse = Input.mousePosition;
                mouse.z = -camera.transform.position.z;

                Vector3 world = camera.ScreenToWorldPoint(mouse);
                world.z = selected.transform.position.z;
                selected.SetDestination(world);
            }

            UpdateIndicator();
        }

        private void SelectVehicleAtScreenPoint(Camera camera, Vector3 mouse)
        {
            var vehicles = FindObjectsByType<RA2VehicleController>(
                FindObjectsSortMode.None
            );

            RA2VehicleController best = null;
            float bestDistance = float.PositiveInfinity;

            foreach (var vehicle in vehicles)
            {
                if (vehicle == null || !vehicle.gameObject.activeInHierarchy)
                    continue;

                if (ScreenRectContains(vehicle, camera, mouse))
                {
                    float distance = (
                        camera.WorldToScreenPoint(vehicle.transform.position) - mouse
                    ).sqrMagnitude;

                    if (distance < bestDistance)
                    {
                        best = vehicle;
                        bestDistance = distance;
                    }

                    continue;
                }

                Vector3 screen = camera.WorldToScreenPoint(vehicle.transform.position);
                float fallback = (screen - mouse).sqrMagnitude;
                float threshold =
                    fallbackSelectionRadiusPixels * fallbackSelectionRadiusPixels;

                if (fallback <= threshold && fallback < bestDistance)
                {
                    best = vehicle;
                    bestDistance = fallback;
                }
            }

            Select(best);
        }

        private static bool ScreenRectContains(
            RA2VehicleController vehicle,
            Camera camera,
            Vector3 mouse
        )
        {
            var renderers = vehicle.GetComponentsInChildren<Renderer>();
            if (renderers == null || renderers.Length == 0)
                return false;

            bool initialized = false;
            Rect rect = default;

            foreach (var renderer in renderers)
            {
                if (renderer == null || !renderer.enabled)
                    continue;

                Bounds b = renderer.bounds;
                Vector3 min = b.min;
                Vector3 max = b.max;

                for (int ix = 0; ix < 2; ix++)
                for (int iy = 0; iy < 2; iy++)
                for (int iz = 0; iz < 2; iz++)
                {
                    Vector3 world = new Vector3(
                        ix == 0 ? min.x : max.x,
                        iy == 0 ? min.y : max.y,
                        iz == 0 ? min.z : max.z
                    );

                    Vector3 screen = camera.WorldToScreenPoint(world);
                    if (screen.z < 0f)
                        continue;

                    if (!initialized)
                    {
                        rect = new Rect(screen.x, screen.y, 0f, 0f);
                        initialized = true;
                    }
                    else
                    {
                        float xMin = Mathf.Min(rect.xMin, screen.x);
                        float xMax = Mathf.Max(rect.xMax, screen.x);
                        float yMin = Mathf.Min(rect.yMin, screen.y);
                        float yMax = Mathf.Max(rect.yMax, screen.y);

                        rect = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
                    }
                }
            }

            if (!initialized)
                return false;

            rect.xMin -= 8f;
            rect.xMax += 8f;
            rect.yMin -= 8f;
            rect.yMax += 8f;

            return rect.Contains(new Vector2(mouse.x, mouse.y));
        }

        private void Select(RA2VehicleController vehicle)
        {
            if (selected == vehicle)
                return;

            if (selected != null)
                selected.SetSelected(false);

            selected = vehicle;

            if (selected != null)
                selected.SetSelected(true);

            UpdateIndicator();
        }

        private void CreateIndicator()
        {
            indicator = new GameObject("VehicleSelectionIndicator");
            indicator.transform.SetParent(transform, false);

            indicatorLine = indicator.AddComponent<LineRenderer>();
            indicatorLine.useWorldSpace = true;
            indicatorLine.loop = true;
            indicatorLine.positionCount = 4;
            indicatorLine.widthMultiplier = 0.035f;
            indicatorLine.material = new Material(Shader.Find("Sprites/Default"));
            indicatorLine.material.color = new Color(0.2f, 1f, 0.25f, 0.95f);
            indicatorLine.enabled = false;
        }

        private void UpdateIndicator()
        {
            if (indicatorLine == null)
                return;

            if (selected == null)
            {
                indicatorLine.enabled = false;
                return;
            }

            indicatorLine.enabled = true;

            Vector3 c = selected.transform.position;
            float rx = 0.62f;
            float ry = 0.32f;
            float z = c.z - 0.15f;

            indicatorLine.SetPosition(0, new Vector3(c.x, c.y + ry, z));
            indicatorLine.SetPosition(1, new Vector3(c.x + rx, c.y, z));
            indicatorLine.SetPosition(2, new Vector3(c.x, c.y - ry, z));
            indicatorLine.SetPosition(3, new Vector3(c.x - rx, c.y, z));
        }
    }
}
