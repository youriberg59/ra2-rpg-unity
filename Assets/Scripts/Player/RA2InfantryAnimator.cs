using UnityEngine;

namespace RA2RPG.Player
{
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(HeroClickMover))]
    public sealed class RA2InfantryAnimator : MonoBehaviour
    {
        [SerializeField] private Sprite[] frames;
        [SerializeField] private float walkFramesPerSecond = 10f;

        private SpriteRenderer spriteRenderer;
        private HeroClickMover mover;
        private int facing;
        private float animationTime;

        // ConSequence from retail RA2 art.ini:
        // Ready=0,1,1
        // Walk=8,6,6
        private const int FacingCount = 8;
        private const int ReadyStart = 0;
        private const int ReadyFramesPerFacing = 1;
        private const int WalkStart = 8;
        private const int WalkFramesPerFacing = 6;

        public void SetFrames(Sprite[] newFrames)
        {
            frames = newFrames;
            animationTime = 0f;

            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();

            ApplyReadyFrame();
        }

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            mover = GetComponent<HeroClickMover>();
        }

        private void Update()
        {
            if (frames == null || frames.Length == 0 || mover == null)
                return;

            Vector3 delta = mover.Target - transform.position;
            bool moving = mover.HasTarget && delta.sqrMagnitude > 0.0001f;

            if (!moving)
            {
                animationTime = 0f;
                ApplyReadyFrame();
                return;
            }

            facing = DirectionToFacing(delta);
            animationTime += Time.deltaTime;

            int localFrame = Mathf.FloorToInt(animationTime * walkFramesPerSecond)
                % WalkFramesPerFacing;

            int frameIndex =
                WalkStart +
                facing * WalkFramesPerFacing +
                localFrame;

            SetFrameSafe(frameIndex);
        }

        private void ApplyReadyFrame()
        {
            int frameIndex =
                ReadyStart +
                facing * ReadyFramesPerFacing;

            SetFrameSafe(frameIndex);
        }

        private int DirectionToFacing(Vector3 direction)
        {
            // Unity world: +X east, +Y north. Convert to clockwise 8-way facing.
            float angle = Mathf.Atan2(-direction.x, direction.y) * Mathf.Rad2Deg;
            if (angle < 0f)
                angle += 360f;

            return Mathf.RoundToInt(angle / 45f) % FacingCount;
        }

        private void SetFrameSafe(int index)
        {
            if (index >= 0 && index < frames.Length && frames[index] != null)
                spriteRenderer.sprite = frames[index];
        }
    }
}
