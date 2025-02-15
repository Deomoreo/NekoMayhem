using System.Collections;
using UnityEngine;

public class ReflectedProjectile : MonoBehaviour
{
    public int damage = 20;
    public float lifetime = 5f; 

    private void Start()
    {
        StartCoroutine(DestroyAfterTime());
    }
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Enemy"))
        {
            other.GetComponent<EnemyController>().TakeDamage(damage);
            Destroy(gameObject);
        }
        if (other.CompareTag("Room") || other.CompareTag("Ground"))
        {
            Destroy(gameObject);
        }
    }

    private IEnumerator DestroyAfterTime()
    {
        yield return new WaitForSeconds(lifetime);
        Destroy(gameObject);
    }
}
