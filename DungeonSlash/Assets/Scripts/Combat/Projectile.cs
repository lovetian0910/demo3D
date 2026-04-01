using UnityEngine;

public class Projectile : MonoBehaviour
{
    [SerializeField] private float speed = 10f;
    [SerializeField] private float lifetime = 5f;
    [SerializeField] private float damage = 10f;

    private Vector3 direction;
    private bool initialized;

    public void Initialize(Vector3 shootDirection, float projectileDamage)
    {
        direction = shootDirection.normalized;
        damage = projectileDamage;
        initialized = true;

        transform.rotation = Quaternion.LookRotation(direction);

        Destroy(gameObject, lifetime);
    }

    private void Update()
    {
        if (!initialized) return;
        transform.position += direction * speed * Time.deltaTime;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Enemy")) return;

        if (other.CompareTag("Player"))
        {
            DamageSystem.DealDamage(other, damage, transform.position);
        }

        Destroy(gameObject);
    }
}
