using UnityEngine;
using System.Collections;

public class CatDash : MonoBehaviour
{
    private Rigidbody rb;
    private Animator animator;
    private CatInputActions controls;

    [Header("Dash Settings")]
    public float dashSpeed = 12f;
    public float dashCooldown = 0.5f;
    private bool canDash = true;
    private bool isDashing; // Vero solo durante il movimento del dash

    [Header("Stamina Settings")]
    public float maxStamina = 100f;
    private float currentStamina;
    public float staminaCost;
    public float staminaRegenRate;
    public float staminaRegenDelay;
    private bool isRegenerating;
    private Coroutine regenCoroutine;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
        controls = new CatInputActions();
        currentStamina = maxStamina;
    }

    private void OnEnable() => controls.Enable();
    private void OnDisable() => controls.Disable();

    private void Start()
    {
        controls.Dash.Newaction.performed += _ => TryDash();
    }

    private void Update()
    {
        if (!isRegenerating && currentStamina < maxStamina)
        {
            regenCoroutine = StartCoroutine(RegenerateStamina());
        }
    }

    public void ConsumeStamina(float amount)
    {
        currentStamina -= amount;
        if (currentStamina < 0) currentStamina = 0;

        if (regenCoroutine != null) StopCoroutine(regenCoroutine);
        isRegenerating = false;
        regenCoroutine = StartCoroutine(RegenerateStamina());
    }

    // Viene chiamato quando si preme il tasto dash.
    private void TryDash()
    {
        // Se il parry è attivo, non eseguo il dash
        CatParry parry = FindObjectOfType<CatParry>();
        if (parry != null && parry.IsParryingActive)
            return;
        if (!canDash || currentStamina < staminaCost) return;

        canDash = false; // Blocca ulteriori input dash fino al termine del cooldown.
        // Attiva l'animazione del dash impostando il bool "Dash" a true
        animator.SetBool("Dash", true);
    }

    // Questo metodo va chiamato dall'evento "DashStart" nell'animazione.
    public void DashStart()
    {
        isDashing = true;
        // Consumo la stamina in corrispondenza dell'inizio dell'azione.
        ConsumeStamina(staminaCost);

        Vector3 dashDirection = transform.forward;
        rb.velocity = dashDirection * dashSpeed;

        Physics.IgnoreLayerCollision(6, 7, true);
        // Reset del bool per evitare che rimanga sempre attivo
        animator.SetBool("Dash", false);
    }

    // Questo metodo va chiamato dall'evento "DashEnd" nell'animazione.
    public void DashEnd()
    {
        rb.velocity = Vector3.zero;
        Physics.IgnoreLayerCollision(6, 7, false);
        isDashing = false;

        StartCoroutine(WaitDashCooldown());
    }

    private IEnumerator WaitDashCooldown()
    {
        yield return new WaitForSeconds(dashCooldown);
        canDash = true;
    }

    private IEnumerator RegenerateStamina()
    {
        isRegenerating = true;
        yield return new WaitForSeconds(staminaRegenDelay);

        while (currentStamina < maxStamina)
        {
            currentStamina += staminaRegenRate * Time.deltaTime;
            currentStamina = Mathf.Min(currentStamina, maxStamina);
            yield return null;
            if (!isRegenerating)
            {
                yield break;
            }
        }
        isRegenerating = false;
    }

    public float GetCurrentStamina() => currentStamina;

    public bool IsDashing => isDashing;
}
