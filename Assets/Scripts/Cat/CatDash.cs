using UnityEngine;
using System.Collections;

public class CatDash : MonoBehaviour
{
    private Rigidbody rb;
    private Animator animator;
    private CatInputActions controls;

    [Header("Dash Settings")]
    public float dashCooldown = 0.5f;
    private bool canDash = true;
    private bool isDashing = false; 

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
        animator.applyRootMotion = false;
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
        if (currentStamina < 0)
            currentStamina = 0;

        if (regenCoroutine != null)
            StopCoroutine(regenCoroutine);
        isRegenerating = false;
        regenCoroutine = StartCoroutine(RegenerateStamina());
    }

    private void TryDash()
    {
        CatParry parry = FindObjectOfType<CatParry>();
        if (parry != null && parry.IsParryingActive)
            return;
        if (!canDash || currentStamina < staminaCost)
            return;

        canDash = false;
        animator.SetBool("Dash", true);
    }

    public void DashStart()
    {
        isDashing = true;
        ConsumeStamina(staminaCost);
        animator.applyRootMotion = true;
        animator.SetBool("Dash", false);
    }

    public void DashEnd()
    {
        isDashing = false;
        animator.applyRootMotion = false;
        rb.velocity = Vector3.zero;
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
        }
        isRegenerating = false;
    }

    public float GetCurrentStamina() => currentStamina;
    public bool IsDashing => isDashing;
}
