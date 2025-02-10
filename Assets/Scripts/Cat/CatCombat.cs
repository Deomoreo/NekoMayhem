using System.Collections;
using UnityEngine;

public class CatCombat : MonoBehaviour
{
    private Animator animator;
    public LayerMask enemyLayers;

    public float attackAngle = 30f;
    public float attackRange = 1.5f;
    public float attackCooldown = 0.5f;

    private bool isAttacking;
    private string currentWeapon = "Spada"; // Può cambiare a runtime
    private float attackDamage;
    private float critChance;

    private void Start()
    {
        animator = GetComponent<Animator>();
        UpdateStats();
    }

    public void PerformAttack()
    {
        if (isAttacking) return;

        isAttacking = true;
        animator.SetTrigger("Attack");
        StartCoroutine(ResetAttackCooldown());
    }

    private IEnumerator ResetAttackCooldown()
    {
        yield return new WaitForSeconds(attackCooldown);
        isAttacking = false;
    }

    // ?? **Metodo chiamato dall'animazione**
    public void ApplyDamageEvent()
    {
        ApplyDamage();
    }

    private void ApplyDamage()
    {
        Vector3 attackOrigin = transform.position;
        Vector3 attackDirection = transform.forward;

        Collider[] hitEnemies = Physics.OverlapSphere(attackOrigin, attackRange, enemyLayers);

        foreach (Collider enemy in hitEnemies)
        {
            Vector3 directionToEnemy = (enemy.transform.position - attackOrigin).normalized;
            float angle = Vector3.Angle(attackDirection, directionToEnemy);
            if (angle <= attackAngle / 2)
            {
                float finalDamage = attackDamage;

                // Controllo critico
                float critRoll = Random.Range(0f, 100f);
                if (critRoll < critChance)
                {
                    finalDamage *= 2; // Colpo critico raddoppiato
                    Debug.Log("COLPO CRITICO!");
                }

                Debug.Log($"Colpito {enemy.name}, Danno: {finalDamage}");
                if (enemy.CompareTag("Enemy"))
                {
                    enemy.GetComponent<EnemyController>().TakeDamage(finalDamage);
                }
            }
        }
    }

    public void UpdateStats()
    {
        attackDamage = PlayerStats.Instance.stats["Forza"];
        critChance = PlayerStats.Instance.stats["Critico"];

        if (WeaponStats.Instance.weaponStats.ContainsKey(currentWeapon))
        {
            attackDamage += WeaponStats.Instance.weaponStats[currentWeapon];
        }

        Debug.Log($"Danni aggiornati: {attackDamage} | Critico: {critChance}%");
    }

    public void ChangeWeapon(string newWeapon)
    {
        currentWeapon = newWeapon;
        UpdateStats();
    }
}
