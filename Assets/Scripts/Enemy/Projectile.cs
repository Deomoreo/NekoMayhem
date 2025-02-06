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
        transform.LookAt(targetPosition); // Fa in modo che il proiettile guardi verso il bersaglio
    }

    private void Start()
    {
        Destroy(gameObject, lifetime); // Distrugge il proiettile dopo un tot di tempo
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
                Destroy(gameObject); // Distrugge il proiettile dopo aver colpito
            }
        }
        else if (other.CompareTag("Wall")) // Se colpisce un muro, si distrugge
        {
            Destroy(gameObject);
        }
        else if (other.CompareTag("Ground")) 
        {
            Destroy(gameObject);
        }
    }
}