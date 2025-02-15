using UnityEngine;

public class Projectile : MonoBehaviour
{
    public float speed = 10f;
    public float damage = 10f;
    public float lifetime = 5f;
    private Vector3 direction;

    public void Initialize(Vector3 targetPosition)
    {
        direction = (targetPosition - transform.position).normalized;
        transform.LookAt(targetPosition);
    }

    private void Start()
    {
        Destroy(gameObject, lifetime);
    }

    private void Update()
    {
        transform.position += speed * Time.deltaTime * direction;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            CatHealth playerHealth = other.GetComponent<CatHealth>();
            if (playerHealth != null)
            {
                playerHealth.TakeDamage(damage);
                Destroy(gameObject);
            }
        }
        else if (other.gameObject.layer == LayerMask.NameToLayer("Walls") || other.gameObject.layer == LayerMask.NameToLayer("Ground")) // Se colpisce un muro, si distrugge
        {
            Destroy(gameObject);
        }
    }
}
