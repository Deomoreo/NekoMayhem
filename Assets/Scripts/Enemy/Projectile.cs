using UnityEngine;

public class Projectile : MonoBehaviour
{
    public float speed = 10f;
    public float damage = 10f;

    private Vector3 direction;

    public void Initialize(Vector3 direction)
    {
        this.direction = direction.normalized;
        Destroy(gameObject, 5f);
    }

    void Update()
    {
        transform.Translate(direction * speed * Time.deltaTime, Space.World);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            other.GetComponent<CatHealth>()?.TakeDamage(damage);
            Destroy(gameObject);
        }
        else if (other.CompareTag("Environment"))
        {
            Destroy(gameObject);
        }
    }
}
