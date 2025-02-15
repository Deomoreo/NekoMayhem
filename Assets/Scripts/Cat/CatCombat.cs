using System.Collections;
using UnityEngine;

public class CatCombat : MonoBehaviour
{
    private Animator animator;
    public LayerMask enemyLayers;

    public float attackAngle = 30f;
    public float attackRange = 1.5f;
    private float baseAttackCooldown = 0.5f;
    public float attackCooldown;
    private bool isAttacking = true;
    private string currentWeapon = "Spada";
    private float attackDamage;
    private float critChance;
    private float defense;
    private float attackSpeed;
    private CatParry catParry; 

    private void Start()
    {
        animator = GetComponent<Animator>();
        catParry = FindObjectOfType<CatParry>();
        if (catParry == null)
        {
            Debug.LogError("CatParry non trovato! Assicurati che il player abbia lo script.");
        }
        UpdateStats();

        PlayerStats.StatsUpdated += UpdateStats;
        WeaponStats.WeaponUpdated += UpdateStats;
    }

    private void OnDestroy()
    {
        PlayerStats.StatsUpdated -= UpdateStats;
        WeaponStats.WeaponUpdated -= UpdateStats;
    }

    public void PerformAttack()
    {
        if (!isAttacking || animator.GetBool("IsAttacking")) return;
        isAttacking = false;
        animator.SetBool("IsAttacking", true);
        //animator.SetTrigger("Attack");
        StartCoroutine(FadeLayerWeight(1, 0.1f)); 
    }
    public void EndAttack()
    {
        animator.SetBool("IsAttacking", false);
        StartCoroutine(FadeLayerWeight(0, 0.1f));
        StartCoroutine(ResetAttackCooldown());
    }
    private IEnumerator FadeLayerWeight(float targetWeight, float duration)
    {
        float currentWeight = animator.GetLayerWeight(1);
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            animator.SetLayerWeight(1, Mathf.Lerp(currentWeight, targetWeight, elapsedTime / duration));
            yield return null;
        }

        animator.SetLayerWeight(1, targetWeight);
    }
    private IEnumerator ResetAttackCooldown()
    {
        yield return new WaitForSeconds(attackCooldown);
        isAttacking = true;
    }
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
            EnemyController enemyController = enemy.GetComponent<EnemyController>();

            if (enemyController == null)
            {
                Debug.LogWarning($"Il collider {enemy.name} è stato colpito, ma non ha EnemyController!");
                continue; 
            }

            Vector3 directionToEnemy = (enemy.transform.position - attackOrigin).normalized;
            float angle = Vector3.Angle(attackDirection, directionToEnemy);
            if (angle <= attackAngle / 2)
            {
                int finalDamage = (int)attackDamage;

                // Controllo critico
                float critRoll = Random.Range(0f, 100f);
                if (critRoll < critChance)
                {
                    finalDamage *= 2;
                    Debug.Log("COLPO CRITICO!");
                }

                if (catParry != null)
                {

                    if (catParry.IsEnemyStunned(enemyController))
                    {
                        finalDamage = Mathf.RoundToInt(finalDamage * 2f);
                        catParry.RemoveStunnedEnemy(enemyController);
                    }
                }
                Debug.Log($"Colpito {enemy.name}, Danno: {finalDamage}");
                enemyController.TakeDamage(finalDamage);
            }
        }
    }
    public void UpdateStats()
    {
        attackDamage = PlayerStats.Instance.stats["Forza"];
        critChance = PlayerStats.Instance.stats["Critico"];
        defense = PlayerStats.Instance.stats["Difesa"];
        if (WeaponStats.Instance.weaponStats.ContainsKey(currentWeapon))
        {
            attackDamage += WeaponStats.Instance.weaponStats[currentWeapon];
        }

        attackSpeed = PlayerStats.Instance.stats.ContainsKey("Velocità Attacco") ? PlayerStats.Instance.stats["Velocità Attacco"] : 1.0f;
        attackCooldown = baseAttackCooldown / attackSpeed; // Più alta è la velocità, più veloce sarà l'attacco

        Debug.Log($"Danni aggiornati: {attackDamage} | Critico: {critChance}% | Cooldown: {attackCooldown} | Difesa: {defense}");
    }
    public void ChangeWeapon(string newWeapon)
    {
        currentWeapon = newWeapon;
        UpdateStats();
    }
}
