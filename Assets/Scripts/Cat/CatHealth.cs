using UnityEngine;
using UnityEngine.UI;

public class CatHealth : MonoBehaviour
{
    public float maxHealth = 100f;
    private float currentHealth;

    private Animator animator;

    public Slider healthSlider;
    void Awake()
    {
        currentHealth = maxHealth;
        healthSlider.maxValue = maxHealth;
        healthSlider.value = currentHealth;
        animator = GetComponent<Animator>();
    }

    public void TakeDamage(int damage)
    {
        if (currentHealth <= 0) return;

        int defense = PlayerStats.Instance.stats.ContainsKey("Difesa") ? Mathf.RoundToInt(PlayerStats.Instance.stats["Difesa"]) : 0;
        int reducedDamage = Mathf.Max(1, damage - defense); // Assicuriamoci che il danno minimo sia sempre 1

        currentHealth -= reducedDamage;
        Debug.Log($"Il player ha subito {reducedDamage} danni! Salute attuale: {currentHealth}");

        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        healthSlider.value = currentHealth;

        Debug.Log("Danni subiti. HP attuali: " + currentHealth);

        if (CameraShake.Instance != null)
            CameraShake.Instance.Shake(0.6f, 0.8f); // Shake 

        if (currentHealth <= 0)
        {
            Die();
        }

    }

    public void Heal(float amount)
    {
        currentHealth += amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        healthSlider.value = currentHealth;
    }

    private void Die()
    {
        Debug.Log("Il gatto è morto!");
        //animator.SetTrigger("Die"); 
    }

    public float GetCurrentHealth()
    {
        return currentHealth;
    }

    public void SetHealth(float health)
    {
        currentHealth = Mathf.Clamp(health, 0, maxHealth);
        healthSlider.value = currentHealth;
        Debug.Log("HP iniziali: " + currentHealth);
    }
}
