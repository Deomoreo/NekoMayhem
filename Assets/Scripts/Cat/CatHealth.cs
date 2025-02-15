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

    public void TakeDamage(float damage)
    {
        if (currentHealth <= 0) return;

        currentHealth -= damage;
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
