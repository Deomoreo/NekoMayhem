using UnityEngine;

public class CatParry : MonoBehaviour
{
    public float parryWindow = 0.2f; // Finestra di tempo utile per il parry
    private bool canParry = false;
    private Animator animator;
    public GameObject reflectedProjectilePrefab; // Prefab del proiettile riflesso

    private CatInputActions controls;

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

    public bool IsParrying => canParry; // Proprietà pubblica per verificare lo stato del parry

    private void StartParry()
    {
        if (canParry) return;

        canParry = true;
        animator.SetTrigger("Parry");
        Invoke(nameof(EndParry), parryWindow);
    }

    private void EndParry()
    {
        canParry = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!canParry) return;

        if (other.CompareTag("Projectile"))
        {
            Debug.Log($"🟢 Parry su proiettile {other.name}!");

            Vector3 spawnPosition = other.transform.position;
            Vector3 reflectDirection = transform.forward.normalized;

            // Segna il proiettile come riflesso e poi lo distrugge
            other.GetComponent<Projectile>().SetReflected();
            Destroy(other.gameObject);

            GameObject reflectedProjectile = Instantiate(reflectedProjectilePrefab, spawnPosition, Quaternion.identity);
            reflectedProjectile.GetComponent<Renderer>().enabled = true;
            reflectedProjectile.tag = "ReflectedProjectile";
            reflectedProjectile.GetComponent<Projectile>().SetReflected(); // Anche il clone è segnato come riflesso

            Rigidbody rb = reflectedProjectile.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.velocity = reflectDirection * 15f;
            }

            Debug.Log($"✨ Proiettile riflesso creato in direzione {reflectDirection}!");
        }
    }
}
