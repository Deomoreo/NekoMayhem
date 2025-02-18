using UnityEngine;
using System.Collections.Generic;

public class CatParry : MonoBehaviour
{
    public float parryWindow = 0.2f;
    private float originalSpeed;
    private bool canParry = true;
    private bool isParrying = false;
    private Animator animator;
    private CatInputActions controls;
    private CatController catController;
    private CatDash playerDash;

    private List<EnemyController> stunnedEnemies = new List<EnemyController>();
    public GameObject reflectedProjectilePrefab;

    [Header("Parry Settings")]
    public float moveSpeedReduction = 0.5f;
    public float parryCooldown = 0.5f;
    public float staminaCost = 15f;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        controls = new CatInputActions();
        catController = GetComponent<CatController>();
        playerDash = FindObjectOfType<CatDash>();
    }

    private void OnEnable() => controls.Enable();
    private void OnDisable() => controls.Disable();

    private void Start()
    {
        controls.Parry.Newaction.performed += _ => StartParry();
        controls.Parry.Newaction.canceled += _ => EndParry();
        originalSpeed = catController.GetBaseSpeed();
    }

    private void StartParry()
    {
        // Se il dash è attivo, non eseguo il parry
        if (playerDash.IsDashing) return;
        // Controllo che ci sia abbastanza stamina
        if (playerDash.GetCurrentStamina() < staminaCost) return;
        if (!canParry) return;

        isParrying = true;
        canParry = false;

        animator.SetBool("IsParrying", true);
        animator.SetFloat("Speed", 0f);

        catController.SetSpeed(originalSpeed * moveSpeedReduction);
        catController.EnableParryRotation(true);

        playerDash.ConsumeStamina(staminaCost);
    }

    private void EndParry()
    {
        if (!isParrying) return;

        isParrying = false;
        animator.SetBool("IsParrying", false);
        animator.SetFloat("Speed", catController.GetBaseSpeed());

        catController.SetSpeed(originalSpeed);
        catController.EnableParryRotation(false);

        Invoke(nameof(ResetParry), parryCooldown);
    }

    private void ResetParry()
    {
        canParry = true;
    }

    // Metodo per verificare lo stato del parry
    public bool IsParrying() => isParrying;
    // Proprietà per controllare facilmente lo stato da altri script
    public bool IsParryingActive => isParrying;

    private void OnTriggerEnter(Collider other)
    {
        if (!isParrying) return;

        if (other.CompareTag("Projectile"))
        {
            Vector3 spawnPosition = other.transform.position;
            Vector3 reflectDirection = transform.forward.normalized;

            other.GetComponent<Projectile>().SetReflected();
            Destroy(other.gameObject);

            GameObject reflectedProjectile = Instantiate(reflectedProjectilePrefab, spawnPosition, Quaternion.identity);
            reflectedProjectile.GetComponent<Renderer>().enabled = true;
            reflectedProjectile.tag = "ReflectedProjectile";
            reflectedProjectile.GetComponent<Projectile>().SetReflected();

            Rigidbody rb = reflectedProjectile.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.velocity = reflectDirection * 15f;
            }
        }
    }

    public bool IsEnemyStunned(EnemyController enemy)
    {
        return stunnedEnemies.Contains(enemy);
    }
    public void AddStunnedEnemy(EnemyController enemy)
    {
        if (!stunnedEnemies.Contains(enemy))
        {
            stunnedEnemies.Add(enemy);
        }
    }
    public void RemoveStunnedEnemy(EnemyController enemy)
    {
        if (stunnedEnemies.Contains(enemy))
        {
            stunnedEnemies.Remove(enemy);
        }
    }
}
