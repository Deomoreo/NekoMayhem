using System.Collections;
using UnityEngine;

public class EnemyController : MonoBehaviour
{
    [Header("Statistiche Base")]
    public int maxHealth = 100;
    private int currentHealth;

    [Header("Stato e Bonus")]
    private bool isStunned = false;
    private bool bonusDamageActive = false; // Se true, il prossimo attacco farà danni aumentati

    private Animator animator; // (Opzionale) Per gestire le animazioni

    private void Awake()
    {
        currentHealth = maxHealth;
        animator = GetComponent<Animator>();
    }

    /// <summary>
    /// Applica danni all'enemy. Se il nemico è segnato per bonus danno, il danno viene aumentato e il bonus si consuma.
    /// </summary>
    /// <param name="damage">Danno base da applicare</param>
    public void TakeDamage(int damage)
    {
        // Se è attivo il bonus, aumenta il danno (ad esempio del 50%)
        if (bonusDamageActive)
        {
            damage = Mathf.RoundToInt(damage * 1.5f);
            bonusDamageActive = false; // Bonus consumato
            Debug.Log($"{gameObject.name} ha subito danno bonus! Nuovo danno: {damage}");
        }

        currentHealth -= damage;
        Debug.Log($"{gameObject.name} ha subito {damage} danni. Salute attuale: {currentHealth}");

        // (Opzionale) Attiva una animazione di "hurt"
        if (animator != null)
        {
            animator.SetTrigger("Hurt");
        }

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    /// <summary>
    /// Applica uno stun all'enemy per la durata specificata.
    /// </summary>
    /// <param name="duration">Durata dello stun in secondi</param>
    public void ApplyStun(float duration)
    {
        if (!isStunned)
        {
            isStunned = true;
            Debug.Log($"{gameObject.name} è stordito per {duration} secondi.");
            // (Opzionale) Puoi attivare una animazione di stun qui

            StartCoroutine(RecoverFromStun(duration));
        }
    }

    /// <summary>
    /// Coroutine per recuperare lo stun dopo il tempo indicato.
    /// </summary>
    /// <param name="duration">Durata dello stun</param>
    private IEnumerator RecoverFromStun(float duration)
    {
        yield return new WaitForSeconds(duration);
        isStunned = false;
        Debug.Log($"{gameObject.name} ha ripreso il controllo.");
    }

    /// <summary>
    /// Segna l'enemy per ricevere un bonus danno nel prossimo attacco.
    /// </summary>
    public void MarkForBonusDamage()
    {
        bonusDamageActive = true;
        Debug.Log($"{gameObject.name} è segnato per un attacco con danno bonus.");
    }

    /// <summary>
    /// Gestisce la morte dell'enemy.
    /// </summary>
    private void Die()
    {
        Debug.Log($"{gameObject.name} è morto.");
        // (Opzionale) Attiva una animazione di morte, attende un po' di tempo e poi distruggi l'oggetto
        Destroy(gameObject);
    }
}
