using System.Collections;
using UnityEngine;

public class CatCombat : MonoBehaviour
{
    private Animator animator;
    public LayerMask enemyLayers;
    private CatParry catParry;
    private SwordTrailController swordTrailController;

    public float attackAngle = 30f;
    public float attackRange = 1.5f;
    private float baseAttackCooldown = 1.3f;
    public float attackCooldown;

    // Stato d'attacco e flag per il chaining
    private bool isAttacking = false;
    private bool attackQueued = false;

    // Parametri per il timing (in secondi)
    public float firstAttackDuration = 1.0f;    // Durata stimata del primo attacco
    public float secondAttackDuration = 1.0f;   // Durata stimata del secondo attacco
    public float chainThreshold = 0.8f;         // Frazione della durata del primo attacco entro cui,
                                                // se viene premuto nuovamente il tasto, viene concatenato il secondo attacco

    private string currentWeapon = "Spada";
    private float attackDamage;
    private float critChance;
    private float defense;
    private float attackSpeed;

    void Start()
    {
        animator = GetComponent<Animator>();
        catParry = FindObjectOfType<CatParry>();
        if (catParry == null)
            Debug.LogError("CatParry non trovato! Assicurati che il player abbia lo script.");
        UpdateStats();
        swordTrailController = FindObjectOfType<SwordTrailController>();
        PlayerStats.StatsUpdated += UpdateStats;
        WeaponStats.WeaponUpdated += UpdateStats;
    }

    void OnDestroy()
    {
        PlayerStats.StatsUpdated -= UpdateStats;
        WeaponStats.WeaponUpdated -= UpdateStats;
    }

    /// <summary>
    /// Viene chiamato quando si preme il tasto di attacco.
    /// Se un attacco è già in corso, viene registrato l'input per concatenare il secondo (attackQueued = true).
    /// Altrimenti, si inizia la sequenza del primo attacco.
    /// </summary>
    public void PerformAttack()
    {
        if (isAttacking)
        {
            // Se già in corso, registra il chaining
            attackQueued = true;
            return;
        }

        isAttacking = true;
        attackQueued = false;
        animator.SetInteger("AttackIndex", 1);
        animator.SetTrigger("Attack");
        StartCoroutine(AttackSequence());
    }

    /// <summary>
    /// Coroutine che gestisce l'intera sequenza d'attacco.
    /// Attende per il tempo di "chain" (chainThreshold * firstAttackDuration);
    /// se è stato registrato un input (attackQueued) viene concatenato il secondo attacco.
    /// Al termine della durata appropriata, viene chiamato EndAttack().
    /// </summary>
    IEnumerator AttackSequence()
    {
        float chainTime = chainThreshold * firstAttackDuration;
        yield return new WaitForSeconds(chainTime);

        if (attackQueued)
        {
            // Concatenazione del secondo attacco
            animator.SetInteger("AttackIndex", 2);
            animator.SetTrigger("Attack");
            attackQueued = false;
            yield return new WaitForSeconds(secondAttackDuration);
            EndAttack();
        }
        else
        {
            // Nessun chaining: attendi il resto del primo attacco
            yield return new WaitForSeconds(firstAttackDuration - chainTime);
            EndAttack();
        }
    }

    /// <summary>
    /// Al termine dell'attacco, resetta lo stato, riportando AttackIndex a 0 e consentendo nuove esecuzioni.
    /// </summary>
    public void EndAttack()
    {
        isAttacking = false;
        attackQueued = false;
        animator.SetInteger("AttackIndex", 0);
        swordTrailController.StopTrail();
    }

    public void ApplyDamageEvent()
    {
        ApplyDamage();
        swordTrailController.StartTrail();
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
                continue;
            Vector3 directionToEnemy = (enemy.transform.position - attackOrigin).normalized;
            float angle = Vector3.Angle(attackDirection, directionToEnemy);
            if (angle <= attackAngle / 2)
            {
                int finalDamage = (int)attackDamage;
                float critRoll = Random.Range(0f, 100f);
                if (critRoll < critChance)
                {
                    finalDamage *= 2;
                    Debug.Log("COLPO CRITICO!");
                }
                if (catParry != null && catParry.IsEnemyStunned(enemyController))
                {
                    finalDamage = Mathf.RoundToInt(finalDamage * 2f);
                    catParry.RemoveStunnedEnemy(enemyController);
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
            attackDamage += WeaponStats.Instance.weaponStats[currentWeapon];
        attackSpeed = PlayerStats.Instance.stats.ContainsKey("Velocità Attacco") ? PlayerStats.Instance.stats["Velocità Attacco"] : 1.0f;
        attackCooldown = baseAttackCooldown / attackSpeed;
        Debug.Log($"Danni aggiornati: {attackDamage} | Critico: {critChance}% | Cooldown: {attackCooldown} | Difesa: {defense}");
    }

    public void ChangeWeapon(string newWeapon)
    {
        currentWeapon = newWeapon;
        UpdateStats();
    }

    /// <summary>
    /// Annulla la combo, resetta eventuali trigger e AttackIndex, e abilita nuovamente gli attacchi.
    /// Questo metodo può essere chiamato anche dal movimento per interrompere la combo.
    /// </summary>
    public void CancelCombo()
    {
        attackQueued = false;
        animator.ResetTrigger("Attack");
        animator.SetInteger("AttackIndex", 0);
        isAttacking = false;
    }
}
