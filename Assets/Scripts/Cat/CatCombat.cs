using System.Collections;
using UnityEngine;

public class CatCombat : MonoBehaviour
{
    private Animator animator;
    private Rigidbody rb;
    public LayerMask enemyLayers;
    private CatParry catParry;
    private SwordTrailController swordTrailController;

    public float attackAngle = 30f;
    public float attackRange = 1.5f;
    private float baseAttackCooldown = 1.3f;
    public float attackCooldown;

    // Gestione della combo
    private bool isAttacking = false;
    private int queuedAttacks = 0;  // Numero di input in coda (limite massimo 2)
    public int maxQueue = 2;

    // currentAttack: 1 = Attack1, 2 = Attack2.
    // La logica alterna tra Attack1 e Attack2
    private int currentAttack = 0;

    // Parametro per il push forward
    public float pushForce = 10f;

    private string currentWeapon = "Spada";
    private float attackDamage;
    private float critChance;
    private float defense;
    private float attackSpeed;

    void Start()
    {
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody>();
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
    /// Metodo chiamato quando viene premuto il tasto d'attacco.
    /// Se non stai attaccando, parte Attack1.
    /// Se stai già attaccando, registra l'input (fino a maxQueue) per concatenare il prossimo attacco.
    /// </summary>
    public void PerformAttack()
    {
        Debug.Log("PerformAttack chiamato.");
        if (isAttacking)
        {
            if (queuedAttacks < maxQueue)
            {
                queuedAttacks++;
                Debug.Log("Input registrato per il prossimo attacco. queuedAttacks = " + queuedAttacks);
            }
            else
            {
                Debug.Log("Max input in coda raggiunto. Input ignorato.");
            }
            return;
        }
        // Non stai attaccando: inizia Attack1
        isAttacking = true;
        queuedAttacks = 0;
        currentAttack = 1;
        Debug.Log("Inizio combo: Attack1 eseguito.");
        TriggerAttack();
    }

    /// <summary>
    /// Invia il trigger "Attack" e applica il push forward.
    /// </summary>
    void TriggerAttack()
    {
        animator.SetInteger("AttackType", currentAttack);
        animator.SetTrigger("Attack");
        PushForward();
    }

    /// <summary>
    /// Applica un impulso in avanti al player.
    /// </summary>
    void PushForward()
    {
        if (rb != null)
        {
            rb.AddForce(transform.forward * pushForce, ForceMode.VelocityChange);
            Debug.Log("Push forward applicato: " + pushForce);
        }
    }

    /// <summary>
    /// Questo metodo deve essere chiamato tramite un Animation Event alla fine (o quasi) di ciascuna animazione d'attacco.
    /// Se c'è un attacco registrato in coda, alterna l'attacco (se Attack1 passa a Attack2, altrimenti viceversa)
    /// e lancia il nuovo attacco; altrimenti, termina la combo.
    /// </summary>
    public void OnAttackAnimationEnd()
    {
        Debug.Log("OnAttackAnimationEnd chiamato. currentAttack = " + currentAttack + ", queuedAttacks = " + queuedAttacks);
        if (queuedAttacks > 0)
        {
            queuedAttacks--;
            // Alterna l'attacco: se Attack1, passa a Attack2; se Attack2, torna ad Attack1
            currentAttack = (currentAttack == 1) ? 2 : 1;
            Debug.Log("Concateno nuovo attacco: Attack" + currentAttack);
            TriggerAttack();
        }
        else
        {
            EndAttack();
        }
    }

    /// <summary>
    /// Termina la combo, resetta lo stato e invia il trigger "EndAttack" per far tornare l'animator al blendtree.
    /// </summary>
    void EndAttack()
    {
        Debug.Log("Combo terminata. Resetto lo stato e torno al blendtree.");
        isAttacking = false;
        queuedAttacks = 0;
        currentAttack = 0;
        animator.SetTrigger("EndAttack");
    }

    // I metodi per ApplyDamage, UpdateStats e ChangeWeapon restano invariati

    public void ApplyDamageEvent()
    {
        ApplyDamage();
        if (swordTrailController != null)
            swordTrailController.StartTrail();
    }

    void ApplyDamage()
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

    void UpdateStats()
    {
        attackDamage = PlayerStats.Instance.stats["Forza"];
        critChance = PlayerStats.Instance.stats["Critico"];
        defense = PlayerStats.Instance.stats["Difesa"];
        if (WeaponStats.Instance.weaponStats.ContainsKey(currentWeapon))
            attackDamage += WeaponStats.Instance.weaponStats[currentWeapon];
        attackSpeed = PlayerStats.Instance.stats.ContainsKey("Velocità Attacco")
            ? PlayerStats.Instance.stats["Velocità Attacco"]
            : 1.0f;
        attackCooldown = baseAttackCooldown / attackSpeed;
        Debug.Log($"Danni aggiornati: {attackDamage} | Critico: {critChance}% | Cooldown: {attackCooldown} | Difesa: {defense}");
    }

    public void ChangeWeapon(string newWeapon)
    {
        currentWeapon = newWeapon;
        UpdateStats();
    }
}
