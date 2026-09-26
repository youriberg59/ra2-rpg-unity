using UnityEngine;

namespace RA2RPG.CameraSystem
{
    public sealed class HeroCameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 0f, -10f);
        [SerializeField] private float followSharpness = 18f;

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            Snap();
        }

        private void LateUpdate()
        {
            if (target == null)
                return;

            Vector3 desired = target.position + offset;
            float t = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, desired, t);
        }

        public void Snap()
        {
            if (target != null)
                transform.position = target.position + offset;
        }
    }
}
