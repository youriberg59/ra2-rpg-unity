using UnityEngine;

namespace RA2RPG.RA2
{
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(RA2InfantryAnimationData))]
    public sealed class RA2GenericInfantryAnimator : MonoBehaviour
    {
        public Sprite[] Frames;
        public float WalkFramesPerSecond = 10f;
        public float MovementThreshold = 0.0005f;

        private SpriteRenderer spriteRenderer;
        private RA2InfantryAnimationData data;
        private Vector3 previousPosition;
        private int facing;
        private float animationTime;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            data = GetComponent<RA2InfantryAnimationData>();
            previousPosition = transform.position;
            ApplyReady();
        }

        private void OnEnable()
        {
            previousPosition = transform.position;
        }

        private void LateUpdate()
        {
            Vector3 delta = transform.position - previousPosition;
            previousPosition = transform.position;

            bool moving = delta.sqrMagnitude > MovementThreshold * MovementThreshold;

            if (!moving)
            {
                animationTime = 0f;
                ApplyReady();
                return;
            }

            facing = DirectionToFacing(delta);

            if (!data.Walk.IsValid)
            {
                ApplyReady();
                return;
            }

            animationTime += Time.deltaTime;
            int local = Mathf.FloorToInt(animationTime * WalkFramesPerSecond)
                % data.Walk.Frames;

            int index =
                data.Walk.Start +
                facing * data.Walk.FacingStride +
                local;

            SetFrame(index);
        }

        private void ApplyReady()
        {
            if (!data.Ready.IsValid)
                return;

            int index =
                data.Ready.Start +
                facing * data.Ready.FacingStride;

            SetFrame(index);
        }

        private int DirectionToFacing(Vector3 direction)
        {
            // RA2 SHP facings are mirrored horizontally relative to Unity XY.
            float angle = Mathf.Atan2(-direction.x, direction.y) * Mathf.Rad2Deg;
            if (angle < 0f)
                angle += 360f;

            return Mathf.RoundToInt(angle / 45f) % 8;
        }

        private void SetFrame(int index)
        {
            if (Frames == null || index < 0 || index >= Frames.Length)
                return;

            Sprite sprite = Frames[index];
            if (sprite != null)
                spriteRenderer.sprite = sprite;
        }
    }
}
