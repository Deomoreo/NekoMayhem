using UnityEngine;
using System.Collections.Generic;

public class CatParry : MonoBehaviour
{
    public float parryWindow = 0.2f; 
    private bool canParry = false;
    private bool isParrying = false;
    private Animator animator;
    private CatInputActions controls;

    private List<EnemyController> stunnedEnemies = new List<EnemyController>();
    public GameObject reflectedProjectilePrefab; 

    private void Awake()
    {
        animator = GetComponent<Animator>();
        controls = new CatInputActions();
    }

    private void OnEnable() => controls.Enable();
    private void OnDisable() => controls.Disable();

    private void Start()
    {
        controls.Parry.Newaction.performed += _ => StartParry();
    }

    private void StartParry()
    {
        if (canParry) return;

        isParrying = true;
        canParry = true;
        animator.SetTrigger("Parry");
        Invoke(nameof(EndParry), parryWindow);
    }

    private void EndParry()
    {
        isParrying = false;
        canParry = false;
    }
    public bool IsParrying() 
    {
        return isParrying;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!canParry) return;

        if (other.CompareTag("Projectile")) 
        {
            Vector3 spawnPosition = other.transform.position;
            Vector3 reflectDirection = transform.forward.normalized;

            // Segna il proiettile come riflesso e poi lo distrugge
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
