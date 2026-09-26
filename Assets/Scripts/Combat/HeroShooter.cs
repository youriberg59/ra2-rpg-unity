using UnityEngine;

namespace RA2RPG.Combat
{
    public sealed class HeroShooter : MonoBehaviour
    {
        [SerializeField] private float projectileSpeed = 9f;
        [SerializeField] private float projectileLifetime = 3f;

        private void Update()
        {
            bool fireClick =
                (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)) &&
                (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));

            if (!fireClick || Camera.main == null)
                return;

            Vector3 mouse = Input.mousePosition;
            mouse.z = -Camera.main.transform.position.z;
            Vector3 target = Camera.main.ScreenToWorldPoint(mouse);
            target.z = transform.position.z;

            Vector3 direction = (target - transform.position).normalized;
            SpawnProjectile(direction);
        }

        private void SpawnProjectile(Vector3 direction)
        {
            GameObject projectile = GameObject.CreatePrimitive(PrimitiveType.Quad);
            projectile.name = "Projectile";
            projectile.transform.position = transform.position;
            projectile.transform.localScale = Vector3.one * 0.15f;

            var renderer = projectile.GetComponent<MeshRenderer>();
            renderer.material = new Material(Shader.Find("Sprites/Default"));
            renderer.material.color = Color.yellow;

            var mover = projectile.AddComponent<ProjectileMover>();
            mover.Initialize(direction, projectileSpeed, projectileLifetime);
        }
    }

    public sealed class ProjectileMover : MonoBehaviour
    {
        private Vector3 direction;
        private float speed;
        private float lifetime;

        public void Initialize(Vector3 newDirection, float newSpeed, float newLifetime)
        {
            direction = newDirection;
            speed = newSpeed;
            lifetime = newLifetime;
        }

        private void Update()
        {
            transform.position += direction * speed * Time.deltaTime;
            lifetime -= Time.deltaTime;

            if (lifetime <= 0f)
                Destroy(gameObject);
        }
    }
}
