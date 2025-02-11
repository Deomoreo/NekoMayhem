using UnityEngine;

public class Projectile : MonoBehaviour
{
    public int damage = 10;
    private bool isReflected = false;

    private void OnTriggerEnter(Collider other)
    {
        if (!isReflected && other.CompareTag("Player"))
        {
            // Controlla se il player sta effettuando il parry
            CatParry parry = other.GetComponent<CatParry>();
            if (parry != null && parry.IsParrying)
            {
                Debug.Log("⚠️ Proiettile intercettato dal parry, nessun danno inflitto.");
                // Non applicare danno, lasciamo che CatParry gestisca il riflesso
                return;
            }
            else
            {
                Debug.Log("⚠️ Il Player è stato colpito da un proiettile!");
                other.GetComponent<CatHealth>().TakeDamage(damage);
                Destroy(gameObject);
            }
        }
        else if (isReflected && other.CompareTag("Enemy"))
        {
            Debug.Log($"💥 Il proiettile riflesso ha colpito {other.name}!");
            other.GetComponent<EnemyController>().TakeDamage(damage * 2);
            Destroy(gameObject);
        }
    }

    public void SetReflected()
    {
        isReflected = true;
        gameObject.layer = LayerMask.NameToLayer("IgnorePlayer");
        Debug.Log($"✨ Il proiettile {gameObject.name} è stato riflesso e non può più colpire il Player!");
    }
}
