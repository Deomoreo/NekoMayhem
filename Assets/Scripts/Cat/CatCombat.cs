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

    // Stato della combo
    private bool isAttacking = false;
    private bool attackQueued = false;
    // currentAttack: 1 = Attack1, 2 = Attack2
    private int currentAttack = 0;

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
    /// Quando si preme il tasto di attacco.
    /// Se non sei già in attacco, parte Attack1.
    /// Se sei già in attacco (cioè Attack1 o Attack2 sono in corso) viene registrato l’input per concatenare il prossimo attacco.
    /// </summary>
    public void PerformAttack()
    {
        Debug.Log("PerformAttack chiamato.");
        if (isAttacking)
        {
            // Registra l'input per il prossimo attacco se già in attacco.
            attackQueued = true;
            Debug.Log("Input registrato per concatenare il prossimo attacco.");
            return;
        }
        // Avvia Attack1 se non sei già in attacco
        isAttacking = true;
        attackQueued = false;
        currentAttack = 1;
        Debug.Log("Inizio combo: Attack1 eseguito.");
        animator.SetInteger("AttackType", currentAttack);
        animator.SetTrigger("Attack");
    }

    /// <summary>
    /// Questo metodo viene chiamato tramite Animation Event alla fine dell'animazione di attacco.
    /// Se c'è un input in coda, alterna l'attacco (se Attack1, passa ad Attack2; se Attack2, passa ad Attack1)
    /// e lancia il nuovo attacco. Se non c'è input in coda, termina la combo (ritorna al blendtree).
    /// </summary>
    public void OnAttackAnimationEnd()
    {
        Debug.Log("OnAttackAnimationEnd chiamato. CurrentAttack: " + currentAttack + ", AttackQueued: " + attackQueued);
        if (attackQueued)
        {
            // Alterna il tipo di attacco: se Attack1, passa ad Attack2, altrimenti torna ad Attack1
            attackQueued = false;
            currentAttack = (currentAttack == 1) ? 2 : 1;
            Debug.Log("Eseguo nuovo attacco: Attack" + currentAttack);
            animator.SetInteger("AttackType", currentAttack);
            animator.SetTrigger("Attack");
        }
        else
        {
            // Nessun input in coda: termina la combo
            EndAttack();
        }
    }

    /// <summary>
    /// Termina la combo, resetta lo stato e riporta l'animator al blendtree (impostando AttackType a 0).
    /// </summary>
    private void EndAttack()
    {
        Debug.Log("Combo terminata. Resetto lo stato e torno al blendtree.");
        isAttacking = false;
        currentAttack = 0;
        animator.SetInteger("AttackType", 0);
    }

    // I metodi ApplyDamage, UpdateStats e ChangeWeapon rimangono invariati

    public void ApplyDamageEvent()
    {
        ApplyDamage();
        if (swordTrailController != null)
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
                Debug.Log("Colpito " + enemy.name + ", Danno: " + finalDamage);
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
        attackSpeed = PlayerStats.Instance.stats.ContainsKey("Velocità Attacco") ?
                      PlayerStats.Instance.stats["Velocità Attacco"] : 1.0f;
        attackCooldown = baseAttackCooldown / attackSpeed;
        Debug.Log($"Stats aggiornate: Danno: {attackDamage}, CritChance: {critChance}%, Cooldown: {attackCooldown}, Difesa: {defense}");
    }

    public void ChangeWeapon(string newWeapon)
    {
        currentWeapon = newWeapon;
        UpdateStats();
    }
}
