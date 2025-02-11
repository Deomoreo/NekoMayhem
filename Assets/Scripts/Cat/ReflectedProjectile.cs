using UnityEngine;

public class ReflectedProjectile : MonoBehaviour
{
    public int damage = 20; // 🔥 Danno aumentato per il proiettile riflesso

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Enemy"))
        {
            Debug.Log($"💥 Il proiettile riflesso ha colpito {other.name}!");
            other.GetComponent<EnemyController>().TakeDamage(damage);
            Destroy(gameObject);
        }
    }
}
