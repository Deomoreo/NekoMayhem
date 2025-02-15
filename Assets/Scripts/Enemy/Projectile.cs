using System.Collections;
using UnityEngine;

public class Projectile : MonoBehaviour
{
    public int damage = 10;
    private bool isReflected = false;
    public float lifetime = 5f; 
    
    private void Start()
    {
        StartCoroutine(DestroyAfterTime()); 
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isReflected && other.CompareTag("Player"))
        {
            // Controlla se il player sta effettuando il parry
            CatParry parry = other.GetComponent<CatParry>();
            if (parry != null && parry.IsParrying())
            {
                return;
            }
            else
            {
                other.GetComponent<CatHealth>().TakeDamage(damage);
                Destroy(gameObject);
            }
        }
        else if (isReflected && other.CompareTag("Enemy"))
        {
            other.GetComponent<EnemyController>().TakeDamage(damage * 2);
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
    public void SetReflected()
    {
        isReflected = true;
        gameObject.layer = LayerMask.NameToLayer("IgnorePlayer");
    }
}
