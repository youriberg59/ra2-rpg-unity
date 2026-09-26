using UnityEngine;

namespace RA2RPG.RA2
{
    /// <summary>
    /// Stores the fixed visual presentation transform for imported RA2 voxel vehicles.
    /// Gameplay heading is applied on a separate parent by RA2VehicleController.
    /// </summary>
    public sealed class RA2VehiclePresentation : MonoBehaviour
    {
        [Header("Fixed visual orientation")]
        [SerializeField] private Vector3 eulerAngles = new Vector3(35.264f, 45f, 0f);

        public Vector3 EulerAngles
        {
            get => eulerAngles;
            set
            {
                eulerAngles = value;
                Apply();
            }
        }

        private void Awake()
        {
            Apply();
        }

        private void OnValidate()
        {
            Apply();
        }

        public void Apply()
        {
            transform.localRotation = Quaternion.Euler(eulerAngles);
        }
    }
}
