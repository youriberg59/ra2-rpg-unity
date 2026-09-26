using UnityEngine;

namespace RA2RPG.Player
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SimpleSpriteAnimator : MonoBehaviour
    {
        [SerializeField] private Sprite[] frames;
        [SerializeField] private float framesPerSecond = 8f;

        private SpriteRenderer spriteRenderer;
        private float time;

        public void SetFrames(Sprite[] newFrames, float fps = 8f)
        {
            frames = newFrames;
            framesPerSecond = Mathf.Max(0.1f, fps);
            time = 0f;

            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();

            if (frames != null && frames.Length > 0)
                spriteRenderer.sprite = frames[0];
        }

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        private void Update()
        {
            if (frames == null || frames.Length == 0)
                return;

            time += Time.deltaTime;
            int frame = Mathf.FloorToInt(time * framesPerSecond) % frames.Length;
            spriteRenderer.sprite = frames[frame];
        }
    }
}
